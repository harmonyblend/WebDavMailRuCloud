using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    internal class YadActiveOperationsPostModel : YadPostModel
    {
        public YadActiveOperationsPostModel()
        {
            Name = "operationsActive";
        }

        public override IEnumerable<KeyValuePair<string, string>> ToKvp(int index)
        {
            foreach (var pair in base.ToKvp(index))
                yield return pair;
        }
    }

    internal class YadActiveOperationsData : YadModelDataBase
    {
        [JsonProperty("ycrid")]
        public string Ycrid { get; set; }

        [JsonProperty("ctime")]
        public long Ctime { get; set; }

        [JsonProperty("dtime")]
        public long Dtime { get; set; }

        [JsonProperty("mtime")]
        public long Mtime { get; set; }

        /// <summary>
        /// Это идентификатор для передачи параметром в метод <see cref="YadWebRequestRepo.WaitForOperation"/>
        /// </summary>
        [JsonProperty("id")]
        public string OpId { get; set; }

        /// <summary>
        /// ID пользователя, запустившего операцию
        /// </summary>
        [JsonProperty("uid")]
        public long Uid { get; set; }

        /// <summary>
        /// Тип операции, например 'move'
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Подтип операции, например, "disk_disk" или "disk" при upload
        /// </summary>
        [JsonProperty("subtype")]
        public string Subtype { get; set; }

        [JsonProperty("md5")]
        public string Md5 { get; set; }

        /// <summary>
        /// Пример: '1', то есть число 1
        /// </summary>
        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("data")]
        public YadActiveOperationsSubData Data { get; set; }
    }

    internal class YadActiveOperationsSubData : YadModelDataBase
    {
        /// <summary>
        /// Пример: "force": 0
        /// </summary>
        [JsonProperty("force")]
        public string Force { get; set; }

        /// <summary>
        /// Пример: "callback": ""
        /// </summary>
        [JsonProperty("callback")]
        public string Callback { get; set; }

        /// <summary>
        /// Пример: "connection_id": "9380702481698343978485"
        /// </summary>
        [JsonProperty("connection_id")]
        public string ConnectionId { get; set; }

        /// <summary>
        /// Пример: "skip_check_rights": false
        /// </summary>
        [JsonProperty("skip_check_rights")]
        public bool SkipCheckRights { get; set; }

        /// <summary>
        /// Пример: "source_resource_id": "12-it's-uid-34:2401665ecf5513706a85f868f67ca316da48a17f3e6e3d9cdd0544fb0120f30c"
        /// </summary>
        [JsonProperty("source_resource_id")]
        public string SourceResourceId { get; set; }

        /// <summary>
        /// Пример: "file_id": "2401665ecf5513706a85f868f67ca316da48a17f3e6e3d9cdd0544fb0120f30c"
        /// </summary>
        [JsonProperty("file_id")]
        public string FileId { get; set; }

        //"filedata": {},
        //"stages": {},

        [JsonProperty("at_version")]
        public long AtVersion { get; set; }

        /// <summary>
        /// Пример: "target": "12-it's-uid-34:/disk/destination-folder"
        /// </summary>
        [JsonProperty("target")]
        public string Target { get; set; }

        /// <summary>
        /// Пример: "source": "12-it's-uid-34:/disk/source-folder"
        /// </summary>
        [JsonProperty("source")]
        public string Source { get; set; }
    }

    internal class YadActiveOperationsParams
    {
    }

    internal struct YadOperation
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
}
