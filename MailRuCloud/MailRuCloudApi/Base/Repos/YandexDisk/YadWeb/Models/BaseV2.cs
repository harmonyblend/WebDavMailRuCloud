using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

public class YadPostDataV2
{
    public YadRequestV2 Request { get; set; }

    public YadPostDataV2(string sk, string idClient)
    {
        Request = new YadRequestV2()
        {
            Sk = sk,
            IdClient = idClient
        };
    }

    public byte[] CreateHttpContent()
        => System.Text.Encoding.UTF8.GetBytes(
            JsonConvert.SerializeObject(
                Request));
}

public abstract class YadModelV2
{
    public string APIMethod { get; set; }
    public YadRequestV2Parameter RequestParameter { get; set; }

    public Action<string> Deserialize = null;

    public List<YadResponseV2Error> Errors { get; set; } = [];
    internal object ResultObject { get; set; }
    internal Type ResultType { get; set; }

    public string SourceJsonForDebug { get; set; }
}


public class YadResponseV2ErrorDescription
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("statusCode")]
    public int StatusCode { get; set; }

    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }
}

// [{"error":{"type":"mpfs","statusCode":404,"code":77,"title":"Wrong path"}}]
public class YadResponseV2Error
{
    [JsonProperty("error")]
    public YadResponseV2ErrorDescription Error { get; set; }
}

public class YadOperationStatusResultV2 : YadResponseV2Error
{
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("state")]
    public string State { get; set; }

    [JsonProperty("at_version")]
    public long AtVersion { get; set; }

    [JsonProperty("oid")]
    public string Oid { get; set; }
}

public class YadRequestV2
{
    [JsonProperty("sk")]
    public string Sk { get; set; }

    [JsonProperty("connection_id")]
    public string IdClient { get; set; }

    [JsonProperty("apiMethod")]
    public string APIMethod { get; set; }

    [JsonProperty("requestParams")]
    public YadRequestV2Parameter RequestParameter { get; set; }
}

public class YadRequestV2Parameter
{
}

public class YadRequestV2ParameterOperation : YadRequestV2Parameter
{
    [JsonProperty("operations")]
    public List<YadRequestV2Operation> Operations { get; set; }
}

public class YadRequestV2Operation
{
    [JsonProperty("src")]
    public string Src { get; set; }
}
