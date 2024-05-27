using System;
using UnityEngine;
using System.Collections;

namespace UnityREST
{
    public abstract class APIManager : MonoBehaviour
    {
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