using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace LibKidsNoteNotifier.Bot
{
    public class NotifierBot
    {
        private Telegram.Bot.TelegramBotClient TheClient;
        private RequestMessageQueue RequestQueue;
        private ResponseMessageQueue ResponseQueue;

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

        private string BoardName(ContentType type)
        {
            switch (type)
            {
                case ContentType.ALBUM:
                    return "앨범";
                case ContentType.CALENDAR:
                    return "일정표";
                case ContentType.MEDS_REQUEST:
                    return "투약의뢰서";
                case ContentType.MENUTABLE:
                    return "식단표";
                case ContentType.NOTICE:
                    return "공지사항";
                case ContentType.REPORT:
                    return "알림장";
                case ContentType.RETURN_HOME_NOTICE:
                    return "귀가동의서";
                default:
                    break;
            }

            return "";
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
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("당신의 텔레그램 ChatId 는 {0} 입니다.\n이 봇은 등록된 사용자만 사용가능합니다.", message.ChatId);
            sb.AppendFormat("\n\nYour Telegram ChatId is {0}.\nThis bot is for registered users only.", message.ChatId);
            ResponseQueue.Enqueue(new List<ChatId>() { message.ChatId }, sb.ToString());
        }

        public void SendNewContents(List<ChatId> receivers, KidsNoteNotifyMessage message)
        {
            ResponseQueue.Enqueue(receivers, message);
        }

        public void SendAdminMessage(ChatId receiver, string message)
        {
            ResponseQueue.Enqueue(new List<ChatId>() { receiver }, message);
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
                        SendResponse(message.ChatIds, message.KidsNoteMessage);
                        break;
                    case ResponseMessage.MessageType.GENERAL_MESSAGE:
                        SendResponse(message.ChatIds, message.Message);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception)
            {
            }
        }

        private void SendResponse(List<ChatId> receivers, KidsNoteNotifyMessage kidsNoteMessage)
        {
            foreach (var each in kidsNoteMessage.NewContents)
            {
                foreach (var eachContent in each.Value)
                {
                    string text = FormatContent(each.Key, eachContent);
                    //TheClient.SendTextMessageAsync(text).Wait();

                    List<Task<Message>> taskList = new List<Task<Message>>();
                    foreach (var user in receivers)
                    {
                        taskList.Add(TheClient.SendTextMessageAsync(user, text));
                    }

                    // 메시지 순서가 섞이지 않도록 모두 보내질 때까지 대기.
                    foreach (var task in taskList)
                    {
                        task.Wait();
                    }

                    LinkedList<InputMediaPhoto> photoAlbum = new LinkedList<InputMediaPhoto>();
                    //LinkedList<InputMediaDocument> otherAttachments = new LinkedList<InputMediaDocument>();
                    if (eachContent.Attachments != null && eachContent.Attachments.Count > 0)
                    {
                        taskList.Clear();

                        System.Diagnostics.Trace.WriteLine(String.Format("Title : {0}", eachContent.Title));

                        int no = 0;
                        foreach (var attach in eachContent.Attachments)
                        {
                            if (attach.Data == null)
                            {
                                // TODO: 관리 사용자에게 통지.
                            }
                            InputMedia media = new InputMedia(attach.Data, String.Format("{0}_{1}", ++no, attach.Name));
                            if (attach.Type == AttachmentType.IMAGE)
                            {
                                photoAlbum.AddLast(new InputMediaPhoto(media));
                                System.Diagnostics.Trace.WriteLine(String.Format("Attachment {0} : {1}", no, attach.Name));
                            }
                            else
                            {
                                // Telegram 으로는 Image 만 보낸다.
                                // otherAttachments.AddLast(new InputMediaDocument(media));
                            }
                        }
                    }

                    if (photoAlbum.Count > 0)
                    {
                        List<Task<Message[]>> mediaTaskList = new List<Task<Message[]>>();
                        foreach (var user in receivers)
                        {
                            mediaTaskList.Add(TheClient.SendMediaGroupAsync(photoAlbum, user));
                        }

                        // 메시지 순서가 섞이지 않도록 모두 보내질 때까지 대기.
                        foreach (var task in mediaTaskList)
                        {
                            task.Wait();
                        }
                    }
                }
            }
        }

        private void SendResponse(List<ChatId> receivers, string message)
        {
            foreach (var user in receivers)
            {
                TheClient.SendTextMessageAsync(user, message);
            }
        }

        private string FormatContent(ContentType type, KidsNoteContent content)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(BoardName(type));
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
