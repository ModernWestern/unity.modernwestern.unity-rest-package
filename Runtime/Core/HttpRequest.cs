using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityREST
{
    using Buffers;
    using Serialization;
    using Streaming;

    /// <summary>
    /// Fluent, pooled request builder. Get one from <see cref="RestClient"/>, configure it and await
    /// one of the <c>SendAsync</c> methods. After sending, the instance goes back to the pool: do not
    /// keep or reuse it.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = await api.Get("users/{id}").Path("id", 42).Query("expand", "teams").SendAsync&lt;User&gt;();
    /// if (result.IsSuccess) Debug.Log(result.Value.Name);
    /// </code>
    /// </example>
    public sealed class HttpRequest
    {
        private enum BodyKind : byte
        {
            None,
            Raw,
            Form,
            Multipart
        }

        private enum DownloadKind : byte
        {
            Buffer,
            None,
            File,
            Texture,
            Audio,
            EventStream
        }

        // Receive buffer for streamed responses; Unity fills it on every chunk.
        private const int StreamBufferSize = 16 * 1024;

        private readonly ParamList _pathParams = new ParamList();
        private readonly ParamList _query = new ParamList();
        private readonly List<HttpHeader> _headers = new List<HttpHeader>(4);
        private readonly PooledBufferWriter _body = new PooledBufferWriter();

        private RestClient _client;
        private string _target;
        private BodyKind _bodyKind;
        private bool _bodyFinished;
        private string _contentType;
        private string _boundary;
        private string _multipartContentType;
        private DownloadKind _downloadKind;
        private string _filePath;
        private bool _flag;
        private AudioType _audioType;
        private ISseListener _listener;
        private SseParser _parser;
        private byte[] _streamBuffer;
        private byte[] _streamCapture;
        private bool _inUse;

        public HttpMethod Method { get; private set; }

        internal float TimeoutOverride { get; private set; }

        internal int RetriesOverride { get; private set; }

        internal IProgress<float> ProgressHandler { get; private set; }

        internal bool IsStreaming => _downloadKind == DownloadKind.EventStream;

        internal ReadOnlySpan<byte> BodySpan => _body.WrittenSpan;

        internal bool HasAcceptHeader { get; private set; }

#region Lifecycle

        internal void Init(RestClient client, HttpMethod method, string target)
        {
            _client = client;
            _target = target ?? string.Empty;
            Method = method;
            TimeoutOverride = -1f;
            RetriesOverride = -1;
            _downloadKind = method == HttpMethod.Head ? DownloadKind.None : DownloadKind.Buffer;
            _inUse = true;
        }

        internal void Release()
        {
            _client = null;
            _target = null;
            _pathParams.Clear();
            _query.Clear();
            _headers.Clear();
            _body.Clear();
            _bodyKind = BodyKind.None;
            _bodyFinished = false;
            _contentType = null;
            _filePath = null;
            _listener = null;
            ProgressHandler = null;
            HasAcceptHeader = false;
            _inUse = false;
        }

        private HttpRequest Check()
        {
            if (!_inUse)
            {
                throw new InvalidOperationException("This HttpRequest was already sent and returned to the pool. Create a new one from the RestClient.");
            }

            return this;
        }

#endregion

#region Path, query and headers

        /// <summary>Replaces <c>{name}</c> in the path with the escaped value.</summary>
        public HttpRequest Path(string name, string value)
        {
            Check()._pathParams.Add(name, value.AsSpan());
            return this;
        }

        public HttpRequest Path(string name, long value)
        {
            Check()._pathParams.Add(name, value);
            return this;
        }

        public HttpRequest Query(string key, string value)
        {
            Check()._query.Add(key, value.AsSpan());
            return this;
        }

        public HttpRequest Query(string key, long value)
        {
            Check()._query.Add(key, value);
            return this;
        }

        public HttpRequest Query(string key, double value)
        {
            Check()._query.Add(key, value);
            return this;
        }

        public HttpRequest Query(string key, float value)
        {
            Check()._query.Add(key, value);
            return this;
        }

        public HttpRequest Query(string key, bool value)
        {
            Check()._query.Add(key, value);
            return this;
        }

        /// <summary>Adds a header for this request only; overrides client and environment headers with the same name.</summary>
        public HttpRequest Header(string name, string value)
        {
            Check()._headers.Add(new HttpHeader(name, value));
            HasAcceptHeader |= string.Equals(name, "Accept", StringComparison.OrdinalIgnoreCase);
            return this;
        }

        public HttpRequest Accept(string mediaType) => Header("Accept", mediaType);

#endregion

#region Body

        /// <summary>Serializes <paramref name="value"/> with the client serializer.</summary>
        public HttpRequest Json<T>(T value)
        {
            BeginBody(BodyKind.Raw, ContentTypes.Json);
            _client.Serializer.Serialize(value, _body);
            return this;
        }

        /// <summary>Writes the JSON body by hand. Pass data through <paramref name="state"/> (not a closure) to avoid allocations.</summary>
        public HttpRequest Json<TState>(TState state, Action<JsonWriter, TState> write)
        {
            BeginBody(BodyKind.Raw, ContentTypes.Json);
            NewtonsoftJsonSerializer.Write(_body, state, write);
            return this;
        }

        public HttpRequest Text(string text, string contentType = ContentTypes.TextUtf8)
        {
            BeginBody(BodyKind.Raw, contentType);
            _body.WriteUtf8(text.AsSpan());
            return this;
        }

        public HttpRequest Bytes(ReadOnlySpan<byte> data, string contentType = ContentTypes.OctetStream)
        {
            BeginBody(BodyKind.Raw, contentType);
            _body.Write(data);
            return this;
        }

        /// <summary>Adds an <c>application/x-www-form-urlencoded</c> field.</summary>
        public HttpRequest Form(string key, string value)
        {
            var first = _bodyKind != BodyKind.Form;
            BeginBody(BodyKind.Form, ContentTypes.FormUrlEncoded);

            if (!first)
            {
                _body.WriteByte((byte)'&');
            }

            UrlEncoding.Escape(key.AsSpan(), _body);
            _body.WriteByte((byte)'=');
            UrlEncoding.Escape(value.AsSpan(), _body);
            return this;
        }

        /// <summary>Adds a <c>multipart/form-data</c> text field.</summary>
        public HttpRequest Multipart(string name, string value)
        {
            BeginPart(name, null, null);
            _body.WriteUtf8(value.AsSpan());
            _body.WriteUtf8("\r\n".AsSpan());
            return this;
        }

        /// <summary>Adds a <c>multipart/form-data</c> file.</summary>
        public HttpRequest Multipart(string name, ReadOnlySpan<byte> data, string fileName, string contentType = ContentTypes.OctetStream)
        {
            BeginPart(name, fileName, contentType);
            _body.Write(data);
            _body.WriteUtf8("\r\n".AsSpan());
            return this;
        }

        private void BeginBody(BodyKind kind, string contentType)
        {
            Check();

            if (_bodyKind != kind)
            {
                if (_bodyKind != BodyKind.None)
                {
                    throw new InvalidOperationException($"The request already has a {_bodyKind} body.");
                }

                _bodyKind = kind;
            }
            else if (kind == BodyKind.Raw)
            {
                _body.Clear();
            }

            _contentType = contentType;
        }

        private void BeginPart(string name, string fileName, string contentType)
        {
            if (_boundary == null)
            {
                // Created once per pooled instance.
                _boundary = "----UnityREST" + Guid.NewGuid().ToString("N");
                _multipartContentType = "multipart/form-data; boundary=" + _boundary;
            }

            BeginBody(BodyKind.Multipart, _multipartContentType);

            _body.WriteUtf8("--".AsSpan());
            _body.WriteUtf8(_boundary.AsSpan());
            _body.WriteUtf8("\r\nContent-Disposition: form-data; name=\"".AsSpan());
            _body.WriteUtf8(name.AsSpan());
            _body.WriteByte((byte)'"');

            if (fileName != null)
            {
                _body.WriteUtf8("; filename=\"".AsSpan());
                _body.WriteUtf8(fileName.AsSpan());
                _body.WriteUtf8("\"\r\nContent-Type: ".AsSpan());
                _body.WriteUtf8(contentType.AsSpan());
            }

            _body.WriteUtf8("\r\n\r\n".AsSpan());
        }

        internal void FinishBody()
        {
            if (_bodyFinished)
            {
                return;
            }

            _bodyFinished = true;

            if (_bodyKind == BodyKind.Multipart)
            {
                _body.WriteUtf8("--".AsSpan());
                _body.WriteUtf8(_boundary.AsSpan());
                _body.WriteUtf8("--\r\n".AsSpan());
            }
        }

#endregion

#region Response handling and options

        /// <summary>Streams the body straight to disk instead of memory.</summary>
        public HttpRequest ToFile(string path, bool append = false)
        {
            _downloadKind = DownloadKind.File;
            _filePath = path;
            _flag = append;
            return Check();
        }

        /// <summary>Decodes the body as a texture; read it from <see cref="HttpResponse.Texture"/>.</summary>
        public HttpRequest AsTexture(bool readable = false)
        {
            _downloadKind = DownloadKind.Texture;
            _flag = readable;
            return Check();
        }

        /// <summary>Decodes the body as audio; read it from <see cref="HttpResponse.AudioClip"/>.</summary>
        public HttpRequest AsAudio(AudioType audioType, bool streamAudio = false)
        {
            _downloadKind = DownloadKind.Audio;
            _audioType = audioType;
            _flag = streamAudio;
            return Check();
        }

        /// <summary>Parses a <c>text/event-stream</c> body as it arrives. Streamed requests are not retried.</summary>
        public HttpRequest Stream(ISseListener listener)
        {
            _downloadKind = DownloadKind.EventStream;
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));

            if (!HasAcceptHeader)
            {
                Header("Accept", ContentTypes.EventStream);
            }

            return Check();
        }

        /// <summary>Seconds per attempt; overrides <see cref="ApiConfig.TimeoutSeconds"/>. 0 disables it.</summary>
        public HttpRequest Timeout(float seconds)
        {
            TimeoutOverride = Mathf.Max(0f, seconds);
            return Check();
        }

        /// <summary>Overrides the retry count of the config's <see cref="RetryPolicy"/>.</summary>
        public HttpRequest Retries(int maxRetries)
        {
            RetriesOverride = Mathf.Max(0, maxRetries);
            return Check();
        }

        /// <summary>Reports upload progress (0-0.5) then download progress (0.5-1).</summary>
        public HttpRequest Progress(IProgress<float> progress)
        {
            ProgressHandler = progress;
            return Check();
        }

