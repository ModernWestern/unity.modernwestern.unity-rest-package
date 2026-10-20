using System;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityREST
{
    public enum HttpMethod : byte
    {
        Get,
        Post,
        Put,
        Patch,
        Delete,
        Head,
        Options
    }

    /// <summary>Why a request did not succeed. <see cref="None"/> means a 2xx response.</summary>
    public enum HttpError : byte
    {
        None,
        /// <summary>The server answered with a 4xx/5xx status.</summary>
        Http,
        /// <summary>DNS, connection, TLS or certificate pinning failure.</summary>
        Network,
        Timeout,
        Cancelled,
        /// <summary>The body could not be converted to the requested type.</summary>
        Deserialization,
        /// <summary>Blocked before sending: plain http while HTTPS is enforced.</summary>
        InsecureUrl
    }

    public enum HttpLogLevel : byte
    {
        None,
        Errors,
        Info,
        Verbose
    }

    public static class ContentTypes
    {
        public const string Json = "application/json";
        public const string TextUtf8 = "text/plain; charset=utf-8";
        public const string OctetStream = "application/octet-stream";
        public const string FormUrlEncoded = "application/x-www-form-urlencoded";
        public const string EventStream = "text/event-stream";
    }

    [Serializable]
    public struct HttpHeader
    {
        [SerializeField]
        private string name;

        [SerializeField]
        private string value;

        public HttpHeader(string name, string value)
        {
            this.name = name;
            this.value = value;
        }

        public string Name => name;

        public string Value => value;
    }

    /// <summary>Hook to inspect or sign every request (HMAC, tracing ids, custom auth) and observe responses.</summary>
    public interface IHttpInterceptor
    {
        void OnSend(UnityWebRequest request);

        void OnReceive(in HttpResponse response);
    }

    internal static class HttpMethodExtensions
    {
        private static readonly string[] Verbs = { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

        public static string ToVerb(this HttpMethod method) => Verbs[(int)method];

        /// <summary>Safe to repeat without side effects (RFC 9110 §9.2.2).</summary>
        public static bool IsIdempotent(this HttpMethod method) => method != HttpMethod.Post && method != HttpMethod.Patch;
    }

    /// <summary>Exponential backoff with jitter. Honours <c>Retry-After</c>.</summary>
    [Serializable]
    public struct RetryPolicy
    {
        // Longest wait accepted from a Retry-After header, in seconds.
        private const float MaxRetryAfter = 60f;

        [Range(0, 10), Tooltip("Extra attempts after the first one.")]
        public int maxRetries;

        [Min(0f), Tooltip("Delay before the first retry, in seconds. Doubles each attempt.")]
        public float baseDelay;

        [Min(0f), Tooltip("Upper bound for the backoff delay, in seconds.")]
        public float maxDelay;

        [Tooltip("Also retry POST/PATCH after network errors, timeouts and 5xx. Only enable when the API is idempotent (e.g. uses idempotency keys).")]
        public bool retryUnsafeMethods;

        public static RetryPolicy Default => new RetryPolicy { maxRetries = 2, baseDelay = 0.5f, maxDelay = 8f };

        public static RetryPolicy None => default;

        internal bool ShouldRetry(HttpMethod method, HttpError error, long status)
        {
            var safe = retryUnsafeMethods || method.IsIdempotent();

            switch (error)
            {
                case HttpError.Network:
                case HttpError.Timeout:
                    return safe;
                case HttpError.Http:
                    // 429 and 503 mean the server refused before doing any work: always safe.
                    if (status == 429 || status == 503)
                    {
                        return true;
                    }

                    return safe && (status == 408 || status == 500 || status == 502 || status == 504);
                default:
                    return false;
            }
        }

        internal float GetDelay(int attempt, float retryAfter)
        {
            if (retryAfter >= 0f)
            {
                return Mathf.Min(retryAfter, MaxRetryAfter);
            }

            var exponential = Mathf.Min(maxDelay, baseDelay * (1 << Mathf.Min(attempt, 16)));

            // Equal jitter: half fixed, half random, so clients do not retry in lockstep.
            return exponential * (0.5f + 0.5f * UnityEngine.Random.value);
        }
    }
}
