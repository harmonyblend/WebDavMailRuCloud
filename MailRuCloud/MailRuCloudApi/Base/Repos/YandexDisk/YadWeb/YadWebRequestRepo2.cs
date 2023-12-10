﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;
using Stream = System.IO.Stream;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb
{
    class YadWebRequestRepo2 : YadWebRequestRepo
    {
        #region Константы и внутренние классы

        /// <summary>
        /// Сколько записей папки читать в первом обращении, до параллельного чтения.
        /// Яндекс читает по 40 записей, путь тоже будет 40.
        /// </summary>
        private const int FirstReadEntriesCount = 40;

        private const int OperationStatusCheckRetryTimeoutMinutes = 5;

        private struct ParallelFolderInfo
        {
            public int Offset;
            public int Amount;
            public YadFolderInfoRequestData Result;
        }

        #endregion

        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadWebRequestRepo2));

        private readonly TimeSpan OperationStatusCheckRetryTimeout = TimeSpan.FromMinutes(OperationStatusCheckRetryTimeoutMinutes);




        public YadWebRequestRepo2(CloudSettings settings, IWebProxy proxy, Credentials credentials)
            : base(settings, proxy, credentials)
        {
        }

        public override Stream GetDownloadStream(File aFile, long? start = null, long? end = null)
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
                    var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
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
                HttpWebRequest request = new YadDownloadRequest(HttpSettings, (YadWebAuth)Auth, url, instart, inend);
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

        public override async Task<UploadFileResult> DoUpload(HttpClient client, PushStreamContent content, File file)
        {
            (var request, string opId) = CreateUploadClientRequest(content, file);
            var responseMessage = await client.SendAsync(request);
            var ures = responseMessage.ToUploadPathResult();

            ures.NeedToAddFile = false;

            if (!string.IsNullOrEmpty(opId))
                WaitForOperation(opId);

            return ures;
        }

        public override async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
        {
            if (path.IsLink)
                throw new NotImplementedException(nameof(FolderInfo));

            if (path.Path.StartsWith(YadMediaPath))
                return await MediaFolderInfo(path.Path);

            // YaD perform async deletion
            YadResponseModel<YadItemInfoRequestData, YadItemInfoRequestParams> itemInfo = null;
            YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo = null;
            YadResponseModel<YadResourceStatsRequestData, YadResourceStatsRequestParams> resourceStats = null;
            YadResponseModel<List<YadActiveOperationsData>, YadActiveOperationsParams> activeOps = null;

            /*
             * Не менее 1 параллельного потока,
             * не более доступного по ограничителю за вычетом одного для соседних запросов,
             * но не более 10, т.к. вряд ли сервер будет выдавать данные быстрее, а канал уже и так будет загружен.
             */
            int maxParallel = Math.Max(Math.Max(_connectionLimiter.CurrentCount - 1, 1), 10);
            // Если доступных подключений к серверу 2 или менее, то не делаем параллельного чтения
            int firstReadLimit = maxParallel <= 2 ? int.MaxValue : FirstReadEntriesCount;

            Logger.Debug($"Listing path {path.Path}");

            Retry.Do(
                () => TimeSpan.Zero,
                () => new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                    .With(new YadItemInfoPostModel(path.Path), out itemInfo)
                    .With(new YadFolderInfoPostModel(path.Path) { WithParent = true, Amount = firstReadLimit }, out folderInfo)
                    .With(new YadResourceStatsPostModel(path.Path), out resourceStats)
                    .With(new YadActiveOperationsPostModel(), out activeOps)
                    .MakeRequestAsync(_connectionLimiter)
                    .Result,
                _ => false,
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryTimeout);


            if (itemInfo?.Error != null ||
                (itemInfo?.Data?.Error?.Id ?? "HTTP_404") != "HTTP_404" ||
                resourceStats?.Error != null ||
                (resourceStats?.Data?.Error?.Id ?? "HTTP_404") != "HTTP_404" ||
                folderInfo?.Error != null ||
                (folderInfo?.Data?.Error?.Id ?? "HTTP_404") != "HTTP_404")
            {
                throw new IOException(string.Concat("Error reading file or directory information from server ",
                    itemInfo?.Error?.Message,
                    " ",
                    itemInfo?.Data?.Error?.Message,
                    " ",
                    resourceStats?.Error?.Message,
                    " ",
                    resourceStats?.Data?.Error?.Message));
            }

            var entryData = itemInfo?.Data;
            if (entryData?.Type is null)
                return null;
            if (entryData.Type == "file")
                return entryData.ToFile(PublicBaseUrlDefault);

            Folder folder = folderInfo.Data.ToFolder(entryData, resourceStats.Data, path.Path, PublicBaseUrlDefault, activeOps?.Data);
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

            /*
             * Не менее 1 параллельного потока,
             * не более доступного по ограничителю за вычетом одного для соседних запросов,
             * но не более 10, т.к. вряд ли сервер будет выдавать данные быстрее, а канал уже и так будет загружен.
             *
             * Здесь просто повторяем расчет, т.к. свободных потоков по ограничителю могло поменяться.
             */
            maxParallel = Math.Max(Math.Max(_connectionLimiter.CurrentCount - 1, 1), 10);
            var info = new ParallelFolderInfo[maxParallel];
            int restAmount = entryCount - alreadyCount;

            int amountParallel = 40 * maxParallel > restAmount
                ? 40
                : (restAmount + maxParallel-1) / maxParallel;

            int startIndex = alreadyCount;
            int lastIndex = 0;
            for (int i = 0; startIndex < entryCount && i < info.Length; i++)
            {
                info[i] = new ParallelFolderInfo
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
                () => TimeSpan.Zero,
                () =>
                {
                    string diskPath = WebDavPath.Combine("/disk", entryData.Path);

                    Parallel.For(0, info.Length, (int index) =>
                    {
                        YadResponseResult noReturn = new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
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
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryTimeout);

            string diskPath = WebDavPath.Combine("/disk", path.Path);
            var children = new List<IEntry>(folder.Descendants);
            for (int i = 0; i < info.Length; i++)
            {
                var fi = info[i].Result.Resources;
                children.AddRange(
                    fi.Where(it => it.Type == "file")
                      .Select(f => f.ToFile(PublicBaseUrlDefault))
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

        protected override void OnCopyCompleted(CopyResult res, string operationOpId) => WaitForOperation(operationOpId);

        protected override void OnMoveCompleted(CopyResult res, string operationOpId) => WaitForOperation(operationOpId);

        protected override void OnRemoveCompleted(RemoveResult res, string operationOpId) => WaitForOperation(operationOpId);

        protected override void OnRenameCompleted(RenameResult res, string operationOpId) => WaitForOperation(operationOpId);

        protected override void WaitForOperation(string operationOpId)
        {
            if (string.IsNullOrWhiteSpace(operationOpId))
                return;

            var flagWatch = Stopwatch.StartNew();

            YadResponseModel<YadOperationStatusData, YadOperationStatusParams> itemInfo = null;
            Retry.Do(
                () => TimeSpan.Zero,
                () => new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                    .With(new YadOperationStatusPostModel(operationOpId), out itemInfo)
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

        public override async Task<CheckUpInfo> DetectOutsideChanges()
        {
            YadResponseModel<List<YadActiveOperationsData>, YadActiveOperationsParams> itemInfo = null;

            _ = await new YaDCommonRequest(HttpSettings, (YadWebAuth)Auth)
                    .With(new YadActiveOperationsPostModel(), out itemInfo)
                    .With(new YadAccountInfoPostModel(),
                        out YadResponseModel<YadAccountInfoRequestData, YadAccountInfoRequestParams> accountInfo)
                    .MakeRequestAsync(_connectionLimiter);

            var list = itemInfo?.Data?
                .Select(x => new ActiveOperation
                {
                    OpId = x.OpId,
                    Uid = x.Uid,
                    Type = x.Type,
                    SourcePath = DtoImportYadWeb.GetOpPath(x.Data.Source),
                    TargetPath = DtoImportYadWeb.GetOpPath(x.Data.Target),
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

    }
}