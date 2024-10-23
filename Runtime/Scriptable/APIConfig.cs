using System;
using System.Linq;
using UnityEngine;

namespace UnityREST
{
    using Editor;

    [CreateAssetMenu(fileName = "New APIConfig", menuName = "UnityREST/APIConfig", order = -1)]
    public class APIConfig : ScriptableObject
    {
        [ReadOnly, SerializeField]
        private EnvironmentType currentEnvironment = EnvironmentType.None;
        
        [Space, SerializeField]
        private Settings settings;
        
        [Space, SerializeField, TextArea]
        private string bearerToken;
        
        [Space, SerializeField]
        private Environment[] environments;

        #region Editor

        public Environment[] Environments => environments;

        public EnvironmentType TargetEnvironment
        {
            get => currentEnvironment;

            set => currentEnvironment = value;
        }

        #endregion
        
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

        public bool TryGetEnvironment(out Environment environment)
        {
            if (environments.FirstOrDefault(e => e.Type.Equals(currentEnvironment)) is { } env)
            {
                environment = env;
                
                return true;
            }

            environment = null;
            
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
        [SerializeField] private APIEnvironment environment;
        
        public string APIKey => environment.GetAPItKey;
        
        public EnvironmentType Type => environment.GetEnvironment;
        
        public string GetPath(string resourceName) => environment.GetPath(resourceName);
        
        public static implicit operator bool(Environment environment)
        {
            return environment.environment != null;
        }
    }

    public enum EnvironmentType
    {
        None = 0,
        Development = 1,
        [InspectorName("Staging | QA")] Staging = 2,
        Production = 3,
        Release = 4
    }
}