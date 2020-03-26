using LibKidsNoteForEveryone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KidsNoteForEveryoneSA
{
    public class KidsNoteListViewItem
    {
        public ulong Id { get; set; }
        public ContentType ContentType { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public int NumAttachments { get; set; }

        public KidsNoteListViewItem(ulong id, ContentType contentType, string title, string author, int numAttachments)
        {
            Id = id;
            ContentType = contentType;
            Title = title;
            Author = author;
            NumAttachments = numAttachments;
        }
    }

}
