namespace YaR.Clouds.Base.Requests.Types;

public class ActiveOperation
{
    /// <summary>
    /// Пользователь, запустивший операцию на сервере.
    /// </summary>
    public long Uid { get; set; }

    /// <summary>
    /// Полный путь к файлу/папке - источнику данных операции.
    /// </summary>
    public string SourcePath { get; set; }

    /// <summary>
    /// Полный путь к файлу/папке - месту назначения данных операции.
    /// </summary>
    public string TargetPath { get; set; }

    /// <summary>
    /// Тип операции, например 'move'.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Идентификатор операции, который можно передавать параметром в метод <see cref="YadWebRequestRepo.WaitForOperation"/>.
    /// </summary>
    public string OpId { get; set; }
}
