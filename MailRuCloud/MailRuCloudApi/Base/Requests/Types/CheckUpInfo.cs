using System.Collections.Generic;

namespace YaR.Clouds.Base.Requests.Types;

public class CheckUpInfo
{
    public struct CheckInfo
    {
        public long FilesCount;
        public long Free;
        public long Trash;
    };

    /// <summary>
    /// Набор информации для проверки изменений на диске, минуя данный сервис.
    /// </summary>
    public CheckInfo AccountInfo { get; set; }

    /// <summary>
    /// Список активных операций на сервере.
    /// </summary>
    public List<ActiveOperation> ActiveOperations { get; set; }
}
