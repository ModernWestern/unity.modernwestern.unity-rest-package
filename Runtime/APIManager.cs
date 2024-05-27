using System;
using UnityEngine;
using System.Collections;
using System.Text;

namespace UnityREST
{
    public abstract class APIManager : MonoBehaviour
    {
        protected const string AuthTokenCache = "AuthorizationTokenCache";

        [SerializeField] protected APIPaths apiPaths;

        [Header("Overrides*"), Space, SerializeField]
        protected APIConfig apiConfig;

        private static event Func<IEnumerator, Coroutine> CoroutineRunner;

        protected static WebTransport transport { get; private set; }

        private static APIPaths _paths;

        protected virtual void Awake()
        {
            transport = new WebTransport(apiConfig ?? ScriptableObject.CreateInstance<APIConfig>());

            CoroutineRunner += CoroutineRunnerHandler;

            _paths = apiPaths;
        }

        protected static void SignIn(string token)
        {
            SaveToken(token);
            transport.SignIn(token);
        }

        protected static void SignIn()
        {
            transport.SignOut();

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
            if (_paths.GetPath(endpointName) is var validPath)
            {
                return CoroutineRunner?.Invoke(path(validPath));
            }

            return default;
        }

        #region Handlers

        private Coroutine CoroutineRunnerHandler(IEnumerator routine) => StartCoroutine(routine);

        #endregion
    }
}