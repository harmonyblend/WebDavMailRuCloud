﻿using System;

using System.Security;
using System.Threading.Tasks;
using NWebDav.Server.Http;
using NWebDav.Server.Locking;
using WebDavMailRuCloudStore;


namespace NWebDav.Server.Stores
{
    public sealed class MailruStore : IStore
    {
        public MailruStore(bool isWritable = true, ILockingManager lockingManager = null)
        {
            LockingManager = lockingManager ?? new InMemoryLockingManager();
        }

        public bool IsWritable { get; }
        public ILockingManager LockingManager { get; }

        public Task<IStoreItem> GetItemAsync(Uri uri, IHttpContext httpContext)
        {
            // Determine the path from the uri
            var path = GetPathFromUri(uri);

            // Check if it's a directory
            //if (Directory.Exists(path))
            //    return Task.FromResult<IStoreItem>(new MailruStoreCollection(LockingManager, new DirectoryInfo(path), IsWritable));

            // Check if it's a file
            //if (File.Exists(path))
                return Task.FromResult<IStoreItem>(new MailruStoreItem(LockingManager, new MailRuCloudApi.File {FullPath = path}, IsWritable));

            // The item doesn't exist
            return Task.FromResult<IStoreItem>(null);
        }

        public Task<IStoreCollection> GetCollectionAsync(Uri uri, IHttpContext httpContext)
        {
            // Determine the path from the uri
            var path = GetPathFromUri(uri);
            //if (!Directory.Exists(path))
            //    return Task.FromResult<IStoreCollection>(null);

            // Return the item
            return Task.FromResult<IStoreCollection>(new MailruStoreCollection(LockingManager, new DirectoryInfo(path), IsWritable));
        }

        private string GetPathFromUri(Uri uri)
        {
            // Determine the path
            var requestedPath = uri.LocalPath.Substring(1).Replace('/', Path.DirectorySeparatorChar);

            // Determine the full path
            //var fullPath = Path.GetFullPath(Path.Combine(BaseDirectory, requestedPath));

            // Make sure we're still inside the specified directory
            //if (fullPath != BaseDirectory && !fullPath.StartsWith(BaseDirectory + Path.DirectorySeparatorChar))
            //    throw new SecurityException($"Uri '{uri}' is outside the '{BaseDirectory}' directory.");

            // Return the combined path
            //return fullPath;

            return requestedPath;
        }
    }
}
