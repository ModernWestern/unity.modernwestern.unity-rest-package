using System;
using System.Globalization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityREST
{
    using Serialization;
    using Streaming;

    /// <summary>
    /// Raw response. The body stays in native memory (<see cref="Data"/>) until <see cref="Dispose"/>;
    /// <see cref="Text"/> and <see cref="GetHeader"/> allocate strings on demand.
    /// </summary>
    public readonly struct HttpResponse : IDisposable
    {
        private readonly UnityWebRequest _request;
        private readonly string _message;

        internal HttpResponse(UnityWebRequest request, HttpError error, string url, int attempts, double elapsedSeconds)
        {
            _request = request;
            _message = null;
            Error = error;
            Url = url;
            Attempts = attempts;
            ElapsedSeconds = elapsedSeconds;
            StatusCode = request.responseCode;
        }

        private HttpResponse(HttpError error, string url, string message)
        {
            _request = null;
            _message = message;
            Error = error;
            Url = url;
            Attempts = 0;
            ElapsedSeconds = 0;
            StatusCode = 0;
        }

        internal static HttpResponse Rejected(HttpError error, string url, string message) => new HttpResponse(error, url, message);

        public HttpError Error { get; }

        public long StatusCode { get; }

        public string Url { get; }

        public int Attempts { get; }

        public double ElapsedSeconds { get; }

        public bool IsSuccess => Error == HttpError.None;

        /// <summary>The underlying request, for advanced use (null when blocked before sending).</summary>
        public UnityWebRequest WebRequest => _request;

        public string ErrorMessage
        {
            get
            {
                switch (Error)
                {
                    case HttpError.None:
                        return null;
                    case HttpError.Timeout:
                        return "Request timed out";
                    case HttpError.Cancelled:
                        return "Request cancelled";
                    default:
                        return _request?.error ?? _message;
                }
            }
        }

        /// <summary>Body bytes without copying. Empty for file, texture, audio and streamed responses.</summary>
        public unsafe ReadOnlySpan<byte> Data
        {
            get
            {
                if (!(_request?.downloadHandler is DownloadHandlerBuffer handler))
                {
                    return default;
                }

                var data = handler.nativeData;
                return data.Length == 0 ? default : new ReadOnlySpan<byte>(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data), data.Length);
            }
        }

        public string Text
        {
            get
            {
                switch (_request?.downloadHandler)
                {
                    case DownloadHandlerBuffer buffer:
                        return buffer.text;
                    case SseDownloadHandler stream:
                        return stream.CapturedText;
                    default:
                        return null;
                }
            }
        }

        public Texture2D Texture => _request?.downloadHandler is DownloadHandlerTexture handler ? handler.texture : null;

        public AudioClip AudioClip => _request?.downloadHandler is DownloadHandlerAudioClip handler ? handler.audioClip : null;

        public string GetHeader(string name) => _request?.GetResponseHeader(name);

        public T ReadJson<T>(IJsonSerializer serializer = null) => (serializer ?? NewtonsoftJsonSerializer.Default).Deserialize<T>(Data);

        /// <summary>Seconds from a numeric <c>Retry-After</c> header, or -1.</summary>
        internal float RetryAfterSeconds()
        {
            var value = GetHeader("Retry-After");
            return value != null && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : -1f;
        }

        public void Dispose() => _request?.Dispose();

        public override string ToString() => IsSuccess ? $"{StatusCode} {Url}" : $"{Error} {StatusCode} {Url}: {ErrorMessage}";
    }

    /// <summary>Typed outcome of <see cref="HttpRequest.SendAsync{T}"/>. Never throws for HTTP or network errors.</summary>
    public readonly struct HttpResult<T>
    {
        // Characters of an error body appended to ErrorMessage.
        private const int ErrorBodyLimit = 1024;

        public HttpResult(T value, HttpError error, long statusCode, string errorMessage)
        {
            Value = value;
            Error = error;
            StatusCode = statusCode;
            ErrorMessage = errorMessage;
        }

        public T Value { get; }

        public HttpError Error { get; }

        public long StatusCode { get; }

        /// <summary>Transport error plus the start of the server's error body, when there is one.</summary>
        public string ErrorMessage { get; }

        public bool IsSuccess => Error == HttpError.None;

        public bool TryGetValue(out T value)
        {
            value = Value;
            return IsSuccess;
        }

        public override string ToString() => IsSuccess ? $"{StatusCode} {Value}" : $"{Error} {StatusCode}: {ErrorMessage}";

        internal static HttpResult<T> From(in HttpResponse response, IJsonSerializer serializer)
        {
            if (!response.IsSuccess)
            {
                return new HttpResult<T>(default, response.Error, response.StatusCode, Describe(response));
            }

            if (typeof(T) == typeof(string))
            {
                return new HttpResult<T>((T)(object)response.Text, HttpError.None, response.StatusCode, null);
            }

            try
            {
                return new HttpResult<T>(serializer.Deserialize<T>(response.Data), HttpError.None, response.StatusCode, null);
            }
            catch (Exception exception)
            {
                return new HttpResult<T>(default, HttpError.Deserialization, response.StatusCode, $"Cannot read {typeof(T).Name}: {exception.Message}");
            }
        }

        private static string Describe(in HttpResponse response)
        {
            var body = response.Error == HttpError.Http ? response.Text : null;

            if (string.IsNullOrEmpty(body))
            {
                return response.ErrorMessage;
            }

            return body.Length > ErrorBodyLimit ? $"{response.ErrorMessage}: {body.Substring(0, ErrorBodyLimit)}…" : $"{response.ErrorMessage}: {body}";
        }
    }
}
