using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;

namespace LibKidsNoteForEveryone.Bot
{
    public class NotifierBot
    {
        public delegate long AdminUserChatIdDelegate();
        public delegate bool AddSubscriberDelegate(long chatId, HashSet<ContentType> exclusions);
        public delegate void AllNotificationsSentDelegate(KidsNoteNotification notification);
        public delegate bool SendImagesAsAttachmentDelegate();

        private Telegram.Bot.TelegramBotClient TheClient;
        private RequestMessageQueue RequestQueue;
        private ResponseMessageQueue ResponseQueue;

        public AdminUserChatIdDelegate AdminUserChatId;
        public AddSubscriberDelegate AddSubscriber;
        public AllNotificationsSentDelegate AllNotificationsSent;
        public SendImagesAsAttachmentDelegate SendImagesAsAttachment;

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

        public void SendAdminMessage(ChatId receiver, string message, MemoryStream htmlBody = null)
        {
            ResponseQueue.Enqueue(new HashSet<long>() { receiver.Identifier }, message, htmlBody);
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
                        SendResponse(message.ChatIds, message.Message, message.HtmlBody);
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
            bool imagesAsAttachment = SendImagesAsAttachment();

            foreach (var eachContent in notification.Contents)
            {
                // 본문 발송.
                string text = eachContent.FormatContent();

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

                //Dictionary<long, LinkedList<InputMediaPhoto>> photoAlbumPerUser = new Dictionary<long, LinkedList<InputMediaPhoto>>();

                bool hasAttachment = eachContent.Attachments != null && eachContent.Attachments.Count > 0;

                if (hasAttachment)
                {
                    if (imagesAsAttachment)
                    {
                        SendImageAttachments(receivers, eachContent);
                    }
                    else
                    {
                        SendImageLinks(receivers, eachContent);
                    }

                    SendVideoLinks(receivers, eachContent);
                }
            }

            AllNotificationsSent(notification);
        }

        private void SendImageAttachments(HashSet<long> receivers, KidsNoteContent content)
        {
            List<MemoryStream> attachmentStreams = new List<MemoryStream>();

            for (int i = 0; i < content.Attachments.Count; ++i)
            {
                var attach = content.Attachments[i];

                if (attach.Data == null)
                {
                    // TODO: 관리 사용자에게 통지.
                    // attachmentStreams.Add(null);
                }
                else
                {
                    if (attach.Type == AttachmentType.IMAGE)
                    {
                        MemoryStream ms = new MemoryStream();
                        attach.Data.CopyTo(ms);
                        attachmentStreams.Add(ms);
                    }
                    else if (attach.Type == AttachmentType.VIDEO)
                    {
                        // Video Link 로 따로 보낸다. (chrome redirect)
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
                for (int i = 0; i < content.Attachments.Count; ++i)
                {
                    var attach = content.Attachments[i];
                    if (attachmentStreams[i] != null)
                    {
                        MemoryStream copied = new MemoryStream();
                        attachmentStreams[i].Seek(0, SeekOrigin.Begin);
                        attachmentStreams[i].CopyTo(copied);
                        copied.Seek(0, SeekOrigin.Begin);
                        InputMedia media = new InputMedia(copied, String.Format("{0}_{1}", i + 1, attach.Name));
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

        private void SendImageLinks(HashSet<long> receivers, KidsNoteContent content)
        {
            LinkedList<string> imageLinks = new LinkedList<string>();
            foreach (var attach in content.Attachments)
            {
                if (attach.Type == AttachmentType.IMAGE && attach.DownloadUrl != "")
                {
                    imageLinks.AddLast(attach.DownloadUrl);
                }
            }

            foreach (var user in receivers)
            {
                foreach (var link in imageLinks)
                {
                    try
                    {
                        string originalLink = link.Replace("&amp;", "&");
                        string message = String.Format("사진을 전송합니다. 잠시 기다리시면 미리보기가 나타납니다.\n\n{0}", link.Replace("&amp;", "&"));
                        message += String.Format("\n\n깨끗한 사진을 보시리면 아래 링크를 클릭하세요.\n\n{0}", originalLink);
                        var task = TheClient.SendTextMessageAsync(user, message);
                        task.Wait();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private void SendVideoLinks(HashSet<long> receivers, KidsNoteContent content)
        {
            LinkedList<string> videoLinks = new LinkedList<string>();
            foreach (var attach in content.Attachments)
            {
                if (attach.Type == AttachmentType.VIDEO && attach.DownloadUrl != "")
                {
                    videoLinks.AddLast(attach.DownloadUrl);
                }
            }

            foreach (var user in receivers)
            {
                foreach (var link in videoLinks)
                {
                    try
                    {
                        string message = String.Format("아래 링크를 클릭하시면 동영상 재생이 됩니다.\n\n{0}", MakeRedirectUri(link));
                        System.Diagnostics.Trace.WriteLine(message);
                        var task = TheClient.SendTextMessageAsync(user, message);
                        task.Wait();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private string MakeRedirectUri(string link)
        {
            return Constants.CHROME_REDIRECT_URI + Uri.EscapeDataString(link.Replace("&amp;", "&").Replace("http", "googlechrome"));
        }

        private void SendResponse(HashSet<long> receivers, string message, MemoryStream htmlBody = null)
        {
            foreach (var user in receivers)
            {
                var messageTask = TheClient.SendTextMessageAsync(user, message);

                if (htmlBody != null)
                {
                    messageTask.Wait();
                    InputOnlineFile attachment = new InputOnlineFile(htmlBody, "attachment.txt");
                    var attachmentTask = TheClient.SendDocumentAsync(user, attachment);
                    attachmentTask.Wait();
                }
            }
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
