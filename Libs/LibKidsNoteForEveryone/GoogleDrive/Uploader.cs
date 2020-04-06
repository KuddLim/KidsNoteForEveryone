using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone.GoogleDrive
{
    // https://stackoverflow.com/questions/19766912/how-do-i-authorise-an-app-web-or-installed-without-user-intervention
    public class Uploader
    {
        public delegate string GetBaseFolderIdDelegate();
        public delegate void SetBaseFolderIdDelegate(string id);
        public delegate void UploadProgressDelegate(string progress);

        private static string[] Scopes = { DriveService.Scope.DriveReadonly };
        private UserCredential Credential;
        private DriveService Service;
        private string CredentialPath;
        private string TokenPath;
        private string ChildName;

        public GetBaseFolderIdDelegate GetBaseFolderId;
        public SetBaseFolderIdDelegate SetBaseFolderId;
        public UploadProgressDelegate UploadProgress;

        public Uploader(string credentialPath, string tokenPath, string childName)
        {
            CredentialPath = credentialPath;
            TokenPath = tokenPath;
            ChildName = childName;
        }

        public bool Startup()
        {
            Authorize();
            CreateService();

            // FIXME
            return true;
        }

        public void Cleanup()
        {

        }

        private string BaseFolderName()
        {
            if (ChildName == "")
            {
                return Constants.GOOGLE_DRIVE_BACKUP_FOLDER_NAME;
            }
            else
            {
                return string.Format("{0} [{1}]", Constants.GOOGLE_DRIVE_BACKUP_FOLDER_NAME, ChildName);
            }
        }

        private bool Authorize()
        {
            using (var stream =
                new FileStream(CredentialPath, FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                Credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(TokenPath, true)).Result;

                System.Diagnostics.Trace.WriteLine("Credential file saved to: " + TokenPath);
            }

            return true;
        }

        private bool CreateService()
        {
            // Create Drive API service.
            Service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = Credential,
                ApplicationName = Constants.GOOGLE_DRIVE_APPLICATION_NAME,
            });

            return true;
        }

        public void List()
        {
            FilesResource.ListRequest listRequest = Service.Files.List();
            listRequest.PageSize = 10;
            listRequest.Fields = "nextPageToken, files(id, name, mimeType, trashed)";

            IList<Google.Apis.Drive.v3.Data.File> files = listRequest.Execute().Files;

            foreach (var each in files)
            {
                System.Diagnostics.Trace.WriteLine(each.Name);
            }

            string baseFolderId = FindBackupFolderId();

            string query = String.Format("mimeType = '{0}' and name = '{1}'", Constants.GOOGLE_DRIVE_MIMETYPE_FOLDER, BaseFolderName());
            listRequest.Q = query;
            listRequest.PageSize = 10;
            listRequest.Fields = "nextPageToken, files(id, name, mimeType, trashed)";

            files = listRequest.Execute().Files;

            if (files.Count == 0)
            {

            }
            else if (files.Count == 1)
            {

            }
            else
            {
                // TODO: 관리자에게 알려주기.
            }
        }

        private string FindBackupFolderId()
        {
            Google.Apis.Drive.v3.Data.File file = null;

            string prevFolderId = GetBaseFolderId();
            string baseFolderId = prevFolderId;
            if (baseFolderId != "")
            {
                FilesResource.GetRequest getRequest = Service.Files.Get(baseFolderId);
                //getRequest.Fields = "nextPageToken, files(id, name, mimeType)";
                getRequest.Fields = "id, name, mimeType, trashed";

                //string query = String.Format("mimeType = 'application/vnd.google-apps.folder' and name = '{0}'", BaseFolderName());
                //string query = String.Format("mimeType = '{0}' and id = '{1}'", Constants.GOOGLE_DRIVE_MIMETYPE_FOLDER, baseFolderId);
                //getRequest.Q  = query;
                try
                {
                    file = getRequest.Execute();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine(e);
                }

                if (file == null || file.Id == "" || file.Trashed.Value)
                {
                    // 새로 생성해야 함.
                    baseFolderId = "";
                }
                else
                {
                    baseFolderId = file.Id;
                }
            }

            if (baseFolderId == "")
            {
                baseFolderId = CreateFolder("", BaseFolderName());
            }

            if (baseFolderId != prevFolderId)
            {
                SetBaseFolderId(baseFolderId);
            }

            return baseFolderId;
        }

        // https://developers.google.com/drive/api/v3/folder
        private string CreateFolder(string parentId, string name)
        {
            Google.Apis.Drive.v3.Data.File folderToCreate = new Google.Apis.Drive.v3.Data.File();
            folderToCreate.MimeType = Constants.GOOGLE_DRIVE_MIMETYPE_FOLDER;
            folderToCreate.Name = name;
            if (parentId != "")
            {
                folderToCreate.Parents = new List<string>();
                folderToCreate.Parents.Add(parentId);
            }

            FilesResource.CreateRequest request = Service.Files.Create(folderToCreate);
            request.Fields = "id";
            Google.Apis.Drive.v3.Data.File created = request.Execute();

            return created.Id;
        }

        public bool Backup(Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents)
        {
            string baseFolderId = FindBackupFolderId();

            Dictionary<DateTime, string> dateIdMap = new Dictionary<DateTime, string>();
            foreach (var each in newContents)
            {
                foreach (var content in each.Value)
                {
                    if (!dateIdMap.ContainsKey(content.Date))
                    {
                        dateIdMap[content.Date] = FindDateFolderId(content.Date, baseFolderId);
                    }

                    LinkedList<string> idList = Upload(content, dateIdMap[content.Date]);
                    if (idList.Count == 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public string FindDateFolderId(DateTime dt, string parentFolder)
        {
            string dateTimeStr = dt.ToString("yyyy-MM-dd");
            IList<Google.Apis.Drive.v3.Data.File> files = null;

            FilesResource.ListRequest listRequest = Service.Files.List();
            listRequest.PageSize = 10;
            listRequest.Fields = "nextPageToken, files(id, name, mimeType, trashed)";
            string query = String.Format("mimeType = '{0}' and name = '{1}' and '{2}' in parents", Constants.GOOGLE_DRIVE_MIMETYPE_FOLDER, dateTimeStr, parentFolder);
            listRequest.Q = query;

            string dateFolderId = "";

            files = listRequest.Execute().Files;
            if (files.Count == 0 || files[0].Trashed.Value)
            {
                dateFolderId = CreateFolder(parentFolder, dateTimeStr);
            }
            else
            {
                dateFolderId = files[0].Id;
            }

            return dateFolderId;
        }

        private LinkedList<string> Upload(KidsNoteContent content, string dateFolderId)
        {
            string folderName = string.Format("[{0}] {1}", content.Type, content.Id);
            string containingFolderId = CreateFolder(dateFolderId, folderName);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(content.Content));
            ms.Seek(0, SeekOrigin.Begin);

            LinkedList<string> idList = new LinkedList<string>();

            UploadProgress("Uploding text..");
            string bodyId = UploadFile(ms, containingFolderId, "본문.txt", "본문.txt", Constants.GOOGLE_DRIVE_MIMETYPE_TEXT);
            idList.AddLast(bodyId);

            int i = 0;
            foreach (var each in content.Attachments)
            {
                ++i;
                int pos = each.DownloadUrl.LastIndexOf('.');
                string ext = "";
                if (pos > 0)
                {
                    ext = each.DownloadUrl.Substring(pos + 1);
                    ext = ext.ToLower();
                }

                string mType = MimeType.get(ext);

                if (each.Type == AttachmentType.VIDEO)
                {
                    // Video 는 용량이 커서 메모리가 부족할 수 있으므로 resumable upload 한다.
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(each.DownloadUrl);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                    Stream videoStream = null;
                    FileStream fileStream = null;
                    string savedFileName = "";

                    try {
                        videoStream = response.GetResponseStream();

                        savedFileName = String.Format("video.{0}", ext);
                        fileStream = File.Create(savedFileName);
                        byte[] buffer = new byte[1024 * 16];
                        int bytesRead = 0;
                        int totalBytesRead = 0;
                        do
                        {
                            bytesRead = videoStream.Read(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                fileStream.Write(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;
                            }
                        }
                        while (bytesRead > 0);

                        videoStream.Close();
                        fileStream.Seek(0, SeekOrigin.Begin);

                        string attachName = string.Format("{0}_{1}", i.ToString("000"), each.Name);
                        string message = String.Format("Uploading attachment... {0}", attachName);
                        UploadProgress(message);

                        string id = UploadFile(fileStream, containingFolderId, attachName, each.Name, mType);
                        idList.AddLast(id);

                        fileStream.Close();
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Trace.WriteLine(e);
                        idList.Clear();
                    }

                    if (videoStream != null)
                    {
                        videoStream.Close();
                    }
                    if (response != null)
                    {
                        response.Close();
                    }
                    if (fileStream != null)
                    {
                        fileStream.Close();
                    }
                    if (System.IO.File.Exists(savedFileName))
                    {
                        System.IO.File.Delete(savedFileName);
                    }
                }
                else
                {
                    each.Data.Seek(0, SeekOrigin.Begin);

                    string attachName = string.Format("{0}_{1}", i.ToString("000"), each.Name);
                    string message = String.Format("Uploading attachment... {0}", attachName);
                    UploadProgress(message);

                    try
                    {
                        string id = UploadFile(each.Data, containingFolderId, attachName, each.Name, mType);
                        idList.AddLast(id);
                    }
                    catch (Exception)
                    {
                        idList.Clear();
                    }
                }
            }

            return idList;
        }

        private string UploadFile(Stream stream, string parent, string name, string originalFileName, string mimeType)
        {
            Google.Apis.Drive.v3.Data.File body = new Google.Apis.Drive.v3.Data.File();
            body.Name = name;
            body.OriginalFilename = originalFileName;
            body.Parents = new List<string>() { parent };
            body.MimeType = mimeType;

            FilesResource.CreateMediaUpload createRequest = Service.Files.Create(body, stream, mimeType);
            Google.Apis.Upload.IUploadProgress progress = createRequest.Upload();
            Google.Apis.Drive.v3.Data.File response = createRequest.ResponseBody;

            return response.Id;
        }

        // https://stackoverflow.com/questions/45663027/resuming-interrupted-upload-using-google-drive-v3-c-sharp-sdk
        private string Test(Stream stream, string parent, string name, string originalFileName, string mimeType)
        {
            Google.Apis.Drive.v3.Data.File body = new Google.Apis.Drive.v3.Data.File();
            body.Name = name;
            body.OriginalFilename = originalFileName;
            body.Parents = new List<string>() { parent };
            body.MimeType = mimeType;

            FilesResource.CreateMediaUpload createRequest = Service.Files.Create(body, stream, mimeType);
            Google.Apis.Upload.IUploadProgress progress = createRequest.Upload();
            //createRequest.ContentStream
            if (progress.Status == Google.Apis.Upload.UploadStatus.Uploading)
            {
                // stream 이 seekable "해야할듯..
            }
            //createRequest.

            //createRequest.Resume()
            //Google.Apis.Upload.IUploadProgress progress = createRequest.Upload();
            //Google.Apis.Drive.v3.Data.File response = createRequest.ResponseBody;

            //return response.Id;

            return "";
        }
    }
}
