using System;
using System.Text;
using UnityEngine;
using UnityREST.Util;

namespace UnityREST
{
    public abstract class APIManager : MonoBehaviour
    {
        protected const string AuthTokenCache = "AuthorizationTokenCache";

        [Header("Overrides"), Space, SerializeField]
        protected APIConfig apiConfig;

        private static event Func<APIConfig> APIConfig;

        protected static WebTransport Transport { get; private set; }

        protected virtual void Awake()
        {
            Transport = new WebTransport(apiConfig ?? ScriptableObject.CreateInstance<APIConfig>());

            APIConfig += () => apiConfig;
        }

        protected static void SetAuth(string token)
        {
            if (!token.IsBase64Encoded())
            {
                var bytesToEncode = Encoding.UTF8.GetBytes(token);

                token = Convert.ToBase64String(bytesToEncode);
            }
            
            Transport.SetAuthToken(token);

            SaveToken(token);
        }

        protected static void SignOut()
        {
            Transport.SignOut();

            DeleteToken();
        }

        protected static string GetCachedToken()
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
            PlayerPrefs.SetString(AuthTokenCache, token);

            PlayerPrefs.Save();
        }

        private static void DeleteToken()
        {
            PlayerPrefs.DeleteKey(AuthTokenCache);

            PlayerPrefs.Save();
        }

        protected static void StartRequest(string endpointName, Action<string> path)
        {
            if (!APIConfig!().TryGetEnvironment(out var env))
            {
                return;
            }

            if (env.GetPath(endpointName) is var validPath)
            {
                path(validPath);
            }
        }
    }
}