﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LibKidsNoteNotifier;

namespace Tester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private LibKidsNoteNotifier.Bot.NotifierBot TheBot;
        private KidsNoteNotifierManager NotifierManager;
        private KidsNoteClient Client;
        private Configuration Conf;
        private FetchHistory History;
        private LibKidsNoteNotifier.GoogleDrive.Uploader Uploader;

        public MainWindow()
        {
            Client = new KidsNoteClient();
            Client.GetCurrentConfiguration = this.GetConfiguration;
            Conf = GetConfiguration();

            TheBot = new LibKidsNoteNotifier.Bot.NotifierBot(Conf.TelegramBotToken);

            InitializeComponent();
            InitUi();
        }

        private void InitUi()
        {
            cmbContentTypes.Items.Add("- 선택 -");
            cmbContentTypes.Items.Add("알림장");
            cmbContentTypes.Items.Add("공지사항");
            cmbContentTypes.Items.Add("앨범");
            cmbContentTypes.Items.Add("일정표");
            cmbContentTypes.Items.Add("식단표");
            cmbContentTypes.Items.Add("투약의뢰서");
            cmbContentTypes.Items.Add("귀가동의서");

            cmbContentTypes.SelectedIndex = 0;

            textKidsNoteId.Text = Conf.KidsNoteId;
            kidsNotePassword.Password = Conf.KidsNotePassword;
            textChildName.Text = Conf.ChildName;
            textTelegramBotToken.Text = Conf.TelegramBotToken;

            List<long> converted = new List<long>();
            foreach (var each in Conf.SubscriberIdList)
            {
                converted.Add(each.Identifier);
            }
            string userList = string.Join(",", converted);
            textTelegramUsers.Text = userList;
            //string userList = string.Join(",", Conf.SubscriberIdList.Select(x => x.ToString()).ToArray());

            textTelegramMainUserId.Text = Conf.ManagerChatId.ToString();
            labelGoogleDriveInfo.Content = "구글 드라이브 백업을 위해 인증이 필요합니다.(최초 1회)" + Environment.NewLine;
            labelGoogleDriveInfo.Content += "자세한 내용은 Github 페이지 참고";
        }

        private void buttonTest_GetContent(object sender, RoutedEventArgs e)
        {
            History = GetHistory();
            ContentType type = (ContentType)cmbContentTypes.SelectedIndex;

            if (type == ContentType.UNSPECIFIED)
            {
                MessageBox.Show("타입을 선택하세요", "", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LinkedList<KidsNoteContent> content = Client.DownloadContent(type, History.GetLastContentId(type));

            foreach (var each in content)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("[{0}] {1} by {2}", each.Type, each.Title, each.Writer);

                listContents.Items.Add(sb.ToString());
            }
        }

        private string SetupFilePath()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\config.json";
            return path;
        }

        private string HistoryFilePath()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\history.json";
            return path;
        }

        private string TokenFilePath()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\token.json";
            return path;
        }

        private string CredentialsPath()
        {
            // 프로젝트에 등록된 credentials.json
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\credentials.json";
            return path;
        }

        private Configuration GetConfiguration()
        {
            string jsonFile = SetupFilePath();

            if (Conf == null)
            {
                if (System.IO.File.Exists(jsonFile))
                {
                    string json = System.IO.File.ReadAllText(jsonFile);
                    Conf = Configuration.FromJson(json);
                }
                else
                {
                    Conf = new Configuration();
                    Conf.KidsNoteId = textKidsNoteId.Text;
                    Conf.KidsNotePassword = kidsNotePassword.Password;

                    TimeSpan span = new TimeSpan(0, 1, 0);

                    DateTime scheduled = DateTime.Now + span;

                    string json = Conf.ToJson();
                    System.IO.File.WriteAllText(jsonFile, json);
                }
            }

            return Conf;
        }

        private FetchHistory GetHistory()
        {
            string jsonFile = HistoryFilePath();

            try
            {
                if (System.IO.File.Exists(jsonFile))
                {
                    if (History == null)
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

        private void buttonStartBot_Click(object sender, RoutedEventArgs e)
        {
            TheBot.Startup();

            buttonStartBot.IsEnabled = false;
            buttonStopBot.IsEnabled = true;
        }

        private void buttonStopBot_Click(object sender, RoutedEventArgs e)
        {
            TheBot.Cleanup();

            buttonStartBot.IsEnabled = true;
            buttonStopBot.IsEnabled = false;
        }

        private void buttonTestAll_Click(object sender, RoutedEventArgs e)
        {
            cmbContentTypes.IsEnabled = false;
            buttonGetContentList.IsEnabled = false;
            buttonStartBot.IsEnabled = false;
            buttonStopBot.IsEnabled = false;
            buttonSaveConfiguration.IsEnabled = false;
            buttonTestAll.IsEnabled = false;
            buttonListFiles.IsEnabled = false;

            HashSet<ContentType> types = new HashSet<ContentType>() { ContentType.REPORT, ContentType.NOTICE };
            NotifierManager = new KidsNoteNotifierManager(types);

            DateTime scheduled = DateTime.Now;
            scheduled += new TimeSpan(0, 0, 0, 5);

            KidsNoteScheduleParameters param = new KidsNoteScheduleParameters();
            param.Days = KidsNoteScheduleParameters.DaysType.MON_FRI;
            param.Job = KidsNoteScheduleParameters.JobType.JOB_CHECK_NEW_CONTENTS;
            NotifierManager.AddJob(param);
            NotifierManager.Startup();
        }

        private void buttonSaveConfiguration_Click(object sender, RoutedEventArgs e)
        {
            Conf.KidsNoteId = textKidsNoteId.Text;
            Conf.KidsNotePassword = kidsNotePassword.Password;
            Conf.TelegramBotToken = textTelegramBotToken.Text;

            string[] tokens = textTelegramUsers.Text.Split(',');
            Conf.SubscriberIdList.Clear();
            foreach (var each in tokens)
            {
                long id = 0;
                if (long.TryParse(each.Trim(), out id))
                {
                    Conf.SubscriberIdList.Add(id);
                }
            }

            long adminUserId = 0;
            if (!long.TryParse(textTelegramMainUserId.Text.Trim(), out adminUserId))
            {
                adminUserId = 0;
            }
            Conf.ManagerChatId = adminUserId;
            Conf.ChildName = textChildName.Text;

            Conf.Save(SetupFilePath());
        }

        private void buttonListFiles_Click(object sender, RoutedEventArgs e)
        {
            if (Uploader == null)
            {
                Uploader = new LibKidsNoteNotifier.GoogleDrive.Uploader(CredentialsPath(), TokenFilePath(), Conf.ChildName);
                Uploader.GetBaseFolderId = this.GetBaseFolderId;
                Uploader.SetBaseFolderId = this.SetBaseFolderId;
                Uploader.Startup();
            }

            Uploader.List();
        }

        private string GetBaseFolderId()
        {
            if (Conf == null)
            {
                Conf = GetConfiguration();
            }

            return Conf.GoogleDriveBackupFolderId;
        }

        private void SetBaseFolderId(string id)
        {
            if (Conf == null)
            {
                Conf = GetConfiguration();
            }

            Conf.GoogleDriveBackupFolderId = id;
            Conf.Save(SetupFilePath());
        }
    }
}