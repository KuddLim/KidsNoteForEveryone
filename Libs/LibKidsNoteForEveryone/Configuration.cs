using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LibKidsNoteForEveryone
{
    public class UserGroup
    {
        [JsonProperty("subscribe_all_boards")]
        public bool SubScribeAllBoards { get; set; }
        [JsonProperty("subscriptions")]
        public List<ContentType> Subscriptions { get; set; }
        [JsonProperty("members")]
        public HashSet<Telegram.Bot.Types.ChatId> Members { get; set; }
        [JsonProperty("include_word")]
        public string IncludeWord { get; set; }
        [JsonProperty("exclude_word")]
        public string ExcludeWord { get; set; }

        public UserGroup()
        {
            SubScribeAllBoards = false;
            Subscriptions = new List<ContentType>();
            Members = new HashSet<Telegram.Bot.Types.ChatId>();
            IncludeWord = "";
            ExcludeWord = "";
        }

        public List<string> GetSubscribers(ContentType type)
        {
            List<string> subscribers = new List<string>();
            if (Subscriptions.Contains(type))
            {
                foreach (var each in Members)
                {
                    subscribers.Add(each.Identifier.ToString());
                }
            }
            return subscribers;
        }
    }

    public class Configuration
    {
        public static long GROUP_ID_ADMINISTRATOR = 0;
        public static long GROUP_ID_UNSPECIFIED = 9999999;

        // 텔레그램 봇과 관리자 간의 텔레그램 Chat ID
        [JsonProperty("manager_chat_id")]
        public Telegram.Bot.Types.ChatId ManagerChatId { get; set; }

        // 텔레그램 봇과 구독자 단체대화방간의 텔레그램 Chat ID
        /*
        [JsonProperty("all_board_subscribers")]
        public HashSet<Telegram.Bot.Types.ChatId> AllBoardSubscribers { get; set; }
        [JsonProperty("subscriber_map")]
        public Dictionary<ContentType, HashSet<Telegram.Bot.Types.ChatId>> SubScriberMap;
        */

        [JsonProperty("subscriber_groups")]
        public Dictionary<ContentType, HashSet<long>> SubscriberGroups;     // value : groupId
        [JsonProperty("subscribers")]
        public Dictionary<long, UserGroup> Subscribers { get; set; }        // key : groupId

        // BotFather 로 생성한 텔레그램 봇 token
        [JsonProperty("telebram_bot_token")]
        public string TelegramBotToken { get; set; }

        // 키즈노트 새글 체크시간
        [JsonProperty("operation_hour_begin")]
        public int OperationHourBegin { get; set; }
        [JsonProperty("operation_hour_end")]
        public int OperationHourEnd { get; set; }

        // 키즈노트 ID
        [JsonProperty("kidsnote_id")]
        public string KidsNoteId { get; set; }
        // 키즈노트 비밀번호
        [JsonProperty("kidsnote_password")]
        public string KidsNotePassword { get; set; }
        // 자녀이름 때는 애칭
        [JsonProperty("child_name")]
        public string ChildName { get; set; }

        // 텔레그램 사진 첨부여부.
        // True 이면 메시지로 사진을 전송하는데, 여러장을 보내는 경우 메시지 상에서 앨범처럼 보이고
        // 스와이프도 가능하지만, 텔레그램의 데이터 크기가 커진다.
        // False 이면 사진의 링크를 전송한다. 여러장을 보내도 각각의 링크가 따로 간다.
        // 스와이프가 불가능한다.
        [JsonProperty("send_image_as_attachment")]
        public bool SendImageAsAttachment { get; set; }

        // 구글 드라이브 백업여부.
        [JsonProperty("backup_to_googledrive")]
        public bool BackupToGoogleDrive;

        // 구글 드라이브 백업 폴더 ID
        // 프로그램 동작중에 생성되므로 사용하자 수정해서는 안되는 값이다.
        [JsonProperty("google_drive_backup_folder_id")]
        private string GoogleDriveBackupFolderId { get; set; }

        // 프로그램 동작중에 생성되므로 사용하자 수정해서는 안되는 값이다.
        [JsonProperty("google_drive_backup_folder_id_debug")]
        private string GoogleDriveBackupFolderIdDebug { get; set; }

        // 구글 드라이브 백업시 암호화 여부.
        [JsonProperty("encrypt_upload")]
        public bool EncryptUpload { get; set; }

        // 로그 사용여부 (일반적인 경우 사용하지 않아도 됨).
        [JsonProperty("use_logger")]
        public bool UseLogger { get; set; }

        static HashSet<ContentType> KnownContentTypes = new HashSet<ContentType>()
        {
            ContentType.REPORT, ContentType.NOTICE, ContentType.ALBUM, ContentType.CALENDAR,
            ContentType.MENUTABLE, ContentType.MEDS_REQUEST, ContentType.RETURN_HOME_NOTICE,
        };

        public Configuration()
        {
            ManagerChatId = 0;
            //AllBoardSubscribers = new HashSet<Telegram.Bot.Types.ChatId>();
            Subscribers = new Dictionary<long, UserGroup>();
            //SubScriberMap = new Dictionary<ContentType, HashSet<Telegram.Bot.Types.ChatId>>();
            SubscriberGroups = new Dictionary<ContentType, HashSet<long>>();
            foreach (ContentType ct in KnownContentTypes)
            {
                //SubScriberMap[ct] = new HashSet<Telegram.Bot.Types.ChatId>();
                SubscriberGroups[ct] = new HashSet<long>();
            }

            TelegramBotToken = "";
            KidsNoteId = "";
            KidsNotePassword = "";
            ChildName = "";
            BackupToGoogleDrive = false;
            GoogleDriveBackupFolderId = "";
        }

        private void CheckNullFields()
        {
            /*
            if (AllBoardSubscribers == null)
            {
                AllBoardSubscribers = new HashSet<Telegram.Bot.Types.ChatId>();
            }
            */

            /*
            foreach (ContentType ct in KnownContentTypes)
            {
                if (!SubScriberMap.ContainsKey(ct))
                {
                    SubScriberMap[ct] = new HashSet<Telegram.Bot.Types.ChatId>();
                }
            }
            */

            if (Subscribers == null)
            {
                Subscribers = new Dictionary<long, UserGroup>();
            }

            List<long> defaultGroups = new List<long>() { GROUP_ID_ADMINISTRATOR, GROUP_ID_UNSPECIFIED };
            foreach (long grpId in defaultGroups)
            {
                if (!Subscribers.ContainsKey(grpId))
                {
                    Subscribers[grpId] = new UserGroup();
                }
            }
        }

        public static Configuration FromJson(string json)
        {
            Configuration conf = JsonConvert.DeserializeObject<Configuration>(json);
            conf.CheckNullFields();
            conf.KidsNoteId = EncryptorAES.DecryptAes(conf.KidsNoteId, EncryptorAES.DefaultAesEncKey);
            conf.KidsNotePassword = EncryptorAES.DecryptAes(conf.KidsNotePassword, EncryptorAES.DefaultAesEncKey);

            return conf;
        }

        public string ToJson()
        {
            string prevId = KidsNoteId;
            string prevPassword = KidsNotePassword;
            KidsNoteId = EncryptorAES.EncryptAes(KidsNoteId, EncryptorAES.DefaultAesEncKey);
            KidsNotePassword = EncryptorAES.EncryptAes(KidsNotePassword, EncryptorAES.DefaultAesEncKey);

            string json = JsonConvert.SerializeObject(this, Formatting.Indented);

            KidsNoteId = prevId;
            KidsNotePassword = prevPassword;

            return json;
        }

        public void Save(string file)
        {
            System.IO.File.WriteAllText(file, ToJson());
        }

        public void AddSubscriber(long id, HashSet<ContentType> exclusions)
        {
            /*
            if (exclusions.Count == 0)
            {
                //AllBoardSubscribers.Add(id);
                SubscriberGroups[ContentType.ALL].Add
            }
            else
            {
                foreach (ContentType ct in KnownContentTypes)
                {
                    if (!exclusions.Contains(ct))
                    {
                        SubScriberMap[ct].Add(id);
                    }
                }
            }
            */
        }

        public HashSet<long> GetSubscribers(ContentType contentType)
        {
            HashSet<long> subscribers = new HashSet<long>();

            if (ManagerChatId.Identifier != 0)
            {
                subscribers.Add(ManagerChatId.Identifier);
            }

            /*
            foreach (var each in AllBoardSubscribers)
            {
                subscribers.Add(each.Identifier);
            }

            if (SubScriberMap.ContainsKey(contentType))
            {
                foreach (var each in SubScriberMap[contentType])
                {
                    subscribers.Add(each.Identifier);
                }
            }
            */



            return subscribers;
        }

        public string GetGoogleDriveBackupFolderId()
        {
#if DEBUG
            return GoogleDriveBackupFolderIdDebug;
#else
            return GoogleDriveBackupFolderId;
#endif
        }

        public void SetGoogleDriveBackupFolderId(string id)
        {
#if DEBUG
            GoogleDriveBackupFolderIdDebug = id;
#else
            GoogleDriveBackupFolderId = id;
#endif
        }
    }
}
