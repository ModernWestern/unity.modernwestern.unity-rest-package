using System.Linq;
using UnityEngine;

namespace UnityREST
{
    using Editor;

    /*
       scheme    whole domain
       ┌───┐   ┌─────────────┐
       https://www.google.com/
       └───┘   └─────────────┘
    */
    
    [CreateAssetMenu(fileName = "New APIEnvironment", menuName = "UnityREST/APIEnvironment", order = 0)]
    public class APIEnvironment : ScriptableObject
    {
        [SerializeField]
        private Scheme scheme;
        
        [HideEnum("None"), SerializeField]
        private EnvironmentType environment = EnvironmentType.Development;
        
        [SerializeField]
        private string domain, apiKey;
        
        [SerializeField]
        private APIPaths apiPaths;
        
        public string GetAPItKey => apiKey;
        
        public EnvironmentType GetEnvironment => environment;
        
        public string GetPath(string resourceName)
        {
            if (apiPaths.GetResources.FirstOrDefault(r => r.name == resourceName) is { } resource)
            {
                return $"{scheme}://{domain}/{resource.path}";
            }
#if UNITY_EDITOR
            
            Debug.Log($"The EndPoint with [{resourceName}] does not exists");
#endif
            return null;
        }
    }
    
    public enum Scheme
    {
        [InspectorName("http")] http,
        [InspectorName("https\t[Secure]")] https,
    }
}