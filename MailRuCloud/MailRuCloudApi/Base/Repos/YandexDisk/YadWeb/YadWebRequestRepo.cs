using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models.Media;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;
using Stream = System.IO.Stream;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb
{
    class YadWebRequestRepo : IRequestRepo
    {
        #region Константы и внутренние классы















        #endregion

        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadWebRequestRepo));

        protected readonly SemaphoreSlim _connectionLimiter;

        private ItemOperation _lastRemoveOperation;

        protected const int OperationStatusCheckIntervalMs = 300;
        protected const int OperationStatusCheckRetryCount = 8;

        protected readonly Credentials _credentials;

        public YadWebRequestRepo(CloudSettings settings, IWebProxy proxy, Credentials credentials)
        {
            _connectionLimiter = new SemaphoreSlim(settings.MaxConnectionCount);

            HttpSettings = new()
            {
                UserAgent = settings.UserAgent,
                CloudSettings = settings,
                Proxy = proxy,
                BaseDomain = "https://disk.yandex.ru"
            };

            _credentials = credentials;

            ServicePointManager.DefaultConnectionLimit = int.MaxValue;

            // required for Windows 7 breaking connection
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;
        }

        protected async Task<Dictionary<string, IEnumerable<PublicLinkInfo>>> GetShareListInner()
        {
            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
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

        public IAuth Auth => CachedAuth.Value;

        protected Cached<YadWebAuth> CachedAuth
            => _cachedAuth ??= new Cached<YadWebAuth>(_ => new YadWebAuth(_connectionLimiter, HttpSettings, _credentials),
                                                      _ => TimeSpan.FromHours(23));
        protected Cached<YadWebAuth> _cachedAuth;

        public Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> CachedSharedList
            => _cachedSharedList ??= new Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>>(
                _ =>
                    {
                        var res = GetShareListInner().Result;
                        return res;
                    },
                    _ => TimeSpan.FromSeconds(30));
        protected Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> _cachedSharedList;


        public HttpCommonSettings HttpSettings { get; private set; }

        public virtual Stream GetDownloadStream(File aFile, long? start = null, long? end = null)
        {
            CustomDisposable<HttpWebResponse> ResponseGenerator(long instart, long inend, File file)
            {
                //var urldata = new YadGetResourceUrlRequest(HttpSettings, (YadWebAuth)Authenticator, file.FullPath)
                //    .MakeRequestAsync(_connectionLimiter)
                //    .Result;

                var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                    .With(new YadGetResourceUrlPostModel(file.FullPath),
                        out YadResponseModel<ResourceUrlData, ResourceUrlParams> itemInfo)
                    .MakeRequestAsync(_connectionLimiter).Result;

                var url = "https:" + itemInfo.Data.File;
                HttpWebRequest request = new YadDownloadRequest(HttpSettings, (YadWebAuth)Auth, url, instart, inend);
                var response = (HttpWebResponse)request.GetResponse();

                return new CustomDisposable<HttpWebResponse>
                {
                    Value = response,
                    OnDispose = () => { }
                };
            }

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

        protected (HttpRequestMessage, string opId) CreateUploadClientRequest(PushStreamContent content, File file)
        {
            var hash = (FileHashYad?)file.Hash;
            var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
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

            return (request, itemInfo?.Data?.OpId);
        }

        public virtual async Task<UploadFileResult> DoUpload(HttpClient client, PushStreamContent content, File file)
        {
            (var request, string opId) = CreateUploadClientRequest(content, file);
            var responseMessage = await client.SendAsync(request);
            var ures = responseMessage.ToUploadPathResult();

            ures.NeedToAddFile = false;
            //await Task.Delay(1_000);;

            return ures;
        }

        protected const string YadMediaPath = "/Media.wdyad";

        public virtual async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
        {
            if (path.IsLink)
                throw new NotImplementedException(nameof(FolderInfo));

            if (path.Path.StartsWith(YadMediaPath))
                return await MediaFolderInfo(path.Path);

            // YaD perform async deletion
            YadResponseModel<YadItemInfoRequestData, YadItemInfoRequestParams> itemInfo = null;
            YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo = null;
            YadResponseModel<YadResourceStatsRequestData, YadResourceStatsRequestParams> resourceStats = null;



            bool hasRemoveOp = _lastRemoveOperation != null &&
                               WebDavPath.IsParentOrSame(path.Path, _lastRemoveOperation.Path) &&
                               (DateTime.Now - _lastRemoveOperation.DateTime).TotalMilliseconds < 1_000;
            Logger.Debug($"Listing path {path.Path}");

            Retry.Do(
                () =>
                {
                    var doPreSleep = hasRemoveOp ? TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs) : TimeSpan.Zero;
                    if (doPreSleep > TimeSpan.Zero)
                        Logger.Debug("Has remove op, sleep before");
                    return doPreSleep;
                },
                () => new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                    .With(new YadItemInfoPostModel(path.Path), out itemInfo)
                    .With(new YadFolderInfoPostModel(path.Path) { Amount = limit }, out folderInfo)
                    .With(new YadResourceStatsPostModel(path.Path), out resourceStats)
                    .MakeRequestAsync(_connectionLimiter)
                    .Result,
                _ =>
                {
                    var doAgain = hasRemoveOp &&
                           folderInfo.Data.Resources.Any(r =>
                               WebDavPath.PathEquals(r.Path.Remove(0, "/disk".Length), _lastRemoveOperation.Path));
                    if (doAgain)
                        Logger.Debug("Remove op still not finished, let's try again");
                    return doAgain;
                },
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryCount);


            var itemData = itemInfo?.Data;
            switch (itemData?.Type)
            {
            case null:
                return null;
            case "file":
                return itemData.ToFile(PublicBaseUrlDefault);
            default:
                Folder folder = folderInfo.Data.ToFolder(itemInfo.Data, resourceStats.Data, path.Path, PublicBaseUrlDefault, null);
                folder.IsChildrenLoaded = true;
                return folder;
            }
        }


        protected async Task<IEntry> MediaFolderInfo(string path)
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

            _ = new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                .With(new YadFolderInfoPostModel(key, "/album"),
                    out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo)
                .MakeRequestAsync(_connectionLimiter)
                .Result;

            Folder folder = folderInfo.Data.ToFolder(null, null, path, PublicBaseUrlDefault, null);
            folder.IsChildrenLoaded = true;

            return folder;
        }

        private async Task<IEntry> MediaFolderRootInfo()
        {
            Folder res = new Folder(YadMediaPath);

            _ = await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
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

            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
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

            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
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

            var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
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

            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                .With(new YadCopyPostModel(sourceFullPath, destFullPath),
                    out YadResponseModel<YadCopyRequestData, YadCopyRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToCopyResult();

            OnCopyCompleted(res, itemInfo?.Data?.OpId);

            return res;
        }

        protected virtual void OnCopyCompleted(CopyResult res, string operationOpId)
        {
        }

        public async Task<CopyResult> Move(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                .With(new YadMovePostModel(sourceFullPath, destFullPath), out YadResponseModel<YadMoveRequestData, YadMoveRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToMoveResult();

            OnMoveCompleted(res, itemInfo?.Data?.OpId);

            return res;
        }

        protected virtual void OnMoveCompleted(CopyResult res, string operationOpId) => WaitForOperation(operationOpId);

        public async Task<PublishResult> Publish(string fullPath)
        {
            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                .With(new YadPublishPostModel(fullPath, false), out YadResponseModel<YadPublishRequestData, YadPublishRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToPublishResult();

            if (res.IsSuccess)
                CachedSharedList.Value[fullPath] = new List<PublicLinkInfo> { new(res.Url) };

            return res;
        }

        public async Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath)
        {
            foreach (var item in CachedSharedList.Value
                .Where(kvp => kvp.Key == fullPath).ToList())
            {
                CachedSharedList.Value.Remove(item.Key);
            }

            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                .With(new YadPublishPostModel(fullPath, true), out YadResponseModel<YadPublishRequestData, YadPublishRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToUnpublishResult();

            return res;
        }

        public async Task<RemoveResult> Remove(string fullPath)
        {
            //var req = await new YadDeleteRequest(HttpSettings, (YadWebAuth)Authenticator, fullPath)
            //    .MakeRequestAsync(_connectionLimiter);

            await new YaDCommonV2Request(HttpSettings, (YadWebAuth)Auth)
                .With(new YadBulkAsyncDelete(fullPath),
                    out YadResponseModel<YadBulkAsyncDeleteRequestData, YadBulkAsyncDeleteRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            // TODO: wait op finish

            var res =  new RemoveResult
            {
                IsSuccess = true,
                DateTime = DateTime.Now,
                Path = fullPath
            };

            OnRemoveCompleted(res, ""); // itemInfo?.Data?.OpId ???

            return res;
        }

        protected virtual void OnRemoveCompleted(RemoveResult res, string operationOpId)
        {
            if (res.IsSuccess)
                _lastRemoveOperation = res.ToItemOperation();
        }

        public async Task<RenameResult> Rename(string fullPath, string newName)
        {
            string destPath = WebDavPath.Parent(fullPath);
            destPath = WebDavPath.Combine(destPath, newName);

            //var req = await new YadMoveRequest(HttpSettings, (YadWebAuth)Authenticator, fullPath, destPath).MakeRequestAsync(_connectionLimiter);

            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                .With(new YadMovePostModel(fullPath, destPath),
                    out YadResponseModel<YadMoveRequestData, YadMoveRequestParams> itemInfo)
                .MakeRequestAsync(_connectionLimiter);

            var res = itemInfo.ToRenameResult();

            OnRenameCompleted(res, itemInfo?.Data?.OpId);

            return res;
        }

        protected virtual void OnRenameCompleted(RenameResult res, string operationOpId)
        {
            if (res.IsSuccess)
                WaitForOperation(operationOpId);

            //if (res.IsSuccess)
            //    _lastRemoveOperation = res.ToItemOperation();
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
            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                .With(new YadCleanTrashPostModel(),
                    out YadResponseModel<YadCleanTrashData, YadCleanTrashParams> _)
                .MakeRequestAsync(_connectionLimiter);
        }




        public IEnumerable<string> PublicBaseUrls { get; set; } = new[]
        {
            "https://yadi.sk"
        };
        public string PublicBaseUrlDefault => PublicBaseUrls.FirstOrDefault();


        public string ConvertToVideoLink(Uri publicLink, SharedVideoResolution videoResolution)
        {
            throw new NotImplementedException("Yad not implemented ConvertToVideoLink");
        }

        protected virtual void WaitForOperation(string operationOpId)
        {
            if (string.IsNullOrWhiteSpace(operationOpId))
                return;

            YadResponseModel<YadOperationStatusData, YadOperationStatusParams> itemInfo = null;
            Retry.Do(
                () => TimeSpan.Zero,
                () => new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                    .With(new YadOperationStatusPostModel(operationOpId), out itemInfo)
                    .MakeRequestAsync(_connectionLimiter)
                    .Result,
                _ =>
                {
                    var doAgain = null == itemInfo.Data.Error && itemInfo.Data.State != "COMPLETED";
                    //if (doAgain)
                    //    Logger.Debug("Move op still not finished, let's try again");
                    return doAgain;
                },
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryCount);
        }

        public virtual async Task<CheckUpInfo> DetectOutsideChanges() => await Task.FromResult<CheckUpInfo>(null);
    }
}
