using System;
using System.Net;
using System.Threading.Tasks;
using NWebDav.Server;
using NWebDav.Server.Http;
using NWebDav.Server.Locking;
using NWebDav.Server.Props;
using NWebDav.Server.Stores;
using YaR.Clouds.Base;

namespace YaR.Clouds.WebDavStore.StoreBase
{
    public sealed class LocalStore : IStore
    {
        public LocalStore(bool isWritable = true, ILockingManager lockingManager = null, Func<string, bool> isEnabledPropFunc = null)
        {
            LockingManager = lockingManager ?? new EmptyLockingManager(); //new InMemoryLockingManager();
            IsWritable = isWritable;

            CollectionPropertyManager = new  PropertyManager<LocalStoreCollection>(new LocalStoreCollectionProps<LocalStoreCollection>(isEnabledPropFunc).Props);
            ItemPropertyManager = new  PropertyManager<LocalStoreItem>(new LocalStoreItemProps<LocalStoreItem>(isEnabledPropFunc).Props);
        }

        public readonly PropertyManager<LocalStoreCollection> CollectionPropertyManager;
        public readonly PropertyManager<LocalStoreItem> ItemPropertyManager;

        private bool IsWritable { get; }
        public ILockingManager LockingManager { get; }

        public async Task<IStoreItem> GetItemAsync(WebDavUri uri, IHttpContext httpContext)
        {
            var path = uri.Path;

            try
            {
                var entry = await CloudManager
                    .Instance(httpContext.Session.Principal.Identity)
                    .GetItemAsync(path);
                if (entry is not null)
                {
                    return entry.IsFile
                        ? new LocalStoreItem((File)entry, IsWritable, this)
                        : new LocalStoreCollection(httpContext, (Folder)entry, IsWritable, this);
                }
            }
            // ReSharper disable once RedundantCatchClause
            #pragma warning disable 168
            catch (Exception ex)
            #pragma warning restore 168
            {
                throw;
            }

            return null;
        }

        public async Task<IStoreCollection> GetCollectionAsync(WebDavUri uri, IHttpContext httpContext)
        {
            var path = uri.Path;

            var entry = await CloudManager
                .Instance(httpContext.Session.Principal.Identity)
                .GetItemAsync(path, Cloud.ItemType.Folder);
            if (entry != null)
            {
                if (entry.IsFile)
                    throw new Exception("File from cloud is processed as a folder");
                return new LocalStoreCollection(httpContext, (Folder)entry, IsWritable, this);
            }
            return null;
        }
    }
}
