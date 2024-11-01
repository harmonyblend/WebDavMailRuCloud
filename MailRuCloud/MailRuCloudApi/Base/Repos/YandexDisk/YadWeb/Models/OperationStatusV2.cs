using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadOperationStatusV2 : YadModelV2
{
    public YadOperationStatusV2(string opId)
    {
        APIMethod = "mpfs/bulk-operation-status";
        ResultType = typeof(Dictionary<string, YadOperationStatusResultV2>);
        RequestParameter = new YadRequestV2ParameterOids()
        {
            Oids = [opId]
        };
    }

    public Dictionary<string /* Oid */, YadOperationStatusResultV2> Result
        => (Dictionary<string /* Oid */, YadOperationStatusResultV2>)ResultObject;
}

public class YadRequestV2ParameterOids : YadRequestV2Parameter
{
    [JsonProperty("oids")]
    public List<string> Oids { get; set; }
}
