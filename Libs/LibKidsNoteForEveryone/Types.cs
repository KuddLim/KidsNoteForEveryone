using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public enum ContentType
    {
        UNSPECIFIED = 0,
        REPORT,             // 알림장
        NOTICE,             // 공지사항
        ALBUM,              // 앨범
        CALENDAR,           // 일정표
        MENUTABLE,          // 식단표
        MEDS_REQUEST,       // 투약의뢰서
        RETURN_HOME_NOTICE, // 귀가동의서
        ALL,                // 모든 타입
    };

    public enum AttachmentType
    {
        IMAGE = 0,
        VIDEO,
        IMAGE_MENU_MORNING_SNACK,       // 오전간식
        IMAGE_MENU_LUNCH,               // 점심
        IMAGE_MENU_AFTERNOON_SNACK,     // 오후간식
        IMAGE_MENU_DINNER,              // 저녁
        IMAGE_MENU_DEFAULT_IMAGE,       // 기본이미지
        OTHER,
    };

    public class ContentTypeConverter
    {
        public static string AttachmentLunchTypeToString(AttachmentType type)
        {
            switch (type)
            {
                case AttachmentType.IMAGE_MENU_AFTERNOON_SNACK:
                    return "오후간식";
                case AttachmentType.IMAGE_MENU_DINNER:
                    return "저녁";
                case AttachmentType.IMAGE_MENU_LUNCH:
                    return "점심";
                case AttachmentType.IMAGE_MENU_MORNING_SNACK:
                    return "오전간식";
                default:
                    return "알수없음";
            }
        }

        public static string ContentTypeToString(ContentType type)
        {
            switch (type)
            {
                case ContentType.ALBUM:
                    return "앨범";
                case ContentType.CALENDAR:
                    return "일정표";
                case ContentType.MEDS_REQUEST:
                    return "투약의뢰서";
                case ContentType.MENUTABLE:
                    return "식단표";
                case ContentType.NOTICE:
                    return "공지사항";
                case ContentType.REPORT:
                    return "알림장";
                case ContentType.RETURN_HOME_NOTICE:
                    return "귀가동의서";
                default:
                    break;
            }

            return "";
        }

        public static ContentType StringToContentType(string s)
        {
            if (s == "알림장")
            {
                return ContentType.REPORT;
            }
            else if (s == "공지사항")
            {
                return ContentType.NOTICE;
            }
            else if (s == "앨범")
            {
                return ContentType.ALBUM;
            }
            else if (s == "일정표")
            {
                return ContentType.CALENDAR;
            }
            else if (s == "식단표")
            {
                return ContentType.MENUTABLE;
            }
            else if (s == "투약의뢰서")
            {
                return ContentType.MEDS_REQUEST;
            }
            else if (s == "귀가동의서")
            {
                return ContentType.RETURN_HOME_NOTICE;
            }
            else
            {
                return ContentType.UNSPECIFIED;
            }
        }
    }
}
