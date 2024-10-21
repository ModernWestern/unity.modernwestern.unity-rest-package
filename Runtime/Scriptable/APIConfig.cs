using System;
using System.Linq;
using UnityEngine;

namespace UnityREST
{
    [CreateAssetMenu(fileName = "New APIConfig", menuName = "UnityREST/APIConfig", order = 1)]
    public class APIConfig : ScriptableObject
    {
        [SerializeField]
        private EnvironmentType targetEnvironment = EnvironmentType.Development;
        
        [Space, SerializeField]
        private Settings settings;
        
        [Space, SerializeField, TextArea]
        private string bearerToken;
        
        [Space, SerializeField]
        private Environment[] environments;

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

        public bool TryGetEnvironment(out string key)
        {
            if (environments.FirstOrDefault(env => env.environmentType.Equals(targetEnvironment)) is { } environment)
            {
                key = environment.key;
                return true;
            }

            key = string.Empty;
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
    
    [Serializable]
    public class Environment
    {
        public EnvironmentType environmentType = EnvironmentType.Development;
        public string key;
    }

    public enum EnvironmentType
    {
        Development = 0,
        Staging = 1,
        Production = 2,
        Release = 3
    }
}