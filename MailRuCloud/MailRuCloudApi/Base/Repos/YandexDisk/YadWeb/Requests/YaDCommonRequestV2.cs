using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests
{
    class YaDCommonRequestV2 : BaseRequestJson<YadModelV2>
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YaDCommonRequest));

        private YadModelV2 Model { get; set; }
        private YadPostDataV2 Request { get; }
        private YadWebAuth YadAuth { get; }

        public YaDCommonRequestV2(HttpCommonSettings settings, YadWebAuth auth) : base(settings, auth)
        {
            YadAuth = auth;
            Request = new YadPostDataV2(YadAuth.DiskSk, YadAuth.Uuid);
        }

        protected override HttpWebRequest CreateRequest(string baseDomain = null)
        {
            var request = base.CreateRequest("https://disk.yandex.ru");
            request.Referer = "https://disk.yandex.ru/client/disk";
            return request;
        }

        protected override byte[] CreateHttpContent() => Request.CreateHttpContent();

        public YaDCommonRequestV2 With<T>(T model)
            where T : YadModelV2
        {
            Model = model;
            Request.Request.APIMethod = model.APIMethod;
            Request.Request.RequestParameter = model.RequestParameter;

            return this;
        }

        public YaDCommonRequestV2 With<T>(T model, out T sameModel)
            where T : YadModelV2
        {
            Model = model;
            sameModel = model;
            Request.Request.APIMethod = model.APIMethod;
            Request.Request.RequestParameter = model.RequestParameter;

            return this;
        }

        protected override string RelationalUri => "/models-v2?_m=" + Request.Request.APIMethod;

        protected override RequestResponse<YadModelV2> DeserializeMessage(
            NameValueCollection responseHeaders, System.IO.Stream stream)
        {
            using var sr = new StreamReader(stream);

            string text = sr.ReadToEnd();
            //Logger.Debug(text);

            Model.Deserialize ??= new Action<string>((string text) =>
            {
//#if NET48
//                bool multiple = text.StartsWith("[");
//#else
//                bool multiple = text.StartsWith('[');
//#endif
                Model.ResultObject = null;

                if (text.StartsWith("[{\"error\""))
                {
                    Model.Errors = JsonConvert.DeserializeObject<List<YadResponseV2Error>>(text);
                }
                else
                if (text.StartsWith("{[\"object Object\"]"))
                {
                    var errorObject = JsonConvert.DeserializeObject<Dictionary<string,YadResponseV2Error>>(text);
                    Model.Errors = errorObject.Values.ToList();
                }
                else
                //if (multiple)
                //{
                //    var list = JsonConvert.DeserializeObject<List<YadOperationStatusResultV2>>(text);
                //    Model.ResultObject = [];
                //    int index = 0;
                //    foreach (var item in list)
                //    {
                //        Model.ResultObject.Add(index.ToString(), item);
                //        index++;
                //    }
                //}
                //else
                //if (text.StartsWith("{\""))
                //{
                //    Model.ResultObject = JsonConvert.DeserializeObject<Dictionary<string, YadOperationStatusResultV2>>(text);
                //}
                //else
                {
                    Model.ResultObject = JsonConvert.DeserializeObject(text, Model.ResultType);
                }

                //if ((Model.Errors?.Count ?? 0) == 0 &&
                //    (Model.ResultObject?.Values?.Any(x => x.Error is not null) ?? false))
                //{
                //    Model.Errors = Model.ResultObject.Values.Where(x => x.Error is not null).Cast<YadResponseV2Error>().ToList();
                //}
            });

            Model.Deserialize(text);
            Model.SourceJsonForDebug = text;

            if (Model.Errors is not null && Model.Errors.Count == 0)
                Model.Errors = null;

            var error = Model.Errors?.FirstOrDefault()?.Error;
            var msg = new RequestResponse<YadModelV2>
            {
                Ok = Model.Errors is null,
                Result = Model,
                Description = $"{Model.APIMethod} -> {error?.Title}",
                ErrorCode = error?.StatusCode,
            };

            return msg;
        }
    }
}
