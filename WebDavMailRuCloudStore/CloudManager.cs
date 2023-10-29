using System;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Authentication;
using System.Security.Principal;
using System.Threading;
using YaR.Clouds.Base;

namespace YaR.Clouds.WebDavStore
{
    public static class CloudManager
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(CloudManager));

        private static readonly ConcurrentDictionary<string, Cloud> CloudCache = new(StringComparer.InvariantCultureIgnoreCase);

        public static CloudSettings Settings { get; set; }

        private static SemaphoreSlim _locker = new SemaphoreSlim(1);

        public static Cloud Instance(IIdentity identity)
        {
            var basicIdentity = (HttpListenerBasicIdentity) identity;
            string key = basicIdentity.Name + basicIdentity.Password;

            if (CloudCache.TryGetValue(key, out var cloud))
                return cloud;

            _locker.Wait();
            try
            {
                if (CloudCache.TryGetValue(key, out cloud))
                    return cloud;

                cloud = CreateCloud(basicIdentity);

                CloudCache.TryAdd(key, cloud);
            }
            finally
            {
                _locker.Release();
            }

            return cloud;
        }

        private static Cloud CreateCloud(HttpListenerBasicIdentity identity)
        {
            var credentials = new Credentials(identity.Name, identity.Password);

            if (credentials.Protocol == Protocol.Autodetect &&
                Settings.Protocol != Protocol.Autodetect)
            {
                // Если протокол не определился из строки логина,
                // то пользуемся подсказкой в виде параметра командной строки
                credentials.Protocol = Settings.Protocol;
            }

            if (credentials.Protocol == Protocol.Autodetect &&
                credentials.CloudType == CloudType.Yandex)
            {
                if (string.IsNullOrEmpty(Settings.BrowserAuthenticatorUrl) ||
                    string.IsNullOrEmpty(Settings.BrowserAuthenticatorPassword))
                {
                    credentials.Protocol = Protocol.YadWeb;
                }
                else
                if (!string.IsNullOrEmpty(Settings.BrowserAuthenticatorUrl) &&
                    !string.IsNullOrEmpty(Settings.BrowserAuthenticatorPassword) &&
                    identity.Password.Equals(Settings.BrowserAuthenticatorPassword, StringComparison.InvariantCultureIgnoreCase))
                {
                    credentials.Protocol = Protocol.YadWebV2;
                }
                else
                {
                    Logger.Info("Protocol auto detect is ON. Can not choose between YadWeb and YadWebV2");
                    throw new InvalidCredentialException(
                        "Protocol auto detect is ON. Can not choose between YadWeb and YadWebV2. " +
                        "Please specify protocol version in login string, see manuals.");
                }
            }

            if (credentials.CloudType == CloudType.Unkown)
            {
                Logger.Info("Cloud type is not detected by user login string");
                throw new InvalidCredentialException("Cloud type is not detected. " +
                    "Please specify protocol and email in login string, see manuals.");
            }

            if (credentials.Protocol == Protocol.Autodetect)
            {
                Logger.Info("Protocol is undefined by user credentials");
                throw new InvalidCredentialException("Protocol type is not detected. " +
                    "Please specify protocol and email in login string, see manuals.");
            }

            var cloud = new Cloud(Settings, credentials);
            Logger.Info($"Cloud instance created for {credentials.Login}");

            return cloud;
        }
    }
}
