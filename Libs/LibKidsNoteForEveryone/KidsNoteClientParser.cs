using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteClientParser
    {
        private static Dictionary<string, string>  ConditionFileSection = new Dictionary<string, string>() { { "class", "file-section" } };
        private static Dictionary<string, string> ConditionFileName = new Dictionary<string, string>() { { "class", "file-name" } };
        private static Dictionary<string, string> ConditionFileDownload = new Dictionary<string, string>() { { "class", "file-download" } };

        private string RemoveLeadingTrailingNewLines(string text)
        {
            while (text.Length > 0 && text.First() == '\n')
            {
                text = text.Substring(1);
            }
            while (text.Length > 0 && text.Last() == '\n')
            {
                text = text.Substring(0, text.Length - 1);
            }

            return text.Trim();
        }

        public KidsNoteContent ParseContent(KidsNoteContent content, string html, string classPrefix)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            var root = doc.DocumentNode;
            HtmlNode contentNode = root.SelectSingleNode("//div[@class='content']");
            if (contentNode == null)
            {
                return null;
            }

            HtmlNode titleNode = contentNode.SelectSingleNode("//h3[@class='sub-header-title']");
            content.Title = RemoveLeadingTrailingNewLines(ReplaceHtmlEscapes(titleNode.InnerText));

            HtmlNode textNode = contentNode.SelectSingleNode("//div[@class='content-text']");
            if (textNode == null)
            {
                return null;
            }
            content.Content = RemoveLeadingTrailingNewLines(ReplaceHtmlEscapes(textNode.InnerHtml));

            HtmlNode imageGridNode = contentNode.SelectSingleNode("//div[@id='img-grid-container']");
            if (imageGridNode != null)
            {
                foreach (var each in imageGridNode.ChildNodes)
                {
                    if (each.Name == "#text")
                    {
                        continue;
                    }

                    HtmlNode aNode = each.SelectSingleNode("a");
                    if (aNode != null)
                    {
                        string href = aNode.GetAttributeValue("href", "");
                        string dataDownload = aNode.GetAttributeValue("data-download", "");

                        KidsNoteContent.Attachment attachment = null;
                        if (href != "" && dataDownload != "")
                        {
                            string[] tokens = href.Split('/');
                            attachment = new KidsNoteContent.Attachment(AttachmentType.IMAGE, tokens.Last(), dataDownload, href);
                            content.Attachments.AddLast(attachment);

                            System.Diagnostics.Trace.WriteLine(dataDownload);
                        }

                        HtmlNode imgNode = FindNode(aNode, "img", null);
                        if (href == "" && imgNode != null && attachment != null)
                        {
                            string src = imgNode.GetAttributeValue("src", "");
                            attachment.ImageSource = src;
                        }
                    }
                }
            }

            HtmlNode detail = root.SelectSingleNode(String.Format("//div[@class='{0}-detail']", classPrefix));
            //HtmlNode fileSection = contentNode.SelectSingleNode("/div[@class='flie-section']");
            //HtmlNode fileSection = detail.SelectSingleNode("//div[@class='flie-section']");

            // Xpath 로는 검색이 되지 않아 직접 DOM 탐색.
            HtmlNode fileSection = FindNode(detail, "div", ConditionFileSection);
            if (fileSection != null)
            {
                foreach (var each in fileSection.ChildNodes)
                {
                    if (each.Name !="div")
                    {
                        continue;
                    }

                    KidsNoteContent.Attachment attach = new KidsNoteContent.Attachment(AttachmentType.OTHER);

                    HtmlNode fileNameNode = FindNode(each, "div", ConditionFileName);
                    if (fileNameNode != null)
                    {
                        HtmlNode firstP = FindNode(fileNameNode, "p", null);
                        if (firstP != null)
                        {
                            attach.Name = firstP.InnerText;
                        }
                    }

                    HtmlNode fileDownloadNode = FindNode(each, "div", ConditionFileDownload);
                    if (fileDownloadNode != null)
                    {
                        HtmlNode aNode = FindNode(fileDownloadNode, "a", null);
                        if (aNode != null)
                        {
                            attach.DownloadUrl = ReplaceHtmlEscapes(aNode.GetAttributeValue("href", ""));
                        }
                    }

                    if (attach.Name != "" && attach.DownloadUrl != "")
                    {
                        content.Attachments.AddLast(attach);
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            HtmlNode authorNode = contentNode.SelectSingleNode("//span[@class='name']");
            if (authorNode == null)
            {
                return null;
            }
            content.Writer = authorNode.InnerText;

            return content;
        }

        public LinkedList<KidsNoteContent> ParseArticleList(ContentType type, string html, string classPrefix)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            var root = doc.DocumentNode;
            // report, notice, album, medication, return-home
            // 일정표와 식단표는 다름.
            var containerNode = root.SelectSingleNode(String.Format("//div[@class='{0}-list-wrapper']", classPrefix));
            if (containerNode == null)
            {
                return null;
            }

            LinkedList<KidsNoteContent> contentList = new LinkedList<KidsNoteContent>();

            foreach (var child in containerNode.ChildNodes)
            {
                if (child.Name != "a")
                {
                    continue;
                }

                string href = child.GetAttributeValue("href", "");

                if (href != "")
                {
                    KidsNoteContent content = new KidsNoteContent(type);

                    content.OriginalPageUrl = Constants.KIDSNOTE_URL + href;

                    UInt64 parsedId = 0;
                    int pos = href.IndexOf("/?req");
                    if (pos > 0)
                    {
                        href = href.Substring(0, pos);
                        string[] tokens = href.Split('/');
                        if (UInt64.TryParse(tokens.Last(), out parsedId))
                        {
                            content.Id = parsedId;
                        }
                    }

                    content.PageUrl = Constants.KIDSNOTE_URL + href;
                    contentList.AddLast(content);
                }
            }

            // TODO: Next 가 있는 경우, 또는 Scroll 을 해야 하는 경우.
            return contentList;
        }

        public bool IsRoleSelectionPage(string html)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            HtmlNode root = doc.DocumentNode;
            HtmlNode pageInner = root.SelectSingleNode("//div[@class='page-inner']");
            if (pageInner != null)
            {
                HtmlNode form = pageInner.SelectSingleNode("//form");
                if (form != null)
                {
                    //var groupSpan = form.SelectSingleNode("//span[@class='input-group-btn']");
                    HtmlNodeCollection nodes = form.SelectNodes("//form[@action='/accounts/role/name/']");
                    if (nodes == null)
                    {
                        throw new Exception("Parse Exception");
                    }
                    return nodes.Count != 0;
                }
            }

            return false;
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
            return text.Replace("<br>", "\n").Replace("<br />", "\n").Replace("&quot;", "\"")
                .Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                .Replace("<p>", "").Replace("</p>", "");
        }
    }
}
