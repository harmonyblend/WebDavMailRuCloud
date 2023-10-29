using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Repos.YandexDisk.YadWebV2.Models;
using YaR.Clouds.Base.Repos.YandexDisk.YadWebV2.Models.Media;
using YaR.Clouds.Base.Repos.YandexDisk.YadWebV2.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;
using Stream = System.IO.Stream;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWebV2
{
    class YadWebRequestRepo : IRequestRepo
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadWebRequestRepo));

        private readonly SemaphoreSlim _connectionLimiter;

        //private ItemOperation _lastRemoveOperation;
        
        private const int OperationStatusCheckIntervalMs = 300;
        private const int OperationStatusCheckRetryCount = 8;
        private readonly TimeSpan OperationStatusCheckRetryTimeout = TimeSpan.FromMinutes(5);

        private readonly IBasicCredentials _creds;

        private struct ParallelInfo
        {
            public int Offset;
            public int Amount;
            public YadFolderInfoRequestData Result;
        }

        public YadWebRequestRepo(CloudSettings settings, IWebProxy proxy, IBasicCredentials credentials)
        {
            _connectionLimiter = new SemaphoreSlim(settings.MaxConnectionCount);

            HttpSettings = new()
            {
                UserAgent = settings.UserAgent,
                CloudSettings = settings,
                Proxy = proxy,
            };

            _creds = credentials;

            ServicePointManager.DefaultConnectionLimit = int.MaxValue;

            // required for Windows 7 breaking connection
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;
        }

        private async Task<Dictionary<string, IEnumerable<PublicLinkInfo>>> GetShareListInner()
        {
            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadFolderInfoPostModel("/", "/published"),
                    out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = folderInfo.Data.Resources
                .Where(it => !string.IsNullOrEmpty(it.Meta?.UrlShort))
                .ToDictionary(
                    it => it.Path.Remove(0, "/disk".Length), 
                    it => Enumerable.Repeat(new PublicLinkInfo("short", it.Meta.UrlShort), 1));

            return res;
        }

        public IAuth Authenticator => CachedAuth.Value;

        private Cached<YadWebAuth> CachedAuth => _cachedAuth ??=
                new Cached<YadWebAuth>(_ => new YadWebAuth(_connectionLimiter, HttpSettings, _creds), _ => TimeSpan.FromHours(23));
        private Cached<YadWebAuth> _cachedAuth;

        public Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> CachedSharedList
            => _cachedSharedList ??= new Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>>(
                _ =>
                    {
                        var res = GetShareListInner().Result;
                        return res;
                    }, 
                    _ => TimeSpan.FromSeconds(30));
        private Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> _cachedSharedList;


        public HttpCommonSettings HttpSettings { get; private set; }

        public Stream GetDownloadStream(File aFile, long? start = null, long? end = null)
        {
            CustomDisposable<HttpWebResponse> ResponseGenerator(long instart, long inend, File file)
            {
                //var urldata = new YadGetResourceUrlRequest(HttpSettings, (YadWebAuth)Authenticator, file.FullPath)
                //    .MakeRequestAsync(_connectionLimiter)
                //    .Result;
                string url = null;
                if (file.DownloadUrlCache == null ||
                    file.DownloadUrlCacheExpirationTime <= DateTime.Now)
                {
                    var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth)Authenticator)
                        .With(new YadGetResourceUrlPostModel(file.FullPath),
                            out YadResponseModel<ResourceUrlData, ResourceUrlParams> itemInfo)
                        .MakeRequestAsync(_connectionLimiter).Result;

                    if (itemInfo == null ||
                        itemInfo.Error != null ||
                        itemInfo.Data == null ||
                        itemInfo.Data.Error != null ||
                        itemInfo?.Data?.File == null)
                    {
                        throw new FileNotFoundException(string.Concat(
                            "File reading error ", itemInfo?.Error?.Message,
                            " ",
                            itemInfo?.Data?.Error?.Message,
                            " ",
                            itemInfo?.Data?.Error?.Body?.Title));
                    }
                    url = "https:" + itemInfo.Data.File;

                    file.DownloadUrlCache = url;
                    file.DownloadUrlCacheExpirationTime = DateTime.Now.AddMinutes(1);
                }
                else
                {
                    url = file.DownloadUrlCache;
                }
                HttpWebRequest request = new YadDownloadRequest(HttpSettings, (YadWebAuth)Authenticator, url, instart, inend);
                var response = (HttpWebResponse)request.GetResponse();

                return new CustomDisposable<HttpWebResponse>
                {
                    Value = response,
                    OnDispose = () => {}
                };
            }

            if (start.HasValue || end.HasValue)
                Logger.Debug($"Download:  {aFile.FullPath} [{start}-{end}]");
            else
                Logger.Debug($"Download:  {aFile.FullPath}");

            var stream = new DownloadStream(ResponseGenerator, aFile, start, end);
            return stream;
        }

        //public HttpWebRequest UploadRequest(File file, UploadMultipartBoundary boundary)
        //{
        //    var urldata = 
        //        new YadGetResourceUploadUrlRequest(HttpSettings, (YadWebAuth)Authenticator, file.FullPath, file.OriginalSize)
        //        .MakeRequestAsync(_connectionLimiter)
        //        .Result;
        //    var url = urldata.Models[0].Data.UploadUrl;

        //    var result = new YadUploadRequest(HttpSettings, (YadWebAuth)Authenticator, url, file.OriginalSize);
        //    return result;
        //}

        public ICloudHasher GetHasher()
        {
            return new YadHasher();
        }

        public bool SupportsAddSmallFileByHash => false;
        public bool SupportsDeduplicate => true;

        private (HttpRequestMessage, string oid) CreateUploadClientRequest(PushStreamContent content, File file)
        {
            var hash = (FileHashYad?) file.Hash;
            var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadGetResourceUploadUrlPostModel(file.FullPath, file.OriginalSize, 
                        hash?.HashSha256.Value ?? string.Empty, 
                        hash?.HashMd5.Value ?? string.Empty),
                    out YadResponseModel<ResourceUploadUrlData, ResourceUploadUrlParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter).Result;

            var url = itemInfo.Data.UploadUrl;

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Put
            };

            request.Headers.Add("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("User-Agent", HttpSettings.UserAgent);

            request.Content = content;
            request.Content.Headers.ContentLength = file.OriginalSize;


            return (request,itemInfo?.Data?.Oid);
        }

        public async Task<UploadFileResult> DoUpload(HttpClient client, PushStreamContent content, File file)
        {
            (var request, string oid) = CreateUploadClientRequest(content, file);
            var responseMessage = await client.SendAsync(request);
            var ures = responseMessage.ToUploadPathResult();

            ures.NeedToAddFile = false;
            //await Task.Delay(1_000);

            if (!string.IsNullOrEmpty(oid))
                WaitForOperation(oid);

            return ures;
        }

        private const string YadMediaPath = "/Media.wdyad";

        /// <summary>
        /// Сколько записей папки читать в первом обращении, до параллельного чтения.
        /// Яндекс читает по 40 записей, путь тоже будет 40.
        /// </summary>
        private const int FirstReadEntriesCount = 40;

        public async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
        {
            if (path.IsLink)
                throw new NotImplementedException(nameof(FolderInfo));

            if (path.Path.StartsWith(YadMediaPath))
                return await MediaFolderInfo(path.Path);

            // YaD perform async deletion
            YadResponseModel<YadItemInfoRequestData, YadItemInfoRequestParams> entryInfo = null;
            YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo = null;
            YadResponseModel<YadResourceStatsRequestData, YadResourceStatsRequestParams> entryStats = null;
            YadResponseModel<List<YadActiveOperationsData>, YadActiveOperationsParams> operInfo = null;

            //bool hasRemoveOp = _lastRemoveOperation != null &&
            //                   WebDavPath.IsParentOrSame(path.Path, _lastRemoveOperation.Path) &&
            //                   (DateTime.Now - _lastRemoveOperation.DateTime).TotalMilliseconds < 1_000;

            int maxParallel = Math.Max(_connectionLimiter.CurrentCount - 1, 1);
            // Если доступных подключений к серверу 2 или менее, то не делаем параллельного чтения
            int firstReadLimit = maxParallel <= 2 ? int.MaxValue : FirstReadEntriesCount;

            Retry.Do(
                () =>
                {
                    //var doPreSleep = hasRemoveOp ? TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs) : TimeSpan.Zero;
                    //if (doPreSleep > TimeSpan.Zero)
                    //    Logger.Debug("Has remove op, sleep before");
                    //return doPreSleep;
                    return TimeSpan.Zero;
                },
                () => new YaDCommonRequest(HttpSettings, (YadWebAuth)Authenticator)
                    .With(new YadItemInfoPostModel(path.Path), out entryInfo)
                    .With(new YadFolderInfoPostModel(path.Path) { WithParent = true, Amount = firstReadLimit }, out folderInfo)
                    .With(new YadResourceStatsPostModel(path.Path), out entryStats)
                    .With(new YadActiveOperationsPostModel(), out operInfo)
                    .MakeRequestAsync(_connectionLimiter)
                    .Result,
                _ => false,
                //_ =>
                //{
                //    bool doAgain = false;
                //    if (hasRemoveOp && _lastRemoveOperation != null)
                //    {
                //        string cmpPath = WebDavPath.Combine("/disk", _lastRemoveOperation.Path);
                //        doAgain = hasRemoveOp &&
                //           folderInfo.Data.Resources.Any(r => WebDavPath.PathEquals(r.Path, cmpPath));
                //    }
                //    if (doAgain)
                //        Logger.Debug("Remove op still not finished, let's try again");
                //    return doAgain;
                //},
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryTimeout);


            if (entryInfo?.Error != null ||
                (entryInfo?.Data?.Error?.Id ?? "HTTP_404") != "HTTP_404" ||
                entryStats?.Error != null ||
                (entryStats?.Data?.Error?.Id ?? "HTTP_404") != "HTTP_404" ||
                folderInfo?.Error != null ||
                (folderInfo?.Data?.Error?.Id ?? "HTTP_404") != "HTTP_404")
            {
                throw new IOException(string.Concat("Error reading file or directory information from server ",
                    entryInfo?.Error?.Message,
                    " ",
                    entryInfo?.Data?.Error?.Message,
                    " ",
                    entryStats?.Error?.Message,
                    " ",
                    entryStats?.Data?.Error?.Message));
            }

            var entryData = entryInfo?.Data;
            if (entryData?.Type is null)
                return null;
            if (entryData.Type == "file")
                return entryData.ToFile();

            Folder folder = folderInfo.Data.ToFolder(entryData, entryStats.Data, path.Path, operInfo?.Data);
            folder.IsChildrenLoaded = limit == int.MaxValue;

            int alreadyCount = folder.Descendants.Count;
            // Если количество полученных элементов списка меньше максимального запрошенного числа элементов,
            // даже с учетом, что в число элементов сверх запрошенного входит информация
            // о папке-контейнере (папке, чей список элементов запросили), то считаем,
            // что получен полный список содержимого папки и возвращает данные.
            if (alreadyCount < firstReadLimit)
                return folder;
            // В противном случае делаем несколько параллельных выборок для ускорения чтения списков с сервера.

            int entryCount = folderInfo?.Data?.Resources.FirstOrDefault()?.Meta?.TotalEntityCount ?? 1;

            // Обновление количества доступных подключений
            maxParallel = Math.Max(_connectionLimiter.CurrentCount - 1, 1);
            var info = new ParallelInfo[maxParallel];
            int restAmount = entryCount - alreadyCount;

            int amountParallel = 40 * maxParallel > restAmount
                ? 40
                : (restAmount + maxParallel-1) / maxParallel;

            int startIndex = alreadyCount;
            int lastIndex = 0;
            for (int i = 0; startIndex < entryCount && i < info.Length; i++)
            {
                info[i] = new ParallelInfo
                {
                    Offset = startIndex,
                    Amount = amountParallel,
                };
                startIndex += amountParallel;
                lastIndex = i;
            }

            if (lastIndex < info.Length - 1)
                Array.Resize(ref info, lastIndex + 1);

            // Хвостовой кусок читаем без ограничения длины, на случай неправильных подсчетов
            // или добавленных в параллели файлов.
            info[info.Length - 1].Amount = int.MaxValue;

            Retry.Do(
                () =>
                {
                    //var doPreSleep = hasRemoveOp ? TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs) : TimeSpan.Zero;
                    //if (doPreSleep > TimeSpan.Zero)
                    //    Logger.Debug("Has remove op, sleep before");
                    //return doPreSleep;
                    return TimeSpan.Zero;
                },
                () =>
                {
                    string diskPath = WebDavPath.Combine("/disk", entryData.Path);

                    Parallel.For(0, info.Length, (int index) =>
                    {
                        YadResponseResult noReturn = new YaDCommonRequest(HttpSettings, (YadWebAuth)Authenticator)
                            .With(new YadFolderInfoPostModel(path.Path)
                            {
                                Offset = info[index].Offset,
                                Amount = info[index].Amount,
                                WithParent = false
                            }, out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderPartInfo)
                            .MakeRequestAsync(_connectionLimiter)
                            .Result;

                        if (folderPartInfo?.Error != null ||
                            folderPartInfo?.Data?.Error != null)
                            throw new IOException(string.Concat("Error reading file or directory information from server ",
                                folderPartInfo?.Error?.Message,
                                " ",
                                folderPartInfo?.Data?.Error?.Message));

                        if (folderPartInfo?.Data is not null && folderPartInfo.Error is null)
                            info[index].Result = folderPartInfo.Data;
                    });

                    return (YadResponseResult)null;
                },
                _ => false,
                //{
                    //TODO: Здесь полностью неправильная проверка
                    //bool doAgain = false;
                    //if (hasRemoveOp && _lastRemoveOperation != null)
                    //{
                    //    string cmpPath = WebDavPath.Combine("/disk", _lastRemoveOperation.Path);
                    //    doAgain = hasRemoveOp &&
                    //       folderInfo.Data.Resources.Any(r => WebDavPath.PathEquals(r.Path, cmpPath));
                    //}
                    //if (doAgain)
                    //    Logger.Debug("Remove op still not finished, let's try again");
                    //return doAgain;
                //},
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryTimeout);

            string diskPath = WebDavPath.Combine("/disk", path.Path);
            var children = new List<IEntry>(folder.Descendants);
            for (int i = 0; i < info.Length; i++)
            {
                var fi = info[i].Result.Resources;
                children.AddRange(
                    fi.Where(it => it.Type == "file")
                      .Select(f => f.ToFile())
                      .ToGroupedFiles());
                children.AddRange(
                    fi.Where(it => it.Type == "dir" &&
                                   // Пропуск элемента с информацией папки о родительской папке,
                                   // этот элемент добавляется в выборки, если читается
                                   // не всё содержимое папки, а делается только вырезка
                                   it.Path != diskPath)
                      .Select(f => f.ToFolder()));
            }
            folder.Descendants = ImmutableList.Create(children.Distinct().ToArray());

            return folder;
        }


        private async Task<IEntry> MediaFolderInfo(string path)
        {
            var entry = await MediaFolderRootInfo();

            if (entry == null || entry is not Folder root)
                return null;
            
            if (WebDavPath.PathEquals(path, YadMediaPath))
                return root;

            string albumName = WebDavPath.Name(path);
            var child = entry.Descendants.FirstOrDefault(child => child.Name == albumName);
            if (child is null)
                return null;

            var album = child;

            var key = album.PublicLinks.Values.FirstOrDefault()?.Key;
            if (key == null)
                return null;

            _ = new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadFolderInfoPostModel(key, "/album"),
                    out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo)
                .MakeRequestAsync(_connectionLimiter)
                .Result;

            Folder folder = folderInfo.Data.ToFolder(null, null, path, null);
            folder.IsChildrenLoaded = true;

            return folder;
        }

        private async Task<IEntry> MediaFolderRootInfo()
        {
            Folder res = new Folder(YadMediaPath);

            _ = await new YaDCommonRequest(HttpSettings, (YadWebAuth)Authenticator)
                .With(new YadGetAlbumsSlicesPostModel(),
                    out YadResponseModel<YadGetAlbumsSlicesRequestData, YadGetAlbumsSlicesRequestParams> slices)
                .With(new YadAlbumsPostModel(),
                    out YadResponseModel<YadAlbumsRequestData[], YadAlbumsRequestParams> albums)
                .MakeRequestAsync(_connectionLimiter);

            var children = new List<IEntry>();

            if (slices.Data.Albums.Camera != null)
            {
                Folder folder =
                    new Folder($"{YadMediaPath}/.{slices.Data.Albums.Camera.Id}")
                    {
                        ServerFilesCount = (int)slices.Data.Albums.Camera.Count
                    };
                children.Add(folder);
            }
            if (slices.Data.Albums.Photounlim != null)
            {
                Folder folder =
                    new Folder($"{YadMediaPath}/.{slices.Data.Albums.Photounlim.Id}")
                    {
                        ServerFilesCount = (int)slices.Data.Albums.Photounlim.Count
                    };
                children.Add(folder);
            }
            if (slices.Data.Albums.Videos != null)
            {
                Folder folder =
                    new Folder($"{YadMediaPath}/.{slices.Data.Albums.Videos.Id}")
                    {
                        ServerFilesCount = (int)slices.Data.Albums.Videos.Count
                    };
                children.Add(folder);
            }
            res.Descendants = res.Descendants.AddRange(children);

            foreach (var item in albums.Data)
            {
                Folder folder = new Folder($"{YadMediaPath}/{item.Title}");
                folder.PublicLinks.TryAdd(
                    item.Public.PublicUrl,
                    new PublicLinkInfo(item.Public.PublicUrl) { Key = item.Public.PublicKey });
            }

            return res;
        }


        public Task<FolderInfoResult> ItemInfo(RemotePath path, int offset = 0, int limit = int.MaxValue)
        {
            throw new NotImplementedException();
        }


        public async Task<AccountInfoResult> AccountInfo()
        {
            //var req = await new YadAccountInfoRequest(HttpSettings, (YadWebAuth)Authenticator).MakeRequestAsync(_connectionLimiter);

            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Authenticator)
                .With(new YadAccountInfoPostModel(),
                    out YadResponseModel<YadAccountInfoRequestData, YadAccountInfoRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToAccountInfo();
            return res;
        }

        public async Task<CreateFolderResult> CreateFolder(string path)
        {
            //var req = await new YadCreateFolderRequest(HttpSettings, (YadWebAuth)Authenticator, path)
            //    .MakeRequestAsync(_connectionLimiter);

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadCreateFolderPostModel(path),
                    out YadResponseModel<YadCreateFolderRequestData, YadCreateFolderRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.Params.ToCreateFolderResult();
            return res;
        }

        public async Task<AddFileResult> AddFile(string fileFullPath, IFileHash fileHash, FileSize fileSize, DateTime dateTime,
            ConflictResolver? conflictResolver)
        {
            var hash = (FileHashYad?)fileHash;

            var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadGetResourceUploadUrlPostModel(fileFullPath, fileSize, hash?.HashSha256.Value, hash?.HashMd5.Value),
                    out YadResponseModel<ResourceUploadUrlData, ResourceUploadUrlParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter).Result;

            var res = new AddFileResult
            {
                Path = fileFullPath,
                Success = itemInfo.Data.Status == "hardlinked"
            };

            return await Task.FromResult(res);
        }

        public Task<CloneItemResult> CloneItem(string fromUrl, string toPath)
        {
            throw new NotImplementedException();
        }

        public async Task<CopyResult> Copy(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

            //var req = await new YadCopyRequest(HttpSettings, (YadWebAuth)Authenticator, sourceFullPath, destFullPath)
            //    .MakeRequestAsync(_connectionLimiter);

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadCopyPostModel(sourceFullPath, destFullPath),
                    out YadResponseModel<YadCopyRequestData, YadCopyRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToCopyResult();

            if (res.IsSuccess)
                WaitForOperation(itemInfo.Data.Oid);

            return res;
        }

        public async Task<CopyResult> Move(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadMovePostModel(sourceFullPath, destFullPath), out YadResponseModel<YadMoveRequestData, YadMoveRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToMoveResult();
            if( res.IsSuccess)
                WaitForOperation(itemInfo.Data.Oid);

            return res;
        }


        public async Task<CheckUpInfo> ActiveOperationsAsync()
        {
            YadResponseModel<List<YadActiveOperationsData>, YadActiveOperationsParams> itemInfo = null;

            _ = await new YaDCommonRequest(HttpSettings, (YadWebAuth)Authenticator)
                    .With(new YadActiveOperationsPostModel(), out itemInfo)
                    .With(new YadAccountInfoPostModel(),
                        out YadResponseModel<YadAccountInfoRequestData, YadAccountInfoRequestParams> accountInfo)
                    .MakeRequestAsync(_connectionLimiter);

            var list = itemInfo?.Data?
                .Select(x => new ActiveOperation
                {
                    Oid = x.Oid,
                    Uid = x.Uid,
                    Type = x.Type,
                    SourcePath = GetOpPath(x.Data.Source),
                    TargetPath = GetOpPath(x.Data.Target),
                })?.ToList();

            var info = new CheckUpInfo
            {
                AccountInfo = new CheckUpInfo.CheckInfo
                {
                    FilesCount = accountInfo?.Data?.FilesCount ?? 0,
                    Free = accountInfo?.Data?.Free ?? 0,
                    Trash = accountInfo?.Data?.Trash ?? 0,
                },
                ActiveOperations = list,
            };

            return info;
        }

        public static string GetOpPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            int colon = path.IndexOf(':');
            return WebDavPath.Clean(path.Substring(1 + colon)).Remove(0, "/disk".Length);
        }

        private void WaitForOperation(string operationOid)
        {
            if (string.IsNullOrWhiteSpace(operationOid))
                return;

            var flagWatch = Stopwatch.StartNew();

            YadResponseModel<YadOperationStatusData, YadOperationStatusParams> itemInfo = null;
            Retry.Do(
                () => TimeSpan.Zero,
                () => new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                    .With(new YadOperationStatusPostModel(operationOid), out itemInfo)
                    .MakeRequestAsync(_connectionLimiter)
                    .Result,
                _ =>
                {
                    /*
                     * Яндекс повторяет проверку при переносе папки каждый 9 секунд.
                     * Когда операция завершилась: "status": "DONE", "state": "COMPLETED", "type": "move"
                     *    "params": {
                     *         "source": "12-it's_uid-34:/disk/source-folder",
                     *         "target": "12-it's_uid-34:/disk/destination-folder"
                     *    },
                     * Когда операция еще в процессе: "status": "EXECUTING", "state": "EXECUTING", "type": "move"
                     *    "params": {
                     *         "source": "12-it's_uid-34:/disk/source-folder",
                     *         "target": "12-it's_uid-34:/disk/destination-folder"
                     *    },
                     */
                    var doAgain = itemInfo.Data.Error is null && itemInfo.Data.State != "COMPLETED";
                    if (doAgain)
                    {
                        if (flagWatch.Elapsed > TimeSpan.FromSeconds(30))
                        {
                            Logger.Debug("Operation is still in progress, let's wait...");
                            flagWatch.Restart();
                        }
                    }
                    return doAgain;
                }, 
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryTimeout);
        }

        public async Task<PublishResult> Publish(string fullPath)
        {
            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadPublishPostModel(fullPath, false), out YadResponseModel<YadPublishRequestData, YadPublishRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToPublishResult();

            if (res.IsSuccess)
                CachedSharedList.Value[fullPath] = new List<PublicLinkInfo> {new(res.Url)};

            return res;
        }

        public async Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath)
        {
            foreach (var item in CachedSharedList.Value
                .Where(kvp => kvp.Key == fullPath).ToList())
            {
                CachedSharedList.Value.Remove(item.Key);
            }

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadPublishPostModel(fullPath, true), out YadResponseModel<YadPublishRequestData, YadPublishRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToUnpublishResult();

            return res;
        }

        public async Task<RemoveResult> Remove(string fullPath)
        {
            //var req = await new YadDeleteRequest(HttpSettings, (YadWebAuth)Authenticator, fullPath)
            //    .MakeRequestAsync(_connectionLimiter);

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadDeletePostModel(fullPath),
                    out YadResponseModel<YadDeleteRequestData, YadDeleteRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToRemoveResult();

            if (res.IsSuccess)
                WaitForOperation(itemInfo.Data.Oid);

            //if (res.IsSuccess)
            //    _lastRemoveOperation = res.ToItemOperation();

            return res;
        }

        public async Task<RenameResult> Rename(string fullPath, string newName)
        {
            string destPath = WebDavPath.Parent(fullPath);
            destPath = WebDavPath.Combine(destPath, newName);

            //var req = await new YadMoveRequest(HttpSettings, (YadWebAuth)Authenticator, fullPath, destPath).MakeRequestAsync(_connectionLimiter);

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadMovePostModel(fullPath, destPath),
                    out YadResponseModel<YadMoveRequestData, YadMoveRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToRenameResult();

            if (res.IsSuccess)
                WaitForOperation(itemInfo.Data.Oid);

            //if (res.IsSuccess)
            //    _lastRemoveOperation = res.ToItemOperation();

            return res;
        }

        public Dictionary<ShardType, ShardInfo> GetShardInfo1()
        {
            throw new NotImplementedException();
        }


        public IEnumerable<PublicLinkInfo> GetShareLinks(string path)
        {
            if (!CachedSharedList.Value.TryGetValue(path, out var links)) 
                yield break;

            foreach (var link in links)
                yield return link;
        }

        
        public async void CleanTrash()
        {
            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authenticator)
                .With(new YadCleanTrashPostModel(), 
                    out YadResponseModel<YadCleanTrashData, YadCleanTrashParams> _)
                .MakeRequestAsync(_connectionLimiter);
        }


        

        public IEnumerable<string> PublicBaseUrls { get; set; } = new[]
        {
            "https://disk.yandex.ru"
        };
        public string PublicBaseUrlDefault => PublicBaseUrls.First();







        public string ConvertToVideoLink(Uri publicLink, SharedVideoResolution videoResolution)
        {
            throw new NotImplementedException("Yad not implemented ConvertToVideoLink");
        }
    }
}
