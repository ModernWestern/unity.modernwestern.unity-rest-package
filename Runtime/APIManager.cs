using System;
using System.Text;
using UnityEngine;
using System.Collections;

namespace UnityREST
{
    public abstract class APIManager : MonoBehaviour
    {
        protected const string AuthTokenCache = "AuthorizationTokenCache";
        
        [Header("Overrides"), Space, SerializeField]
        protected APIConfig apiConfig;

        private static event Func<IEnumerator, Coroutine> CoroutineRunner;

        private static event Func<APIConfig> APIConfig;

        protected static WebTransport Transport { get; private set; }
        
        protected virtual void Awake()
        {
            Transport = new WebTransport(apiConfig ?? ScriptableObject.CreateInstance<APIConfig>());

            CoroutineRunner += StartCoroutine;

            APIConfig += () => apiConfig;
        }
        
        protected static void SetAuth(string token)
        {
            Transport.SetAuthToken(token);

            SaveToken(token);
        }

        protected static void SignOut()
        {
            Transport.SignOut();

            DeleteToken();
        }

        protected static string GetCacheToken()
        {
            if (PlayerPrefs.HasKey(AuthTokenCache))
            {
                var tokenBase64 = PlayerPrefs.GetString(AuthTokenCache, null);

                if (tokenBase64 != null)
                {
                    return Encoding.UTF8.GetString(Convert.FromBase64String(tokenBase64));
                }
            }

            return null;
        }

        private static void SaveToken(string token)
        {
            var bytesToEncode = Encoding.UTF8.GetBytes(token);

            var encodedToken = Convert.ToBase64String(bytesToEncode);

            PlayerPrefs.SetString(AuthTokenCache, encodedToken);

            PlayerPrefs.Save();
        }

        private static void DeleteToken()
        {
            PlayerPrefs.DeleteKey(AuthTokenCache);

            PlayerPrefs.Save();
        }
        
        protected static Coroutine StartRequest(string endpointName, Func<string, IEnumerator> path)
        {
            if (!APIConfig!().TryGetEnvironment(out var env))
            {
                return null;
            }
            
            if (env.GetPath(endpointName) is var validPath)
            {
                return CoroutineRunner!.Invoke(path(validPath));
            }

            return null;
        }
    }
}