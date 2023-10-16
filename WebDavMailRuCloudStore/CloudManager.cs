using System;
using System.Collections.Concurrent;
using System.Net;
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
            Logger.Info($"Cloud instance created for {identity.Name}");

            var credentials = new Credentials(identity.Name, identity.Password);

            var cloud = new Cloud(Settings, credentials);
            return cloud;
        }
    }
}
