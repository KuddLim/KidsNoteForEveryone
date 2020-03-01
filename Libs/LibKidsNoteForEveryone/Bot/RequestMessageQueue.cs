using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone.Bot
{
    public class RequestMessage
    {
        public long ChatId { get; set; }
        public string Message { get; set; }

        public RequestMessage()
        {
            ChatId = 0;
            Message = "";
        }

        public RequestMessage(long chatId, string message)
        {
            ChatId = chatId;
            Message = message;
        }
    }

    public class RequestMessageQueue : Fundamentals.Looper
    {
        private NotifierBot Bot;
        private Queue<RequestMessage> ResponseQueue;
        private object Locker;

        public RequestMessageQueue(NotifierBot bot)
            : base("BotMessageRequestQueue")
        {
            Bot = bot;
            Locker = new object();

            ResponseQueue = new Queue<RequestMessage>();
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

        public void Enqueue(long chatId, string message)
        {
            lock (Locker)
            {
                ResponseQueue.Enqueue(new RequestMessage(chatId, message));
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

                Bot.HandleMessage(message);

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
