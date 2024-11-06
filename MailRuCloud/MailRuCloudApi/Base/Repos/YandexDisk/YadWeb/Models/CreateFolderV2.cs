using System.Collections.Generic;
using Newtonsoft.Json;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadCreateFolderPostModelV2 : YadModelV2
    {
        public YadCreateFolderPostModelV2(string path)
        {
            APIMethod = "mpfs/mkdir";
            ResultType = typeof(void);
            RequestParameter = new YadRequestV2CreateFolder()
            {
                Path = WebDavPath.Combine("/disk", path)
            };
        }

        /// <summary>Result is not supported. Use bulk-resource-info</summary>
        public string Result => null;
    }
}

public class YadRequestV2CreateFolder : YadRequestV2Parameter
{
    [JsonProperty("path")]
    public string Path { get; set; }
}
