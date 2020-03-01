using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone
{
    public class KidsNoteClientResponse
    {
        public HttpResponseMessage Response { get; set; }
        public string Html { get; set; }
        public Stream Binary { get; set; }

        public KidsNoteClientResponse(HttpResponseMessage response, string html, Stream binary = null)
        {
            Response = response;
            Html = html;
            Binary = binary;
        }
    }

    public class KidsNoteClient
    {
        public delegate Configuration GetCurrentConfigurationDelegate();
        public delegate void KidsNoteClientProgressMessageDelegate(string message);
        public GetCurrentConfigurationDelegate GetCurrentConfiguration;
        public KidsNoteClientProgressMessageDelegate KidsNoteClientProgressMessage;

        private KidsNoteClientParser Parser;
        private HttpClient WebClient;
        private HttpClientHandler WebClientHandler;
        private CookieContainer Cookies;
        private bool LoggedIn;
        private bool RoleSelected;

        private enum LoginStage
        {
            SIGNING_IN = 0,
            LOGGED_IN,
        };

        public KidsNoteClient()
        {
            Parser = new KidsNoteClientParser();

            Cookies = new CookieContainer();
            WebClientHandler = new HttpClientHandler();
            WebClientHandler.CookieContainer = Cookies;

            WebClient = new HttpClient(WebClientHandler);
            LoggedIn = false;
            RoleSelected = false;
        }

        private string ContentTypeUrl(ContentType type)
        {
            switch (type)
            {
                case ContentType.REPORT:
                    return Constants.KIDSNOTE_URL + "/reports/";
                case ContentType.NOTICE:
                    return Constants.KIDSNOTE_URL + "/notices/";
                case ContentType.ALBUM:
                    return Constants.KIDSNOTE_URL + "/albums/";
                case ContentType.CALENDAR:
                    return Constants.KIDSNOTE_URL + "/calendars/";
                case ContentType.MENUTABLE:
                    return Constants.KIDSNOTE_URL + "/menus/";
                case ContentType.MEDS_REQUEST:
                    return Constants.KIDSNOTE_URL + "/medication-requests/";
                case ContentType.RETURN_HOME_NOTICE:
                    return Constants.KIDSNOTE_URL + "/return-home-notices/";
                default:
                    break;
            }

            return "";
        }

        private string CssClassPrefix(ContentType type)
        {
            switch (type)
            {
                case ContentType.REPORT:
                    return "report";
                case ContentType.NOTICE:
                    return "notice";
                case ContentType.ALBUM:
                    return "album";
                //case ContentType.CALENDAR:
                //    return "calendars";
                //case ContentType.MENUTABLE:
                //    return "menus";
                case ContentType.MEDS_REQUEST:
                    return "medication";
                case ContentType.RETURN_HOME_NOTICE:
                    return "return-home";
                default:
                    break;
            }

            return "";
        }


        private KidsNoteClientResponse DownloadPage(string url, bool asBinary = false)
        {
            KidsNoteClientResponse info = null;

            try
            {
                Task<HttpResponseMessage> getTask = WebClient.GetAsync(url);
                getTask.Wait();

                HttpResponseMessage response = getTask.Result;

                string html = "";
                Stream binary = null;

                if (asBinary)
                {
                    //binary = response.Content.ReadAsByteArrayAsync().Result;
                    binary = response.Content.ReadAsStreamAsync().Result;
                }
                else
                {
                    html = response.Content.ReadAsStringAsync().Result;
                }

                info = new KidsNoteClientResponse(response, html, binary);
            }
            catch (Exception)
            {
                info = null;
            }

            return info;
        }

        public LinkedList<KidsNoteContent> DownloadContent(ContentType type, UInt64 lastContentId, int page = 1)
        {
            string url = ContentTypeUrl(type);
            if (url == "")
            {
                return null;
            }

            if (page > 1)
            {
                url += String.Format("?page={0}", page);
            }

            KidsNoteClientResponse response = DownloadPage(Constants.KIDSNOTE_URL);
            if (!LoggedIn && response != null && IsLoginPage(response.Html))
            {
                KidsNoteClientResponse loginResult = Login(response.Html);
                LoggedIn = (loginResult.Response.StatusCode == HttpStatusCode.Found ||
                                loginResult.Response.StatusCode == HttpStatusCode.OK);
            }

            if (!LoggedIn)
            {
                if (KidsNoteClientProgressMessage != null)
                {
                    KidsNoteClientProgressMessage("Login Failed");
                }
                return null;
            }

            KidsNoteClientResponse downLoadResult = DownloadPage(url);
            if (downLoadResult == null)
            {
                return null;
            }

            if (page > 1 && downLoadResult.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return new LinkedList<KidsNoteContent>();
            }

            if (downLoadResult.Response.StatusCode == HttpStatusCode.OK)
            {
                string prefix = CssClassPrefix(type);
                LinkedList<KidsNoteContent> contents = Parser.ParseArticleList(type, downLoadResult.Html, prefix);

                if (contents == null)
                {
                    return null;
                }

                var node = contents.First;
                while (node != null)
                {
                    var next = node.Next;
                    if (node.Value.Id <= lastContentId)
                    {
                        contents.Remove(node);
                    }

                    node = next;
                }

                foreach (var each in contents)
                {
                    KidsNoteClientResponse eachResp = DownloadPage(each.OriginalPageUrl);
                    if (eachResp != null && eachResp.Response.StatusCode == HttpStatusCode.OK)
                    {
                        if (!RoleSelected && Parser.IsRoleSelectionPage(eachResp.Html))
                        {
                            RoleSelected = true;
                            string csrfMiddlewareToken = Parser.GetCsrfMiddlewareToken(eachResp.Html);

                            string next = each.OriginalPageUrl.Substring(Constants.KIDSNOTE_URL.Length);
                            FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
                            {
                                new KeyValuePair<string, string>("csrfmiddlewaretoken", csrfMiddlewareToken),
                                new KeyValuePair<string, string>("nickname", "father"),
                                new KeyValuePair<string, string>("next", next)
                            }); ;

                            KidsNoteClientResponse roleResp = PostData(Constants.KIDSNOTE_ROLE_POST_URL, content);
                            if (roleResp.Response.StatusCode != HttpStatusCode.OK)
                            {
                                return null;
                            }
                        }

                        // Get Again
                        eachResp = DownloadPage(each.PageUrl);
                        if (eachResp != null && eachResp.Response.StatusCode == HttpStatusCode.OK)
                        {
                            Parser.ParseContent(each, eachResp.Html, prefix);
                        }

                        foreach (var attach in each.Attachments)
                        {
                            KidsNoteClientResponse resp = DownloadPage(attach.DownloadUrl, true);
                            if (resp.Response.StatusCode == HttpStatusCode.OK)
                            {
                                attach.Data = resp.Binary;
                            }
                            else if (attach.ImageSource != "")
                            {
                                // URL 원문이 한글로 되어 있으면 다운로드가 안되는 듯 하다.
                                // KidsNote 웹서버 (nginx) 문제인지, 키즈노트 서비스 문제인지는 불확실.
                                // 이 경우 img 태그의 src 로 받아본다.
                                resp = DownloadPage(attach.ImageSource, true);
                                if (resp.Response.StatusCode == HttpStatusCode.OK)
                                {
                                    attach.Data = resp.Binary;
                                }
                                else
                                {
                                    System.Diagnostics.Trace.WriteLine("Download 실패");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Trace.WriteLine("Download 실패");
                            }
                        }
                    }
                }
                return contents;
            }

            return null;
        }

        private bool IsLoginPage(string html)
        {
            return html.IndexOf("<form action=\"/login/\"") > 0;
        }

        private KidsNoteClientResponse Login(string formHtml)
        {
            string csrfMiddlewareToken = Parser.GetCsrfMiddlewareToken(formHtml);

            Configuration conf = GetCurrentConfiguration();

            FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("csrfmiddlewaretoken", csrfMiddlewareToken),
                new KeyValuePair<string, string>("username", conf.KidsNoteId),
                new KeyValuePair<string, string>("password", conf.KidsNotePassword)
            });

            return PostData(Constants.KIDSNOTE_LOGIN_POST_URL, content);
        }

        private KidsNoteClientResponse PostData(string url, FormUrlEncodedContent form)
        {
            Task<HttpResponseMessage> postTask = WebClient.PostAsync(url, form);
            postTask.Wait();

            HttpResponseMessage response = postTask.Result;
            string html = response.Content.ReadAsStringAsync().Result;

            KidsNoteClientResponse info = new KidsNoteClientResponse(response, html);
            return info;
        }
    }
}
