﻿using System;
using System.Data;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using YaR.MailRuCloud.Api.Base.Requests;
using YaR.MailRuCloud.Api.Base.Requests.Types;
using YaR.MailRuCloud.Api.Extensions;

namespace YaR.MailRuCloud.Api.Base.Threads
{
    /// <summary>
    /// Upload stream based on HttpWebRequest
    /// </summary>
    /// <remarks>Suitable for .NET desktop, large file uploading does not work on .NET Core</remarks>
    abstract class UploadStreamHttpWebRequest : Stream
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(UploadStream));

        protected UploadStreamHttpWebRequest(string destinationPath, MailRuCloud cloud, long size)
        {
            _cloud = cloud;
            _file = new File(destinationPath, size, null);

            Initialize();
        }

        private void Initialize()
        {
            _requestTask = Task.Run(async () =>
            {
                try
                {
                    var shard = _cloud.CloudApi.Account.RequestRepo.GetShardInfo(ShardType.Upload).Result;
                    _request = _cloud.CloudApi.Account.RequestRepo.UploadRequest(shard, _file, null);

                    Logger.Debug($"HTTP:{_request.Method}:{_request.RequestUri.AbsoluteUri}");

                    if (_file.OriginalSize > 0)
                    {
                        using (var requeststream = await _request.GetRequestStreamAsync())
                        {
                            await _ringBuffer.CopyToAsync(requeststream);
                        }
                        var response = _request.GetResponse();
                        return (HttpWebResponse)response;
                    }
                    return null;
                }
                catch (Exception e)
                {
                    Logger.Error($"Uploading to {_file.FullPath} failed with {e.Message}");
                    throw;
                }
            });
        }

        public bool CheckHashes { get; set; } = true;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (CheckHashes)
                _sha1.Append(buffer, offset, count);

            _ringBuffer.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;

            try
            {
                _ringBuffer.Flush();

                using (var response = _requestTask.Result)
                {
                    if (response != null) // file length > 0
                    {
                        if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.OK)
                            throw new Exception("Cannot upload file, status " + response.StatusCode);

                        var ures = response.ReadAsText(_cloud.CloudApi.CancelToken)
                            .ToUploadPathResult();

                        if (ures.Size > 0 && _file.OriginalSize != ures.Size)
                            throw new Exception("Local and remote file size does not match");
                        _file.Hash = ures.Hash;

                        if (CheckHashes && _sha1.HashString != ures.Hash)
                            throw new HashMatchException(_sha1.HashString, ures.Hash);
                    }
                    _cloud.AddFileInCloud(_file, ConflictResolver.Rewrite)
                        .Result
                        .ThrowIf(r => !r.Success, r => new Exception("Cannot add file"));
                }
            }
            catch (Exception ex)
            {
                throw;
            }

            finally 
            {
                _ringBuffer?.Dispose();
                _sha1?.Dispose();
            }
        }

        private readonly MailRuCloud _cloud;
        private readonly File _file;

        private readonly MailRuSha1Hash _sha1 = new MailRuSha1Hash();
        private HttpWebRequest _request;
        private Task<HttpWebResponse> _requestTask;
        private readonly RingBufferedStream _ringBuffer = new RingBufferedStream(65536);

        //===========================================================================================================================

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => _file.OriginalSize;
        public override long Position { get; set; }

        public override void SetLength(long value)
        {
            _file.OriginalSize = value;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}