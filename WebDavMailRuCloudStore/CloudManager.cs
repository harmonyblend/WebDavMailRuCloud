using System;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using YaR.Clouds.Base;

namespace YaR.Clouds.WebDavStore;

public static class CloudManager
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private class ClockedCloud
    {
        public DateTime LastAccess;
        public Cloud Cloud;
    }

    private static readonly ConcurrentDictionary<string, ClockedCloud> CloudCache
        = new(StringComparer.InvariantCultureIgnoreCase);

    public static CloudSettings Settings { get; set; }

    private static readonly SemaphoreSlim _dictionaryLocker = new SemaphoreSlim(1);
    private static readonly SemaphoreSlim _creationLocker = new SemaphoreSlim(1);
    private static readonly System.Timers.Timer _cleanTimer;

    static CloudManager()
    {
        _cleanTimer = new System.Timers.Timer()
        {
            Interval = 60 * 1000, // 1 minute
            Enabled = false,
            AutoReset = true
        };
        _cleanTimer.Elapsed += RemoveExpired;
    }

    public static Cloud Instance(IIdentity identity)
    {
        var basicIdentity = (HttpListenerBasicIdentity)identity;
        string key = basicIdentity.Name + basicIdentity.Password;

        _dictionaryLocker.Wait();
        try
        {
            if (CloudCache.TryGetValue(key, out var cloudItem))
            {
                cloudItem.LastAccess = DateTime.Now;
                return cloudItem.Cloud;
            }
        }
        finally
        {
            _dictionaryLocker.Release();
        }

        _creationLocker.Wait();
        try
        {
            // Когда дождались своей очереди на создание экземпляра,
            // обязательно надо проверить, что кто-то уже не создал экземпляр,
            // пока стояли в очереди на создание.
            if (CloudCache.TryGetValue(key, out var cloudItem))
            {
                _dictionaryLocker.Wait();
                try
                {
                    cloudItem.LastAccess = DateTime.Now;
                    return cloudItem.Cloud;
                }
                finally
                {
                    _dictionaryLocker.Release();
                }
            }
            Cloud cloudInstance = CreateCloud(basicIdentity);
            _dictionaryLocker.Wait();
            try
            {
                CloudCache.TryAdd(key, new ClockedCloud { LastAccess = DateTime.Now, Cloud = cloudInstance });

                if (!_cleanTimer.Enabled)
                    _cleanTimer.Enabled = true;

                return cloudInstance;
            }
            finally
            {
                _dictionaryLocker.Release();
            }
        }
        finally
        {
            _creationLocker.Release();
        }
    }

    private static Cloud CreateCloud(HttpListenerBasicIdentity identity)
    {
        var credentials = new Credentials(Settings, identity.Name, identity.Password);

        var cloud = new Cloud(Settings, credentials);
        Logger.Warn($"{(credentials.CloudType == CloudType.Mail ? "Mail.Ru" : "Yandex.Ru")} " +
            $"cloud instance created for {credentials.Login}, " +
            $"protocol is {credentials.Protocol}" +
            $"{(credentials.AuthenticationUsingBrowser ? ", using browser cookies" : "")}");

        return cloud;
    }

    private static void RemoveExpired(object sender, System.Timers.ElapsedEventArgs e)
    {
        _dictionaryLocker.Wait();
        try
        {
            DateTime threshold = DateTime.Now - TimeSpan.FromMinutes(Settings.CloudInstanceTimeoutMinutes);
            foreach (var pair in CloudCache)
            {
                if (pair.Value.LastAccess < threshold)
                {
                    CloudCache.TryRemove(pair.Key, out var removedItem);

                    Credentials credentials = removedItem.Cloud.Credentials;
                    Logger.Warn($"{(credentials.CloudType == CloudType.Mail ? "Mail.Ru" : "Yandex.Ru")} " +
                        $"cloud instance for {credentials.Login} is disposed");

                    try
                    {
                        removedItem.Cloud?.Dispose();
                    }
                    catch { }
                }
            }

            if (CloudCache.IsEmpty)
                _cleanTimer.Enabled = false;
        }
        finally
        {
            _dictionaryLocker.Release();
        }
    }
}
