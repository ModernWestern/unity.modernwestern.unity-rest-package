using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityREST
{
    [CreateAssetMenu(fileName = "New APIPaths", menuName = "UnityREST/APIPaths", order = 0)]
    public class APIPaths : ScriptableObject
    {
        private const string Https = "https://";
        private const string Http = "http://";

        [SerializeField] private Scheme scheme;
        [SerializeField] private string domain;
        [SerializeField] private EndPoint[] endPoints;

        public string GetPath(string endPointName)
        {
            if (endPoints.FirstOrDefault(endPoint => endPoint.name == endPointName) is { } validEndPoint)
            {
                return $"{(scheme == Scheme.Local ? Http : Https)}{domain}/{validEndPoint.path}";
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

    public enum Scheme
    {
        [InspectorName("http\t[Local]")] Local,
        [InspectorName("https\t[Remote]")] Remote,
    }
}