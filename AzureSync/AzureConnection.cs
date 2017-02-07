using AzureSync.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace AzureSync
{
    public class AzureConnection
    {
        public static AzureConnection Open()
        {
            var connstr = AzureSyncConfiguration.Current.ConnectionString;
            var folder = AzureSyncConfiguration.Current.SyncFolder;
            var containerName = Path.GetFileName(folder);
            return new AzureConnection(connstr, containerName);
        }

        private CloudTableClient _tableClient;
        private CloudBlobClient _blobClient;
        private CloudBlobContainer _container;

        public AzureConnection(string connstr, string containerName)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connstr);

            // create the table client
            _tableClient = storageAccount.CreateCloudTableClient();

            // create the blob client
            _blobClient = storageAccount.CreateCloudBlobClient();
            _container = _blobClient.GetContainerReference(containerName);
            _container.CreateIfNotExists();            
        }

        public IEnumerable<AzureFile> ListFiles()
        {
            var list = _container.ListBlobs(null, true);

            var result = new List<AzureFile>();

            foreach (IListBlobItem item in list)
            {
                if (typeof(ICloudBlob).IsAssignableFrom(item.GetType()))
                {
                    var blob = (ICloudBlob)item;

                    var azureFile = new AzureFile()
                    {
                        Name = blob.Name,
                        ContentType = blob.Properties.ContentType,
                        LastModifiedUTC = GetLastModified(blob)
                    };
                    result.Add(azureFile);
                }
            }

            return result;
        }

        private bool IsSyncRequired(AzureSyncEntity entity, ICloudBlob blob, string path)
        {
            // determine if this blob should be synchronized

            if (entity == null)
                throw new ArgumentNullException("entity");

            if (blob == null)
                throw new ArgumentNullException("blob");

            if (!File.Exists(path)) return true;

            var blobLastModified = GetLastModified(blob);

            if (!blobLastModified.HasValue) return true;

            if (!entity.RemoteLastModifiedUtc.HasValue) return true;

            if (blobLastModified.Value > entity.RemoteLastModifiedUtc.Value) return true;

            var localLastModified = File.GetLastWriteTimeUtc(path);

            if (!entity.LocalLastModifiedUtc.HasValue) return true;

            if (localLastModified > entity.LocalLastModifiedUtc.Value) return true;

            if (!entity.LastSyncUtc.HasValue) return true;

            if (blobLastModified.Value > entity.LastSyncUtc.Value) return true;

            if (localLastModified > entity.LastSyncUtc.Value) return true;

            return false;
        }

        public int DownloadFolder(string dest)
        {
            var query = QuerySyncTable();

            // track updates to the sync table
            var updates = new List<AzureSyncEntity>();

            var list = _container.ListBlobs(null, true);

            foreach (IListBlobItem item in list)
            {
                if(typeof(ICloudBlob).IsAssignableFrom(item.GetType()))
                {
                    var blob = (ICloudBlob)item;

                    var path = Path.GetFullPath(Path.Combine(dest, blob.Name));

                    var dir = Path.GetDirectoryName(path);

                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var entity = query.FirstOrDefault(x => x.DecodeName() == blob.Name);

                    if (entity == null)
                        entity = new AzureSyncEntity(_container.Name, blob.Name);

                    if (IsSyncRequired(entity, blob, path))
                    {
                        blob.DownloadToFile(path, FileMode.Create);
                        entity.LocalPath = path;
                        entity.LastSyncUtc = DateTime.UtcNow;
                        entity.LocalLastModifiedUtc = File.GetLastWriteTimeUtc(path);
                        entity.RemoteLastModifiedUtc = GetLastModified(blob);
                        updates.Add(entity);
                    }
                }
            }

            UpdateSyncEntities(updates);

            return updates.Count;
        }

        public int UploadFolder(string src)
        {
            var files = new List<FolderFile>();

            FileUtility.GetFolderFilesRecursive(files, src);

            int result = 0;

            foreach (var file in files)
            {
                UploadResult uploadResult = null;

                if (AzureSyncConfiguration.Current.GetBlobType(file.Name) == BlobType.Block)
                    uploadResult = UploadBlockBlob(file.Path);
                else
                    uploadResult = UploadPageBlob(file.Path);

                if (uploadResult.Uploaded)
                    result += 1;

                string contentType = AzureSyncConfiguration.Current.GetContentType(file.Name);

                if (uploadResult.Blob.Properties.ContentType != contentType)
                {
                    uploadResult.Blob.Properties.ContentType = contentType;
                    uploadResult.Blob.SetProperties();
                }
            }

            return result;
        }

        public CloudTable GetSyncTable()
        {
            // Retrieve a reference to the table.
            CloudTable table = _tableClient.GetTableReference("azuresync");

            // Create the table if it doesn't exist.
            bool result = table.CreateIfNotExists();

            return table;
        }

        public IEnumerable<AzureSyncEntity> QuerySyncTable()
        {
            // Retrieve a reference to the table.
            var table = GetSyncTable();

            var query = new TableQuery<AzureSyncEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, _container.Name));

            var result = table.ExecuteQuery(query);

            return result;
        }

        public int ClearSyncTable()
        {
            var query = QuerySyncTable();

            var result = query.Count();

            if (result > 0)
            { 
                var table = GetSyncTable();

                foreach (var item in query)
                    table.Execute(TableOperation.Delete(item));
            }

            return result;
        }

        public void UpdateSyncEntities(IEnumerable<AzureSyncEntity> entities)
        {
            if (entities == null || entities.Count() == 0)
                return;

            var table = GetSyncTable();
            
            var batch = new TableBatchOperation();

            foreach(var entity in entities)
                batch.InsertOrReplace(entity);

            table.ExecuteBatch(batch);
        }

        private UploadResult UploadBlockBlob(string file)
        {
            string azurePath = FileUtility.GetAzurePath(_container.Name, file);

            UploadResult result = new UploadResult();

            var blob = _container.GetBlockBlobReference(azurePath);
            var uploaded = false;

            if (!blob.Exists())
            {
                blob.UploadFromFile(file);
                uploaded = true;
            }
            else
            {
                if (File.GetLastWriteTimeUtc(file) > blob.Properties.LastModified)
                {
                    blob.UploadFromFile(file);
                    uploaded = true;
                }
            }

            return new UploadResult() { Blob = blob, Uploaded = uploaded };
        }

        private UploadResult UploadPageBlob(string file)
        {
            string azurePath = FileUtility.GetAzurePath(_container.Name, file);

            var blob = _container.GetPageBlobReference(azurePath);
            var uploaded = false;

            if (!blob.Exists())
            {
                blob.UploadFromFile(file);
                uploaded = true;
            }
            else
            {
                if (File.GetLastWriteTimeUtc(file) > blob.Properties.LastModified)
                {
                    blob.UploadFromFile(file);
                    uploaded = true;
                }
            }

            return new UploadResult() { Blob = blob, Uploaded = uploaded };
        }

        private DateTime? GetLastModified(ICloudBlob blob)
        {
            if (blob.Properties.LastModified.HasValue)
                return blob.Properties.LastModified.Value.DateTime;
            else
                return default(DateTime?);
        }
    }

    public class AzureSyncEntity : TableEntity
    {
        public AzureSyncEntity() { }

        public AzureSyncEntity(string containerName, string name)
        {
            PartitionKey = containerName;
            RowKey = WebUtility.UrlEncode(name);
        }

        public string LocalPath { get; set; }
        public DateTime? LastSyncUtc { get; set; }
        public DateTime? LocalLastModifiedUtc { get; set; }
        public DateTime? RemoteLastModifiedUtc { get; set; }

        public string DecodeName()
        {
            return WebUtility.UrlDecode(RowKey);
        }
    }

    public class UploadResult
    {
        public CloudBlob Blob { get; set; }
        public bool Uploaded { get; set; }
    }
}
