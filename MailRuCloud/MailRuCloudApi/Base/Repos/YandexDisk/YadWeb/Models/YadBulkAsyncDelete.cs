using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Xml.Linq;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;
using YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.Types;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadBulkAsyncDelete : YadPostModelV2
    {
        public YadBulkAsyncDelete(string path)
        {
            Name = "mpfs/bulk-async-delete";
            Path = path;
        }

        public string Path { get; set; }

        public override object ToContent()
        {
            return new
            {
                operations = new[] 
                { 
                    new 
                    { 
                        src = "/disk" + Path
                    }
                }
            };
        }
    }

    public class YadBulkAsyncDeleteRequestData : YadModelDataBase
    {
        [JsonProperty("at_version")]
        public long AtVersion { get; set; }

        [JsonProperty("oid")]
        public string Oid { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class YadBulkAsyncDeleteRequestParams
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}