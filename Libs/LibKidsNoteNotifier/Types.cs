using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteNotifier
{
    public enum ContentType
    {
        UNSPECIFIED = 0,
        REPORT,             // 알림장
        NOTICE,             // 공지사항
        ALBUM,              // 앨범
        CALENDAR,           // 일정표
        MENUTABLE,          // 식단표
        MEDS_REQUEST,        // 투약의뢰서
        RETURN_HOME_NOTICE, // 귀가동의서
    };

    public enum AttachmentType
    {
        IMAGE = 0,
        OTHER,
    };
}
