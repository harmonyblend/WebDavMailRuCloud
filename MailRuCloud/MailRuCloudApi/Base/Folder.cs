using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using YaR.Clouds.Common;

namespace YaR.Clouds.Base
{
    /// <summary>
    /// Server file info.
    /// </summary>
    [DebuggerDisplay("{" + nameof(FullPath) + "}")]
    public class Folder : IEntry, ICanForget
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Folder" /> class.
        /// </summary>
        public Folder(string fullPath)
        {
            FullPath = WebDavPath.Clean(fullPath);
            Name = WebDavPath.Name(FullPath);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Folder" /> class.
        /// </summary>
        /// <param name="size">Folder size.</param>
        /// <param name="fullPath">Full folder path.</param>
        /// <param name="publicLinks">Public folder link.</param>
        public Folder(FileSize size, string fullPath, IEnumerable<PublicLinkInfo> publicLinks = null)
            : this(fullPath)
        {
            Size = size;
            if (publicLinks != null)
            {
                foreach (var link in publicLinks)
                    PublicLinks.TryAdd(link.Uri.AbsolutePath, link);
            }
        }

        public IEnumerable<IEntry> Entries
        {
            get
            {
                foreach (var file in Files.Values)
                    yield return file;
                foreach (var folder in Folders.Values)
                    yield return folder;
            }
        }

        public ConcurrentDictionary<string /* FullPath of File */, File> Files { get; set; }
            = new(StringComparer.InvariantCultureIgnoreCase);

        public ConcurrentDictionary<string /* FullPath of Folder */, Folder> Folders { get; set; }
            = new(StringComparer.InvariantCultureIgnoreCase);


        /// <summary>
        /// Gets folder name.
        /// </summary>
        /// <value>Folder name.</value>
        public string Name { get; }

        /// <summary>
        /// Gets folder size.
        /// </summary>
        /// <value>Folder size.</value>
        public FileSize Size { get; }

        /// <summary>
        /// Gets full folder path on the server.
        /// </summary>
        /// <value>Full folder path.</value>
        public string FullPath { get; }


        /// <summary>
        /// Gets public file link.
        /// </summary>
        /// <value>Public link.</value>
        public ConcurrentDictionary<string, PublicLinkInfo> PublicLinks
            => _publicLinks ??= new ConcurrentDictionary<string, PublicLinkInfo>(StringComparer.InvariantCultureIgnoreCase);

        private ConcurrentDictionary<string, PublicLinkInfo> _publicLinks;

        public IEnumerable<PublicLinkInfo> GetPublicLinks(Cloud cloud)
        {
            return PublicLinks.IsEmpty
                ? cloud.GetSharedLinks(FullPath) 
                : PublicLinks.Values;
        }


        public DateTime CreationTimeUtc { get; set; } = DateTime.Now.AddDays(-1);

        public DateTime LastWriteTimeUtc { get; set; } = DateTime.Now.AddDays(-1);


        public DateTime LastAccessTimeUtc { get; set; } = DateTime.Now.AddDays(-1);


        public FileAttributes Attributes { get; set; } = FileAttributes.Directory;

        public bool IsFile => false;

        public bool IsChildrenLoaded { get; internal set; }


        public int? ServerFoldersCount { get; set; }
        public int? ServerFilesCount { get; set; }

        public PublishInfo ToPublishInfo()
        {
            var info = new PublishInfo();
            if (!PublicLinks.IsEmpty)
                info.Items.Add(new PublishInfoItem { Path = FullPath, Urls = PublicLinks.Select(pli => pli.Value.Uri).ToList() });
            return info;
        }


        //public List<KeyValuePair<string, IEntry>> GetLinearChildren()
        //{
            
        //}
        public void Forget(object whomKey)
        {
            string key = whomKey?.ToString();

            if (string.IsNullOrEmpty(key))
                return;

            // Удалять начинаем с директорий, т.к. их обычно меньше,
            // а значит поиск должен завершиться в среднем быстрее.

            if (!Folders.TryRemove(key, out _))
            {
                // Если по ключу в виде полного пути не удалось удалить директорию,
                // пытаемся по этому же ключу удалить файл, если он есть.
                // Если ничего не удалилось, значит и удалять нечего.
                _ = Files.TryRemove( key, out _);
            }
        }
    }
}
