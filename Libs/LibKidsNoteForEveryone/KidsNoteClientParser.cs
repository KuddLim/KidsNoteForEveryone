using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteClientParser
    {
        private KidsNoteContent ParseContent(JToken token, ContentType type, string childName, KidsNoteChildEnrollment enrollment)
        {
            KidsNoteContent content = new KidsNoteContent(type);

            content.Id = (ulong)token["id"];
            content.ChildName = childName;
            content.Enrollment = enrollment;
            content.Title = String.Format("[{0}] {1}", type, (string)token["date_written"]);
            content.Content = (string)token["content"];
            content.Writer = (string)token["author_name"];
            content.Date = DateTime.Parse((string)token["created"], null, System.Globalization.DateTimeStyles.RoundtripKind);

            JToken images = token["attached_images"];
            if (images != null)
            {
                foreach (var image in images)
                {
                    string imageSource = (string)image["original"];
                    string name = (string)image["original_file_name"];
                    KidsNoteContent.Attachment attach = new KidsNoteContent.Attachment(AttachmentType.IMAGE, name, imageSource, imageSource);
                    attach.ImageSource = (string)image["original"];
                    content.Attachments.Add(attach);
                }
            }

            JToken files = token["attached_files"];
            if (files != null)
            {
                foreach (var file in files)
                {
                    // id(ulong), access_key(string), file_size(ulong), status(string)
                    string name = (string)file["original_file_name"];
                    string link = (string)file["original"];
                    KidsNoteContent.Attachment attach = new KidsNoteContent.Attachment(AttachmentType.OTHER, name, link, link);
                    content.Attachments.Add(attach);
                }
            }

            JToken video = token["attached_video"];
            if (video != null && video.HasValues)
            {
                string name = "";
                JToken nameToken = video["original_file_name"];
                if (nameToken != null)
                {
                    name = (string)nameToken;
                }
                string linkHigh = "";
                JToken linkHighToken = video["high"];
                if (linkHighToken != null)
                {
                    linkHigh = (string)linkHighToken;
                }

                if (linkHigh != "")
                {
                    KidsNoteContent.Attachment attach = new KidsNoteContent.Attachment(AttachmentType.VIDEO, name, linkHigh, linkHigh);
                    content.Attachments.Add(attach);
                }
                // id (ulong), access_key(string), file_size(ulong), source_type(string)
                //string linkHigh = (string)video["high"];
                //string linkLow = (string)video["low"];
                //string preview = (string)video["preview"];
                //string preview_small = (string)video["preview_small"];
            }

            return content;
        }

        public LinkedList<KidsNoteContent> ParseArticleList(ContentType type, string json, string childName,
                                                            KidsNoteChildEnrollment enrollment, out string nextPageToken)
        {
            nextPageToken = "";
            LinkedList<KidsNoteContent> articleList = new LinkedList<KidsNoteContent>();

            try
            {
                JObject document = JObject.Parse(json);
                //var results = document["results"];
                nextPageToken = (string)document["next"];
                if (nextPageToken == null)
                {
                    nextPageToken = "";
                }
                JToken results = document["results"];
                foreach (var result in results)
                {
                    KidsNoteContent content = ParseContent(result, type, childName, enrollment);
                    if (content != null)
                    {
                        articleList.AddLast(content);
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e);
            }

            return articleList;
        }

        private LinkedList<KidsNoteContent> ParseDayMenutable(JToken node)
        {
            LinkedList<KidsNoteContent> dayMenutable = new LinkedList<KidsNoteContent>();

            string date = (string)node["date_menu"];
            string writer = (string)node["author_name"];
            string id = (string)node["id"];
            DateTime dt = DateTime.Parse((string)node["created"], null, System.Globalization.DateTimeStyles.RoundtripKind);

            Action<string, string> addMealInfo = (key, mealName) =>
            {
                string mealInfo = (string)node[key];

                if (mealInfo != null && mealInfo != "")
                {
                    KidsNoteContent meal = new KidsNoteContent(ContentType.MENUTABLE);
                    string items = mealInfo.Replace("\n", ", ");
                    meal.Title = String.Format("{0} {1} : {2}", date, mealName, items);
                    meal.Writer = writer;
                    meal.Content = mealInfo;
                    meal.Date = dt;

                    JToken attachment = node[key + "_img"];
                    if (attachment != null)
                    {
                        string imageSource = (string)attachment["original"];
                        KidsNoteContent.Attachment attach = new KidsNoteContent.Attachment(AttachmentType.IMAGE, "", imageSource, imageSource);
                        meal.Attachments.Add(attach);
                    }

                    dayMenutable.AddLast(meal);
                }
            };

            addMealInfo("morning_snack", "오전간식");
            addMealInfo("lunch", "점심");
            addMealInfo("afternoon_snack", "오후간식");

            return dayMenutable;
        }

        public LinkedList<KidsNoteContent> ParseMenuTable(ContentType type, string json, string childName,
                                                            KidsNoteChildEnrollment enrollment, out string nextPageToken)
        {
            nextPageToken = "";
            LinkedList<KidsNoteContent> contents = new LinkedList<KidsNoteContent>();

            try
            {
                JObject document = JObject.Parse(json);
                nextPageToken = (string)document["next"];
                if (nextPageToken == null)
                {
                    nextPageToken = "";
                }
                JToken results = document["results"];
                foreach (var result in results)
                {
                    LinkedList<KidsNoteContent> dayMenu = ParseDayMenutable(result);
                    foreach (var dm in dayMenu)
                    {
                        dm.ChildName = childName;
                        dm.Enrollment = enrollment;
                        contents.AddLast(dm);
                    }
                }

            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e);
            }

            return contents;
        }

        public LinkedList<KidsNoteContent> ParseAlbum(ContentType type, string json, string childName,
                                                            KidsNoteChildEnrollment enrollment, out string nextPageToken)
        {
            nextPageToken = "";
            LinkedList<KidsNoteContent> contents = new LinkedList<KidsNoteContent>();

            return contents;
        }

        // XPath 로 바로 찾아갈 수 있지만, 아래와 같이 경로를 하나하나 찾아가면 간단한 페이지 변경시
        public string GetCsrfMiddlewareToken(string formHtml)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(formHtml);

            var root = doc.DocumentNode;
            var form = root.SelectSingleNode("//form");
            if (form == null)
            {
                return "";
            }

            var hiddenInput = FindNode(form, "input", new Dictionary<string, string>() { { "type", "hidden" } });
            if (hiddenInput == null)
            {
                return "";
            }

            string csrf = hiddenInput.GetAttributeValue("value", "");
            return csrf;
        }

        protected HtmlNode FindNode(HtmlNode current, string tagName, Dictionary<string, string> attributes, bool exact = false)
        {
            if (current == null || current.ChildNodes == null)
            {
                return null;
            }

            for (int i = 0; i < current.ChildNodes.Count; ++i)
            {
                var n = current.ChildNodes[i];

                if (n.Name == tagName)
                {
                    if (attributes == null || attributes.Count == 0)
                    {
                        return n;
                    }
                    if (exact && n.Attributes.Count != attributes.Count)
                    {
                        return null;
                    }

                    int attrCount = attributes.Count;

                    for (int j = 0; j < n.Attributes.Count; ++j)
                    {
                        var attr = n.Attributes[j];

                        if (attributes.ContainsKey(attr.Name) && attributes[attr.Name] == attr.Value)
                        {
                            --attrCount;
                        }
                    }

                    if (attrCount == 0)
                    {
                        return n;
                    }
                }
            }

            return null;
        }

        private string ReplaceHtmlEscapes(string text)
        {
            string escaped = text.Replace("<br>", "\n").Replace("<br />", "\n").Replace("&quot;", "\"")
                .Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                .Replace("<p>", "").Replace("</p>", "");
            string decoded = System.Net.WebUtility.HtmlDecode(escaped);
            return decoded;
        }
    }
}
