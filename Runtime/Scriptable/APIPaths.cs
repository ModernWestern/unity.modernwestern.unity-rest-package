using System;
using System.Linq;
using UnityEngine;

namespace UnityREST
{
    [CreateAssetMenu(fileName = "New APIPaths", menuName = "UnityREST/APIPaths", order = 1)]
    public class APIPaths : ScriptableObject
    {
        private const string Https = "https://";

        [SerializeField] private string domain;
        [SerializeField] private EndPoint[] endPoints;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(domain))
            {
                domain = Https;
            }
            else
            {
                if (!domain.StartsWith(Https))
                {
                    domain = Https + domain;
                }
            }
        }
#endif

        public string GetPath(string endPointName)
        {
            if (endPoints.FirstOrDefault(endPoint => endPoint.name == endPointName) is { } validEndPoint)
            {
                return $"{domain}/{validEndPoint.path}";
            }

            Debug.Log($"The EndPoint with [{endPointName}] does not exists");

            return null;
        }
    }

    [Serializable]
    public class EndPoint
    {
        public string name, path;
    }
}