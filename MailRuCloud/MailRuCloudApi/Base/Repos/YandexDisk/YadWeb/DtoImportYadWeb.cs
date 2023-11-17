using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb
{
    static class DtoImportYadWeb
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(DtoImportYadWeb));

        public static AccountInfoResult ToAccountInfo(this YadResponseModel<YadAccountInfoRequestData, YadAccountInfoRequestParams> data)
        {
            if (data?.Data == null || data.Error != null)
            {
                throw new AuthenticationException(string.Concat(
                    "The cloud server rejected the credentials provided: ", data?.Error?.Message));
            }

            var info = data.Data;
            var res = new AccountInfoResult
            {

                FileSizeLimit = info.FileSizeLimit,

                DiskUsage = new DiskUsage
                {
                    Total = info.Limit,
                    Used = info.Used,
                    OverQuota = info.Used > info.Limit
                }
            };

            return res;
        }

        public static string GetOpPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            int colon = path.IndexOf(':');
            return WebDavPath.Clean(path.Substring(1 + colon)).Remove(0, "/disk".Length);
        }

        public static Folder ToFolder(this YadFolderInfoRequestData folderData,
            YadItemInfoRequestData entryData, YadResourceStatsRequestData entryStats, string path,
            string publicBaseUrl, List<YadActiveOperationsData> activeOps)
        {
            if (folderData is null)
                throw new ArgumentNullException(nameof(folderData));

            List<string> paths = new List<string>();
            if (activeOps is not null)
            {
                foreach (var op in activeOps)
                {
                    string p = GetOpPath(op.Data.Target);
                    if (!string.IsNullOrEmpty(p))
                        paths.Add(p);
                    p = GetOpPath(op.Data.Source);
                    if (!string.IsNullOrEmpty(p))
                        paths.Add(p);
                }
            }

            if (path.StartsWith("/disk"))
                path = path.Remove(0, "/disk".Length);

            var folder = new Folder(entryStats?.Size ?? entryData?.Meta?.Size ?? 0, path) { IsChildrenLoaded = false };
            if (!string.IsNullOrEmpty(entryData?.Meta?.UrlShort))
            {
                PublicLinkInfo item = new PublicLinkInfo("short", entryData.Meta.UrlShort);
                folder.PublicLinks.TryAdd(item.Uri.AbsoluteUri, item);
            }
            if (paths.Any(x => WebDavPath.IsParentOrSame(x, folder.FullPath)))
            {
                // Признак операции на сервере, под которую подпадает папка
                folder.Attributes |= System.IO.FileAttributes.Offline;
            }

            string diskPath = WebDavPath.Combine("/disk", path);
            var fi = folderData.Resources;
            var children = new List<IEntry>();

            children.AddRange(
                fi.Where(it => it.Type == "file")
                  .Select(f => f.ToFile(publicBaseUrl))
                  .ToGroupedFiles());
            children.AddRange(
                fi.Where(it => it.Type == "dir" &&
                               // Пропуск элемента с информацией папки о родительской папке,
                               // этот элемент добавляется в выборки, если читается
                               // не всё содержимое папки, а делается только вырезка
                               it.Path != diskPath)
                  .Select(f => f.ToFolder()));

            folder.Descendants = folder.Descendants.AddRange(children);
            folder.Descendants.ForEach(entry =>
            {
                if (paths.Any(x => WebDavPath.IsParentOrSame(x, entry.FullPath)))
                {
                    // Признак операции на сервере, под которую подпадает папка
                    folder.Attributes |= System.IO.FileAttributes.Offline;
                }
            });

            folder.ServerFilesCount ??= folder.Descendants.Count(f => f.IsFile);
            folder.ServerFoldersCount ??= folder.Descendants.Count(f => !f.IsFile);

            return folder;
        }

        public static File ToFile(this FolderInfoDataResource data, string publicBaseUrl)
        {
            var path = data.Path.Remove(0, "/disk".Length);

            var res = new File(path, data.Meta.Size ?? throw new Exception("File size is null"))
            {
                CreationTimeUtc = UnixTimeStampToDateTime(data.Ctime, DateTime.MinValue),
                LastAccessTimeUtc = UnixTimeStampToDateTime(data.Utime, DateTime.MinValue),
                LastWriteTimeUtc = UnixTimeStampToDateTime(data.Mtime, DateTime.MinValue)
            };
            if (!string.IsNullOrEmpty(data.Meta.UrlShort))
            {
                PublicLinkInfo item = new PublicLinkInfo("short", data.Meta.UrlShort);
                res.PublicLinks.TryAdd(item.Uri.AbsoluteUri, item);
            }
            return res;
        }

        public static File ToFile(this YadItemInfoRequestData data, string publicBaseUrl)
        {
            var path = data.Path.Remove(0, "/disk".Length);

            var res = new File(path, data.Meta.Size)
            {
                CreationTimeUtc = UnixTimeStampToDateTime(data.Ctime, DateTime.MinValue),
                LastAccessTimeUtc = UnixTimeStampToDateTime(data.Utime, DateTime.MinValue),
                LastWriteTimeUtc = UnixTimeStampToDateTime(data.Mtime, DateTime.MinValue)
                //PublicLink = data.Meta.UrlShort.StartsWith(publicBaseUrl)
                //    ? data.Meta.UrlShort.Remove(0, publicBaseUrl.Length)
                //    : data.Meta.UrlShort
            };
            if (!string.IsNullOrEmpty(data.Meta.UrlShort))
            {
                PublicLinkInfo item = new PublicLinkInfo("short", data.Meta.UrlShort);
                res.PublicLinks.TryAdd(item.Uri.AbsoluteUri, item);
            }

            return res;
        }

        public static Folder ToFolder(this FolderInfoDataResource resource)
        {
            var path = resource.Path.Remove(0, "/disk".Length);

            var res = new Folder(path) { IsChildrenLoaded = false };

            return res;
        }

        public static RenameResult ToRenameResult(this YadResponseModel<YadMoveRequestData, YadMoveRequestParams> data)
        {
            var res = new RenameResult
            {
                IsSuccess = data.Data.Error is null,
                DateTime = DateTime.Now,
                Path = data.Params.Src.Remove(0, "/disk".Length)
            };
            return res;
        }

        //public static ItemOperation ToItemOperation(this RenameResult data)
        //{
        //    var res = new ItemOperation
        //    {
        //        DateTime = data.DateTime,
        //        Path = data.Path
        //    };
        //    return res;
        //}

        public static RemoveResult ToRemoveResult(this YadResponseModel<YadDeleteRequestData, YadDeleteRequestParams> data)
        {
            var res = new RemoveResult
            {
                IsSuccess = true,
                DateTime = DateTime.Now,
                Path = data.Params.Id.Remove(0, "/disk".Length)
            };
            return res;
        }

        public static ItemOperation ToItemOperation(this RemoveResult data)
        {
            var res = new ItemOperation
            {
                DateTime = data.DateTime,
                Path = data.Path
            };
            return res;
        }

        public static CreateFolderResult ToCreateFolderResult(this YadCreateFolderRequestParams data)
        {
            var res = new CreateFolderResult
            {
                IsSuccess = true,
                Path = data.Id.Remove(0, "/disk".Length)
            };
            return res;
        }

        public static CopyResult ToCopyResult(this YadResponseModel<YadCopyRequestData, YadCopyRequestParams> data)
        {
            var res = new CopyResult
            {
                IsSuccess = true,
                NewName = data.Params.Dst.Remove(0, "/disk".Length),
                OldFullPath = data.Params.Src.Remove(0, "/disk".Length),
                DateTime = DateTime.Now
            };
            return res;
        }

        public static CopyResult ToMoveResult(this YadResponseModel<YadMoveRequestData, YadMoveRequestParams> data)
        {
            var res = new CopyResult
            {
                IsSuccess = data.Data.Error is null,
                NewName = data.Params.Dst.Remove(0, "/disk".Length),
                OldFullPath = data.Params.Src.Remove(0, "/disk".Length),
                DateTime = DateTime.Now
            };
            return res;
        }

        //public static ItemOperation ToItemOperation(this CopyResult data)
        //{
        //    var res = new ItemOperation
        //    {
        //        DateTime = data.DateTime,
        //        Path = data.OldFullPath
        //    };
        //    return res;
        //}

        public static PublishResult ToPublishResult(this YadResponseModel<YadPublishRequestData, YadPublishRequestParams> data)
        {
            var res = new PublishResult
            {
                IsSuccess = !string.IsNullOrEmpty(data.Data.ShortUrl),
                Url = data.Data.ShortUrl
            };
            return res;
        }

        public static UnpublishResult ToUnpublishResult(this YadResponseModel<YadPublishRequestData, YadPublishRequestParams> data)
        {
            var res = new UnpublishResult
            {
                IsSuccess = true
            };
            return res;
        }



        private static DateTime UnixTimeStampToDateTime(double unixTimeStamp, DateTime defaultvalue)
        {
            try
            {
                // Unix timestamp is seconds past epoch
                var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                dtDateTime = dtDateTime.AddSeconds(unixTimeStamp); //.ToLocalTime(); - doesn't need, clients usually convert to local time by itself
                return dtDateTime;
            }
            catch (Exception e)
            {
                Logger.Error($"Error converting unixTimeStamp {unixTimeStamp} to DateTime, {e.Message}");
                return defaultvalue;
            }
        }
    }
}
