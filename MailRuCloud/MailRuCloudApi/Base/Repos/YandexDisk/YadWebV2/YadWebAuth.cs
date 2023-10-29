using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YaR.Clouds.Base.Repos.YandexDisk.YadWebV2.Models;
using YaR.Clouds.Base.Repos.YandexDisk.YadWebV2.Requests;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWebV2
{
    class YadWebAuth : IAuth
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadWebAuth));

        public YadWebAuth(SemaphoreSlim connectionLimiter, HttpCommonSettings settings, IBasicCredentials credentials)
        {

            _settings = settings;
            _creds = credentials;
            Cookies = new CookieContainer();
            bool doRegularLogin = true;

            // if local cookie cache on disk is enabled
            if (!string.IsNullOrEmpty(_settings.CloudSettings.BrowserAuthenticatorCacheDir))
            {
                string path = null;

                // Если в кеше аутентификации пустой, пытаемся загрузить куки из кеша
                try
                {
                    // Check file with cookies is created
                    path = Path.Combine(
                        settings.CloudSettings.BrowserAuthenticatorCacheDir,
                        credentials.Login);

                    if (System.IO.File.Exists(path))
                    {
                        var testAuthenticator = new YadWebAuth(_settings, _creds, path);
                        // Try to get user info using cached cookie
                        new YaDCommonRequest(_settings, testAuthenticator)
                            .With(new YadAccountInfoPostModel(),
                                out YadResponseModel<YadAccountInfoRequestData, YadAccountInfoRequestParams> itemInfo)
                            .MakeRequestAsync(connectionLimiter).Wait();

                        var res = itemInfo.ToAccountInfo();

                        // Request for user info using cached cookie finished successfully
                        Cookies = testAuthenticator.Cookies;
                        DiskSk = testAuthenticator.DiskSk;
                        Uuid = testAuthenticator.Uuid;
                        doRegularLogin = false;
                        Logger.Info($"Browser authentication refreshed using cached cookie");
                    }
                }
                catch (Exception)
                {
                    // Request for user info using cached cookie failed

                    // Delete file with cache first
                    try
                    {
                        System.IO.File.Delete(path);
                    }
                    catch (Exception) { }
                    // Then make regular login
                    doRegularLogin = true;
                }
            }

            if (doRegularLogin)
            {
                try
                {
                    MakeLogin().Wait();
                }
                catch (AggregateException aex) when (aex.InnerException is HttpRequestException ex)
                {
                    Logger.Error("Browser authentication failed! " +
                        "Please check browser authentication component is running!");

                    throw new InvalidCredentialException("Browser authentication failed! Browser component is not running!");
                }
                catch (AggregateException aex) when (aex.InnerException is AuthenticationException ex)
                {
                    string txt = string.Concat("Browser authentication failed! ", ex.Message);
                    Logger.Error(txt);

                    throw new InvalidCredentialException(txt);
                }
                catch (Exception ex)
                {
                    Logger.Error("Browser authentication failed! " +
                        "Check the URL and the password for browser authentication component!");

                    throw new InvalidCredentialException("Browser authentication failed!");
                }
                Logger.Info($"Browser authentication successful");
            }
        }

        public YadWebAuth(HttpCommonSettings settings, IBasicCredentials credentials, string path)
        {
            _settings = settings;
            _creds = credentials;
            Cookies = new CookieContainer();

            string content = System.IO.File.ReadAllText(path);
            BrowserAppResponse response = JsonConvert.DeserializeObject<BrowserAppResponse>(content);

            DiskSk = /*YadAuth.DiskSk*/ response.Sk;
            Uuid = /*YadAuth.Uuid*/response.Uuid; //yandexuid

            foreach (var item in response.Cookies)
            {
                var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain);
                Cookies.Add(cookie);
            }
        }

        private readonly IBasicCredentials _creds;
        private readonly HttpCommonSettings _settings;

        public async Task MakeLogin()
        {
            (BrowserAppResponse response, string responseHtml) = await ConnectToBrowserApp();

            if (response != null &&
                !string.IsNullOrEmpty(response.Sk) &&
                !string.IsNullOrEmpty(response.Uuid) &&
                !string.IsNullOrEmpty(response.Login) &&
                GetNameOnly(response.Login)
                    .Equals(GetNameOnly(Login), StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(response.ErrorMessage)
                )
            {
                DiskSk = /*YadAuth.DiskSk*/ response.Sk;
                Uuid = /*YadAuth.Uuid*/response.Uuid; //yandexuid

                foreach (var item in response.Cookies)
                {
                    var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain);
                    Cookies.Add(cookie);
                }

                // Если аутентификация прошла успешно, сохраняем результат в кеш в файл
                if (!string.IsNullOrEmpty(_settings.CloudSettings.BrowserAuthenticatorCacheDir))
                {
                    string path = Path.Combine(
                        _settings.CloudSettings.BrowserAuthenticatorCacheDir,
                        _creds.Login);

                    try
                    {
                        string dir = Path.GetDirectoryName(path);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                    }
                    catch (Exception)
                    {
                        throw new AuthenticationException("Directory for cache can not be created, " +
                            "remove attribute CacheDir in BrowserAuthenticator tag in configuration file!");
                    }
                    try
                    {
#if NET48
                        System.IO.File.WriteAllText(path, responseHtml);
#else
                        await System.IO.File.WriteAllTextAsync(path, responseHtml);
#endif
                    }
                    catch (Exception) { }
                }
            }
            else
            {
                if (string.IsNullOrEmpty(response?.ErrorMessage))
                    throw new AuthenticationException("OAuth: Authentication using YandexAuthBrowser is failed!");

                throw new AuthenticationException(
                    string.Concat(
                        "OAuth: Authentication using YandexAuthBrowser is failed! ",
                        response.ErrorMessage));
            }
        }

        public string Login => _creds.Login;
        public string Password => _creds.Password;
        public string DiskSk { get; set; }
        /// <summary>
        /// yandexuid
        /// </summary>
        public string Uuid { get; set; }

        public bool IsAnonymous => false;
        public string AccessToken { get; }
        public string DownloadToken { get; }
        public CookieContainer Cookies { get; private set; }
        public void ExpireDownloadToken()
        {
            throw new NotImplementedException();
        }

        public class BrowserAppResponse
        {
            [JsonProperty("ErrorMessage")]
            public string ErrorMessage { get; set; }

            [JsonProperty("Login")]
            public string Login { get; set; }

            /// <summary>
            /// yandexuid
            /// </summary>
            [JsonProperty("Uuid")]
            public string Uuid { get; set; }

            [JsonProperty("Sk")]
            public string Sk { get; set; }

            [JsonProperty("Cookies")]
            public List<BrowserAppCookieResponse> Cookies { get; set; }
        }
        public class BrowserAppCookieResponse
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

        private static string GetNameOnly(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            int pos = value.IndexOf('@');
            if (pos == 0)
                return "";
            if (pos > 0)
                return value.Substring(0, pos);
            return value;
        }

        private async Task<(BrowserAppResponse, string)> ConnectToBrowserApp()
        {
            string url = _settings.CloudSettings.BrowserAuthenticatorUrl;
            string password = string.IsNullOrWhiteSpace(Password)
                ? _settings.CloudSettings.BrowserAuthenticatorPassword
                : Password;

            if (string.IsNullOrEmpty(url))
            {
                throw new Exception("Ошибка! " +
                    "Для работы с Яндекс.Диском запустите сервер аутентификации и задайте в параметре YandexAuthenticationUrl его настройки!");
            }

            using var client = new HttpClient { BaseAddress = new Uri(url) };
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"/{Uri.EscapeDataString(Login)}/{Uri.EscapeDataString(password)}/", UriKind.Relative),
                Headers = {
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { HttpRequestHeader.ContentType.ToString(), "application/json" },
                },
                //Content = new StringContent(JsonConvert.SerializeObject(""))
            };

            client.Timeout = new TimeSpan(0, 5, 0);
            using var response = await client.SendAsync(httpRequestMessage);
            var responseText = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            BrowserAppResponse data = JsonConvert.DeserializeObject<BrowserAppResponse>(responseText);
            return (data, responseText);
        }
    }
}
