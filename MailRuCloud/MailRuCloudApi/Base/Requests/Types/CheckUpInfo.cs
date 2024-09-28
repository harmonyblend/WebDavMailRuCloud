using System;
using System.Collections.Generic;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

namespace YaR.Clouds.Base.Requests.Types;

public class CheckUpInfo
{
    public class CheckInfo
    {
        //public long FilesCount;
        //public long Free;
        //public long Trash;

        /// <summary>Набор счетчиков, возвращаемых методом cloud/virtual-disk-journal-counters</summary>
        public JournalCounters JournalCounters;
    };

    /// <summary>Набор информации для проверки изменений на диске, минуя данный сервис.</summary>
    public CheckInfo AccountInfo { get; set; }

    /// <summary>Список активных операций на сервере.</summary>
    public List<ActiveOperation> ActiveOperations { get; set; }
}

public enum CounterOperation
{
    None = 0,
    Remove,
    Rename,
    Move,
    Copy,
    Upload,
    TakeSomeonesFolder,
    NewFolder,
    //Update,
    RemoveToTrash,
    RestoreFromTrash,
    TrashDropItem,
    TrashDropAll
}

public class JournalCounters
{
    /// <summary>Удаление навсегда</summary>
    public long RemoveCounter = 0;
    public const string RemoveCounterStr = "fs-rm";

    /// <summary>Переименование</summary>
    public long RenameCounter = 0;
    public const string RenameCounterStr = "fs-rename";

    /// <summary>Перемещение</summary>
    public long MoveCounter = 0;
    public const string MoveCounterStr = "fs-move";

    /// <summary>Копирование</summary>
    public long CopyCounter = 0;
    public const string CopyCounterStr = "fs-copy";

    /// <summary>Загрузка</summary>
    public long UploadCounter = 0;
    public const string UploadCounterStr = "fs-store";

    /// <summary>Сохранение на Диск</summary>
    public long TakeSomeonesFolderCounter = 0;
    public const string TakeSomeonesFolderCounterStr = "fs-store-download";

    /// <summary>Создание папки</summary>
    public long NewFolderCounter = 0;
    public const string NewFolderCounterStr = "fs-mkdir";

    /// <summary>Изменение файла</summary>
    /*
     * Не используется, не понятно что делать с этим счетчиком.
     * При удалении файла и тут же загрузке этого же файла,
     * счетчик увеличивается. Но если править какой-нибудь
     * документ онлайн на Диске, то счетчик не меняется.
     */
    //public long UpdateCounter = 0;
    //public const string UpdateCounterStr = "fs-store-update";

    /// <summary>Удаление в Корзину</summary>
    public long RemoveToTrashCounter = 0;
    public const string RemoveToTrashCounterStr = "fs-trash-append";

    /// <summary>Удаление в Корзину</summary>
    public long RestoreFromTrashCounter = 0;
    public const string RestoreFromTrashCounterStr = "fs-trash-restore";

    /// <summary>Удаление из Корзины</summary>
    public long TrashDropItemCounter = 0;
    public const string TrashDropItemCounterStr = "fs-trash-drop";

    /// <summary>Очистка Корзины</summary>
    public long TrashDropAllCounter = 0;
    public const string TrashDropAllCounterStr = "fs-trash-drop-all";

    public JournalCounters()
    {
    }

    public JournalCounters(YadJournalCountersV2 data)
    {
        long val;
        var dict = data.EventTypes;
        RemoveCounter = dict.TryGetValue(RemoveCounterStr, out val) ? val : 0;
        RenameCounter = dict.TryGetValue(RenameCounterStr, out val) ? val : 0;
        MoveCounter = dict.TryGetValue(MoveCounterStr, out val) ? val : 0;
        CopyCounter = dict.TryGetValue(CopyCounterStr, out val) ? val : 0;
        UploadCounter = dict.TryGetValue(UploadCounterStr, out val) ? val : 0;
        TakeSomeonesFolderCounter = dict.TryGetValue(TakeSomeonesFolderCounterStr, out val) ? val : 0;
        NewFolderCounter = dict.TryGetValue(NewFolderCounterStr, out val) ? val : 0;
        //UpdateCounter = dict.TryGetValue(UpdateCounterStr, out val) ? val : 0;
        RemoveToTrashCounter = dict.TryGetValue(RemoveToTrashCounterStr, out val) ? val : 0;
        RestoreFromTrashCounter = dict.TryGetValue(RestoreFromTrashCounterStr, out val) ? val : 0;
        TrashDropItemCounter = dict.TryGetValue(TrashDropItemCounterStr, out val) ? val : 0;
        TrashDropAllCounter = dict.TryGetValue(TrashDropAllCounterStr, out val) ? val : 0;
    }

    public void Increment(CounterOperation operation)
    {
        switch (operation)
        {
        case CounterOperation.None:
            break;
        case CounterOperation.Remove:
            RemoveCounter++;
            break;
        case CounterOperation.Rename:
            RenameCounter++;
            break;
        case CounterOperation.Move:
            MoveCounter++;
            break;
        case CounterOperation.Copy:
            CopyCounter++;
            break;
        case CounterOperation.Upload:
            UploadCounter++;
            break;
        case CounterOperation.TakeSomeonesFolder:
            TakeSomeonesFolderCounter++;
            break;
        case CounterOperation.NewFolder:
            NewFolderCounter++;
            break;
        //case CounterOperation.Update:
        //    UpdateCounter++;
        //    break;
        case CounterOperation.RemoveToTrash:
            RemoveToTrashCounter++;
            break;
        case CounterOperation.RestoreFromTrash:
            RestoreFromTrashCounter++;
            break;
        case CounterOperation.TrashDropItem:
            TrashDropItemCounter++;
            break;
        case CounterOperation.TrashDropAll:
            TrashDropAllCounter++;
            break;
        }
    }

    public void TakeMax(JournalCounters src)
    {
        RemoveCounter = Math.Max(RemoveCounter, src.RemoveCounter);
        RenameCounter = Math.Max(RenameCounter, src.RenameCounter);
        MoveCounter = Math.Max(MoveCounter, src.MoveCounter);
        CopyCounter = Math.Max(CopyCounter, src.CopyCounter);
        UploadCounter = Math.Max(UploadCounter, src.UploadCounter);
        TakeSomeonesFolderCounter = Math.Max(TakeSomeonesFolderCounter, src.TakeSomeonesFolderCounter);
        NewFolderCounter = Math.Max(NewFolderCounter, src.NewFolderCounter);
        //UpdateCounter = Math.Max(UpdateCounter, src.UpdateCounter);
        RemoveToTrashCounter = Math.Max(RemoveToTrashCounter, src.RemoveToTrashCounter);
        RestoreFromTrashCounter = Math.Max(RestoreFromTrashCounter, src.RestoreFromTrashCounter);
        TrashDropItemCounter = Math.Max(TrashDropItemCounter, src.TrashDropItemCounter);
        TrashDropAllCounter = Math.Max(TrashDropAllCounter, src.TrashDropAllCounter);
    }
};
