using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteContent
    {
        public ContentType Type { get; set; }
        public string ChildName { get; set; }
        public KidsNoteChildEnrollment  Enrollment { get; set; }
        public UInt64 Id { get; set; }
        public string PageUrl { get; set; }
        public string OriginalPageUrl { get; set; }
        public string Title { get; set; }
        public string Writer { get; set; }
        public string Content { get; set; }
        public List<Attachment> Attachments { get; set; }
        public DateTime Date { get; set; }
        public Dictionary<string, string> StatusReport;

        public class Attachment
        {
            public AttachmentType Type { get; set; }
            public string Name { get; set; }
            public string ImageSource { get; set; }
            public string DownloadUrl { get; set; }
            //public byte[] Data { get; set; }
            public Stream Data { get; set; }
            public string Description { get; set; }
            public bool DownloadOnTheFly { get; set; }

            public Attachment(AttachmentType type, string name = "", string url = "", string imageSource = "")
            {
                Type = type;
                Name = name;
                DownloadUrl = url;
                ImageSource = imageSource;
                Description = "";
                DownloadOnTheFly = false;
            }
        }

        public KidsNoteContent(ContentType type)
        {
            Type = type;
            ChildName = "";
            Enrollment = new KidsNoteChildEnrollment();
            Id = 0;
            OriginalPageUrl = "";
            PageUrl = "";
            Title = "";
            Writer = "";
            Date = DateTime.MinValue;
            Content = "";
            Attachments = new List<Attachment>();
            StatusReport = new Dictionary<string, string>();
        }

        public string FormatContent()
        {
            StringBuilder sb = new StringBuilder();

            if (Type == ContentType.MENUTABLE)
            {
                sb.AppendFormat("{0} 식단표\n", DateTime.Now);

                foreach (var attach in Attachments)
                {
                    string menuType = ContentTypeConverter.AttachmentLunchTypeToString(attach.Type);
                    sb.AppendFormat("\n{0} : {1}", menuType, attach.Description);
                }
            }
            else if (Type == ContentType.ALBUM)
            {
                sb.AppendFormat("[{0}] {1} ({2}), 작성자 : {3}", Enrollment.ClassName, ContentTypeConverter.ContentTypeToString(Type), Id, Writer);
                sb.Append("\n\n");
                sb.Append(GetContentString());
            }
            else
            {
                sb.AppendFormat("\n[{0}] 제목 : {1} ({2}), 작성자 : {3}", Enrollment.ClassName, Title, Id, Writer);
                sb.Append("\n\n");
                sb.Append(GetContentString());
            }

            return sb.ToString();
        }

        public string GetContentString()
        {
            if (StatusReport != null && StatusReport.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(Content + "\n");

                foreach (var each in StatusReport)
                {
                    sb.AppendFormat("\n{0} : {1}", each.Key, each.Value);
                }

                return sb.ToString();
            }
            else
            {
                return Content;
            }
        }
    }
}
