using System;
using System.Net;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebV2.Requests
{
    class UploadRequest
    {
        public UploadRequest(string shardUrl, File file, IAuth auth, HttpCommonSettings settings)
        {
            Request = CreateRequest(shardUrl, auth, settings.Proxy, file, settings.UserAgent);
        }

        public HttpWebRequest Request { get; }

        private static HttpWebRequest CreateRequest(string shardUrl, IAuth auth, IWebProxy proxy, File file, string userAgent)
        {
            var url = new Uri($"{shardUrl}?cloud_domain=2&{auth.Login}");

#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            request.Proxy = proxy;
            request.CookieContainer = auth.Cookies;
            request.Method = "PUT";
            request.ContentLength = file.OriginalSize; // + boundary.Start.LongLength + boundary.End.LongLength;
            request.Referer = $"{ConstSettings.CloudDomain}/home/{Uri.EscapeDataString(file.Path)}";
            request.Headers.Add("Origin", ConstSettings.CloudDomain);
            request.Host = url.Host;
            //request.ContentType = $"multipart/form-data; boundary=----{boundary.Guid}";
            request.Accept = "*/*";
            request.UserAgent = userAgent;
            request.AllowWriteStreamBuffering = false;
            return request;
        }

        public static implicit operator HttpWebRequest(UploadRequest v)
        {
            return v.Request;
        }
    }
}
