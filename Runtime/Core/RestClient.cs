using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityREST
{
    using Security;
    using Serialization;

    /// <summary>
    /// HTTP client bound to an <see cref="ApiConfig"/>. Requests are pooled, bodies live in reused
    /// buffers and responses are read from native memory. Safe to call from any thread: sending
    /// hops to the main thread, as UnityWebRequest requires.
    /// </summary>
    public sealed class RestClient
    {
        // Pooled HttpRequest instances kept for reuse.
        private const int MaxPooledRequests = 32;

        private const string AuthorizationHeader = "Authorization";

        private static readonly Stack<HttpRequest> Pool = new Stack<HttpRequest>(MaxPooledRequests);

        [ThreadStatic]
        private static StringBuilder _urlBuilder;

        private readonly List<HttpHeader> _headers = new List<HttpHeader>(4);
        private readonly List<IHttpInterceptor> _interceptors = new List<IHttpInterceptor>();
        private PinnedCertificateHandler _pinning;
        private ApiEnvironment _pinningEnvironment;

        public RestClient(ApiConfig config)
        {
            Config = config ? config : throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Client without an asset: a single environment pointing at <paramref name="baseUrl"/>.</summary>
        public RestClient(string baseUrl) : this(ApiConfig.Create(baseUrl))
        {
        }

        public ApiConfig Config { get; }

        public IJsonSerializer Serializer { get; set; } = NewtonsoftJsonSerializer.Default;

        /// <summary>
        /// Called once when a request gets 401. Refresh the credentials (e.g. <see cref="SetBearerToken"/>)
        /// and return true to resend the request.
        /// </summary>
        public Func<RestClient, CancellationToken, UniTask<bool>> RefreshAuthorization { get; set; }

#region Requests

        public HttpRequest Get(string path) => Request(HttpMethod.Get, path);

        public HttpRequest Post(string path) => Request(HttpMethod.Post, path);

        public HttpRequest Put(string path) => Request(HttpMethod.Put, path);

        public HttpRequest Patch(string path) => Request(HttpMethod.Patch, path);

        public HttpRequest Delete(string path) => Request(HttpMethod.Delete, path);

        public HttpRequest Head(string path) => Request(HttpMethod.Head, path);

        /// <param name="path">Relative to the active environment, or an absolute http(s) URL.</param>
        public HttpRequest Request(HttpMethod method, string path)
        {
            HttpRequest request;

            lock (Pool)
            {
                request = Pool.Count > 0 ? Pool.Pop() : new HttpRequest();
            }

            request.Init(this, method, path);
            return request;
        }

        /// <summary>Request for a named endpoint of the config.</summary>
        public HttpRequest Endpoint(string name)
        {
            if (!Config.TryGetEndpoint(name, out var endpoint))
            {
                throw new KeyNotFoundException($"Endpoint '{name}' is not defined in {Config.name}.");
            }

            return Request(endpoint.Method, endpoint.Path);
        }

#endregion

#region Headers and auth

        /// <summary>Header sent with every request of this client. Replaces a header with the same name.</summary>
        public void SetHeader(string name, string value)
        {
            RemoveHeader(name);
            _headers.Add(new HttpHeader(name, value));
        }

        public bool RemoveHeader(string name)
        {
            for (var i = 0; i < _headers.Count; i++)
            {
                if (string.Equals(_headers[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _headers.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void SetBearerToken(string token) => SetHeader(AuthorizationHeader, "Bearer " + token);

        public void SetBasicAuth(string user, string password) =>
            SetHeader(AuthorizationHeader, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)));

        public void ClearAuthorization() => RemoveHeader(AuthorizationHeader);

        public bool HasAuthorization
        {
            get
            {
                foreach (var header in _headers)
                {
                    if (string.Equals(header.Name, AuthorizationHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void AddInterceptor(IHttpInterceptor interceptor) => _interceptors.Add(interceptor);

        public bool RemoveInterceptor(IHttpInterceptor interceptor) => _interceptors.Remove(interceptor);

#endregion

#region Pipeline

        internal async UniTask<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var environment = Config.ActiveEnvironment;
                var url = BuildUrl(request, environment?.BaseUrl);

                if (Config.EnforceHttps && !IsSecureOrLoopback(url))
                {
                    var rejected = HttpResponse.Rejected(HttpError.InsecureUrl, url, "Plain http is blocked by ApiConfig.EnforceHttps. Use https:// or a loopback host.");
                    HttpLog.Write(Config.LogLevel, request, rejected);
                    return rejected;
                }

                request.FinishBody();

                var timeout = request.TimeoutOverride >= 0f ? request.TimeoutOverride : Config.TimeoutSeconds;
                var retry = Config.Retry;
                var maxRetries = request.RetriesOverride >= 0 ? request.RetriesOverride : request.IsStreaming ? 0 : retry.maxRetries;
                var refreshed = false;

                for (var attempt = 0;; attempt++)
                {
                    var response = await SendOnceAsync(request, url, environment, timeout, attempt, cancellationToken);

                    if (response.StatusCode == 401 && !refreshed && RefreshAuthorization != null && response.Error == HttpError.Http)
                    {
                        refreshed = true;

                        if (await RefreshAuthorization(this, cancellationToken))
                        {
                            response.Dispose();
                            attempt--;
                            continue;
                        }
                    }

                    if (attempt < maxRetries && retry.ShouldRetry(request.Method, response.Error, response.StatusCode))
                    {
                        var delay = retry.GetDelay(attempt, response.RetryAfterSeconds());
                        HttpLog.WriteRetry(Config.LogLevel, request, response, delay);
                        response.Dispose();

                        if (await UniTask.Delay(TimeSpan.FromSeconds(delay), true, PlayerLoopTiming.Update, cancellationToken).SuppressCancellationThrow())
                        {
                            return HttpResponse.Rejected(HttpError.Cancelled, url, "Request cancelled");
                        }

                        continue;
                    }

                    for (var i = 0; i < _interceptors.Count; i++)
                    {
                        _interceptors[i].OnReceive(response);
                    }

                    HttpLog.Write(Config.LogLevel, request, response);
                    return response;
                }
            }
            finally
            {
                Return(request);
            }
        }

        private async UniTask<HttpResponse> SendOnceAsync(HttpRequest request, string url, ApiEnvironment environment, float timeout, int attempt,
            CancellationToken cancellationToken)
        {
            var webRequest = new UnityWebRequest(url, request.Method.ToVerb(), request.CreateDownloadHandler(url), request.CreateUploadHandler());

            if (!request.HasAcceptHeader && webRequest.downloadHandler is DownloadHandlerBuffer)
            {
                webRequest.SetRequestHeader("Accept", ContentTypes.Json);
            }

            // Precedence: environment < client < request.
            if (environment != null)
            {
                if (!string.IsNullOrEmpty(environment.ApiKey) && !string.IsNullOrEmpty(environment.ApiKeyHeader))
                {
                    webRequest.SetRequestHeader(environment.ApiKeyHeader, environment.ApiKey);
                }

                var headers = environment.Headers;

                for (var i = 0; i < headers.Count; i++)
                {
                    webRequest.SetRequestHeader(headers[i].Name, headers[i].Value);
                }

#if !UNITY_WEBGL
                var pinning = GetPinning(environment);

                if (pinning != null)
                {
                    webRequest.certificateHandler = pinning;
                    webRequest.disposeCertificateHandlerOnDispose = false;
                }
#endif
            }

            for (var i = 0; i < _headers.Count; i++)
            {
                webRequest.SetRequestHeader(_headers[i].Name, _headers[i].Value);
            }

            request.ApplyHeaders(webRequest);

            for (var i = 0; i < _interceptors.Count; i++)
            {
                _interceptors[i].OnSend(webRequest);
            }

            var progress = request.ProgressHandler;
            var start = Time.realtimeSinceStartupAsDouble;
            var deadline = timeout > 0f ? start + timeout : double.MaxValue;
            var error = HttpError.None;
            var operation = webRequest.SendWebRequest();

            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    webRequest.Abort();
                    error = HttpError.Cancelled;
                    break;
                }

                if (Time.realtimeSinceStartupAsDouble > deadline)
                {
                    webRequest.Abort();
                    error = HttpError.Timeout;
                    break;
                }

                progress?.Report(webRequest.uploadProgress * 0.5f + webRequest.downloadProgress * 0.5f);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (error == HttpError.None)
            {
                error = webRequest.result switch
                {
                    UnityWebRequest.Result.Success => HttpError.None,
                    UnityWebRequest.Result.ProtocolError => HttpError.Http,
                    _ => HttpError.Network
                };

                if (error == HttpError.None)
                {
                    progress?.Report(1f);
                }
            }

            return new HttpResponse(webRequest, error, url, attempt + 1, Time.realtimeSinceStartupAsDouble - start);
        }

        private static string BuildUrl(HttpRequest request, string baseUrl)
        {
            var builder = _urlBuilder ??= new StringBuilder(256);
            builder.Clear();
            request.WriteUrl(builder, baseUrl);
            return builder.ToString();
        }

        private PinnedCertificateHandler GetPinning(ApiEnvironment environment)
        {
            if (environment.CertificatePins.Count == 0)
            {
                return null;
            }

            if (_pinningEnvironment != environment)
            {
                _pinning?.Dispose();
                _pinning = new PinnedCertificateHandler(environment.CertificatePins);
                _pinningEnvironment = environment;
            }

            return _pinning;
        }

        internal static bool IsSecureOrLoopback(string url)
        {
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var host = url.AsSpan(7);
            var end = host.IndexOfAny('/', '?', '#');
            host = end < 0 ? host : host.Slice(0, end);

            // Strip the port, keeping bracketed IPv6 literals intact.
            var colon = host.LastIndexOf(':');

            if (colon > 0 && host.LastIndexOf(']') < colon)
            {
                host = host.Slice(0, colon);
            }

            return host.Equals("localhost".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                   host.StartsWith("127.".AsSpan(), StringComparison.Ordinal) ||
                   host.Equals("[::1]".AsSpan(), StringComparison.Ordinal);
        }

        private static void Return(HttpRequest request)
        {
            request.Release();

            lock (Pool)
            {
                if (Pool.Count < MaxPooledRequests)
                {
                    Pool.Push(request);
                }
            }
        }

#endregion
    }
}
