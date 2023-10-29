using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NWebDav.Server;
using NWebDav.Server.Props;
using YaR.Clouds.WebDavStore.CustomProperties;
using YaR.Clouds.Base;

namespace YaR.Clouds.WebDavStore.StoreBase
{
    class LocalStoreCollectionProps<T> where T : LocalStoreCollection
    {
        private static readonly XElement SxDavCollection = new(WebDavNamespaces.DavNsCollection);

        public LocalStoreCollectionProps(Func<string, bool> isEnabledPropFunc)
        {
            var props = new DavProperty<T>[]
            {
                //new DavLoctoken<LocalStoreCollection>
                //{
                //    Getter = (context, collection) => ""
                //},

                // collection property required for WebDrive
                new DavCollection<T>
                {
                    Getter = (_, _) => string.Empty
                },

                new DavGetEtag<T>
                {
                    Getter = (_, item) => item.CalculateETag()
                },

                //new DavBsiisreadonly<LocalStoreCollection>
                //{
                //    Getter = (context, item) => false
                //},

                //new DavSrtfileattributes<LocalStoreCollection>
                //{
                //    Getter = (context, collection) =>  collection.DirectoryInfo.Attributes,
                //    Setter = (context, collection, value) =>
                //    {
                //        collection.DirectoryInfo.Attributes = value;
                //        return DavStatusCode.Ok;
                //    }
                //},
                ////====================================================================================================
            

                new DavIsreadonly<T>
                {
                    Getter = (_, item) => !item.IsWritable
                },

                new DavQuotaAvailableBytes<T>
                {
                    Getter = (cntext, collection) => collection.FullPath == "/" ? CloudManager.Instance(cntext.Session.Principal.Identity).GetDiskUsage().Free.DefaultValue : long.MaxValue,
                    IsExpensive = true  //folder listing performance
                },

                new DavQuotaUsedBytes<T>
                {
                    Getter = (_, collection) =>
                        collection.FolderWithDescendants.Size
                    //IsExpensive = true  //folder listing performance
                },

                // RFC-2518 properties
                new DavCreationDate<T>
                {
                    Getter = (_, collection) => collection.FolderWithDescendants.CreationTimeUtc,
                    Setter = (_, collection, value) =>
                    {
                        (collection.FolderWithDescendants as Folder).CreationTimeUtc = value;
                        return DavStatusCode.Ok;
                    }
                },
                new DavDisplayName<T>
                {
                    Getter = (_, collection) => collection.FolderWithDescendants.Name
                },
                new DavGetLastModified<T>
                {
                    Getter = (_, collection) => (collection.FolderWithDescendants as Folder).LastWriteTimeUtc,
                    Setter = (_, collection, value) =>
                    {
                        (collection.FolderWithDescendants as Folder).LastWriteTimeUtc = value;
                        return DavStatusCode.Ok;
                    }
                },

                new DavLastAccessed<T>
                {
                    Getter = (_, collection) => (collection.FolderWithDescendants as Folder).LastWriteTimeUtc,
                    Setter = (_, collection, value) =>
                    {
                        (collection.FolderWithDescendants as Folder).LastWriteTimeUtc = value;
                        return DavStatusCode.Ok;
                    }
                },


                //new DavGetResourceType<LocalStoreCollection>
                //{
                //    Getter = (context, collection) => new XElement(WebDavNamespaces.DavNsCollection)
                //},
                new DavGetResourceType<T>
                {
                    Getter = (_, _) => new []{SxDavCollection}
                },


                // Default locking property handling via the LockingManager
                new DavLockDiscoveryDefault<T>(),
                new DavSupportedLockDefault<T>(),

                //Hopmann/Lippert collection properties
                new DavExtCollectionChildCount<T>
                {
                    Getter = (_, collection) =>
                    {
                        var folder = collection.FolderWithDescendants as Folder;
                        return Math.Max(collection.FolderWithDescendants.Descendants.Count,
                            (folder.ServerFoldersCount ?? 0) + (folder.ServerFilesCount ?? 0));
                    }
                },
                new DavExtCollectionIsFolder<T>
                {
                    Getter = (_, _) => true
                },
                new DavExtCollectionIsHidden<T>
                {
                    Getter = (_, _) => false
                },
                new DavExtCollectionIsStructuredDocument<T>
                {
                    Getter = (_, _) => false
                },

                new DavExtCollectionHasSubs<T> //Identifies whether this collection contains any collections which are folders (see "isfolder").
                {
                    Getter = (_, collection)
                        => ((collection.FolderWithDescendants as Folder)?.ServerFoldersCount ?? 0)> 0
                        || collection.FolderWithDescendants.Descendants.Any(x=>x is Folder)
                },

                new DavExtCollectionNoSubs<T> //Identifies whether this collection allows child collections to be created.
                {
                    Getter = (_, _) => false
                },

                new DavExtCollectionObjectCount<T> //To count the number of non-folder resources in the collection.
                {
                    Getter = (_, collection) => collection.FolderWithDescendants is Folder folder
                        ? Math.Max(folder.ServerFilesCount ?? 0, collection.FolderWithDescendants.Descendants.Count(x=>x.IsFile))
                        : 0
                },

                new DavExtCollectionReserved<T>
                {
                    Getter = (_, collection) => !collection.IsWritable
                },

                new DavExtCollectionVisibleCount<T>  //Counts the number of visible non-folder resources in the collection.
                {
                    Getter = (_, collection) => collection.FolderWithDescendants is Folder folder
                        ? Math.Max(folder.ServerFilesCount ?? 0, collection.FolderWithDescendants.Descendants.Count(x=>x.IsFile))
                        : 0
                },

                // Win32 extensions
                new Win32CreationTime<T>
                {
                    Getter = (_, collection) => (collection.FolderWithDescendants as Folder).CreationTimeUtc,
                    Setter = (_, collection, value) =>
                    {
                        (collection.FolderWithDescendants as Folder).CreationTimeUtc = value;
                        return DavStatusCode.Ok;
                    }
                },
                new Win32LastAccessTime<T>
                {
                    Getter = (_, collection) => (collection.FolderWithDescendants as Folder).LastAccessTimeUtc,
                    Setter = (_, collection, value) =>
                    {
                        (collection.FolderWithDescendants as Folder).LastAccessTimeUtc = value;
                        return DavStatusCode.Ok;
                    }
                },
                new Win32LastModifiedTime<T>
                {
                    Getter = (_, collection) => (collection.FolderWithDescendants as Folder).LastWriteTimeUtc,
                    Setter = (_, collection, value) =>
                    {
                        (collection.FolderWithDescendants as Folder).LastWriteTimeUtc = value;
                        return DavStatusCode.Ok;
                    }
                },
                new Win32FileAttributes<T>
                {
                    Getter = (_, collection) => (collection.FolderWithDescendants as Folder).Attributes,
                    Setter = (_, collection, value) =>
                    {
                        (collection.FolderWithDescendants as Folder).Attributes = value;
                        return DavStatusCode.Ok;
                    }
                },
                new DavGetContentLength<T>
                {
                    Getter = (_, collection) => (collection.FolderWithDescendants as Folder).Size
                },
                new DavGetContentType<T>
                {
                    Getter = (_, _) => "httpd/unix-directory" //"application/octet-stream"
                },
                new DavSharedLink<T>
                {
                    Getter = (_, collection) => (collection.FolderWithDescendants as Folder)
                        .PublicLinks.Values.FirstOrDefault()?.Uri.OriginalString ?? string.Empty,
                    Setter = (_, _, _) => DavStatusCode.Ok
                }
            };

            _props = props.Where(p => isEnabledPropFunc?.Invoke(p.Name.ToString()) ?? true).ToArray();
        }

        public IEnumerable<DavProperty<T>> Props => _props;
        private readonly DavProperty<T>[] _props;
    }
}
