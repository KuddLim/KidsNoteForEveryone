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

        public bool Backup(Dictionary<ContentType, LinkedList<KidsNoteContent>> newContents, bool encrypt)
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

                    LinkedList<string> idList = Upload(content, dateIdMap[content.Date], encrypt);
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

        private LinkedList<string> Upload(KidsNoteContent content, string dateFolderId, bool encrypt)
        {
            string folderName = string.Format("[{0}] {1}", content.Type, content.Id);
            string containingFolderId = CreateFolder(dateFolderId, folderName);

            MemoryStream ms = null;
            if (encrypt)
            {
                byte[] plain = Encoding.UTF8.GetBytes(content.FormatContent());
                byte[] encrypted = new byte[plain.Length];

                EncryptorChaCha chacha = new EncryptorChaCha(true, EncryptorChaCha.DefaultChaChaEncKey, EncryptorChaCha.DefaultChaChaEncNonce);
                chacha.Process(plain, 0, plain.Length, encrypted, 0);
                ms = new MemoryStream(encrypted);
            }
            else
            {
                ms = new MemoryStream(Encoding.UTF8.GetBytes(content.FormatContent()));
            }
            ms.Seek(0, SeekOrigin.Begin);

            LinkedList<string> idList = new LinkedList<string>();

            UploadProgress(string.Format("Uploding [{0}] {1}", content.Type, content.Id));

            string name = encrypt ? "본문.txt.chacha" : "본문.txt";
            // 이미 암호화 해 두었다.
            string bodyId = UploadFile(ms, containingFolderId, name, name, Constants.GOOGLE_DRIVE_MIMETYPE_TEXT);
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

                bool isVideo = each.Type == AttachmentType.VIDEO;
                string id = isVideo ? UploadVideoAttachment(each, i, containingFolderId, ext, encrypt)
                                    : UploadPhotoAttachment(each, i, containingFolderId, ext, encrypt);
                if (id != null && id.Length > 0)
                {
                    idList.AddLast(id);
                }
            }

            return idList;
        }

        private string UploadVideoAttachment(KidsNoteContent.Attachment attach, int index, string parentId, string ext, bool encrypt)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(attach.DownloadUrl);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            string mType = MimeType.get(ext);
            string id = "";

            Stream videoDownloadStream = null;
            FileStream fileStream = null;
            string savedFileName = "";

            try
            {
                EncryptorChaCha chacha = null;
                if (encrypt)
                {
                    chacha = new EncryptorChaCha(true, EncryptorChaCha.DefaultChaChaEncKey, EncryptorChaCha.DefaultChaChaEncNonce);
                }

                videoDownloadStream = response.GetResponseStream();

                savedFileName = String.Format("video.{0}", ext);
                fileStream = File.Create(savedFileName);

                byte[] buffer = new byte[1024 * 16];
                byte[] bufferEnc = new byte[buffer.Length];
                int bytesRead = 0;
                int totalBytesRead = 0;
                do
                {
                    bytesRead = videoDownloadStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        if (chacha != null)
                        {
                            chacha.Process(buffer, 0, bytesRead, bufferEnc, 0);
                            fileStream.Write(bufferEnc, 0, bytesRead);
                        }
                        else
                        {
                            fileStream.Write(buffer, 0, bytesRead);
                        }
                        totalBytesRead += bytesRead;
                    }
                }
                while (bytesRead > 0);

                videoDownloadStream.Close();
                fileStream.Seek(0, SeekOrigin.Begin);

                string attachName = string.Format("{0}_{1}", index.ToString("000"), attach.Name);
                string suffix = encrypt ? ".chacha" : "";
                string message = String.Format("Uploading attachment... {0}", attachName);
                UploadProgress(message);

                id = UploadFile(fileStream, parentId, attachName + suffix, attach.Name + suffix, mType);

                fileStream.Close();
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e);
                id = "";
            }

            if (videoDownloadStream != null)
            {
                videoDownloadStream.Close();
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

            return id;
        }

        private string UploadPhotoAttachment(KidsNoteContent.Attachment attach, int index, string parentId, string ext, bool encrypt)
        {
            attach.Data.Seek(0, SeekOrigin.Begin);

            MemoryStream encryptedStream = null;
            if (encrypt)
            {
                EncryptorChaCha chacha = new EncryptorChaCha(true, EncryptorChaCha.DefaultChaChaEncKey, EncryptorChaCha.DefaultChaChaEncNonce);
                encryptedStream = new MemoryStream();

                byte[] readBuffer = new byte[4096];
                byte[] encBuffer = new byte[readBuffer.Length];

                int nRead = attach.Data.Read(readBuffer, 0, readBuffer.Length);
                while (nRead > 0)
                {
                    chacha.Process(readBuffer, 0, nRead, encBuffer, 0);
                    encryptedStream.Write(encBuffer, 0, nRead);
                    nRead = attach.Data.Read(readBuffer, 0, readBuffer.Length);
                }

                encryptedStream.Seek(0, SeekOrigin.Begin);
            }

            string mType = MimeType.get(ext);
            string attachName = string.Format("{0}_{1}", index.ToString("000"), attach.Name);
            string message = String.Format("Uploading attachment... {0}", attachName);
            UploadProgress(message);

            Stream toUpload = encrypt ? encryptedStream : attach.Data;
            string suffix = encrypt ? ".chacha" : "";

            string id = "";
            try
            {
                id = UploadFile(toUpload, parentId, attachName + suffix, attach.Name + suffix, mType);
            }
            catch (Exception)
            {
                id = "";
            }

            return id;
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
