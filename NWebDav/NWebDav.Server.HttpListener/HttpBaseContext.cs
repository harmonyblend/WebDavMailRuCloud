using System.Net;
using System.Threading.Tasks;
using NWebDav.Server.Http;

namespace NWebDav.Server.HttpListener
{
    public abstract class HttpBaseContext : IHttpContext
    {
        private readonly HttpListenerResponse _response;

        protected HttpBaseContext(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Assign properties
            Request = new HttpRequest(request);
            response.SendChunked = true;
            Response = new HttpResponse(response);

            // Save response
            _response = response;
        }

        public IHttpRequest Request { get; }
        public IHttpResponse Response { get; }
        public abstract IHttpSession Session { get; }

        public Task CloseAsync()
        {
            if (_response != null)
            {
                // Prevent any exceptions
                try
                {
                    // At first send remaining buffered byte to client
                    _response.OutputStream?.Flush();
                }
                catch { }

                try
                {
                    // Then close the response
                    _response.Close();
                }
                catch { }
            }

            // Command completed synchronous
            return Task.FromResult(true);
        }
    }
}
