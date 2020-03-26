using LibKidsNoteForEveryone;
using LibKidsNoteForEveryone.Fundamentals;
using MahApps.Metro.Controls;
using System;
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
using Telegram.Bot.Types;

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

        public MainWindow()
        {
            InitializeComponent();

            HashSet<ContentType> types = new HashSet<ContentType>() { ContentType.REPORT, ContentType.NOTICE, ContentType.ALBUM };
            TheManager = new KidsNoteNotifierManager(types);
            TheManager.OnGetNewContents = this.OnGetNewContents;
            TheManager.OnUploadProgressMessage = this.OnUploadProgressMessage;

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
            foreach (var each in TheConfiguration.AllBoardSubscribers)
            {
                moderators.AddLast(each.Identifier.ToString());
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

            TheConfiguration.SubScriberMap[PreviousContentType] = GetUseChatIds();
            PreviousContentType = contentType;

            textTelegramUserChatIds.Text = String.Join(",", TheConfiguration.SubScriberMap[contentType]);
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

        private void OnGetNewContents(Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents)
        {
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
    }
}