#endregion

#region Send

        /// <summary>Sends and returns the raw response. Dispose it (<c>using var</c>) to release native memory.</summary>
        public UniTask<HttpResponse> SendAsync(CancellationToken cancellationToken = default)
        {
            Check();
            return _client.SendAsync(this, cancellationToken);
        }

        /// <summary>Sends and converts the body: JSON for any type, raw text for <see cref="string"/>.</summary>
        public async UniTask<HttpResult<T>> SendAsync<T>(CancellationToken cancellationToken = default)
        {
            Check();
            var serializer = _client.Serializer;

            using (var response = await _client.SendAsync(this, cancellationToken))
            {
                return HttpResult<T>.From(response, serializer);
            }
        }

#endregion

#region Transport

        internal void WriteUrl(StringBuilder builder, string baseUrl)
        {
            var target = _target.AsSpan();

            if (!IsAbsolute(target) && !string.IsNullOrEmpty(baseUrl))
            {
                var root = baseUrl.AsSpan();

                if (root[root.Length - 1] == '/')
                {
                    root = root.Slice(0, root.Length - 1);
                }

                builder.Append(root);

                if (target.Length > 0 && target[0] != '/' && target[0] != '?')
                {
                    builder.Append('/');
                }
            }

            var inQuery = false;

            for (var i = 0; i < target.Length; i++)
            {
                var c = target[i];
                inQuery |= c == '?';

                if (c == '{' && !inQuery)
                {
                    var close = target.Slice(i + 1).IndexOf('}');

                    if (close > 0)
                    {
                        var index = _pathParams.IndexOf(target.Slice(i + 1, close));

                        if (index >= 0)
                        {
                            UrlEncoding.Escape(_pathParams.ValueAt(index), builder);
                            i += close + 1;
                            continue;
                        }
                    }
                }

                builder.Append(c);
            }

            for (var i = 0; i < _query.Count; i++)
            {
                builder.Append(i == 0 && !inQuery ? '?' : '&');
                UrlEncoding.Escape(_query.KeyAt(i).AsSpan(), builder);
                builder.Append('=');
                UrlEncoding.Escape(_query.ValueAt(i), builder);
            }
        }

        internal void ApplyHeaders(UnityWebRequest request)
        {
            for (var i = 0; i < _headers.Count; i++)
            {
                request.SetRequestHeader(_headers[i].Name, _headers[i].Value);
            }
        }

        internal UploadHandler CreateUploadHandler()
        {
            if (_bodyKind == BodyKind.None)
            {
                return null;
            }

            var length = _body.WrittenCount;
            var data = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            if (length > 0)
            {
                NativeArray<byte>.Copy(_body.RawBuffer, 0, data, 0, length);
            }

            // The handler owns and frees the native copy.
            return new UploadHandlerRaw(data, true) { contentType = _contentType };
        }

        internal DownloadHandler CreateDownloadHandler(string url)
        {
            switch (_downloadKind)
            {
                case DownloadKind.None:
                    return null;
                case DownloadKind.File:
                    return new DownloadHandlerFile(_filePath, _flag) { removeFileOnAbort = true };
                case DownloadKind.Texture:
                    return new DownloadHandlerTexture(_flag);
                case DownloadKind.Audio:
                    return new DownloadHandlerAudioClip(url, _audioType) { streamAudio = _flag };
                case DownloadKind.EventStream:
                    _parser ??= new SseParser();
                    _streamBuffer ??= new byte[StreamBufferSize];
                    _streamCapture ??= new byte[SseDownloadHandler.CaptureSize];
                    return new SseDownloadHandler(_streamBuffer, _streamCapture, _parser, _listener);
                default:
                    return new DownloadHandlerBuffer();
            }
        }

        private static bool IsAbsolute(ReadOnlySpan<char> target)
        {
            return target.StartsWith("https://".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                   target.StartsWith("http://".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

#endregion
    }
}
