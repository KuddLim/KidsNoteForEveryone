using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace LibKidsNoteForEveryone.Bot
{
    public class NotifierBot
    {
        public delegate long AdminUserChatIdDelegate();
        public delegate bool AddSubscriberDelegate(long chatId, HashSet<ContentType> exclusions);
        public delegate void AllNotificationsSentDelegate(KidsNoteNotification notification);

        private Telegram.Bot.TelegramBotClient TheClient;
        private RequestMessageQueue RequestQueue;
        private ResponseMessageQueue ResponseQueue;

        public AdminUserChatIdDelegate AdminUserChatId;
        public AddSubscriberDelegate AddSubscriber;
        public AllNotificationsSentDelegate AllNotificationsSent;

        public NotifierBot(string apiKey)
        {
            TheClient = new TelegramBotClient(apiKey);
            RequestQueue = new RequestMessageQueue(this);
            ResponseQueue = new ResponseMessageQueue(this);

            TheClient.OnMessage += this.OnMessageReceived;
            TheClient.OnCallbackQuery += this.OnCallbackQueryReceived;
            TheClient.OnInlineResultChosen += this.OnChosenInlineResultReceived;
            TheClient.OnInlineQuery += this.OnInlineQueryReceived;
            TheClient.OnReceiveError += this.OnReceiveError;
            TheClient.OnReceiveGeneralError += this.OnReceiveGeneralError;
            TheClient.OnUpdate += this.OnUpdateReceived;
        }

        public void Startup()
        {
            RequestQueue.Start();
            ResponseQueue.Start();

            try
            {
                TheClient.StartReceiving();
            }
            catch (Exception)
            {

            }
        }

        public void Cleanup()
        {
            ResponseQueue.Cleanup();

            if (TheClient.IsReceiving)
            {
                TheClient.StopReceiving();
            }

            RequestQueue.Cleanup();
        }

        public void HandleMessage(RequestMessage message)
        {
            bool handledAsAsmin = false;
            if (message.ChatId == AdminUserChatId())
            {
                string[] tokens = message.Message.Split(' ');
                if (tokens.Length == 3)
                {
                    long param = 0;
                    if (tokens[0] == "추가" && long.TryParse(tokens[1], out param))
                    {
                        string[] exclusionStrings = tokens[2].Split(',');

                        HashSet<ContentType> exclusions = new HashSet<ContentType>();
                        foreach (string ex in exclusionStrings)
                        {
                            ContentType ct = ContentTypeConverter.StringToContentType(ex);
                            if (ct == ContentType.UNSPECIFIED)
                            {
                                string error = String.Format("올바르지 않은 이름입니다 : {0}", ex);
                                ResponseQueue.Enqueue(new HashSet<long>() { message.ChatId }, error);
                                return;
                            }
                            else
                            {
                                exclusions.Add(ct);
                            }
                        }

                        if (AddSubscriber(param, exclusions))
                        {
                            handledAsAsmin = true;
                            ResponseQueue.Enqueue(new HashSet<long>(){message.ChatId}, "추가하였습니다");
                        }
                    }
                }
            }
            if (!handledAsAsmin)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("당신의 텔레그램 ChatId 는 {0} 입니다.\n이 봇은 등록된 사용자만 사용가능합니다.", message.ChatId);
                sb.AppendFormat("\n\nYour Telegram ChatId is {0}.\nThis bot is for registered users only.", message.ChatId);
                ResponseQueue.Enqueue(new HashSet<long>() { message.ChatId }, sb.ToString());
            }
        }

        public void SendNewContents(Dictionary<ContentType, KidsNoteNotification> notification)
        {
            ResponseQueue.Enqueue(notification);
        }

        public void SendAdminMessage(ChatId receiver, string message)
        {
            ResponseQueue.Enqueue(new HashSet<long>() { receiver.Identifier }, message);
        }

        // TODO: ResponseQueue 에서만 접근 가능하도록.
        public void SendResponseMessage(ResponseMessage message)
        {
            if (message.ChatIds.Count == 0)
            {
                return;
            }

            try
            {
                switch (message.Type)
                {
                    case ResponseMessage.MessageType.KIDS_NOTE_MESSAGE:
                        SendResponse(message.ChatIds, message.Notification);
                        break;
                    case ResponseMessage.MessageType.GENERAL_MESSAGE:
                        SendResponse(message.ChatIds, message.Message);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e);
                // TODO: ImplementMe.
            }
        }

        private void SendResponse(HashSet<long> receivers, KidsNoteNotification notification)
        {
            foreach (var eachContent in notification.Contents)
            {
                string text = FormatContent(eachContent.Type, eachContent);
                //TheClient.SendTextMessageAsync(text).Wait();

                List<Task<Message>> taskList = new List<Task<Message>>();
                foreach (var user in receivers)
                {
                    if (user != 0)
                    {
                        taskList.Add(TheClient.SendTextMessageAsync(user, text));
                    }
                }

                // 메시지 순서가 섞이지 않도록 모두 보내질 때까지 대기.
                foreach (var task in taskList)
                {
                    try
                    {
                        task.Wait();
                    }
                    catch (Exception)
                    {
                    }
                }

                List<MemoryStream> attachmentStreams = new List<MemoryStream>();
                //Dictionary<long, LinkedList<InputMediaPhoto>> photoAlbumPerUser = new Dictionary<long, LinkedList<InputMediaPhoto>>();

                bool hasAttachment = eachContent.Attachments != null && eachContent.Attachments.Count > 0;

                if (hasAttachment)
                {
                    taskList.Clear();

                    System.Diagnostics.Trace.WriteLine(String.Format("Title : {0}", eachContent.Title));

                    int no = 0;
                    //foreach (var attach in eachContent.Attachments)
                    for (int i = 0; i < eachContent.Attachments.Count; ++i)
                    {
                        var attach = eachContent.Attachments[i];
                        if (attach.Data == null)
                        {
                            // TODO: 관리 사용자에게 통지.
                            attachmentStreams.Add(null);
                        }
                        else
                        {
                            if (attach.Type == AttachmentType.IMAGE)
                            {
                                MemoryStream ms = new MemoryStream();
                                attach.Data.CopyTo(ms);
                                attachmentStreams.Add(ms);

                                System.Diagnostics.Trace.WriteLine(String.Format("Attachment {0} : {1}", no, attach.Name));
                                //InputMedia media = new InputMedia(ms, String.Format("{0}_{1}", ++no, attach.Name));
                            }
                            else
                            {
                                // Telegram 으로는 Image 만 보낸다.
                                // otherAttachments.AddLast(new InputMediaDocument(media));

                                attachmentStreams.Add(null);
                            }
                        }
                    }

                    List<Task<Message[]>> mediaTaskList = new List<Task<Message[]>>();
                    foreach (var user in receivers)
                    {
                        LinkedList<InputMediaPhoto> photoAlbum = new LinkedList<InputMediaPhoto>();
                        for (int i = 0; i < eachContent.Attachments.Count; ++i)
                        {
                            var attach = eachContent.Attachments[i];
                            if (attachmentStreams[i] != null)
                            {
                                MemoryStream copied = new MemoryStream();
                                attachmentStreams[i].Seek(0, SeekOrigin.Begin);
                                attachmentStreams[i].CopyTo(copied);
                                copied.Seek(0, SeekOrigin.Begin);
                                InputMedia media = new InputMedia(copied, String.Format("{0}_{1}", ++no, attach.Name));
                                photoAlbum.AddLast(new InputMediaPhoto(media));
                            }
                        }

                        if (photoAlbum.Count > 0)
                        {
                            Task<Message[]> task = TheClient.SendMediaGroupAsync(photoAlbum, user);
                            try
                            {
                                // 메시지 순서가 섞이지 않도록 모두 보내질 때까지 대기.
                                task.Wait();
                            }
                            catch (Exception e)
                            {
                                System.Diagnostics.Trace.WriteLine(e);
                            }
                        }
                    }
                }
            }

            AllNotificationsSent(notification);
        }

        private void SendResponse(HashSet<long> receivers, string message)
        {
            foreach (var user in receivers)
            {
                TheClient.SendTextMessageAsync(user, message);
            }
        }

        private string FormatContent(ContentType type, KidsNoteContent content)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0} [{1}]", ContentTypeConverter.ContentTypeToString(type), content.Id);
            sb.AppendFormat("\n제목 : {0}, 작성자 : {1}", content.Title, content.Writer);
            sb.Append("\n\n");
            sb.Append(content.Content);

            return sb.ToString();
        }

        #region 텔레그램 Delegate
        private void OnMessageReceived(object sender, MessageEventArgs args)
        {
            if (args.Message.Text != "")
            {
                RequestQueue.Enqueue(args.Message.Chat.Id, args.Message.Text);
            }
        }

        private void OnCallbackQueryReceived(object sender, CallbackQueryEventArgs args)
        {
        }

        private void OnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs args)
        {
        }

        private void OnInlineQueryReceived(object sender, InlineQueryEventArgs args)
        {
        }
        private void OnReceiveError(object sender, ReceiveErrorEventArgs args)
        {
        }

        private void OnReceiveGeneralError(object sender, ReceiveGeneralErrorEventArgs args)
        {
        }

        private void OnUpdateReceived(object sender, UpdateEventArgs args)
        {
        }
        #endregion
    }
}
