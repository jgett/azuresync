using System.Configuration;
using System.IO;

namespace AzureSync
{
    public class AzureSyncConfiguration : ConfigurationSection
    {
        public static AzureSyncConfiguration Current { get; }

        static AzureSyncConfiguration()
        {
            Current = (AzureSyncConfiguration)ConfigurationManager.GetSection("azureSync");
        }

        [ConfigurationProperty("serviceUrl", IsRequired = true)]
        public string ServiceUrl
        {
            get { return (string)this["serviceUrl"]; }
            set { this["serviceUrl"] = value; }
        }

        [ConfigurationProperty("connectionString", IsRequired = true)]
        public string ConnectionString
        {
            get { return (string)this["connectionString"]; }
            set { this["connectionString"] = value; }
        }

        [ConfigurationProperty("syncFolder", IsRequired = true)]
        public string SyncFolder
        {
            get { return (string)this["syncFolder"]; }
            set { this["syncFolder"] = value; }
        }

        [ConfigurationProperty("mimeTypes", IsRequired = false)]
        public MimeTypeCollection MimeTypes
        {
            get { return this["mimeTypes"] as MimeTypeCollection; }
        }

        public string GetContentType(string fileName, string defval = "application/octet-stream")
        {
            if (MimeTypes != null)
            {

                string ext = Path.GetExtension(fileName); //includes a leading dot (.)

                var mime = MimeTypes[ext];

                if (mime == null)
                    return defval;
                else
                    return mime.ContentType;
            }

            return defval;
        }

        public BlobType GetBlobType(string fileName, BlobType defval = BlobType.Block)
        {
            if (MimeTypes != null)
            {

                string ext = Path.GetExtension(fileName); //includes a leading dot (.)

                var mime = MimeTypes[ext];

                if (mime == null)
                    return defval;
                else
                    return mime.BlobType;
            }

            return defval;
        }
    }

    [ConfigurationCollection(typeof(MimeTypeConfigElement))]
    public class MimeTypeCollection : ConfigurationElementCollection
    {
        public new MimeTypeConfigElement this[string ext]
        {
            get
            {
                return BaseGet(ext) as MimeTypeConfigElement;
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new MimeTypeConfigElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((MimeTypeConfigElement)element).Ext;
        }
    }

    public enum BlobType
    {
        Block = 1,
        Page = 2
    }

    public class MimeTypeConfigElement : ConfigurationElement
    {
        [ConfigurationProperty("ext", IsRequired = true, IsKey = true)]
        public string Ext
        {
            get { return (string)this["ext"]; }
            set { this["ext"] = value; }
        }

        [ConfigurationProperty("contentType", IsRequired = true)]
        public string ContentType
        {
            get { return (string)this["contentType"]; }
            set { this["contentType"] = value; }
        }

        [ConfigurationProperty("blobType", IsRequired = false, DefaultValue = BlobType.Block)]
        public BlobType BlobType
        {
            get { return (BlobType)this["blobType"]; }
            set { this["blobType"] = value; }
        }
    }
}
