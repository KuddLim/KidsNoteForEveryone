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
        public UInt64 Id { get; set; }
        public string PageUrl { get; set; }
        public string OriginalPageUrl { get; set; }
        public string Title { get; set; }
        public string Writer { get; set; }
        public string Content { get; set; }
        public List<Attachment> Attachments { get; set; }
        public DateTime Date { get; set; }

        public class Attachment
        {
            public AttachmentType Type { get; set; }
            public string Name { get; set; }
            public string ImageSource { get; set; }
            public string DownloadUrl { get; set; }
            //public byte[] Data { get; set; }
            public Stream Data { get; set; }

            public Attachment(AttachmentType type, string name = "", string url = "", string imageSource = "")
            {
                Type = type;
                Name = name;
                DownloadUrl = url;
                ImageSource = imageSource;
            }
        }

        public KidsNoteContent(ContentType type)
        {
            Type = type;
            Id = 0;
            OriginalPageUrl = "";
            PageUrl = "";
            Title = "";
            Writer = "";
            Date = DateTime.MinValue;
            Content = "";
            Attachments = new List<Attachment>();
        }
    }
}
