using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Requests.Types;

public class BrowserAppResult
{
    [JsonProperty("ErrorMessage")]
    public string ErrorMessage { get; set; }

    [JsonProperty("Login")]
    public string Login { get; set; }

    [JsonProperty("Cloud")]
    public string Cloud { get; set; }


    /// <summary>
    /// yandexuid
    /// </summary>
    [JsonProperty("Uuid")]
    public string Uuid { get; set; }

    [JsonProperty("Sk")]
    public string Sk { get; set; }

    [JsonProperty("Cookies")]
    public List<BrowserAppCookie> Cookies { get; set; }
}

public class BrowserAppCookie
{
    [JsonProperty("Name")]
    public string Name { get; set; }

    [JsonProperty("Value")]
    public string Value { get; set; }

    [JsonProperty("Path")]
    public string Path { get; set; }

    [JsonProperty("Domain")]
    public string Domain { get; set; }
}
