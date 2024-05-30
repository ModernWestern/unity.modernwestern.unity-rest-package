using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace UnityREST
{
    [CreateAssetMenu(fileName = "New APIConfig", menuName = "UnityREST/APIConfig", order = 1)]
    public class APIConfig : ScriptableObject
    {
        [SerializeField] private Settings settings;
        [SerializeField] private bool addCORS;
        [Space, SerializeField, TextArea] private string bearerToken;

        public int Timeout => settings.timeout;
        public int MaxRetryAttempts => settings.maxRetryAttempts;
        public float RetryDelay => settings.retryDelay;
        public string JsonContentType => settings.jsonContentType;
        public bool HasCORS => addCORS;

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

    public static class CORSHeaders
    {
        private static readonly (string key, string value) AllowOrigin = ("Access-Control-Allow-Origin", "*");
        private static readonly (string key, string value) AllowMethods = ("Access-Control-Allow-Origin", "GET,POST,PUT,PATCH,DELETE,HEAD,OPTIONS");
        private static readonly (string key, string value) AllowHeaders = ("Access-Control-Allow-Origin", "Content-Type, Origin, Accept, Authorization, Content-Length, X-Requested-With");

        public static (string key, string value)[] GetHeaders()
        {
            return new[] { AllowOrigin, AllowMethods, AllowHeaders };
        }
    }
}