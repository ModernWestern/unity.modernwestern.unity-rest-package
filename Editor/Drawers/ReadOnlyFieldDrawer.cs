using UnityEditor;
using UnityEngine;

namespace UnityREST.Editor
{
    [CustomPropertyDrawer(typeof(ReadOnly))]
    public class ReadOnlyFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;

            EditorGUI.PropertyField(position, property, label, true);

            GUI.enabled = true;
        }
    }
}