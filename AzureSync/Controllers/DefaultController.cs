using AzureSync.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Http;

namespace AzureSync.Controllers
{
    public class DefaultController : ApiController
    {
        /// <summary>
        /// Returns the service name.
        /// </summary>
        [HttpGet, Route("")]
        public string Get()
        {
            return "azuresync";
        }

        /// <summary>
        /// Returns the path defined by the appSetting 'SyncFolder' in App.config.
        /// </summary>
        [HttpGet, Route("folder")]
        public string GetFolder()
        {
            return AzureSyncConfiguration.Current.SyncFolder;
        }

        /// <summary>
        /// Returns true if the folder exists, otherwise false.
        /// </summary>
        [HttpGet, Route("folder/exists")]
        public bool FolderExists()
        {
            var path = GetFolder();
            return Directory.Exists(path);
        }

        /// <summary>
        /// Returns true if the folder is created, false if the folder already exists.
        /// </summary>
        [HttpGet, Route("folder/create")]
        public bool CreateFolder()
        {
            var path = GetFolder();

            if (!FolderExists())
            {
                Directory.CreateDirectory(path);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a list of files currently in the sync folder.
        /// </summary>
        [HttpGet, Route("folder/files")]
        public IEnumerable<FolderFile> GetFolderFiles()
        {
            var result = new List<FolderFile>();
            FileUtility.GetFolderFilesRecursive(result, GetFolder());
            return result;
        }

        /// <summary>
        /// Uploads files from the sync folder if they do not exist or are newer. Returns the number of files uploaded.
        /// </summary>
        [HttpGet, Route("folder/files/sync")]
        public int SyncFolderFiles()
        {
            var azure = AzureConnection.Open();

            int result = azure.UploadFolder(GetFolder());

            return result;
        }

        /// <summary>
        /// Returns a list of files currenlty in the Azure container.
        /// </summary>
        [HttpGet, Route("azure/files")]
        public IEnumerable<AzureFile> GetAzureFiles()
        {
            var azure = AzureConnection.Open();
            return azure.ListFiles();
        }

        /// <summary>
        /// Downloads files from the Azure container if they do not exist or are newer. Returns the number of files dowloaded.
        /// </summary>
        [HttpGet, Route("azure/files/sync")]
        public int SyncAzureFiles()
        {
            FolderWatcher.Current.Stop();

            var azure = AzureConnection.Open();
            int result = azure.DownloadFolder(GetFolder());

            FolderWatcher.Current.Start();

            return result;
        }

        [HttpGet, Route("azure/table")]
        public IEnumerable<SyncTableItem> GetSyncTable()
        {
            var azure = AzureConnection.Open();

            var result = azure.QuerySyncTable().Select(x => new SyncTableItem()
            {
                ContainerName = x.PartitionKey,
                Name = x.DecodeName(),
                LocalPath = x.LocalPath,
                LastSyncUtc = x.LastSyncUtc,
                LocalLastModifiedUtc = x.LocalLastModifiedUtc,
                RemoteLastModifiedUtc = x.RemoteLastModifiedUtc
            });

            return result;
        }

        [HttpGet, Route("azure/table/clear")]
        public int ClearSyncTable()
        {
            var azure = AzureConnection.Open();
            int result = azure.ClearSyncTable();
            return result;
        }
    }
}
