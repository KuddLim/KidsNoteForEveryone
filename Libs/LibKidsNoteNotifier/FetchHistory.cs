using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteNotifier
{
    public class FetchHistory
    {
        [JsonProperty("last_report_id")]
        public UInt64 LastReportId { get; set; }
        [JsonProperty("last_notice_id")]
        public UInt64 LastNoticeId { get; set; }
        [JsonProperty("last_album_id")]
        public UInt64 LastAlbumId { get; set; }
        [JsonProperty("last_calendar_id")]
        public UInt64 LastCalendarId { get; set; }
        [JsonProperty("last_menutable_id")]
        public UInt64 LastMenuTableId { get; set; }
        [JsonProperty("last_medication_request_id")]
        public UInt64 LastMedicationRequestId { get; set; }
        [JsonProperty("last_return_home_notice_id")]
        public UInt64 LastReturnHomeNoticeId { get; set; }

        public static FetchHistory FromJson(string json)
        {
            return JsonConvert.DeserializeObject<FetchHistory>(json);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public void Save(string file)
        {
            System.IO.File.WriteAllText(file, ToJson());
        }

        public UInt64 GetLastContentId(ContentType type)
        {
            switch (type)
            {
                case ContentType.ALBUM:
                    return LastAlbumId;
                case ContentType.CALENDAR:
                    return LastCalendarId;
                case ContentType.MEDS_REQUEST:
                    return LastMedicationRequestId;
                case ContentType.MENUTABLE:
                    return LastMenuTableId;
                case ContentType.NOTICE:
                    return LastNoticeId;
                case ContentType.REPORT:
                    return LastReportId;
                case ContentType.RETURN_HOME_NOTICE:
                    return LastReturnHomeNoticeId;
                default:
                    break;
            }

            return UInt64.MaxValue;
        }

        public void SetLastContentId(ContentType type, UInt64 id)
        {
            switch (type)
            {
                case ContentType.ALBUM:
                    LastAlbumId = id;
                    break;
                case ContentType.CALENDAR:
                    LastCalendarId = id;
                    break;
                case ContentType.MEDS_REQUEST:
                    LastMedicationRequestId = id;
                    break;
                case ContentType.MENUTABLE:
                    LastMenuTableId = id;
                    break;
                case ContentType.NOTICE:
                    LastNoticeId = id;
                    break;
                case ContentType.REPORT:
                    LastReportId = id;
                    break;
                case ContentType.RETURN_HOME_NOTICE:
                    LastReturnHomeNoticeId = id;
                    break;
                default:
                    break;
            }
        }
    }
}
