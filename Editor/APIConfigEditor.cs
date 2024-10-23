using UnityEngine;
using System.Linq;

namespace UnityREST.Editor
{
    using UnityEditor;

    [CustomEditor(typeof(APIConfig))]
    public class APIConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (APIConfig)target;
            
            DrawDefaultInspector();

           var availableEnvironmentTypes = config.Environments
                 .Where(env => env)
                 .Select(env => env.Type)
                 .Distinct()
                 .ToList();
           
            if (availableEnvironmentTypes.Count > 0)
            {
                var enumNames = availableEnvironmentTypes
                    .Select(e => e.ToString())
                    .ToList();
                
                var currentIndex = availableEnvironmentTypes.IndexOf(config.TargetEnvironment);

                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }
                
                currentIndex = EditorGUILayout.Popup("Target Environment", currentIndex, enumNames.ToArray());
                
                config.TargetEnvironment = availableEnvironmentTypes[currentIndex];
            }
            else
            {
                EditorGUILayout.HelpBox("No available environments in the list.", MessageType.Warning);

                config.TargetEnvironment = EnvironmentType.None;
            }
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }
    }
}