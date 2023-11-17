using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Extensions;

namespace YaR.Clouds.Base;

public partial class Credentials : IBasicCredentials
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Credentials));

    private static readonly string[] AnonymousLogins = { "anonymous", "anon", "anonym", string.Empty };


    public bool IsAnonymous { get; private set; }

    public Protocol Protocol { get; private set; } = Protocol.Autodetect;
    public CloudType CloudType { get; private set; }

    public string Login { get; private set; }
    public string Password { get; private set; }

    public string PasswordCrypt { get; set; }

    public bool CanCrypt => !string.IsNullOrEmpty(PasswordCrypt);

    public bool AuthenticationUsingBrowser { get; private set; }


    #region На текущий момент специфично только для Янднекс.Диска

    public bool IsCacheUsed { get; private set; }
    public CookieContainer Cookies { get; private set; }

    public string Sk { get; set; }
    public string Uuid { get; set; }

    #endregion

    private readonly CloudSettings _settings;

    public Credentials(CloudSettings settings, string login, string password)
    {
        _settings = settings;
        IsCacheUsed = false;
        Cookies = new CookieContainer();

        if (string.IsNullOrWhiteSpace(login))
            login = string.Empty;

        if (AnonymousLogins.Contains(login))
        {
            IsAnonymous = true;
            Login = login;
            Password = string.Empty;
            PasswordCrypt = string.Empty;
            CloudType = GetCloundTypeFromLogin(Login);
            Protocol = Protocol.Autodetect;

            return;
        }

        ParseLoginPassword(login, password);
    }

    private static CloudType GetCloundTypeFromLogin(string login)
    {
        foreach (var domain in MailRuBaseRepo.AvailDomains)
        {
            bool hasMail = login.ContainsIgnoreCase(string.Concat("@", domain, "."));
            if (hasMail)
                return CloudType.Mail;
        }

        bool hasYandex = login.ContainsIgnoreCase("@yandex.") || login.ContainsIgnoreCase("@ya.");

        if (hasYandex)
            return CloudType.Yandex;

        return CloudType.Unkown;
    }

    private void ParseLoginPassword(string login, string password)
    {
        CloudType = CloudType.Unkown;
        Protocol = Protocol.Autodetect;

        /*
         * Ожидаемые форматы логина и пароля:
         * login = <имя> # <разделитель> | <имя>
         * Если login содержит символ #, то пароль должен иметь вид
         * password = <пароль> <разделитель> <ключ шифрования>,
         * при этом <разделитель> и <ключ шифрования> не могут быть пустыми.
         * Если login не содержит символ #, то пароль должен иметь вид
         * password = <пароль>.
         * <имя> может быть представлено в следующих вариантах:
         * <имя> = <!> <email или учетная запись> | <?> <email или учетная запись> | <email или учетная запись>
         * Здесь <email или учетная запись> - это email или иное название, идентифицирующее учетную запись на облачном сервере,
         * <?> - вопросительный знак перед email указывает на необходимость аутентификации через браузер
         * и соответствующий протокол обращения к облаку,
         * <!> - восклицательный знак перед email указывает на запрет аутентификации через браузер
         * и соответствующий протокол обращения к облаку.
         * На текущий момент знаки ? и ! обрабатываются всегда, но обращение к браузеру для аутентификации
         * для email с доменами Mail.Ru не производится.
         */

        login ??= string.Empty;
        password ??= string.Empty;
        string[] loginParts = login.Split(new char[] { '#' }, StringSplitOptions.RemoveEmptyEntries);
        switch (loginParts.Length)
        {
        case 0:
            IsAnonymous = true;
            Login = login.Trim();
            Password = string.Empty;
            PasswordCrypt = string.Empty;
            break;
        case 1:
            IsAnonymous = false;
            Login = loginParts[0].Trim();
            Password = password;
            PasswordCrypt = string.Empty;
            break;
        case 2:
            IsAnonymous = false;
            Login = loginParts[0].Trim();

            string sep = loginParts[1].Trim();
            if (string.IsNullOrEmpty(sep))
            {
                throw new InvalidCredentialException("Invalid credential format: " +
                    "encryption part after # symbol in login string is too short. See manuals.");
            }

            int sepPos = password.IndexOf(sep, StringComparison.InvariantCulture /* case sensitive! */);
            if (sepPos < 0 || sepPos + sep.Length >= password.Length)
            {
                throw new InvalidCredentialException("Invalid credential format: " +
                    "password doesn't contain encryption part. See manuals.");
            }
            PasswordCrypt = password.Substring(sepPos + sep.Length);
            Password = password.Substring(0, sepPos);
            break;
        default:
            throw new InvalidCredentialException("Invalid credential format: " +
                "too many # symbols in login string. See manuals.");
        }

        CloudType = GetCloundTypeFromLogin(Login);
        /*
         * Восклицательный знак перед логином указывает на запрет использования
         * браузера для аутентификации.
         * Наоборот, вопросительный знак перед логином указывает на
         * обязательное использование браузера для аутентификации.
         */
        bool browserAuthenticatorDisabled = Login.StartsWith("!");
        bool browserAuthenticatorAsked = Login.StartsWith("?");

        if (browserAuthenticatorAsked || browserAuthenticatorDisabled)
            Login = Login.Remove(0, 1).Trim();

        AuthenticationUsingBrowser = false;

        if (browserAuthenticatorAsked && CloudType != CloudType.Mail)
        {
            AuthenticationUsingBrowser = true;
        }
        else
        if (!browserAuthenticatorDisabled)
        {
            // Если аутентификация браузером не запрошена через строку логина,
            // но пароль совпадает с паролем сервиса аутентификации через браузер,
            // считаем это указанием использовать браузер для аутентификации.
            if (!string.IsNullOrEmpty(_settings.BrowserAuthenticatorPassword) &&
                password == _settings.BrowserAuthenticatorPassword &&
                CloudType != CloudType.Mail)
            {
                AuthenticationUsingBrowser = true;
            }
        }

        if (AuthenticationUsingBrowser)
            GetBrowserCookiesAsync().Wait();

        // Если тип облака на данном этапе еще не определен,
        // пытаемся определить его из строки логина, через домен учетной записи.
        if (CloudType == CloudType.Unkown)
            CloudType = GetCloundTypeFromLogin(Login);

        // Если на данном этапе протокол еще не определен,
        // определяем его по типу облака и подсказке типа протокола в параметре запуска программы.
        if (Protocol == Protocol.Autodetect)
        {
            Protocol = CloudType == CloudType.Yandex
                ? Protocol.YadWeb
                : _settings.Protocol == Protocol.WebV2
                  ? Protocol.WebV2
                  : Protocol.WebM1Bin;
        }

        // Если на этом этапа все еще не определены протокол и облако,
        // но при этом задан протокол в параметре запуска программы,
        // выставляем облако и протокол по его значению.
        if (Protocol == Protocol.Autodetect &&
            CloudType == CloudType.Unkown &&
            _settings.Protocol != Protocol.Autodetect)
        {
            Protocol = _settings.Protocol;
            CloudType = Protocol switch
            {
                Protocol.WebM1Bin => CloudType.Mail,
                Protocol.WebV2 => CloudType.Mail,
                Protocol.YadWeb => CloudType.Yandex,
                _ => CloudType.Unkown
            };
        }

        if (Protocol == Protocol.Autodetect &&
            CloudType == CloudType.Unkown)
        {
            throw new InvalidCredentialException("Invalid credential format: " +
                "cloud server is not detected by supplied login, use fully qualified email. See manuals.");
        }

        if (CloudType == CloudType.Mail &&
            !(Protocol == Protocol.WebM1Bin || Protocol == Protocol.WebV2) ||
            CloudType == CloudType.Yandex && Protocol != Protocol.YadWeb)
        {
            throw new InvalidCredentialException("Invalid credential format: " +
                "cloud type and protocol are incompatible. See manuals.");
        }
    }

    private async Task GetBrowserCookiesAsync()
    {
        Cookies = new CookieContainer();
        bool doRegularLogin = true;
        BrowserAppResult response = null;

        // Куки уже может быть сохранен в кеше на диске, проверяем
        if (!string.IsNullOrEmpty(_settings.BrowserAuthenticatorCacheDir))
        {
            string path = null;

            try
            {
                string fileName = string.IsNullOrWhiteSpace(Login) ? "anonymous" : Login;
                path = Path.Combine(_settings.BrowserAuthenticatorCacheDir, fileName);

                if (System.IO.File.Exists(path))
                {
#if NET48
                    string content = System.IO.File.ReadAllText(path);
#else
                    string content = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
#endif
                    response = JsonConvert.DeserializeObject<BrowserAppResult>(content);

                    IsCacheUsed = true;

                    doRegularLogin = false;
                    Logger.Info($"Browser authentication: cache is used");
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
            // Куки нет в кеше, кеш не задан или ошибка при использовании куки из кеша,
            // тогда полноценный вход через браузер

            try
            {
                response = await MakeLogin().ConfigureAwait(false);
            }
            catch (Exception e) when (e.OfType<HttpRequestException>().Any())
            {
                Logger.Error("Browser authentication failed! " +
                    "Please check browser authentication component is running!");

                throw new InvalidCredentialException("Browser authentication failed! Browser component is not running!");
            }
            catch (Exception e)
            {
                if (e.OfType<AuthenticationException>() is AuthenticationException ae)
                {
                    string txt = string.Concat("Browser authentication failed! ", ae.Message);
                    Logger.Error(txt);

                    throw new InvalidCredentialException(txt);
                }

                Logger.Error("Browser authentication failed! " +
                    "Check the URL and the password for browser authentication component!");

                throw new InvalidCredentialException("Browser authentication failed!");
            }

            IsCacheUsed = false;
            Logger.Info($"Browser authentication successful");

            // Сохраняем новый куки, если задан путь для кеша
            if (!string.IsNullOrEmpty(_settings.BrowserAuthenticatorCacheDir))
            {
                string fileName = string.IsNullOrWhiteSpace(Login) ? "anonymous" : Login;
                string path = Path.Combine(_settings.BrowserAuthenticatorCacheDir, fileName);
                try
                {
                    string content = JsonConvert.SerializeObject(response);

                    try
                    {
                        if (!Directory.Exists(_settings.BrowserAuthenticatorCacheDir))
                            Directory.CreateDirectory(_settings.BrowserAuthenticatorCacheDir);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(
                            $"Directory for cache can not be created, " +
                            $"remove attribute CacheDir in BrowserAuthenticator tag in configuration file! " +
                            $"{ex.Message}");

                        path = null;
                    }

                    if (path is not null)
                    {
#if NET48
                        System.IO.File.WriteAllText(path, content);
#else
                        await System.IO.File.WriteAllTextAsync(path, content).ConfigureAwait(false);
#endif
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error saving cookies to the file {path}: {ex.Message}");
                }
            }
        }

        if (response.Cookies is null)
        {
            AuthenticationUsingBrowser = false;
        }
        else
        {
            AuthenticationUsingBrowser = true;
            foreach (var item in response.Cookies)
            {
                var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain);
                Cookies.Add(cookie);
            }
        }

        CloudType = response.Cloud == "mail.ru" ? CloudType.Mail : CloudType.Yandex;
        if (CloudType == CloudType.Yandex &&
            !string.IsNullOrEmpty(response.Sk) &&
            !string.IsNullOrEmpty(response.Uuid))
        {
            Sk = response.Sk;
            Uuid = response.Uuid;
        }
        else
        {
            Sk = null;
            Uuid = null;
            AuthenticationUsingBrowser = false;
        }
    }

    /// <summary>
    /// <para>Если аутентификация была через браузер,
    /// стирает файл с кешем куки и запрашивает повторную аутентификацию через браузер.</para>
    /// <para>Возвращает true, если обновление прошло.</para>
    /// <para>Возвращает false, если обновление не прошло и надо отправить исключение.</para>
    /// </summary>
    /// <returns></returns>
    public bool Refresh()
    {
        if (!string.IsNullOrEmpty(_settings.BrowserAuthenticatorCacheDir))
        {
            string fileName = string.IsNullOrWhiteSpace(Login) ? "anonymous" : Login;
            string path = Path.Combine(_settings.BrowserAuthenticatorCacheDir, fileName);

            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deleting cookie file {path}: {ex.Message}");
            }
        }
        if (AuthenticationUsingBrowser)
        {
            Protocol saveProtocol = Protocol;
            CloudType saveCloudType = CloudType;
            // Если аутентификация не прошла, будет исключение
            GetBrowserCookiesAsync().Wait();
            if (saveCloudType != CloudType || saveProtocol != Protocol)
                return false;
            return true;
        }
        return false;
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

    private async Task<(BrowserAppResult, string)> ConnectToBrowserApp()
    {
        string url = _settings.BrowserAuthenticatorUrl;
        string password = string.IsNullOrWhiteSpace(Password)
            ? _settings.BrowserAuthenticatorPassword
            : Password;

        if (string.IsNullOrEmpty(url))
        {
            throw new AuthenticationException("Error connecting to browser authenticator application. " +
                "Check the BrowserAuthenticator is running and have correct port.");
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
        };

        client.Timeout = new TimeSpan(0, 5, 0);
        try
        {
            using var response = await client.SendAsync(httpRequestMessage);
            var responseText = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            BrowserAppResult data = JsonConvert.DeserializeObject<BrowserAppResult>(responseText);
            return (data, responseText);
        }
        catch (Exception)
        {
            throw;
        }
    }

    private async Task<BrowserAppResult> MakeLogin()
    {
        (BrowserAppResult response, string responseHtml) = await ConnectToBrowserApp();

        if (response != null &&
            !string.IsNullOrEmpty(response.Sk) &&
            !string.IsNullOrEmpty(response.Uuid) &&
            !string.IsNullOrEmpty(response.Login) &&
            GetNameOnly(response.Login)
                .Equals(GetNameOnly(Login), StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(response.ErrorMessage)
            )
        {
            CloudType = response.Cloud.Equals("mail.ru", StringComparison.InvariantCultureIgnoreCase)
                ? CloudType.Mail
                : CloudType.Yandex;
            Sk = response.Sk;
            Uuid = response.Uuid;

            foreach (var item in response.Cookies)
            {
                var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain);
                Cookies.Add(cookie);
            }

            // Если аутентификация прошла успешно, сохраняем результат в кеш в файл
            if (!string.IsNullOrEmpty(_settings.BrowserAuthenticatorCacheDir))
            {
                string path = Path.Combine(_settings.BrowserAuthenticatorCacheDir, Login);

                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
                catch (Exception)
                {
                    string text = "Failed to create cache storage directory. " +
                        "The attribute CacheDir of BrowserAuthenticator tag in configuration file must be removed!";
                    Logger.Error(text);
                    throw new AuthenticationException(text);
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
            string text = string.IsNullOrEmpty(response?.ErrorMessage)
                ? "Authentication using BrowserAuthenticator application is failed!"
                : string.Concat("Authentication using BrowserAuthenticator application is failed! ", response.ErrorMessage);

            Logger.Error(text);
            throw new AuthenticationException(text);
        }

        return response;
    }
}
