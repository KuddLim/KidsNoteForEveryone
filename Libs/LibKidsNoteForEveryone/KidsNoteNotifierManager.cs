using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibKidsNoteForEveryone.GoogleDrive;
using Quartz;
using Quartz.Impl;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteNotifierManager
    {
        public delegate void OnGetNewContentsDelegate(Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents);
        public delegate void OnUploadProgressMessageDelegate(string message);
        public OnGetNewContentsDelegate OnGetNewContents;
        public OnUploadProgressMessageDelegate OnUploadProgressMessage;

        private KidsNoteClient TheClient;
        private Uploader TheUploader;
        private Configuration TheConfiguration;
        private FetchHistory History;
        private HashSet<ContentType> MonitoringTypes;
        private HashSet<string> ActiveScheduleNames;
        private Bot.NotifierBot TheBot;
        private DateTime LastErrorTime;

        public KidsNoteNotifierManager(HashSet<ContentType> monitoringTypes)
        {
            ActiveScheduleNames = new HashSet<string>();
            MonitoringTypes = monitoringTypes;
            TheConfiguration = GetConfiguration();

            FileLogger.Singleton.UseLogger = this.UseLogger;
            FileLogger.Singleton.WriteLog("Config path : " + SetupFilePath());

            LastErrorTime = DateTime.MinValue;
        }

        public void Startup()
        {
            if (TheBot == null)
            {
                TheBot = new Bot.NotifierBot(TheConfiguration.TelegramBotToken);
                TheBot.AdminUserChatId = this.AdminUserChatId;
                TheBot.AddSubscriber = this.AddSubscriber;
                TheBot.AllNotificationsSent = this.AllNotificationsSent;
                TheBot.SendImagesAsAttachment = this.SendImagesAsAttachment;
            }

            var schedulerTask = StdSchedulerFactory.GetDefaultScheduler();
            schedulerTask.Wait();
            IScheduler scheduler = schedulerTask.Result;
            scheduler.Start();

            TheBot.Startup();
            if (TheConfiguration.ManagerChatId.Identifier != 0)
            {
                TheBot.SendAdminMessage(TheConfiguration.ManagerChatId, "서비스가 시작되었습니다");
            }
        }

        public void Cleanup()
        {
            var schedulerTask = StdSchedulerFactory.GetDefaultScheduler();
            schedulerTask.Wait();
            IScheduler scheduler = schedulerTask.Result;
            scheduler.Shutdown();

            if (TheConfiguration.ManagerChatId.Identifier != 0)
            {
                TheBot.SendAdminMessage(TheConfiguration.ManagerChatId, "서비스가 종료되었습니다");
            }
            TheBot.Cleanup();

            if (TheUploader != null)
            {
                TheUploader.Cleanup();
            }
        }

        private long AdminUserChatId()
        {
            TheConfiguration = GetConfiguration();
            return TheConfiguration.ManagerChatId.Identifier;
        }

        private bool AddSubscriber(long chatId, HashSet<ContentType> exclusions)
        {
            string jsonPath = SetupFilePath();
            string jsonPathBackup = jsonPath + ".backup";
            bool success = true;
            try
            {
                string json = System.IO.File.ReadAllText(jsonPath);
                Configuration newConf = Configuration.FromJson(json);
                newConf.AddSubscriber(chatId, exclusions);
                newConf.Save(jsonPathBackup);
                System.IO.File.Copy(jsonPathBackup, jsonPath, true);
                System.IO.File.Delete(jsonPathBackup);
            }
            catch (Exception)
            {
                success = false;
            }

            return success;
        }

        private void AllNotificationsSent(KidsNoteNotification notification)
        {
            UpdateLastNotifiedIds(notification.ContentType, notification.Contents);
        }

        private bool SendImagesAsAttachment()
        {
            return TheConfiguration.SendImageAsAttachment;
        }

        public void AddJob(KidsNoteScheduleParameters param)
        {
            var schedulerTask = StdSchedulerFactory.GetDefaultScheduler();
            schedulerTask.Wait();
            IScheduler scheduler = schedulerTask.Result;

            string jobName = param.ToString();

            if (ActiveScheduleNames.Contains(jobName))
            {
                return;
            }

            /*
            1.Seconds
            2.Minutes
            3.Hours
            4.Day - of - Month
            5.Month
            6.Day - of - Week
            7.Year(optional field)
            */

#if DEBUG
            DateTime scheduled = DateTime.Now;
            scheduled += new TimeSpan(0, 0, 0, 30);
#endif

            string cronFormat = "";
            if (param.Days == KidsNoteScheduleParameters.DaysType.MON_FRI)
            {
#if DEBUG
                cronFormat = String.Format("{0} {1} {2} ? * MON-FRI", scheduled.Second, scheduled.Minute, scheduled.Hour);
#else
                cronFormat = "0 0 * ? * MON-FRI";
#endif
            }
            else
            {
#if DEBUG
                cronFormat = String.Format("{0} {1} {2} * * ?", scheduled.Second, scheduled.Minute, scheduled.Hour);
#else
                cronFormat = "0 0 * * * ?";
#endif
            }


            IJobDetail job = JobBuilder.Create<KidsNoteScheduledJob>().WithIdentity(jobName, Constants.KISNOTE_SCHEDULER_GROUP_NAME).Build();
            job.JobDataMap.Put("param", param);
            job.JobDataMap.Put("owner", this);

            ITrigger trigger =
                TriggerBuilder.Create()
                    .WithIdentity(jobName, Constants.KISNOTE_SCHEDULER_GROUP_NAME)
                    .WithCronSchedule(cronFormat)
                    .ForJob(jobName, Constants.KISNOTE_SCHEDULER_GROUP_NAME)
                    .Build();

            scheduler.ScheduleJob(job, trigger);

            ActiveScheduleNames.Add(jobName);
        }

        private Dictionary<ContentType, LinkedList<KidsNoteContent>> GetNewContents()
        {
            Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents
                = new Dictionary<ContentType, LinkedList<KidsNoteContent>>();

            try
            {
                foreach (var eachType in MonitoringTypes)
                {
                    UInt64 lastId = History.GetLastContentId(eachType);

                    int page = 1;
                    KidsNoteContentDownloadResult result = TheClient.DownloadContent(eachType, lastId, page);
                    LinkedList<KidsNoteContent> newOnes = result.ContentList;

                    // 공지사항은 너무 많고, 아이에게 크게 중요치 않으므로 다음페이지를 가져오지는 않는다.
                    while (eachType != ContentType.NOTICE && newOnes != null && newOnes.Count > 1 && newOnes.Last().Id > lastId)
                    {
                        System.Diagnostics.Trace.WriteLine("Get next page...");

                        ++page;
                        KidsNoteContentDownloadResult nextResult = TheClient.DownloadContent(eachType, lastId, page);

                        LinkedList<KidsNoteContent> nextOnes = nextResult.ContentList;

                        if (nextOnes.Count == 0)
                        {
                            break;
                        }

                        foreach (var nextOne in nextOnes)
                        {
                            newOnes.AddLast(nextOne);
                        }
                    }

                    if (newOnes == null)
                    {
                        // 보통은 첫 페이지를 넘어가지 않을 것이므로 result.Html 만 참조한다.
                        HandleContentParseFailed(eachType, result.Html);
                        continue;
                    }

                    if (newOnes.Count > 0)
                    {
                        newContents[eachType] = newOnes;
                    }
                }
            }
            catch (Exception)
            {
                newContents = null;
            }

            return newContents;
        }

        private void UpdateAndNotifyContents(Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents)
        {
            Dictionary<ContentType, KidsNoteNotification> notification = new Dictionary<ContentType, KidsNoteNotification>();
            foreach (var each in newContents)
            {
                notification[each.Key] = new KidsNoteNotification();
                notification[each.Key].ContentType = each.Key;
                notification[each.Key].Receivers = TheConfiguration.GetSubscribers(each.Key);
                notification[each.Key].Contents = each.Value;
            }

            try
            {
                TheBot.SendNewContents(notification);
            }
            catch (Exception)
            {
                HandleTelegramError();
            }
        }

        private void UpdateLastNotifiedIds(ContentType contentType, LinkedList<KidsNoteContent> newContents)
        {
            if (newContents.Count > 0)
            {
                History.SetLastContentId(contentType, newContents.First().Id);
            }

            History.Save(HistoryFilePath());
        }

        public void DoScheduledCheck(bool forceReload)
        {
            TheConfiguration = GetConfigurationImpl(forceReload);
            History = GetHistory(true);

            DateTime now = DateTime.Now;
            if (TheConfiguration.OperationHourBegin != 0 && TheConfiguration.OperationHourBegin > now.Hour)
            {
                return;
            }
            if (TheConfiguration.OperationHourEnd != 0 && TheConfiguration.OperationHourEnd < now.Hour)
            {
                return;
            }

            MakeNewClient();

            Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents = GetNewContents();

            if (newContents != null && newContents.Count > 0)
            {
                // 백업은 동기적으로 동작시킬 수 있어 백업부터 하고, 이후에 텔레그램 통지한다.
                if (TheConfiguration.BackupToGoogleDrive)
                {
                    BackupToGoogleDrive(newContents);
                }

                UpdateAndNotifyContents(newContents);

                if (OnGetNewContents != null)
                {
                    OnGetNewContents(newContents);
                }
            }
        }

        public bool BackupToGoogleDrive(Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents)
        {
            if (TheUploader == null)
            {
                TheUploader = new Uploader(GoogleApiCredentialPath(), GoogleApiTokenFilePath(), TheConfiguration.ChildName);
                TheUploader.GetBaseFolderId = this.GetBaseFolderId;
                TheUploader.SetBaseFolderId = this.SetBaseFolderId;
                TheUploader.UploadProgress = this.UploadProgress;
                TheUploader.Startup();
            }

            return TheUploader.Backup(newContents);
        }

        private string GetBaseFolderId()
        {
            return TheConfiguration.GetGoogleDriveBackupFolderId();
        }

        private void SetBaseFolderId(string id)
        {
            TheConfiguration.SetGoogleDriveBackupFolderId(id);
            TheConfiguration.Save(SetupFilePath());
        }

        private void UploadProgress(string message)
        {
            if (OnUploadProgressMessage != null)
            {
                OnUploadProgressMessage(message);
            }
        }

        private KidsNoteClient MakeNewClient()
        {
            TheClient = new KidsNoteClient();
            TheClient.GetCurrentConfiguration = this.GetConfiguration;

            return TheClient;
        }

        private string SetupFilePath()
        {
            string path = "config.json";

            if (!Fundamentals.Platform.IsRunningOnMono())
            {
                path = AppDomain.CurrentDomain.BaseDirectory + System.IO.Path.DirectorySeparatorChar + path;
            }

            return path;
        }

        private string HistoryFilePath()
        {
            string path = "history.json";

            if (!Fundamentals.Platform.IsRunningOnMono())
            {
                path = AppDomain.CurrentDomain.BaseDirectory + System.IO.Path.DirectorySeparatorChar + path;
            }

            return path;
        }

        private string GoogleApiCredentialPath()
        {
#if DEBUG
            //string path = "credentials_mine_DONOT_open.json";
            string path = "credentials.json";
#else
            string path = "credentials.json";
#endif

            if (!Fundamentals.Platform.IsRunningOnMono())
            {
                path = AppDomain.CurrentDomain.BaseDirectory + System.IO.Path.DirectorySeparatorChar + path;
            }

            return path;
        }

        private string GoogleApiTokenFilePath()
        {
            string path = "token.json";

            if (!Fundamentals.Platform.IsRunningOnMono())
            {
                path = AppDomain.CurrentDomain.BaseDirectory + System.IO.Path.DirectorySeparatorChar + path;
            }

            return path;
        }

        public Configuration GetConfiguration()
        {
            return GetConfigurationImpl(false);
        }

        private Configuration GetConfigurationImpl(bool forceReload)
        {
            string jsonFile = SetupFilePath();

            try
            {
                if (System.IO.File.Exists(jsonFile))
                {
                    if (TheConfiguration == null || forceReload)
                    {
                        string json = System.IO.File.ReadAllText(jsonFile);
                        TheConfiguration = Configuration.FromJson(json);
                    }

                    return TheConfiguration;
                }
                else
                {
                    TheConfiguration = new Configuration();
                    return TheConfiguration;
                }
            }
            catch (Exception)
            {
                HandleConfigurationLoadFailed();
            }

            return null;
        }

        private FetchHistory GetHistory(bool forceReload)
        {
            string jsonFile = HistoryFilePath();

            try
            {
                if (System.IO.File.Exists(jsonFile))
                {
                    if (History == null || forceReload)
                    {
                        string json = System.IO.File.ReadAllText(jsonFile);
                        History = FetchHistory.FromJson(json);
                    }

                    return History;
                }

                History = new FetchHistory();
            }
            catch (Exception)
            {
                History = new FetchHistory();
            }

            return History;
        }

        private void HandleConfigurationLoadFailed()
        {
            NotifyError("설정 읽기에 실패하였습니다", false);
        }

        private void HandleContentParseFailed(ContentType type, string html)
        {
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(html));
            ms.Seek(0, SeekOrigin.Begin);

            string message = "게시글 글 분석 실패 : " + type;
            NotifyError(message, true, ms);
        }

        private void HandleTelegramError()
        {
            NotifyError("텔레그램 전송 에러가 발생하였습니다");
        }

        private void NotifyError(string message, bool checkLastTime = true, MemoryStream textAttachment = null)
        {
            if (checkLastTime)
            {
                TimeSpan elapsed = DateTime.Now - LastErrorTime;
                if (elapsed.TotalSeconds <= 60 * 60 * 8)
                {
                    return;
                }

                LastErrorTime = DateTime.Now;
            }

            if (TheConfiguration.ManagerChatId.Identifier != 0)
            {
                TheBot.SendAdminMessage(TheConfiguration.ManagerChatId, message, textAttachment);
            }
        }

        private bool UseLogger()
        {
            return TheConfiguration.UseLogger;
        }
    }

    public class KidsNoteScheduledJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;
            KidsNoteScheduleParameters param = (KidsNoteScheduleParameters)dataMap["param"];
            KidsNoteNotifierManager owner = (KidsNoteNotifierManager)dataMap["owner"];

            Task task = Task.Run(() =>
            {
                owner.DoScheduledCheck(true);
            });

            return task;
        }
    }

}
