using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace LibKidsNoteForEveryone.Bot
{
    public class ResponseMessage
    {
        public enum MessageType
        {
            KIDS_NOTE_MESSAGE = 0,
            GENERAL_MESSAGE,
        }

        public MessageType Type { get; set; }
        public HashSet<ChatId> ChatIds;
        public string Message { get; set; }
        public KidsNoteNotification Notification { get; set; }

        public ResponseMessage(HashSet<ChatId> chatIds, string message)
        {
            Type = MessageType.GENERAL_MESSAGE;
            ChatIds = chatIds;
            Message = message;
        }

        public ResponseMessage(HashSet<ChatId> chatIds, KidsNoteNotification notification)
        {
            Type = MessageType.KIDS_NOTE_MESSAGE;
            ChatIds = chatIds;
            Notification = notification;
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

        public void Enqueue(Dictionary<ContentType, KidsNoteNotification> notification)
        {
            lock (Locker)
            {
                foreach (var each in notification)
                {
                    ResponseQueue.Enqueue(new ResponseMessage(each.Value.Receivers, each.Value));
                }
            }
        }

        public void Enqueue(HashSet<ChatId> chatIds, string message)
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
