using System.Collections.Generic;
using System.Linq;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

class YadDeletePostModelV2 : YadModelV2
{
    public YadDeletePostModelV2(string path)
    {
        APIMethod = "mpfs/bulk-async-delete";
        ResultType = typeof(List<YadOperationStatusResultV2>);
        RequestParameter = new YadRequestV2ParameterOperation()
        {
            Operations = [new YadRequestV2Operation() { Src = WebDavPath.Combine("/disk", path) }]
        };
    }

    /// <summary>Oid of operation.</summary>
    public string Result
        => ((List<YadOperationStatusResultV2>)ResultObject)?.FirstOrDefault()?.Oid;
}
