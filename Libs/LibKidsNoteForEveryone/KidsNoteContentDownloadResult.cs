using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteContentDownloadResult
    {
        public string Html { get; set; }
        public string Description { get; set; }
        public LinkedList<KidsNoteContent> ContentList { get; set; }
        public bool NotNow { get; set; }

        public KidsNoteContentDownloadResult()
        {
            Html = "";
            Description = "";
            NotNow = false;
        }
    }
}
