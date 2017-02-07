using System;

namespace AzureSync.Models
{
    public class FolderFile
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public DateTime LastModifiedUTC { get; set; }
    }
}
