using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests
{
    abstract class YadPostModelV2
    {
        public virtual object ToContent()
        {
            return null;
        }

        public string Name { get; set; }
    }

    class YadPostDataV2
    {
        public string Sk { get; set; }
        public string IdClient { get; set; }
        public YadPostModelV2 Model { get; set; }

        public byte[] CreateHttpContent()
        {
            var content = Model.ToContent();

            var data = new
            {
                sk = Sk,
                connection_id = IdClient,
                apiMethod = Model.Name,
                requestParams = content
            };
            var text = JsonConvert.SerializeObject(data);
            return System.Text.Encoding.UTF8.GetBytes(text);
        }
    }
    class YaDCommonV2Request : BaseRequestJson<YadMoveRequestData[]>
    {

        private readonly YadPostDataV2 _postData = new();

        private readonly List<object> _outData = new();

        private YadWebAuth YadAuth { get; }

        public YaDCommonV2Request(HttpCommonSettings settings, YadWebAuth auth) : base(settings, auth)
        {
            YadAuth = auth;
        }

        protected override HttpWebRequest CreateRequest(string baseDomain = null)
        {
            var request = base.CreateRequest("https://disk.yandex.ru");
            request.Referer = "https://disk.yandex.ru/client/disk";
            request.ContentType = "application/json";
            return request;
        }

        protected override byte[] CreateHttpContent()
        {
            _postData.Sk = YadAuth.DiskSk;            
            _postData.IdClient = YadAuth.Uuid;

            return _postData.CreateHttpContent();
        }

        public YaDCommonV2Request With<T, TOut>(T model, out TOut resOUt)
            where T : YadPostModelV2
            where TOut : YadResponseModel, new()
        {
            _postData.Model = model;
            _outData.Add(resOUt = new TOut());

            return this;
        }

        protected override string RelationalUri => "/models-v2?_m=" + _postData.Model.Name;

        protected override RequestResponse<YadMoveRequestData[]> DeserializeMessage(NameValueCollection responseHeaders, System.IO.Stream stream)
        {
            using var sr = new StreamReader(stream);

            string text = sr.ReadToEnd();

            var msg = new RequestResponse<YadMoveRequestData[]>
            {
                Ok = true,
                Result = JsonConvert.DeserializeObject<YadMoveRequestData[]>(text, new KnownYadModelConverter(_outData))
            };
            return msg;
        }
    }
}