using UnityEngine;

namespace UnityREST
{
    public abstract class APIManager : MonoBehaviour
    {
        [SerializeField] protected APIConfig apiConfig;

        [SerializeField] protected APIPaths apiPaths;

        protected WebTransport instance { get; private set; }

        protected virtual void Awake()
        {
            instance = new WebTransport(apiConfig);
        }
    }
}