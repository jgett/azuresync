using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;
using System.IO;
using System.Configuration;
using System;

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

        private CloudBlobClient _client;
        private CloudBlobContainer _container;

        public AzureConnection(string connstr, string containerName)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connstr);
            _client = storageAccount.CreateCloudBlobClient();
            _container = _client.GetContainerReference(containerName);
            _container.CreateIfNotExists();            
        }

        public IEnumerable<string> ListFiles()
        {
            var list = _container.ListBlobs(null, true);

            var result = new List<string>();

            foreach (IListBlobItem item in list)
            {
                if (typeof(ICloudBlob).IsAssignableFrom(item.GetType()))
                {
                    var blob = (ICloudBlob)item;
                    result.Add(blob.Name);
                }
            }

            return result;
        }

        public int DownloadFolder(string dest)
        {
            var list = _container.ListBlobs(null, true);

            int result = 0;

            foreach (IListBlobItem item in list)
            {
                if(typeof(ICloudBlob).IsAssignableFrom(item.GetType()))
                {
                    var blob = (ICloudBlob)item;

                    var path = Path.GetFullPath(Path.Combine(dest, blob.Name));

                    var dir = Path.GetDirectoryName(path);

                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (File.Exists(path))
                    {
                        // overwrite if newer
                        if (blob.Properties.LastModified > File.GetLastWriteTimeUtc(path))
                        {
                            blob.DownloadToFile(path, FileMode.Create);
                            result++;
                        }
                    }
                    else
                    {
                        blob.DownloadToFile(path, FileMode.CreateNew);
                        result++;
                    }
                }
            }

            return result;
        }

        public int UploadFolder(string src)
        {
            var files = new List<string>();

            FileUtility.GetFolderFilesRecursive(files, src);

            int result = 0;

            foreach (var file in files)
            {
                UploadResult uploadResult = null;

                if (AzureSyncConfiguration.Current.GetBlobType(file) == BlobType.Block)
                    uploadResult = UploadBlockBlob(file);
                else
                    uploadResult = UploadPageBlob(file);

                if (uploadResult.Uploaded)
                    result += 1;

                string contentType = AzureSyncConfiguration.Current.GetContentType(file);

                if (uploadResult.Blob.Properties.ContentType != contentType)
                {
                    uploadResult.Blob.Properties.ContentType = contentType;
                    uploadResult.Blob.SetProperties();
                }
            }

            return result;
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
    }

    public class UploadResult
    {
        public CloudBlob Blob { get; set; }
        public bool Uploaded { get; set; }
    }
}
