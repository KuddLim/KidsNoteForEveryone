using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private static string[] Scopes = { DriveService.Scope.DriveReadonly };
        private UserCredential Credential;
        private DriveService Service;
        private string CredentialPath;
        private string TokenPath;
        private string ChildName;

        public GetBaseFolderIdDelegate GetBaseFolderId;
        public SetBaseFolderIdDelegate SetBaseFolderId;

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
            listRequest.Fields = "nextPageToken, files(id, name, mimeType)";

            IList<Google.Apis.Drive.v3.Data.File> files = listRequest.Execute().Files;

            foreach (var each in files)
            {
                System.Diagnostics.Trace.WriteLine(each.Name);
            }

            string baseFolderId = FindBackupFolderId();

            string query = String.Format("mimeType = 'application/vnd.google-apps.folder' and name = '{0}'", BaseFolderName());
            listRequest.Q = "mimeType = 'application/vnd.google-apps.folder' and name = 'test_folder'";
            listRequest.PageSize = 10;
            listRequest.Fields = "nextPageToken, files(id, name, originalFileName, mimeType)";

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
            IList<Google.Apis.Drive.v3.Data.File> files = null;

            FilesResource.ListRequest listRequest = Service.Files.List();
            listRequest.PageSize = 10;
            listRequest.Fields = "nextPageToken, files(id, name, originalFileName, mimeType)";

            string prevFolderId = GetBaseFolderId();
            string baseFolderId = prevFolderId;
            if (baseFolderId != "")
            {
                //string query = String.Format("mimeType = 'application/vnd.google-apps.folder' and name = '{0}'", BaseFolderName());
                string query = String.Format("mimeType = '{0}' and id = '{1}'", Constants.GOOGLE_DRIVE_MIMETYPE_FOLDER, baseFolderId);
                listRequest.Q  = query;
                files = listRequest.Execute().Files;

                if (files.Count == 0)
                {
                    // 새로 생성해야 함.
                    baseFolderId = "";
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

            FilesResource.CreateRequest request = Service.Files.Create(folderToCreate);
            request.Fields = "id";
            Google.Apis.Drive.v3.Data.File created = request.Execute();

            return created.Id;
        }

    }
}
