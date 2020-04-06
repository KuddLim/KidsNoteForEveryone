using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class Constants
    {
        public const string KIDSNOTE_URL = "https://www.kidsnote.com";
        public const string KIDSNOTE_LOGIN_POST_URL = KIDSNOTE_URL + "/login/";
        public const string KIDSNOTE_ROLE_POST_URL = KIDSNOTE_URL + "/accounts/role/name/";

        public const string KISNOTE_SCHEDULER_GROUP_NAME = "KIDS_NOTE_DEFAULT_GROUP";
        public const string CHROME_REDIRECT_URI = "https://ydbong.com/chrome_redirect.php?url=";

        public const string GOOGLE_DRIVE_APPLICATION_NAME = "KidsNote Uploader";
#if DEBUG
        public const string GOOGLE_DRIVE_BACKUP_FOLDER_NAME = "[개발용] 키즈노트 백업";
#else
        public const string GOOGLE_DRIVE_BACKUP_FOLDER_NAME = "키즈노트 백업";
#endif
        public const string GOOGLE_DRIVE_MIMETYPE_FOLDER = "application/vnd.google-apps.folder";
        public const string GOOGLE_DRIVE_MIMETYPE_TEXT = "text/plain";
        public const string GOOGLE_DRIVE_MIMETYPE_JPG = "image/jpeg";
        public const string GOOGLE_DRIVE_MIMETYPE_PNG = "image/png";
        public const string GOOGLE_DRIVE_MIMETYPE_PDF = "application/pdf";
        public const string GOOGLE_DRIVE_MIMETYPE_BIN = "application/octet-stream";
        public const string GOOGLE_DRIVE_MIMETYPE_MSWORD = "application/msword";
        public const string GOOGLE_DRIVE_MIMETYPE_MSWORD_DOCX = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        public const string GOOGLE_DRIVE_MIMETYPE_MSPPT = "application/vnd.ms-powerpoint";
        public const string GOOGLE_DRIVE_MIMETYPE_MSPPT_PPTX = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        public const string GOOGLE_DRIVE_MIMETYPE_MSEXCEL = "application/vnd.ms-excel";
        public const string GOOGLE_DRIVE_MIMETYPE_MSEXCEL_XLSX = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public const string GOOGLE_DRIVE_MIMETYPE_MP4 = "video/mp4";
        public const string GOOGLE_DRIVE_MIMETYPE_MOV = "video/mov";

    }
}
