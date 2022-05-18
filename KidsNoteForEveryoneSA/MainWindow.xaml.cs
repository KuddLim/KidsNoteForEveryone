using LibKidsNoteForEveryone;
using LibKidsNoteForEveryone.Fundamentals;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Data;
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
using System.IO;
using Telegram.Bot.Types;
using Microsoft.Win32;

namespace KidsNoteForEveryoneSA
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private KidsNoteNotifierManager TheManager;
        private Configuration TheConfiguration;
        private const int BaseBeginHour = 6;
        private const int BaseEndHour = 13;
        private ContentType PreviousContentType;
        private HashSet<KeyValuePair<ContentType, ulong>> SelectedContents;
        Dictionary<ContentType, LinkedList<KidsNoteContent>> LatestContents;
        private static long TEMPORARY_GROUP_ID = 1000;

        public MainWindow()
        {
            InitializeComponent();

            //HashSet<ContentType> types = new HashSet<ContentType>() { ContentType.REPORT, ContentType.NOTICE, ContentType.ALBUM };
            HashSet<ContentType> types = new HashSet<ContentType>() { ContentType.REPORT, ContentType.NOTICE, ContentType.ALBUM, ContentType.MENUTABLE };
            //HashSet<ContentType> types = new HashSet<ContentType>() { ContentType.ALBUM };
            TheManager = new KidsNoteNotifierManager(types);
            TheManager.OnGetNewContents = this.OnGetNewContents;
            TheManager.OnUploadProgressMessage = this.OnUploadProgressMessage;

            SelectedContents = new HashSet<KeyValuePair<ContentType, ulong>>();

            InitUi();
        }

        private string SetupFilePath()
        {
            string path = "";

            if (Platform.IsRunningOnMono())
            {
                path = "config.json";
            }
            else
            {
                path = AppDomain.CurrentDomain.BaseDirectory + System.IO.Path.DirectorySeparatorChar + "config.json";
            }

            return path;
        }

        private void HandleConfigurationLoadFailed()
        {

        }

        private void InitUi()
        {
            AddLog("Application started");

            TheConfiguration = TheManager.GetConfiguration();

            AddLog("Configuration loaded");

            InitUi_Operation();
            InitUi_KidsNote();
            InitUi_Telegram();

            buttonStop.IsEnabled = false;
            buttonFetchNow.IsEnabled = false;

            AddLog("UI initialized");
        }

        private void InitUi_Operation()
        {
            GridView gv = new GridView();
            listViewContents.View = gv;

            List<string> headers = new List<string>()
            {
                "Id", "ContentType", "Title", "Author", "NumAttachments"
            };

            foreach (var each in headers)
            {
                gv.Columns.Add(new GridViewColumn
                {
                    Header = each,
                    DisplayMemberBinding = new Binding(each)
                });
            }
        }

        private void InitUi_KidsNote()
        {
            textKidsNoteID.Text = TheConfiguration.KidsNoteId;
            passwordKidsNotePassword.Password = TheConfiguration.KidsNotePassword;
            textChildName.Text = TheConfiguration.ChildName;

            for (int i = 0; i < 60; ++i)
            {
                comboFetchMinute.Items.Add("Minute " + i.ToString("00") + " on every hour");
            }
            comboFetchMinute.SelectedIndex = 0;
            comboFetchMinute.IsEnabled = false;

            for (int i = BaseBeginHour; i <= 10; ++i)
            {
                comboOperationBeginHour.Items.Add(i.ToString("00") + ":00");
            }
            comboOperationBeginHour.SelectedIndex =
                TheConfiguration.OperationHourBegin == 0 ? TheConfiguration.OperationHourBegin
                                                         : BaseBeginHour - TheConfiguration.OperationHourBegin;
            for (int i = BaseEndHour; i <= 20; ++i)
            {
                comboOperationEndHour.Items.Add(i.ToString("00") + ":00");
            }
            comboOperationEndHour.SelectedIndex =
                TheConfiguration.OperationHourEnd == 0 ? TheConfiguration.OperationHourEnd
                                                       : TheConfiguration.OperationHourEnd - BaseEndHour;

            checkBackupToGoogleDrive.IsChecked = TheConfiguration.BackupToGoogleDrive;
        }

        private void InitUi_Telegram()
        {
            textTelegramBotToken.Text = TheConfiguration.TelegramBotToken;
            textTelegramAdminChatId.Text = TheConfiguration.ManagerChatId.Identifier.ToString();

            LinkedList<string> moderators = new LinkedList<string>();
            /*
             * foreach (var each in TheConfiguration.AllBoardSubscribers)
            {
                moderators.AddLast(each.Identifier.ToString());
            }
            */
            foreach (var each in TheConfiguration.Subscribers)
            {
                if (each.Value.SubScribeAllBoards)
                {
                    moderators.Concat(each.Value.Members.Select(m => m.Identifier.ToString()));
                }
            }
            textTelegramModeratorChatIds.Text = String.Join(",", moderators);

            comboBoardTypes.Items.Add(ContentType.REPORT);
            comboBoardTypes.Items.Add(ContentType.NOTICE);
            comboBoardTypes.Items.Add(ContentType.ALBUM);
            comboBoardTypes.SelectedIndex = 0;

            PreviousContentType = ContentType.REPORT;

            fillSubscribers();
        }

        private void comboOperationBeginHour_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TheConfiguration.OperationHourBegin = comboOperationBeginHour.SelectedIndex + BaseBeginHour;
        }

        private void comboOperationEndHour_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TheConfiguration.OperationHourEnd = comboOperationEndHour.SelectedIndex + BaseEndHour;
        }

        private void comboBoardTypes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ContentType contentType = (ContentType)comboBoardTypes.SelectedItem;
            if (contentType == PreviousContentType)
            {
                return;
            }

            if (!TheConfiguration.Subscribers.ContainsKey(TEMPORARY_GROUP_ID))
            {
                TheConfiguration.Subscribers[TEMPORARY_GROUP_ID] = new UserGroup();
            }

            var userGroup = TheConfiguration.Subscribers[TEMPORARY_GROUP_ID];
            userGroup.Members = GetUseChatIds();
            PreviousContentType = contentType;

            textTelegramUserChatIds.Text = String.Join(",", userGroup.Members);
        }

        private HashSet<ChatId> GetUseChatIds()
        {
            HashSet<ChatId> subscribers = new HashSet<ChatId>();
            string[] idList = textTelegramUserChatIds.Text.Split(',');
            foreach (var each in idList)
            {
                long id = 0;
                if (long.TryParse(each.Trim(), out id))
                {
                    subscribers.Add(id);
                }
            }

            return subscribers;
        }

        private void fillSubscribers()
        {
            LinkedList<string> idList = new LinkedList<string>();
            ContentType currentType = (ContentType)comboBoardTypes.SelectedItem;

            if (TheConfiguration.SubScriberMap.ContainsKey(currentType))
            {
                foreach (var each in TheConfiguration.SubScriberMap[currentType])
                {
                    idList.AddLast(each.Identifier.ToString());
                }
            }

            textTelegramUserChatIds.Text = String.Join(",", idList);
        }

        private void checkSendImagesAsTelegramAttachments_Checked(object sender, RoutedEventArgs e)
        {
            TheConfiguration.SendImageAsAttachment = checkSendImagesAsTelegramAttachments.IsChecked.Value;
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            TheConfiguration.KidsNoteId = textKidsNoteID.Text.Trim();
            TheConfiguration.KidsNotePassword = passwordKidsNotePassword.Password.Trim();
            TheConfiguration.ChildName = textChildName.Text.Trim();
            TheConfiguration.OperationHourBegin = comboOperationBeginHour.SelectedIndex + BaseBeginHour;
            TheConfiguration.OperationHourEnd = comboOperationEndHour.SelectedIndex + BaseEndHour;
            TheConfiguration.BackupToGoogleDrive = checkBackupToGoogleDrive.IsChecked.Value;

            TheConfiguration.TelegramBotToken = textTelegramBotToken.Text;
            long adminChatId = 0;
            if (long.TryParse(textTelegramAdminChatId.Text.Trim(), out adminChatId))
            {
                TheConfiguration.ManagerChatId = adminChatId;
            }

            string[] moderators = textTelegramModeratorChatIds.Text.Split(',');
            TheConfiguration.AllBoardSubscribers.Clear();
            foreach (var each in moderators)
            {
                long id = 0;
                if (long.TryParse(each.Trim(), out id))
                {
                    TheConfiguration.AllBoardSubscribers.Add(id);
                }
            }

            ContentType contentType = (ContentType)comboBoardTypes.SelectedItem;
            TheConfiguration.SubScriberMap[PreviousContentType] = GetUseChatIds();
            TheConfiguration.SendImageAsAttachment = checkSendImagesAsTelegramAttachments.IsChecked.Value;

            TheConfiguration.Save(SetupFilePath());
        }

        private void AddLog(string log)
        {
            listBoxLogs.Items.Add(log);
            listBoxLogs.SelectedItem = listBoxLogs.Items.Count - 1;
            listBoxLogs.ScrollIntoView(listBoxLogs.SelectedItem);
        }

        private void buttonRun_Click(object sender, RoutedEventArgs e)
        {
            if (TheConfiguration.KidsNoteId == "")
            {
                MessageBox.Show("Please set KidsNote ID and save configuration", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (TheConfiguration.KidsNotePassword == "")
            {
                MessageBox.Show("Please set KidsNote password and save configuration", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (TheConfiguration.TelegramBotToken == "")
            {
                MessageBox.Show("Please set Telegram bot token and save configuration", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (TheConfiguration.ManagerChatId.Identifier == 0)
            {
                MessageBox.Show("Please set Telegram admin chat id token and save configuration", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AddLog("Starting manager..");

            //TheManager.Startup();

            AddLog("Manager started..");

            DateTime next = DateTime.Now.AddHours(1);
            DateTime nextFetch = new DateTime(next.Year, next.Month, next.Day, next.Hour, 0, 0);

            labelNextScheduledFetch.Content = "Next scheduled fetch : " + nextFetch.ToString("HH:mm:ss");

            buttonRun.IsEnabled = false;
            buttonStop.IsEnabled = true;
            buttonFetchNow.IsEnabled = true;
            (tabControl.Items[1] as TabItem).IsEnabled = false;

            KidsNoteScheduleParameters param = new KidsNoteScheduleParameters();
            param.Days = KidsNoteScheduleParameters.DaysType.WHOLE_WEEK;
            param.Job = KidsNoteScheduleParameters.JobType.JOB_CHECK_NEW_CONTENTS;
            TheManager.AddJob(param);

            TheManager.Startup();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            TheManager.Cleanup();

            buttonRun.IsEnabled = true;
            buttonStop.IsEnabled = false;
            buttonFetchNow.IsEnabled = false;

            (tabControl.Items[1] as TabItem).IsEnabled = true;
        }

        private void buttonFetchNow_Click(object sender, RoutedEventArgs e)
        {
            TheManager.DoScheduledCheck(false);
        }

        private void buttonUploadSelected_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<ContentType, LinkedList<KidsNoteContent>> toBackup
                = new Dictionary<ContentType, LinkedList<KidsNoteContent>>();

            foreach (var each in LatestContents)
            {
                LinkedList<KidsNoteContent> contents = new LinkedList<KidsNoteContent>();
                foreach (var content in each.Value)
                {
                    if (SelectedContents.Contains(new KeyValuePair<ContentType, ulong>(each.Key, content.Id)))
                    {
                        contents.AddLast(content);
                    }
                }

                if (contents.Count != 0)
                {
                    toBackup[each.Key] = contents;
                }
            }

            if (toBackup.Count != 0)
            {
                TheManager.BackupToGoogleDrive(toBackup);
            }
        }

        private void buttonTestChaCha_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                FileStream inStream = new FileStream(dialog.FileName, FileMode.Open);

                string encFile = dialog.FileName + ".enc";
                string decFile = dialog.FileName + ".dec";
                FileStream outStreamEnc = new FileStream(encFile, FileMode.Create);
                EncryptorChaCha chacha = new EncryptorChaCha(true, EncryptorChaCha.DefaultChaChaEncKey, EncryptorChaCha.DefaultChaChaEncNonce);

                byte[] readBuffer = new byte[4096];
                byte[] outBuffer = new byte[readBuffer.Length];
                int nRead = inStream.Read(readBuffer, 0, readBuffer.Length);
                int offset = 0;
                while (nRead > 0)
                {
                    if (offset == 0)
                    {
                        outStreamEnc.Write(chacha.Nonce, 0, chacha.Nonce.Length);
                    }
                    offset += nRead;
                    chacha.Process(readBuffer, 0, nRead, outBuffer, 0);
                    outStreamEnc.Write(outBuffer, 0, nRead);
                    nRead = inStream.Read(readBuffer, 0, readBuffer.Length);
                }

                inStream.Close();
                outStreamEnc.Close();

                byte[] nonce = new byte[EncryptorChaCha.DefaultChaChaEncNonce.Length];

                chacha = new EncryptorChaCha(false, EncryptorChaCha.DefaultChaChaEncKey, nonce);

                inStream = new FileStream(encFile, FileMode.Open);
                FileStream outStreamDec = new FileStream(decFile, FileMode.Create);

                nRead = inStream.Read(nonce, 0, nonce.Length);
                if (nRead > 0)
                {
                    nRead = inStream.Read(readBuffer, 0, readBuffer.Length);
                    while (nRead > 0)
                    {
                        chacha.Process(readBuffer, 0, nRead, outBuffer, 0);
                        outStreamDec.Write(outBuffer, 0, nRead);

                        nRead = inStream.Read(readBuffer, 0, readBuffer.Length);
                    }
                }

                inStream.Close();
                outStreamDec.Close();
            }
        }

        private void buttonDecrypt_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult dialogResult = dialog.ShowDialog();

            if (dialogResult == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                DecryptFolder(dialog.SelectedPath);
            }
        }

        private async Task DecryptFolder(string folder)
        {
            await Task.Run(() => DecryptFolderImpl(folder));
        }

        private void DecryptFolderImpl(string folder)
        {
            string[] files = Directory.GetFiles(folder);
            foreach (var file in files)
            {
                int pos = file.IndexOf(".chacha");
                if (pos < 0)
                {
                    continue;
                }

                string outFile = file.Substring(0, pos);

                EncryptorChaCha chacha = new EncryptorChaCha(false, EncryptorChaCha.DefaultChaChaEncKey, EncryptorChaCha.DefaultChaChaEncNonce);
                byte[] readBuffer = new byte[1024 * 16];
                byte[] writeBuffer = new byte[readBuffer.Length];

                FileStream inStream = new FileStream(file, FileMode.Open);
                FileStream outStream = new FileStream(outFile, FileMode.Create);

                int nBytes = inStream.Read(readBuffer, 0, readBuffer.Length);
                while (nBytes > 0)
                {
                    chacha.Process(readBuffer, 0, nBytes, writeBuffer, 0);
                    outStream.Write(writeBuffer, 0, nBytes);
                    nBytes = inStream.Read(readBuffer, 0, readBuffer.Length);
                }

                inStream.Close();
                outStream.Close();
            }

            string[] directories = Directory.GetDirectories(folder);
            foreach (var dir in directories)
            {
                DecryptFolderImpl(dir);
            }
        }

        private void OnGetNewContents(Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents)
        {
            LatestContents = newContents;
            Dispatcher.Invoke(new Action(() => FillNewContents(newContents)));
        }

        private void OnUploadProgressMessage(string message)
        {
            Dispatcher.Invoke(new Action(() => AddLog(message)));
        }

        private void FillNewContents(Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents)
        {
            foreach (var each in newContents)
            {
                foreach (var content in each.Value)
                {
                    KidsNoteListViewItem lvi = new KidsNoteListViewItem(content.Id, each.Key, content.Title, content.Writer, content.Attachments.Count);
                    listViewContents.Items.Add(lvi);
                }
            }
        }

        private void listViewContents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedContents.Clear();

            foreach (var each in listViewContents.SelectedItems)
            {
                KidsNoteListViewItem item = each as KidsNoteListViewItem;
                if (item != null)
                {
                    SelectedContents.Add(new KeyValuePair<ContentType, ulong>(item.ContentType, item.Id));
                }
            }

            buttonUploadSelected.IsEnabled = SelectedContents.Count != 0;
        }
    }
}
