using System;
using UnityEngine;

namespace UnityREST
{
    [CreateAssetMenu(fileName = "New APIConfig", menuName = "UnityREST/APIConfig", order = 1)]
    public class APIConfig : ScriptableObject
    {
        [SerializeField] private Settings settings;
        [Space, SerializeField, TextArea] private string bearerToken;

        public int Timeout => settings.timeout;
        public int MaxRetryAttempts => settings.maxRetryAttempts;
        public float RetryDelay => settings.retryDelay;
        public string JsonContentType => settings.jsonContentType;

        public bool TryGetBearerToken(out string token)
        {
            if (!string.IsNullOrEmpty(bearerToken))
            {
                token = bearerToken;
                return true;
            }

            token = string.Empty;
            return false;
        }
    }

    [Serializable]
    public class Settings
    {
        public int timeout = 10;
        public int maxRetryAttempts = 3;
        public float retryDelay = 0.25f;
        public string jsonContentType = "application/json";
    }
}