using System;
using UnityEngine;
using System.Collections;

namespace UnityREST
{
    public abstract class APIManager : MonoBehaviour
    {
        [SerializeField] protected APIConfig apiConfig;

        [SerializeField] protected APIPaths apiPaths;
        private static event Func<IEnumerator, Coroutine> CoroutineRunner;

        protected static WebTransport transport { get; private set; }

        private static APIPaths _paths;

        protected virtual void Awake()
        {
            CoroutineRunner += CoroutineRunnerHandler;

            transport = new WebTransport(apiConfig);

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