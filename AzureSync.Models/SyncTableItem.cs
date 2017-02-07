using System;

namespace AzureSync.Models
{
    public class SyncTableItem
    {
        public string ContainerName { get; set; }
        public string Name { get; set; }
        public string LocalPath { get; set; }
        public DateTime? LastSyncUtc { get; set; }
        public DateTime? LocalLastModifiedUtc { get; set; }
        public DateTime? RemoteLastModifiedUtc { get; set; }
    }
}
