using UnityEngine;

namespace UnityREST
{
    [CreateAssetMenu(fileName = "New APIConfig", menuName = "UnityREST/APIConfig", order = 0)]
    public class APIConfig : ScriptableObject
    {
        [SerializeField] private int timeout = 10;
        [SerializeField] private int maxRetryAttempts = 3;
        [SerializeField] private float retryDelay = 0.25f;
        [SerializeField] private string jsonContentType = "application/json";

        public int Timeout => timeout;
        public int MaxRetryAttempts => maxRetryAttempts;
        public float RetryDelay => retryDelay;
        public string JsonContentType => jsonContentType;
    }
}