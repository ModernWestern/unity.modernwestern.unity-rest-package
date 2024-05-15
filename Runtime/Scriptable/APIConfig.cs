using System;
using UnityEngine;

namespace UnityREST
{
    [CreateAssetMenu(fileName = "New APIConfig", menuName = "UnityREST/APIConfig", order = 1)]
    public class APIConfig : ScriptableObject
    {
        [SerializeField] private HttpSettings httpSettings;

        public int Timeout => httpSettings.timeout;
        public int MaxRetryAttempts => httpSettings.maxRetryAttempts;
        public float RetryDelay => httpSettings.retryDelay;
        public string JsonContentType => httpSettings.jsonContentType;
    }

    [Serializable]
    public class HttpSettings
    {
        public int timeout = 10;
        public int maxRetryAttempts = 3;
        public float retryDelay = 0.25f;
        public string jsonContentType = "application/json";
    }
}