using UnityEngine;
using UnityEditor;

namespace UnityREST.Editor
{
    [CustomPropertyDrawer(typeof(Resource))]
    public class EndPointDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var labelWidth = EditorGUIUtility.labelWidth;

            EditorGUIUtility.labelWidth = 40f; // Adjust the label width as needed

            var contentPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), GUIContent.none);

            var nameRect = new Rect(contentPosition.x, contentPosition.y, contentPosition.width * 0.5f - 5f, EditorGUIUtility.singleLineHeight);

            var pathRect = new Rect(contentPosition.x + contentPosition.width * 0.5f + 5f, contentPosition.y, contentPosition.width * 0.5f - 5f, EditorGUIUtility.singleLineHeight);

            var nameProp = property.FindPropertyRelative("name");

            var pathProp = property.FindPropertyRelative("path");

            EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("Name"));

            EditorGUI.PropertyField(pathRect, pathProp, new GUIContent("Path"));

            EditorGUIUtility.labelWidth = labelWidth; // Restore original label width

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // Height for one line and spacing
        }
    }
}