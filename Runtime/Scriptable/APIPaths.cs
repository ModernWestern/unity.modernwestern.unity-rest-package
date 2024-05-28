using System;
using System.Linq;
using UnityEngine;

namespace UnityREST
{
    /*
       scheme    whole domain
       ┌───┐   ┌─────────────┐
       https://www.google.com/
       └───┘   └─────────────┘
    */

    [CreateAssetMenu(fileName = "New APIPaths", menuName = "UnityREST/APIPaths", order = 0)]
    public class APIPaths : ScriptableObject
    {
        [SerializeField] private Scheme scheme;
        [SerializeField] private string domain;
        [SerializeField] private Resource[] resources;

        public string GetPath(string resourceName)
        {
            if (resources.FirstOrDefault(r => r.name == resourceName) is { } resource)
            {
                return $"{scheme}://{domain}/{resource.path}";
            }
#if UNITY_EDITOR
            
            Debug.Log($"The EndPoint with [{resourceName}] does not exists");
#endif
            return null;
        }
    }

    [Serializable]
    public class Resource
    {
        public string name, path;
    }

    public enum Scheme
    {
        [InspectorName("http")] http,
        [InspectorName("https\t[Secure]")] https,
    }
}