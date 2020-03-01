using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace LibKidsNoteNotifier.Bot
{
    public class ResponseMessage
    {
        public enum MessageType
        {
            KIDS_NOTE_MESSAGE = 0,
            GENERAL_MESSAGE,
        }

        public MessageType Type { get; set; }
        public List<ChatId> ChatIds;
        public string Message { get; set; }
        public KidsNoteNotifyMessage KidsNoteMessage { get; set; }

        public ResponseMessage(List<ChatId> chatIds, string message)
        {
            Type = MessageType.GENERAL_MESSAGE;
            ChatIds = chatIds;
            Message = message;
        }

        public ResponseMessage(List<ChatId> chatIds, KidsNoteNotifyMessage message)
        {
            Type = MessageType.KIDS_NOTE_MESSAGE;
            ChatIds = chatIds;
            KidsNoteMessage = message;
        }
    }

    public class ResponseMessageQueue : Fundamentals.Looper
    {
        private NotifierBot Bot;
        private Queue<ResponseMessage> ResponseQueue;
        private object Locker;

        public ResponseMessageQueue(NotifierBot bot)
            : base("BotMessageQueue")
        {
            Bot = bot;
            Locker = new object();

            ResponseQueue = new Queue<ResponseMessage>();
        }

        public void Cleanup()
        {
            lock (Locker)
            {
                ResponseQueue.Clear();
            }

            Quit();
            Join();
        }

        public void Enqueue(List<ChatId> chatIds, KidsNoteNotifyMessage message)
        {
            lock (Locker)
            {
                ResponseQueue.Enqueue(new ResponseMessage(chatIds, message));
            }
        }

        public void Enqueue(List<ChatId> chatIds, string message)
        {
            lock (Locker)
            {
                ResponseQueue.Enqueue(new ResponseMessage(chatIds, message));
            }
        }

        protected override void LooperFunc()
        {
            bool hasItem = false;

            Monitor.Enter(Locker);

            hasItem = ResponseQueue.Count > 0;

            while (hasItem)
            {
                var message = ResponseQueue.Peek();
                ResponseQueue.Dequeue();

                Monitor.Exit(Locker);

                Bot.SendResponseMessage(message);

                Monitor.Enter(Locker);
                hasItem = ResponseQueue.Count > 0;
            }

            if (Monitor.IsEntered(Locker))
            {
                Monitor.Exit(Locker);
            }

            if (!hasItem)
            {
                Thread.Sleep(50);

                lock (Locker)
                {
                    hasItem = ResponseQueue.Count > 0;
                }
            }
        }
    }
}
