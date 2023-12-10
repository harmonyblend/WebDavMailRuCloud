using Newtonsoft.Json;

namespace BrowserAuthenticator;

#pragma warning disable CA1507 // Use nameof to express symbol names


public class BrowserAppRequest
{
    [JsonProperty("login")]
    public string? Login { get; set; }

    [JsonProperty("password")]
    public string? Password { get; set; }

    [JsonProperty("user-agent")]
    public string? UserAgent { get; set; }

    [JsonProperty("sec-ch-ua")]
    public string? SecChUa { get; set; }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
}

public class BrowserAppResult
{
    [JsonProperty("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("Login")]
    public string? Login { get; set; }

    [JsonProperty("Cloud")]
    public string? Cloud { get; set; }

    [JsonProperty("Uuid")]
    public string? Uuid { get; set; }

    [JsonProperty("Sk")]
    public string? Sk { get; set; }

    [JsonProperty("Cookies")]
    public List<BrowserAppCookieResponse>? Cookies { get; set; }

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }
}

public class BrowserAppCookieResponse
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("Value")]
    public string? Value { get; set; }

    [JsonProperty("Path")]
    public string? Path { get; set; }

    [JsonProperty("Domain")]
    public string? Domain { get; set; }
}
