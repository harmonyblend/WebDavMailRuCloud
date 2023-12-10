using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Requests.Types;

public class BrowserAppRequest
{
    [JsonProperty("login")]
    public string Login { get; set; }

    [JsonProperty("password")]
    public string Password { get; set; }

    [JsonProperty("user-agent")]
    public string UserAgent { get; set; }

    [JsonProperty("sec-ch-ua")]
    public string SecChUa { get; set; }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
}

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
