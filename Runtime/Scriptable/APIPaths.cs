using System;
using UnityEngine;

namespace UnityREST
{
    [CreateAssetMenu(fileName = "New APIPaths", menuName = "UnityREST/APIPaths", order = 0)]
    public class APIPaths : ScriptableObject
    {
        [SerializeField]
        private Resource[] resources;

        public Resource[] GetResources => resources;
    }

    [Serializable]
    public class Resource
    {
        public string name, path;
    }
}