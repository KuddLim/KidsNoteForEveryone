using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteContentDownloadResult
    {
        public string Content { get; set; }
        public string Description { get; set; }
        public LinkedList<KidsNoteContent> ContentList { get; set; }
        public bool NotNow { get; set; }
        public string NextPageToken { get; set; }

        public KidsNoteContentDownloadResult()
        {
            Content = "";
            Description = "";
            NotNow = false;
            NextPageToken = "";
        }
    }
}
