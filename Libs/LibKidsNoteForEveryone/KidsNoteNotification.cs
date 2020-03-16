using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteNotification
    {
        public ContentType ContentType;
        public HashSet<Telegram.Bot.Types.ChatId> Receivers;
        public LinkedList<KidsNoteContent> Contents;

        public KidsNoteNotification()
        {
            ContentType = ContentType.UNSPECIFIED;
            Receivers = new HashSet<Telegram.Bot.Types.ChatId>();
            Contents = new LinkedList<KidsNoteContent>();
        }
    }
}
