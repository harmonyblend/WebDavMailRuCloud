using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base;
using YaR.Clouds.Links;
using YaR.Clouds.Base.Requests.Types;
using System.Linq;

namespace YaR.Clouds.Common;

public class EntryCache : IDisposable
{
    public enum GetState
    {
        /// <summary>
        /// Когда из менеджера кеша запрашивается файл, файла нет в кеше,
        /// а менеджер не обладает информацией о наличии или отсутствии файла в облаке.
        /// </summary>
        Unknown,
        /// <summary>
        /// Когда менеджер кеша имеет в кеше всю папку, где должен быть файл,
        /// но файла такого в папке в кеше нету, то его нет и в облаке.
        /// </summary>
        NotExists,
        /// <summary>
        /// Когда менеджер имеет в кеше файл и возвращает его.
        /// </summary>
        Entry,
        /// <summary>
        /// Когда менеджер имеет в кеше папку, но в кеше отсутствует ее содержимое, которое надо считать с сервера.
        /// </summary>
        EntryWithUnknownContent
    };

    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(EntryCache));

    //private static readonly TimeSpan _minCleanUpInterval = new TimeSpan(0, 0, 10 /* секунды */ );
    //private static readonly TimeSpan _maxCleanUpInterval = new TimeSpan(0, 10 /* минуты */, 0);

    // По умолчанию очистка кеша от устаревших записей производится каждые 30 секунд
    private TimeSpan _cleanUpPeriod = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _expirePeriod;

    public bool IsCacheEnabled { get; private set; }

    private readonly System.Timers.Timer _cleanTimer;

    private readonly ConcurrentDictionary<string /* full path */, CacheItem> _root =
        new(StringComparer.InvariantCultureIgnoreCase);

    private readonly SemaphoreSlim _locker = new SemaphoreSlim(1);

    public delegate Task<CheckUpInfo> CheckOperations();
    private readonly CheckOperations _activeOperationsAsync;
    private readonly System.Timers.Timer _checkActiveOperationsTimer;
    // Проверка активных операций на сервере и внешних изменений в облаке не через сервис
    // производится каждые 24 секунды, число не кратное очистке кеша, чтобы не пересекались
    // алгоритмы.
    private readonly TimeSpan _opCheckPeriod = TimeSpan.FromSeconds(24);

    private CheckUpInfo.CheckInfo? _lastComparedInfo;

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal class CacheItem
    {
        /// <summary>
        /// Отметка о времени размещения в кеше.
        /// </summary>
        public DateTime CreationTime { get; set; }
        /// <summary>
        /// Entry - файл, папка или link в кеше, если не null.
        /// Или null, если сохраняется информация о том,
        /// что на сервере нет такого файла или папки, а то есть некоторые клиенты,
        /// которые многократно требуют то, что было уже ранее удалено.
        /// </summary>
        public IEntry Entry { get; set; }

        /// <summary>
        /// Если часть файлов кеша папки устарело и отсутствует,
        /// нельзя выдавать содержимое папки из кеша.
        /// </summary>
        public bool AllDescendantsInCache { get; set; }

        public string DebuggerDisplay =>
            $"{(Entry is null
                ? "<<DELETED on server>>"
                : Entry is Link
                    ? "<<Link>>"
                    : Entry is File
                        ? "<<File>>"
                        : AllDescendantsInCache
                            ? "<<FOLDER with DESCENDANTS>>"
                            : "<<JUST folder entry>>")}"
            + $", Since {CreationTime:HH:mm:ss} {Entry?.FullPath}";
    }

    public EntryCache(TimeSpan expirePeriod, CheckOperations activeOperationsAsync)
    {
        _expirePeriod = expirePeriod;
        IsCacheEnabled = Math.Abs(_expirePeriod.TotalMilliseconds) > 0.01;

        _lastComparedInfo = null;
        _activeOperationsAsync = activeOperationsAsync;
        _checkActiveOperationsTimer = null;
        _cleanTimer = null;

        if (IsCacheEnabled)
        {
            _cleanTimer = new System.Timers.Timer()
            {
                Interval = _cleanUpPeriod.TotalMilliseconds,
                Enabled = true,
                AutoReset = true
            };
            _cleanTimer.Elapsed += RemoveExpired;

            if (_activeOperationsAsync is not null && expirePeriod.TotalMinutes >= 1)
            {
                // Если кеш достаточно длительный, делаются регулярные
                // проверки на изменения в облаке
                _checkActiveOperationsTimer = new System.Timers.Timer()
                {
                    Interval = _opCheckPeriod.TotalMilliseconds,
                    Enabled = true,
                    AutoReset = true
                };
                _checkActiveOperationsTimer.Elapsed += CheckActiveOps;
            }
        }
    }

    #region IDisposable Support
    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _checkActiveOperationsTimer?.Stop();
            _checkActiveOperationsTimer?.Dispose();
            _cleanTimer?.Stop();
            _cleanTimer?.Dispose();
            Clear();
            _locker?.Dispose();

            Logger.Debug("EntryCache disposed");
        }
        _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(true);
    }
    #endregion

    //public TimeSpan CleanUpPeriod
    //{
    //    get => _cleanUpPeriod;
    //    set
    //    {
    //        // Очистку кеша от устаревших записей не следует проводить часто чтобы не нагружать машину,
    //        // и не следует проводить редко, редко, чтобы не натыкаться постоянно на устаревшие записи.
    //        _cleanUpPeriod = value < _minCleanUpInterval
    //                         ? _minCleanUpInterval
    //                         : value > _maxCleanUpInterval
    //                           ? _maxCleanUpInterval
    //                           : value;

    //        if (IsCacheEnabled)
    //        {
    //            long cleanPreiod = (long)_cleanUpPeriod.TotalMilliseconds;
    //            _cleanTimer.Change(cleanPreiod, cleanPreiod);
    //        }
    //    }
    //}

    private void RemoveExpired(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (_root.IsEmpty)
            return;

        var watch = Stopwatch.StartNew();

        DateTime threshold = DateTime.Now - _expirePeriod;
        int removedCount = 0;
        int partiallyExpiredCount = 0;
        foreach (var entry in _root)
        {
            _locker.Wait();
            try
            {
                if (entry.Value.CreationTime <= threshold &&
                    _root.TryRemove(entry.Key, out var cacheEntry))
                {
                    /*
                     * Если элемент кеша устарел и удаляется, то
                     * у папки, в которой он содержится, если она в кеше,
                     * ставится признак AllChildrenInCache=false,
                     * чтобы при запросе на выборку содержимого папки
                     * не был возвращен неполный список из-за удаленных
                     * из кеша устаревших элементов.
                     */
                    removedCount++;
                    if (entry.Key != WebDavPath.Root)
                    {
                        if (_root.TryGetValue(WebDavPath.Parent(entry.Key), out var parentEntry) &&
                            parentEntry.Entry is Folder)
                        {
                            parentEntry.AllDescendantsInCache = false;
                            partiallyExpiredCount++;
                        }
                    }
                }
            }
            finally
            {
                _locker.Release();
            }
        }

        if (removedCount > 0)
            Logger.Debug($"Items cache clean: removed {removedCount} expired " +
                $"items, {partiallyExpiredCount} marked partially expired ({watch.ElapsedMilliseconds} ms)");

        return;
    }

    public void ResetCheck()
    {
        if (!IsCacheEnabled)
            return;

        _locker.Wait();
        try
        {
            _lastComparedInfo = null;
        }
        finally
        {
            _locker.Release();
        }
    }

    private async void CheckActiveOps(object sender, System.Timers.ElapsedEventArgs e)
    {
        CheckUpInfo info = await _activeOperationsAsync();
        if (info is null)
            return;

        CheckUpInfo.CheckInfo? currentValue;

        _locker.Wait();
        try
        {
            currentValue = _lastComparedInfo;
            _lastComparedInfo = info.AccountInfo;
        }
        finally
        {
            _locker.Release();
        }

        if (currentValue is not null)
        {
            if (info.AccountInfo.FilesCount != currentValue.Value.FilesCount ||
                info.AccountInfo.Trash != currentValue.Value.Trash ||
                info.AccountInfo.Free != currentValue.Value.Free)
            {
                // Если между проверками что-то изменились, делаем полный сброс кеша
                Clear();
                return;
            }
        }

        List<string> paths = new List<string>();
        foreach (var op in info.ActiveOperations)
        {
            if (!string.IsNullOrEmpty(op.SourcePath))
            {
                if (!paths.Contains(WebDavPath.Parent(op.SourcePath)))
                {
                    paths.Add(WebDavPath.Parent(op.SourcePath));
                    Logger.Debug($"Operation '{op.Type}' is progressing, clean up under {op.SourcePath}");
                }
            }
            if (!string.IsNullOrEmpty(op.TargetPath))
            {
                if (!paths.Contains(WebDavPath.Parent(op.TargetPath)))
                {
                    paths.Add(WebDavPath.Parent(op.TargetPath));
                    Logger.Debug($"Operation '{op.Type}' is progressing, clean up under {op.TargetPath}");
                }
            }
        }
        if (paths.Count == 0)
            return;

        await _locker.WaitAsync();
        try
        {
            foreach (var cacheItem in _root)
            {
                if (paths.Any(x => WebDavPath.IsParent(x, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false)))
                {
                    _root.TryRemove(cacheItem.Key, out _);
                }
            }
        }
        finally
        {
            _locker.Release();
        }
    }

    public (IEntry, GetState) Get(string fullPath)
    {
        if (!IsCacheEnabled)
            return (default, GetState.Unknown);

        IEntry result = default;

        _locker.Wait();
        try
        {
            if (!_root.TryGetValue(fullPath, out var cachedEntry))
            {
                if (_root.TryGetValue(WebDavPath.Parent(fullPath), out var parentEntry) &&
                    parentEntry.Entry is Folder parentFolder &&
                    parentEntry.AllDescendantsInCache)
                {
                    // Когда в кеше нет элемента, но в родительской директории,
                    // где он должен быть, загружены все элементы в кеш,
                    // то можно точно сказать, что такого элемента нет
                    // не только в кеше, но и на сервере.
                    Logger.Debug($"Cache says: {fullPath} doesn't exist");
                    return (default, GetState.NotExists);
                }

                Logger.Debug($"Cache missed: {fullPath}");
                return (default, GetState.Unknown);
            }

            if (cachedEntry.Entry is null)
            {
                Logger.Debug($"Cache says: {fullPath} doesn't exist");
                return (default, GetState.NotExists);
            }

            DateTime threshold = DateTime.Now - _expirePeriod;
            if (cachedEntry.CreationTime <= threshold)
            {
                Logger.Debug($"Cache expired: {fullPath}");
                return (default, GetState.Unknown);
            }

            if (cachedEntry.Entry is File file)
            {
                result = file.New(file.FullPath);
            }

            if (cachedEntry.Entry is Folder folder)
            {
                if (!cachedEntry.AllDescendantsInCache)
                {
                    Logger.Debug($"Cache says: {fullPath} folder's content isn't cached");
                    return (default, GetState.EntryWithUnknownContent);
                }

                var children = new List<IEntry>();

                foreach (var cacheItem in _root)
                {
                    if (WebDavPath.IsParent(fullPath, cacheItem.Key, selfTrue: false, oneLevelDistanceOnly: true))
                    {
                        // Если при формировании списка содержимого папки из кеша
                        // выясняется, что часть содержимого в кеше устарело,
                        // то список из кеша сформировать не можем.
                        if (cacheItem.Value.CreationTime <= threshold)
                            return (default, GetState.Unknown);

                        if (cacheItem.Value.Entry is not null)
                        {
                            // В кеше может быть информация о том, что файла/папки нет,
                            // то есть null, такие пропускаем.
                            children.Add(cacheItem.Value.Entry);
                        }
                    }
                }
                result = folder.New(folder.FullPath, children);
            }
        }
        finally
        {
            _locker.Release();
        }

        Logger.Debug($"Cache hit: {fullPath}");
        return (result, GetState.Entry);
    }

    public void Add(IEntry entry)
    {
        if (!IsCacheEnabled)
            return;

        AddInternal(entry);
    }

    private void AddInternal(IEntry entry)
    {
        // Параметр со временем нужен для того, чтобы все
        // добавляемые в кеш элементы ровно в одно и то же время
        // становились устаревшими.
        if (entry is Link link)
            Add(link, DateTime.Now);
        else
        if (entry is File file)
            AddInternal(file, DateTime.Now);
        else
        if (entry is Folder folder)
        {
            if (folder.IsChildrenLoaded)
            {
                _locker.Wait();
                try
                {
                    AddWithChildren(folder, DateTime.Now);
                }
                finally
                {
                    _locker.Release();
                }
            }
            else
            {
                AddOne(folder, DateTime.Now);
            }
        }
    }

    private void AddInternal(File file, DateTime creationTime)
    {
        if (file.Attributes.HasFlag(System.IO.FileAttributes.Offline))
        {
            // Файл затронут активной операцией на сервере
            RemoveOne(file.FullPath);
        }
        else
        {
            string fullPath = file.FullPath;
            var cachedItem = new CacheItem()
            {
                Entry = file.New(fullPath),
                AllDescendantsInCache = true,
                CreationTime = creationTime
            };
            _root.AddOrUpdate(fullPath, cachedItem, (_, _) => cachedItem);
        }
    }

    private void Add(Link link, DateTime creationTime)
    {
        string fullPath = link.FullPath;
        var cachedItem = new CacheItem()
        {
            Entry = link,
            AllDescendantsInCache = true,
            CreationTime = creationTime
        };
        _root.AddOrUpdate(fullPath, cachedItem, (_, _) => cachedItem);
    }

    private CacheItem AddOne(Folder folder, DateTime creationTime)
    {
        string fullPath = folder.FullPath;

        if (folder.Attributes.HasFlag(System.IO.FileAttributes.Offline))
        {
            // Папка затронута активной операцией на сервере
            RemoveTree(folder.FullPath);
            return null;
        }
        else
        {
            var cachedItem = new CacheItem()
            {
                // При добавлении в кеш из folder всего содержимого,
                // делается очистка его списка содержимого, чтобы не было
                // соблазна использовать информацию где-то дальше,
                // т.к. она однозначно перестанет быть актуальной,
                // т.к. алгоритмы кеша не работают с этим списком,
                // этот список только носитель данных при выгрузке с сервера.
                Entry = folder.New(fullPath),
                AllDescendantsInCache = false,
                CreationTime = creationTime
            };
            _root.AddOrUpdate(fullPath, cachedItem,
                (key, value) =>
                {
                    /*
                     * Если папка в кеше имела признак, что все потомки загружены в кеш,
                     * а потом пришло обновление entry этой папки, но признака наличия
                     * в кеше всех потомков нету, то мы выставляем его принудительно,
                     * т.к. знаем, что в кеше все есть, даже если они устарели,
                     * т.к. это будет обработано при следующем чтении списка папки.
                     */
                    if (value.AllDescendantsInCache && !cachedItem.AllDescendantsInCache)
                        cachedItem.AllDescendantsInCache = true;
                    return cachedItem;
                });

            return cachedItem;
        }
    }

    private void AddWithChildren(Folder folder, DateTime creationTime)
    {
        if (folder.Attributes.HasFlag(System.IO.FileAttributes.Offline))
        {
            // Папка затронута активной операцией на сервере
            RemoveTreeNoLock(folder.FullPath);
        }
        else
        {
            foreach (var child in folder.Descendants)
            {
                if (child is File file)
                    AddInternal(file, creationTime);
                else
                if (child is Folder fld)
                    AddOne(fld, creationTime);
            }
            CacheItem cachedItem = AddOne(folder, creationTime);
            cachedItem.AllDescendantsInCache = true;
            /*
             * Внимание! У добавляемого в кеш folder список Descendants всегда пустой!
             * Он специально очищается, чтобы не было соблазна им пользоваться!
             * Содержимое папки берется не из этого списка, а собирается из кеша по path всех entry.
             */
        }
    }

    public async void OnCreateAsync(string fullPath, Task<IEntry> newEntryTask)
    {
        if (!IsCacheEnabled)
            return;

        IEntry newEntry = newEntryTask is null
            ? null
            : await newEntryTask;

        if (newEntry is null)
        {
            await _locker.WaitAsync();
            try
            {
                if (_root.TryRemove(fullPath, out var cachedItem))
                {
                    // Нового нет, но был, удалить из кеша
                    if (fullPath != WebDavPath.Root)
                    {
                        if (_root.TryGetValue(WebDavPath.Parent(fullPath), out var parentEntry) &&
                            parentEntry.Entry is Folder)
                        {
                            parentEntry.AllDescendantsInCache = false;
                        }
                    }
                }
                else
                {
                    // Нового нет, и не было
                    return;
                }
            }
            finally
            {
                _locker.Release();
            }
        }
        else
        {
            await _locker.WaitAsync();
            try
            {
                bool removed = _root.TryRemove(fullPath, out var cachedItem);

                // Добавить новый
                if (newEntry is File file)
                    AddInternal(file, DateTime.Now);
                else
                if (newEntry is Folder folder)
                {
                    /* Данный метод вызывается для созданных и переименованных файлов и папок.
                     * С сервера читали entry самой папки и одного вложенного элемента.
                     * Если Descendants.Count равен 0, то можно ставить
                     * IsChildrenLoaded = true, т.к. ничего вложенного нет.
                     * Но если Descendants.Count>0, тогда IsChildrenLoaded = false,
                     * т.к. что-то внутри есть, но мы не читали полный список содержимого.
                     * IsChildrenLoaded ставится в true,
                     * чтобы последующие чтения entry созданного элемента
                     * читались из кеша, а не находили только entry от директории
                     * без потомков, что автоматически приводит к игнорированию кеша.
                     */
                    folder.IsChildrenLoaded = folder.Descendants.Count == 0;
                    AddWithChildren(folder, DateTime.Now);
                }
                // После добавления или обновления элемента надо обновить родителя,
                // если у него AllDescendantsInCache=true, иначе нет смысла
                string parent = WebDavPath.Parent(fullPath);
                if (fullPath != WebDavPath.Root &&
                    _root.TryGetValue(parent, out var parentEntry) &&
                        parentEntry.Entry is Folder fld &&
                        parentEntry.AllDescendantsInCache)
                {
                    /*
                     * Внимание! У добавляемого в кеш folder список Descendants всегда пустой!
                     * Он специально очищается, чтобы не было соблазна им пользоваться!
                     * Содержимое папки берется не из этого списка, а собирается из кеша по path всех entry.
                     * В кеше у папок Descendants всегда = ImmutableList<IEntry>.Empty
                     */
                    if (removed && cachedItem?.Entry is not null)
                    {
                        if (cachedItem.Entry.IsFile)
                        {
                            if (fld.ServerFilesCount.HasValue && fld.ServerFilesCount > 0)
                                fld.ServerFilesCount--;
                        }
                        else
                        {
                            if (fld.ServerFoldersCount.HasValue && fld.ServerFoldersCount > 0)
                                fld.ServerFoldersCount--;
                        }
                    }
                    if (newEntry.IsFile)
                    {
                        if (fld.ServerFilesCount.HasValue)
                            fld.ServerFilesCount++;
                    }
                    else
                    {
                        if (fld.ServerFoldersCount.HasValue)
                            fld.ServerFoldersCount++;
                    }
                }
            }
            finally
            {
                _locker.Release();
            }
        }
    }

    public async void OnRemoveTreeAsync(string fullPath, Task<IEntry> newEntryTask)
    {
        if (!IsCacheEnabled)
            return;

        IEntry newEntry = newEntryTask is null
            ? null
            : await newEntryTask;

        if (newEntry is not null)
        {
            // Если операция удалила элемент, но он снова получен с сервера,
            // это не нормальная ситуация.
            // Сбрасываем весь кеш, чтобы все перечитать заново на всякий случай.
            await _locker.WaitAsync();
            try
            {
                _root.Clear();
            }
            finally
            {
                _locker.Release();
            }
            return;
        }
        else
        {
            // Нового элемента на сервере нет,
            // очищаем кеш от элемента и всего, что под ним
            await _locker.WaitAsync();
            try
            {
                // Если элемент был, а нового нет, надо удалить его у родителя,
                // чтобы не перечитывать родителя целиком с сервера,
                // но только, если у родителя AllDescendantsInCache=true, иначе нет смысла
                if (fullPath != WebDavPath.Root &&
                    _root.TryGetValue(WebDavPath.Parent(fullPath), out var parentEntry) &&
                        parentEntry.Entry is Folder fld &&
                        parentEntry.AllDescendantsInCache)
                {
                    /*
                     * Внимание! У добавляемого в кеш folder список Descendants всегда пустой!
                     * Он специально очищается, чтобы не было соблазна им пользоваться!
                     * Содержимое папки берется не из этого списка, а собирается из кеша по path всех entry.
                     * В кеше у папок Descendants всегда = ImmutableList<IEntry>.Empty
                     */
                    if (_root.TryRemove(fullPath, out var cachedItem))
                    {
                        if (cachedItem.Entry.IsFile)
                        {
                            if (fld.ServerFilesCount.HasValue && fld.ServerFilesCount > 0)
                                fld.ServerFilesCount--;
                        }
                        else
                        {
                            if (fld.ServerFoldersCount.HasValue && fld.ServerFoldersCount > 0)
                                fld.ServerFoldersCount--;
                        }
                    }
                }

                foreach (var cacheItem in _root)
                {
                    if (WebDavPath.IsParent(fullPath, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false))
                    {
                        cacheItem.Value.CreationTime = DateTime.MinValue;
                    }
                }

                // Добавляем информацию о том, что на сервере нет элемента с таким путем,
                // т.к. он был удален, а нового (из newEntryTask) нет.
                var deletedItem = new CacheItem()
                {
                    Entry = null,
                    AllDescendantsInCache = true,
                    CreationTime = DateTime.Now
                };
                _root.TryAdd(fullPath, deletedItem);
            }
            finally
            {
                _locker.Release();
            }
        }
    }

    public void RemoveOne(string fullPath)
    {
        if (!IsCacheEnabled)
            return;

        _locker.Wait();
        try
        {
            _root.TryRemove(fullPath, out _);

            if (_root.TryGetValue(WebDavPath.Parent(fullPath), out var parentEntry) &&
                parentEntry.Entry is Folder)
            {
                parentEntry.AllDescendantsInCache = false;
            }
        }
        finally
        {
            _locker.Release();
        }
    }

    public void RemoveTree(string fullPath)
    {
        if (!IsCacheEnabled)
            return;

        _locker.Wait();
        try
        {
            RemoveTreeNoLock(fullPath);
        }
        finally
        {
            _locker.Release();
        }
    }

    private void RemoveTreeNoLock(string fullPath)
    {
        _root.TryRemove(fullPath, out _);

        if (_root.TryGetValue(WebDavPath.Parent(fullPath), out var parentEntry) &&
            parentEntry.Entry is Folder)
        {
            parentEntry.AllDescendantsInCache = false;
        }
        foreach (var cacheItem in _root)
        {
            if (WebDavPath.IsParent(fullPath, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false))
            {
                _root.TryRemove(cacheItem.Key, out _);
            }
        }
    }

    public void Clear()
    {
        if (!IsCacheEnabled)
            return;

        _locker.Wait();
        try
        {
            _root.Clear();
        }
        finally
        {
            _locker.Release();
        }
    }
}
