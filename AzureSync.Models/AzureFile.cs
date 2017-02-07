using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureSync.Models
{
    public class AzureFile
    {
        public string Name { get; set; }
        public string ContentType { get; set; }
        public DateTime? LastModifiedUTC { get; set; }
    }
}
