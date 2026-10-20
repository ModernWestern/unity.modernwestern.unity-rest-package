using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityREST.Editor
{
    /// <summary>Password-style field with a reveal toggle.</summary>
    [CustomPropertyDrawer(typeof(SecretAttribute))]
    internal sealed class SecretDrawer : PropertyDrawer
    {
        private static readonly HashSet<string> Revealed = new HashSet<string>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var key = property.serializedObject.targetObject.GetInstanceID() + property.propertyPath;
            var visible = Revealed.Contains(key);
            var field = new Rect(position.x, position.y, position.width - 24f, position.height);
            var toggle = new Rect(position.xMax - 22f, position.y, 22f, position.height);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var value = visible ? EditorGUI.TextField(field, label, property.stringValue) : EditorGUI.PasswordField(field, label, property.stringValue);

            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = value;
            }

            EditorGUI.EndProperty();

            if (GUI.Button(toggle, visible ? RestStyles.HideIcon : RestStyles.RevealIcon, EditorStyles.iconButton))
            {
                if (visible)
                {
                    Revealed.Remove(key);
                }
                else
                {
                    Revealed.Add(key);
                }
            }
        }
    }

    /// <summary>Header as one line: name | value.</summary>
    [CustomPropertyDrawer(typeof(HttpHeader))]
    internal sealed class HttpHeaderDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var content = EditorGUI.PrefixLabel(position, label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var nameWidth = content.width * 0.4f;
            EditorGUI.PropertyField(new Rect(content.x, content.y, nameWidth - 2f, content.height), property.FindPropertyRelative("name"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(content.x + nameWidth + 2f, content.y, content.width - nameWidth - 2f, content.height),
                property.FindPropertyRelative("value"), GUIContent.none);

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Endpoint as one line: method badge | name | path.</summary>
    [CustomPropertyDrawer(typeof(Endpoint))]
    internal sealed class EndpointDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => Draw(position, property);

        public static void Draw(Rect position, SerializedProperty property)
        {
            EditorGUI.BeginProperty(position, GUIContent.none, property);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var method = property.FindPropertyRelative("method");
            var line = new Rect(position.x, position.y + 1f, position.width, EditorGUIUtility.singleLineHeight);
            var methodRect = new Rect(line.x, line.y, 64f, line.height);
            var nameRect = new Rect(methodRect.xMax + 4f, line.y, (line.width - 68f) * 0.35f, line.height);
            var pathRect = new Rect(nameRect.xMax + 4f, line.y, line.xMax - nameRect.xMax - 4f, line.height);

            EditorGUI.BeginChangeCheck();
            var value = RestStyles.MethodPopup(methodRect, (HttpMethod)method.enumValueIndex);

            if (EditorGUI.EndChangeCheck())
            {
                method.enumValueIndex = (int)value;
            }

            DrawPlaceholder(nameRect, property.FindPropertyRelative("name"), "name");
            DrawPlaceholder(pathRect, property.FindPropertyRelative("path"), "path/{id}");

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static void DrawPlaceholder(Rect rect, SerializedProperty property, string placeholder)
        {
            EditorGUI.PropertyField(rect, property, GUIContent.none);

            if (string.IsNullOrEmpty(property.stringValue) && Event.current.type == EventType.Repaint)
            {
                EditorStyles.centeredGreyMiniLabel.Draw(rect, placeholder, false, false, false, false);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 2f;
    }

    /// <summary>Config field plus a dropdown of its endpoints.</summary>
    [CustomPropertyDrawer(typeof(EndpointRef))]
    internal sealed class EndpointRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var content = EditorGUI.PrefixLabel(position, label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var configProperty = property.FindPropertyRelative("config");
            var nameProperty = property.FindPropertyRelative("name");
            var configRect = new Rect(content.x, content.y, content.width * 0.4f - 2f, content.height);
            var endpointRect = new Rect(configRect.xMax + 4f, content.y, content.xMax - configRect.xMax - 4f, content.height);

            EditorGUI.PropertyField(configRect, configProperty, GUIContent.none);

            var config = configProperty.objectReferenceValue as ApiConfig;

            if (config == null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.Popup(endpointRect, 0, new[] { "Assign an ApiConfig" });
                }
            }
            else
            {
                var endpoints = config.Endpoints;
                var options = new GUIContent[endpoints.Count + 1];
                var selected = 0;
                options[0] = new GUIContent("None");

                for (var i = 0; i < endpoints.Count; i++)
                {
                    options[i + 1] = new GUIContent($"{endpoints[i].Method.ToVerb()}  {endpoints[i].Name}", endpoints[i].Path);

                    if (endpoints[i].Name == nameProperty.stringValue)
                    {
                        selected = i + 1;
                    }
                }

                var color = GUI.color;

                if (selected == 0 && !string.IsNullOrEmpty(nameProperty.stringValue))
                {
                    // The referenced endpoint was renamed or removed.
                    GUI.color = new Color(1f, 0.55f, 0.5f);
                    options[0] = new GUIContent($"Missing: {nameProperty.stringValue}");
                }

                var next = EditorGUI.Popup(endpointRect, selected, options);
                GUI.color = color;

                if (next != selected)
                {
                    nameProperty.stringValue = next == 0 ? string.Empty : endpoints[next - 1].Name;
                }
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
