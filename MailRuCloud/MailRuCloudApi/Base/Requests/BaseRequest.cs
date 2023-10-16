﻿using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos;
using YaR.Clouds.Base.Repos.MailRuCloud;

namespace YaR.Clouds.Base.Requests
{
    internal abstract class BaseRequest<TConvert, T> where T : class
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(BaseRequest<TConvert, T>));

        protected readonly HttpCommonSettings Settings;
        protected readonly IAuth Auth;

        protected BaseRequest(HttpCommonSettings settings, IAuth auth)
        {
            Settings = settings;
            Auth = auth;
        }

        protected abstract string RelationalUri { get; }

        protected virtual HttpWebRequest CreateRequest(string baseDomain = null)
        {
            string domain = string.IsNullOrEmpty(baseDomain) ? ConstSettings.CloudDomain : baseDomain;
            var uriz = new Uri(new Uri(domain), RelationalUri);

            // suppressing escaping is obsolete and breaks, for example, Chinese names
            // url generated for %E2%80%8E and %E2%80%8F seems ok, but mail.ru replies error
            // https://stackoverflow.com/questions/20211496/uri-ignore-special-characters
            //var udriz = new Uri(new Uri(domain), RelationalUri, true);

#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var request = WebRequest.CreateHttp(uriz);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            request.Host = uriz.Host;
            request.Proxy = Settings.Proxy;
            request.CookieContainer = Auth?.Cookies;
            request.Method = "GET";
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "application/json";
            request.UserAgent = Settings.UserAgent;
            request.ContinueTimeout = Settings.CloudSettings.Wait100ContinueTimeoutSec * 1000;
            request.Timeout = Settings.CloudSettings.WaitResponseTimeoutSec * 1000;
            request.ReadWriteTimeout = Settings.CloudSettings.ReadWriteTimeoutSec * 1000;
            request.AllowWriteStreamBuffering = false;
            request.AllowReadStreamBuffering = true;
            request.SendChunked = false;
            request.ServicePoint.Expect100Continue = false;
            request.KeepAlive = true;

#if NET48
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
#else
            request.AutomaticDecompression = DecompressionMethods.All;
#endif

            //request.AllowReadStreamBuffering = true;

            return request;
        }

        protected virtual byte[] CreateHttpContent()
        {
            return null;
        }


        private const int MaxRetryCount = 10;

        public virtual async Task<T> MakeRequestAsync()
        {
            /*
             * По всей видимости, при нескольких последовательных обращениях к серверу
             * инфраструктура .NET повторно использует подключение после его закрытие по Close.
             * По этой причине может получиться так, что поток, который предназначен для чтения
             * данных с сервера, читает до того, как данные отправлены на сервер (как вариант),
             * или (как вариант) до того, как сервер начал отправку данных клиенту.
             * В таком случае поток читает 0 байт и выдает ошибку
             * throw new HttpIOException(HttpRequestError.ResponseEnded, SR.net_http_invalid_response_premature_eof);
             * см. код сборки .NET здесь:
             * https://github.com/dotnet/runtime/blob/139f45e56b85b7e643d7e4f81cb5cdf640cd9021/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/HttpConnection.cs#L625
             * Судя по сообщениям в интернете, такое поведение многим не нравится, но ситуация не исправляется.
             * Учитывая, что рядом с исключением устанавливается _canRetry = true,
             * можно предположить, что предполагается повторение обращения к серверу,
             * что мы тут и сделаем.
             */

            Stopwatch totalWatch = Stopwatch.StartNew();
            int retry = MaxRetryCount;
            while (true)
            {
                retry--;
                bool isRetryState = false;
                Stopwatch watch = Stopwatch.StartNew();
                HttpWebRequest httpRequest = null;

                try
                {
                    httpRequest = CreateRequest();

                    var requestContent = CreateHttpContent();
                    if (requestContent != null)
                    {
                        httpRequest.Method = "POST";
                        using Stream requestStream = await httpRequest.GetRequestStreamAsync().ConfigureAwait(false);

                        /*
                         * The debug add the following to a watch list:
                         *      System.Text.Encoding.UTF8.GetString(content)
                         */
#if NET48
                        await requestStream.WriteAsync(requestContent, 0, requestContent.Length).ConfigureAwait(false);
#else
                        await requestStream.WriteAsync(requestContent).ConfigureAwait(false);
#endif
                        await requestStream.FlushAsync().ConfigureAwait(false);
                        requestStream.Close();
                    }

                    /*
                     * Здесь в методе GetResponseAsync() иногда происходит исключение
                     * throw new HttpIOException(HttpRequestError.ResponseEnded, SR.net_http_invalid_response_premature_eof);
                     * Мы его отлавливаем и повторяем обращение к серверу.
                     */
                    using var response = (HttpWebResponse)await httpRequest.GetResponseAsync().ConfigureAwait(false);

                    if ((int)response.StatusCode >= 500)
                    {
                        throw new RequestException("Server fault")
                        {
                            StatusCode = response.StatusCode
                        };
                    }

                    RequestResponse<T> result;
                    using (var responseStream = response.GetResponseStream())
                    {
                        result = DeserializeMessage(response.Headers, Transport(responseStream));
                        responseStream.Close();
                    }

                    if (!result.Ok || response.StatusCode != HttpStatusCode.OK)
                    {
                        var exceptionMessage =
                            $"Request failed (status code {(int)response.StatusCode}): {result.Description}";
                        throw new RequestException(exceptionMessage)
                        {
                            StatusCode = response.StatusCode,
                            ResponseBody = string.Empty,
                            Description = result.Description,
                            ErrorCode = result.ErrorCode
                        };
                    }

                    response.Close();

                    return result.Result;
                }
                catch (WebException iex2) when (iex2?.InnerException is System.Net.Http.HttpRequestException iex1 &&
                                                iex1?.InnerException is IOException iex)
                {
                    /*
                     * Здесь мы ловим ошибку
                     * throw new HttpIOException(HttpRequestError.ResponseEnded, SR.net_http_invalid_response_premature_eof),
                     * которая здесь выглядит следующим образом:
                     * System.AggregateException: One or more errors occurred. (An error occurred while sending the request.)
                     *  ---> System.Net.WebException: An error occurred while sending the request.
                     *  ---> System.Net.Http.HttpRequestException: An error occurred while sending the request.
                     *  ---> System.IO.IOException: The response ended prematurely.
                     *         at System.Net.Http.HttpConnection.SendAsyncCore(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
                     *  --- End of inner exception stack trace ---
                     */
                    if (retry <= 0)
                    {
                        string msg = "The response ended prematurely, retry count completed";
#if DEBUG
                        Logger.Warn(msg);
#else
                        Logger.Debug(msg);
#endif
                        throw;
                    }
                    else
                    {
                        isRetryState = true;
                        string msg = "The response ended prematurely, retrying...";
#if DEBUG
                        Logger.Warn(msg);
#else
                        Logger.Debug(msg);
#endif
                    }
                }
                // ReSharper disable once RedundantCatchClause
#pragma warning disable 168
                catch (Exception ex)
#pragma warning restore 168
                {
                    throw;
                }
                finally
                {
                    watch.Stop();
                    string totalText = null;
                    if (!isRetryState && retry < MaxRetryCount - 1)
                    {
                        totalWatch.Stop();
                        totalText = $"({totalWatch.Elapsed.Milliseconds} ms of {MaxRetryCount - retry} retry laps)";
                    }
                    Logger.Debug($"HTTP:{httpRequest.Method}:{httpRequest.RequestUri.AbsoluteUri} " +
                        $"({watch.Elapsed.Milliseconds} ms){(isRetryState ? ", retrying" : totalText)}");
                }
            }
        }

        protected abstract TConvert Transport(Stream stream);

        protected abstract RequestResponse<T> DeserializeMessage(NameValueCollection responseHeaders, TConvert data);
    }
}
