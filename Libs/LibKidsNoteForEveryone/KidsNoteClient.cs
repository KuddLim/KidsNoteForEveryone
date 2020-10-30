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
                WebClient.DefaultRequestHeaders.Add("Accept-Language", "ko-KR,ko;q=0.8,en-US;q=0.5,en;q=0.3");
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

        public KidsNoteContentDownloadResult DownloadContent(ContentType type, UInt64 lastContentId, int page = 1)
        {
            KidsNoteContentDownloadResult result = new KidsNoteContentDownloadResult();

#if !DEBUG
            // 식단표는 섭취 여부는 상관없이 음식이 만들어지고 나서 업데이트 되는 것으로 보인다.
            // 마지막 cron 작업때 기준으로 체크한다.
            if (type == ContentType.MENUTABLE)
            {
                Configuration conf = GetCurrentConfiguration();
                int endHour = conf.OperationHourEnd != 0 ? conf.OperationHourEnd : 20;

                DateTime now = DateTime.Now;
                if (now.Hour < endHour)
                {
                    result.NotNow = true;
                    return result;
                }
            }
#endif

            string url = ContentTypeUrl(type);
            if (url == "")
            {
                result.Description = "URL 을 알 수 없음";
                return result;
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
                result.Description = "로그인 실패";
                return result;
            }

            KidsNoteClientResponse downLoadResult = DownloadPage(url);
            if (downLoadResult == null)
            {
                result.Description = "페이지 다운로드 실패";
                return result;
            }

            if (page > 1 && downLoadResult.Response.StatusCode == HttpStatusCode.NotFound)
            {
                result.Description = "페이지 다운로드 실패 : NotFound";
                result.ContentList = new LinkedList<KidsNoteContent>();
                return result;
            }

            if (downLoadResult.Response.StatusCode == HttpStatusCode.OK)
            {
                result.Html = downLoadResult.Html;

                string prefix = CssClassPrefix(type);

                if (type == ContentType.MENUTABLE)
                {
                    // 식단표는 목록구성 없이 당일 데이터만 수집한다.
                    result.ContentList = Parser.ParseMenuTable(type, downLoadResult.Html);
                }
                else
                {
                    result.ContentList = Parser.ParseArticleList(type, downLoadResult.Html, prefix);
                }

                if (result.ContentList == null)
                {
                    result.Description = "Parse 실패";
                    return result;
                }

                var node = result.ContentList.First;
                while (node != null)
                {
                    var next = node.Next;
                    if (node.Value.Id <= lastContentId)
                    {
                        result.ContentList.Remove(node);
                    }

                    node = next;
                }

                foreach (var each in result.ContentList)
                {
                    if (each.Type == ContentType.MENUTABLE)
                    {
                        foreach (var content in result.ContentList)
                        {
                            foreach (var attach in content.Attachments)
                            {
                                KidsNoteClientResponse resp = DownloadPage(attach.DownloadUrl, true);
                                if (resp.Response.StatusCode == HttpStatusCode.OK)
                                {
                                    attach.Data = resp.Binary;
                                }
                                else
                                {
                                    result.Description = "Download 실패 : 첨부 이미지 다운로드 실패";
                                }
                            }
                        }
                    }
                    else
                    {
                        KidsNoteClientResponse eachResp = DownloadPage(each.OriginalPageUrl);
                        if (eachResp != null && eachResp.Response.StatusCode == HttpStatusCode.OK)
                        {
                            result.Html = eachResp.Html;

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
                                    result.Description = "Role 설정 실패";
                                    return result;
                                }
                            }

                            // Get Again
                            eachResp = DownloadPage(each.PageUrl);
                            if (eachResp != null && eachResp.Response.StatusCode == HttpStatusCode.OK)
                            {
                                Parser.ParseContent(each, eachResp.Html, prefix);
                                if (type != ContentType.MENUTABLE && (each.Content.Length == 0 || each.Title.Length == 0))
                                {
                                    result.Description = "본문/제목 Parse 실패";
                                    return result;
                                }
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
                                        result.Description = "Download 실패 : 첨부 이미지 다운로드 실패";
                                    }
                                }
                                else
                                {
                                    result.Description = "Download 실패 : 코드 OK 아님";
                                }
                            }
                        }
                    }
                }
                return result;
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
