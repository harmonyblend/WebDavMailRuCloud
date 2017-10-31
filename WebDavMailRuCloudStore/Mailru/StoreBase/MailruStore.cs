﻿using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NWebDav.Server;
using NWebDav.Server.Http;
using NWebDav.Server.Locking;
using NWebDav.Server.Stores;
using YaR.MailRuCloud.Api.Base;

namespace YaR.WebDavMailRu.CloudStore.Mailru.StoreBase
{
    public sealed class MailruStore : IStore
    {
        public MailruStore(bool isWritable = true, ILockingManager lockingManager = null)
        {
            LockingManager = lockingManager ?? new InMemoryLockingManager();
            IsWritable = isWritable;
        }

        private bool IsWritable { get; }
        private ILockingManager LockingManager { get; }

        public Task<IStoreItem> GetItemAsync(WebDavUri uri, IHttpContext httpContext)
        {
            //TODO: Refact

            var identity = (HttpListenerBasicIdentity)httpContext.Session.Principal.Identity;
            var path = uri.Path;
            
            //TODO: clean this trash
            try
            {
                var item = Cloud.Instance(identity).GetItems(path).Result;
                if (item != null)
                {
                    if (item.FullPath == path)
                    {
                        var dir = new Folder(item.NumberOfFolders, item.NumberOfFiles, item.Size, path);
                        return Task.FromResult<IStoreItem>(new MailruStoreCollection(httpContext, LockingManager, dir, IsWritable));
                    }
                    var fa = item.Files.FirstOrDefault(k => k.FullPath == path);
                    if (fa != null)
                        return Task.FromResult<IStoreItem>(new MailruStoreItem(LockingManager, fa, IsWritable));

                    string parentPath = WebDavPath.Parent(path);
                    item = Cloud.Instance(identity).GetItems(parentPath).Result;
                    if (item != null)
                    {
                        var f = item.Files.FirstOrDefault(k => k.FullPath == path);
                        return null != f
                            ? Task.FromResult<IStoreItem>(new MailruStoreItem(LockingManager, f, IsWritable))
                            : null;
                    }

                }
            }
            catch (AggregateException e)
            {
                var we = e.InnerExceptions.OfType<WebException>().FirstOrDefault();
                if (we == null || we.Status != WebExceptionStatus.ProtocolError) throw;
            }
            // ReSharper disable once RedundantCatchClause
            #if DEBUG
            #pragma warning disable 168
            catch (Exception ex)
            {
                throw;
            }
            #pragma warning restore 168
            #endif

            return Task.FromResult<IStoreItem>(null);
        }

        public Task<IStoreCollection> GetCollectionAsync(WebDavUri uri, IHttpContext httpContext)
        {
            var path = uri.Path;
            return Task.FromResult<IStoreCollection>(new MailruStoreCollection(httpContext, LockingManager, new Folder(path), IsWritable));
        }
    }
}
