using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityREST
{
    /// <summary>
    /// One asset describing an API: environments (base URL, keys, headers, certificate pins),
    /// named endpoints and transport settings. <see cref="Client"/> gives a ready-to-use client.
    /// </summary>
    [CreateAssetMenu(fileName = "ApiConfig", menuName = "UnityREST/Api Config", order = 0)]
    public sealed class ApiConfig : ScriptableObject
    {
        [SerializeField]
        private int activeEnvironment;

        [SerializeField]
        private ApiEnvironment[] environments = { new ApiEnvironment("Development", "https://localhost") };

        [SerializeField]
        private Endpoint[] endpoints = Array.Empty<Endpoint>();

        [SerializeField, Min(0f), Tooltip("Seconds before a single attempt is aborted. 0 disables the timeout.")]
        private float timeoutSeconds = 30f;

        [SerializeField]
        private RetryPolicy retry = RetryPolicy.Default;

        [SerializeField, Tooltip("Reject plain http:// URLs (loopback hosts are always allowed for local development).")]
        private bool enforceHttps = true;

        [SerializeField]
        private HttpLogLevel logLevel = HttpLogLevel.Errors;

        [NonSerialized]
        private Dictionary<string, Endpoint> _endpointIndex;

        [NonSerialized]
        private RestClient _client;

        /// <summary>Shared client bound to this config. Environment switches apply to it immediately.</summary>
        public RestClient Client => _client ??= new RestClient(this);

        public IReadOnlyList<ApiEnvironment> Environments => environments;

        public IReadOnlyList<Endpoint> Endpoints => endpoints;

        public ApiEnvironment ActiveEnvironment =>
            environments != null && (uint)activeEnvironment < (uint)environments.Length ? environments[activeEnvironment] : null;

        public int ActiveEnvironmentIndex
        {
            get => activeEnvironment;
            set => activeEnvironment = environments == null || environments.Length == 0 ? 0 : Mathf.Clamp(value, 0, environments.Length - 1);
        }

        public float TimeoutSeconds
        {
            get => timeoutSeconds;
            set => timeoutSeconds = Mathf.Max(0f, value);
        }

        public RetryPolicy Retry
        {
            get => retry;
            set => retry = value;
        }

        public bool EnforceHttps
        {
            get => enforceHttps;
            set => enforceHttps = value;
        }

        public HttpLogLevel LogLevel
        {
            get => logLevel;
            set => logLevel = value;
        }

        /// <summary>Selects the environment whose name matches (case-insensitive).</summary>
        public bool SetActiveEnvironment(string environmentName)
        {
            for (var i = 0; i < environments.Length; i++)
            {
                if (string.Equals(environments[i].Name, environmentName, StringComparison.OrdinalIgnoreCase))
                {
                    activeEnvironment = i;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetEndpoint(string endpointName, out Endpoint endpoint)
        {
            if (_endpointIndex == null)
            {
                _endpointIndex = new Dictionary<string, Endpoint>(endpoints.Length, StringComparer.Ordinal);

                foreach (var e in endpoints)
                {
                    if (!string.IsNullOrEmpty(e.Name))
                    {
                        _endpointIndex[e.Name] = e;
                    }
                }
            }

            endpoint = null;
            return endpointName != null && _endpointIndex.TryGetValue(endpointName, out endpoint);
        }

        /// <summary>In-memory config for code-only setups.</summary>
        public static ApiConfig Create(string baseUrl, params Endpoint[] endpoints)
        {
            var config = CreateInstance<ApiConfig>();
            config.hideFlags = HideFlags.DontSave;
            config.environments = new[] { new ApiEnvironment("Default", baseUrl) };
            config.endpoints = endpoints ?? Array.Empty<Endpoint>();
            return config;
        }

        private void OnValidate()
        {
            _endpointIndex = null;
            ActiveEnvironmentIndex = activeEnvironment;
        }
    }

    [Serializable]
    public sealed class ApiEnvironment
    {
        [SerializeField]
        private string name;

        [SerializeField, Tooltip("Scheme and host, optionally with a base path: https://api.example.com/v1")]
        private string baseUrl;

        [SerializeField]
        private string apiKeyHeader = "x-api-key";

        [SerializeField, Secret, Tooltip("Sent in the header above. Anything stored in an asset ships with the build: keep real secrets on a server.")]
        private string apiKey;

        [SerializeField]
        private HttpHeader[] headers = Array.Empty<HttpHeader>();

        [SerializeField, Tooltip("Base64 SHA-256 of the server's SubjectPublicKeyInfo (leaf certificate). Add a backup pin. Empty disables pinning.")]
        private string[] certificatePins = Array.Empty<string>();

        public ApiEnvironment()
        {
        }

        public ApiEnvironment(string name, string baseUrl)
        {
            this.name = name;
            this.baseUrl = baseUrl;
        }

        public string Name => name;

        public string BaseUrl
        {
            get => baseUrl;
            set => baseUrl = value;
        }

        public string ApiKeyHeader => apiKeyHeader;

        public string ApiKey
        {
            get => apiKey;
            set => apiKey = value;
        }

        public IReadOnlyList<HttpHeader> Headers => headers;

        public IReadOnlyList<string> CertificatePins => certificatePins;

        public bool IsSecure => baseUrl != null && baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public sealed class Endpoint
    {
        [SerializeField]
        private string name;

        [SerializeField]
        private HttpMethod method;

        [SerializeField, Tooltip("Relative to the environment base URL. Use {placeholders} for path parameters: users/{id}")]
        private string path;

        public Endpoint()
        {
        }

        public Endpoint(string name, HttpMethod method, string path)
        {
            this.name = name;
            this.method = method;
            this.path = path;
        }

        public string Name => name;

        public HttpMethod Method => method;

        public string Path => path;
    }

    /// <summary>Inspector-friendly reference to a named endpoint of an <see cref="ApiConfig"/>.</summary>
    [Serializable]
    public struct EndpointRef
    {
        [SerializeField]
        private ApiConfig config;

        [SerializeField]
        private string name;

        public ApiConfig Config => config;

        public string Name => name;

        public bool IsValid => config != null && config.TryGetEndpoint(name, out _);

        public HttpRequest Request() => config.Client.Endpoint(name);
    }

    /// <summary>Masks a string field in the inspector, with a reveal toggle.</summary>
    public sealed class SecretAttribute : PropertyAttribute
    {
    }
}
