using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadResourceInfoPostModelV2 : YadModelV2
    {
        public YadResourceInfoPostModelV2(string path)
        {
            APIMethod = "mpfs/bulk-resource-info";
            ResultType = typeof(List<FolderInfoDataResource>);
            var pathes = new YadRequestV2ResourceInfo();
            RequestParameter = new YadRequestV2ResourceInfo()
            {
                Pathes = [WebDavPath.Combine("/disk", path)]
            };
        }

        /// <summary>Result is not supported. Use bulk-resource-info</summary>
        public FolderInfoDataResource Result
            => ((List<FolderInfoDataResource>)ResultObject)?.FirstOrDefault();
    }
}

public class YadRequestV2ResourceInfo : YadRequestV2Parameter
{
    [JsonProperty("ids")]
    public List<string> Pathes { get; set; }
}
