using Newtonsoft.Json.Linq;
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
        public string Content { get; set; }
        public Stream Binary { get; set; }

        public KidsNoteClientResponse(HttpResponseMessage response, string html, Stream binary = null)
        {
            Response = response;
            Content = html;
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
        private KidsNoteUserInfo UserInfo;
        private int ActiveChildId;
        private List<KidsNoteChildEnrollment> ActiveChildEnrollments;
        private string ActiveChildName;
        private static int DEFAULT_ATTACH_DOWNLOAD_RETRY = 30;

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

            ActiveChildId = -1;
            ActiveChildName = "";
            ActiveChildEnrollments = new List<KidsNoteChildEnrollment>();
        }

        private string ContentTypeUrl(ContentType type, int childId, int centerId, int classId, string pageToken)
        {
            // https://www.kidsnote.com/api/v1_2/children/2346060/reports/?page_size=12&tz=Asia%2FSeoul&center_id=58663&cls=480289&child=2346060
            // https://www.kidsnote.com/api/v1_2/children/2346060/reports/?page=cD0yMDIyLTA0LTEyKzA3JTNBMDAlM0EwMC4xNTEzNDklMkIwMCUzQTAw&page_size=12&tz=Asia/Seoul&center_id=58663&cls=480289&child=2346060
            // https://www.kidsnote.com/api/v1/centers/58663/notices?cls=480289&page_size=12
            // https://www.kidsnote.com/api/v1_2/children/2346060/albums/?tz=Asia%2FSeoul&page_size=12&center=58663&cls=480289&child=2346060
            // https://www.kidsnote.com/api/v1/centers/58663/menu?page_size=1000&month_menu=2022-04
            // https://www.kidsnote.com/api/v1/centers/58663/calendar?cls=480289&month_event=2022-04
            // https://www.kidsnote.com/api/v1_2/centers/58663/medications?page_size=12&cls=480289&child=2346060

            DateTime now = DateTime.Now;

            string page = "";
            if (pageToken != null && pageToken != "")
            {
                page = String.Format("&page={0}", pageToken);
            }

            switch (type)
            {
                case ContentType.REPORT:
                    return String.Format("{0}/api/v1_2/children/2346060/reports/?page_size=12&tz=Asia%2FSeoul&center_id={1}&cls={2}&child={3}{4}",
                                            Constants.KIDSNOTE_URL, centerId, classId, childId, page);
                case ContentType.NOTICE:
                    return String.Format("{0}/api/v1/centers/{1}/notices?cls={2}&page_size=12{3}",
                                         Constants.KIDSNOTE_URL, centerId, classId, page);
                case ContentType.ALBUM:
                    return String.Format("{0}/api/v1_2/children/{1}/albums/?tz=Asia%2FSeoul&page_size=12&center={2}&cls={3}&child={1}{4}",
                                          Constants.KIDSNOTE_URL, childId, centerId, classId, page);
                case ContentType.CALENDAR:
                    return String.Format("{0}/api/v1/centers/{1}/calendar?cls={2}&month_event={3}{4}",
                                         Constants.KIDSNOTE_URL, centerId, classId, now.ToString("yyyy-MM"), page);
                case ContentType.MENUTABLE:
                    return String.Format("{0}/api/v1/centers/{1}/menu?page_size=1000&month_menu={2}{3}",
                                         Constants.KIDSNOTE_URL, centerId, now.ToString("yyyy-MM"), page);
                case ContentType.MEDS_REQUEST:
                    //return Constants.KIDSNOTE_URL + "/medication-requests/";
                    return String.Format("{0}/api/v1_2/centers/{1}/medications?page_size=12&cls={2}&child={3}{4}",
                                         Constants.KIDSNOTE_URL, centerId, classId, childId, page);
                case ContentType.RETURN_HOME_NOTICE:
                    // 귀가동의서가 사라졌다.
                    //return Constants.KIDSNOTE_URL + "/return-home-notices/";
                default:
                    break;
            }

            return "";
        }

        private KidsNoteClientResponse DownloadDataImpl(HttpClient client, string url, bool asBinary)
        {
            KidsNoteClientResponse info = null;

            try
            {
                client.DefaultRequestHeaders.Add("Accept-Language", "ko-KR,ko;q=0.8,en-US;q=0.5,en;q=0.3");
                Task<HttpResponseMessage> getTask = client.GetAsync(url);
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

        private KidsNoteClientResponse DownloadPage(string url, bool asBinary = false)
        {
            return DownloadDataImpl(WebClient, url, asBinary);
        }

        public KidsNoteClientResponse DownloadAttachment(string url, bool asBinary = false)
        {
            return DownloadDataImpl(new HttpClient(), url, asBinary);
        }

        public KidsNoteContentDownloadResult DownloadContent(ContentType type, UInt64 lastContentId, string pageToken, bool onTheFly = true)
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

            KidsNoteClientResponse response = DownloadPage(Constants.KIDSNOTE_URL);

            if (!LoggedIn && response != null && IsLoginPage(response.Content))
            {
                KidsNoteClientResponse loginResult = Login(response.Content);
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

            if (UserInfo == null)
            {
                MakeUserInfo();
            }

            if (UserInfo == null)
            {
                result.Description = "원아정보를 알 수 없음";
                return result;
            }

            foreach (var enr in ActiveChildEnrollments)
            {
                string url = ContentTypeUrl(type, ActiveChildId, enr.CenterId, enr.ClassId, pageToken);
                if (url == "")
                {
                    result.Description = "URL 을 알 수 없음";
                    return result;
                }

                KidsNoteClientResponse downLoadResult = DownloadPage(url);
                //KidsNoteClientResponse report1 = DownloadPage("https://www.kidsnote.com/api/v1_2/children/2346060/reports/?cls=480289&child=2346060&tz=Asia%2FSeoul&center_id=58663");

                // https://www.kidsnote.com/api/v1_2/children/2346060/reports/?page_size=12&tz=Asia%2FSeoul&center_id=58663&cls=480289&child=2346060
                // https://www.kidsnote.com/api/v1/centers/58663/notices?cls=480289&page_size=12
                // https://www.kidsnote.com/api/v1_2/children/2346060/albums/?tz=Asia%2FSeoul&page_size=12&center=58663&cls=480289&child=2346060
                // https://www.kidsnote.com/api/v1/centers/58663/menu?page_size=1000&month_menu=2022-04
                // https://www.kidsnote.com/api/v1/centers/58663/calendar?cls=480289&month_event=2022-04
                // https://www.kidsnote.com/api/v1_2/centers/58663/medications?page_size=12&cls=480289&child=2346060

                if (downLoadResult == null)
                {
                    result.Description = "페이지 다운로드 실패";
                    return result;
                }

                if (downLoadResult.Response.StatusCode == HttpStatusCode.OK)
                {
                    result.Content = downLoadResult.Content;

                    string nextPageToken = "";
                    if (type == ContentType.MENUTABLE)
                    {
                        // 식단표는 목록구성 없이 당일 데이터만 수집한다.
                        result.ContentList = Parser.ParseMenuTable(type, downLoadResult.Content, ActiveChildName, enr, out nextPageToken);
                        result.NextPageToken = nextPageToken;
                    }
                    /*else if (type == ContentType.ALBUM)
                    {
                        result.ContentList = Parser.ParseAlbum(type, downLoadResult.Content, ActiveChildName, enr, out nextPageToken);
                        result.NextPageToken = nextPageToken;
                    }*/
                    else
                    {
                        result.ContentList = Parser.ParseArticleList(type, downLoadResult.Content, ActiveChildName, enr, out nextPageToken);
                        result.NextPageToken = nextPageToken;
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
                        int attachCount = 0;
                        foreach (var attach in each.Attachments)
                        {
                            bool success = false;
                            HttpStatusCode lastCode = HttpStatusCode.OK;

                            if (onTheFly)
                            {
                                attach.DownloadOnTheFly = true;
                                success = true;
                            }
                            else
                            {
                                System.Diagnostics.Trace.WriteLine(String.Format("[{0}] Trying {1}", attachCount, attach.DownloadUrl));
                                ++attachCount;

                                int remain = DEFAULT_ATTACH_DOWNLOAD_RETRY;
                                while (remain-- > 0)
                                {

                                    KidsNoteClientResponse resp = DownloadAttachment(attach.DownloadUrl, true);
                                    lastCode = resp.Response.StatusCode;
                                    if (resp.Response.StatusCode == HttpStatusCode.OK)
                                    {
                                        attach.Data = resp.Binary;
                                        success = true;
                                        break;
                                    }

                                    System.Threading.Thread.Sleep(500);
                                }
                            }

                            if (!success)
                            {
                                result.Description = String.Format("Download 실패 : 코드 OK 아님 : {0} on request {1}", lastCode, attachCount);
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("Request failed");
                }
            }

            return result;
        }

        private void MakeUserInfo()
        {
            KidsNoteClientResponse myInfoResult = DownloadPage(Constants.KIDSNOTE_MYINFO_URL);
            if (myInfoResult.Response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    JObject myInfo = JObject.Parse(myInfoResult.Content);
                    if (myInfo != null)
                    {
                        UserInfo = new KidsNoteUserInfo();

                        var kidsNoteUser = myInfo["user"];
                        UserInfo.Id = (int)kidsNoteUser["id"];
                        UserInfo.UserName = (string)kidsNoteUser["username"];
                        UserInfo.Name = (string)kidsNoteUser["name"];

                        var children = myInfo["children"];
                        foreach (var child in children)
                        {
                            KidsNoteChildInfo ci = new KidsNoteChildInfo();
                            ci.Id = (int)child["id"];
                            ci.Name = (string)child["name"];
                            ci.ParentId = (int)child["parent"]["id"];

                            if (ActiveChildId < 0)
                            {
                                ActiveChildId = ci.Id;
                                ActiveChildName = ci.Name;
                            }

                            var enrollments = child["enrollment"];
                            foreach (var enrollment in enrollments)
                            {
                                KidsNoteChildEnrollment en = new KidsNoteChildEnrollment();
                                en.Id = (int)enrollment["id"];
                                en.CenterId = (int)enrollment["center_id"];
                                en.CenterName = (string)enrollment["center_name"];
                                en.ClassId = (int)enrollment["belong_to_class"];
                                en.ClassName = (string)enrollment["class_name"];

                                ci.Enrollments.Add(en);

                                if (ci.Id == ActiveChildId)
                                {
                                    ActiveChildEnrollments.Add(en);
                                }
                            }

                            UserInfo.Children.Add(ci);
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine(e);
                }
            }
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
