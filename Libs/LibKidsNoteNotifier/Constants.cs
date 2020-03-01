using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteNotifier
{
    public class Constants
    {
        public const string KIDSNOTE_URL = "https://www.kidsnote.com";
        public const string KIDSNOTE_LOGIN_POST_URL = KIDSNOTE_URL + "/login/";
        public const string KIDSNOTE_ROLE_POST_URL = KIDSNOTE_URL + "/accounts/role/name/";

        public const string KISNOTE_SCHEDULER_GROUP_NAME = "KIDS_NOTE_DEFAULT_GROUP";

        public const string GOOGLE_DRIVE_APPLICATION_NAME = "KidsNote Uploader";
        public const string GOOGLE_DRIVE_BACKUP_FOLDER_NAME = "키즈노트 백업";
        public const string GOOGLE_DRIVE_MIMETYPE_FOLDER = "application/vnd.google-apps.folder";
    }
}
