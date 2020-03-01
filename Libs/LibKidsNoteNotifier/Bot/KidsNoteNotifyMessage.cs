using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteNotifier.Bot
{
    public class KidsNoteNotifyMessage
    {
        public Dictionary<ContentType, LinkedList<KidsNoteContent>> NewContents;

        public KidsNoteNotifyMessage()
        {
            NewContents = new Dictionary<ContentType, LinkedList<KidsNoteContent>>();
        }
        public KidsNoteNotifyMessage(Dictionary<ContentType, LinkedList<KidsNoteContent>> contents)
        {
            NewContents = contents;
        }
    }
}
