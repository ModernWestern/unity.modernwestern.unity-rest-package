using System;
using UnityEditor;
using UnityEngine;

namespace UnityREST.Editor
{
    [CustomPropertyDrawer(typeof(HideEnum))]
    public class HideEnumDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                var enumAttribute = (HideEnum)attribute;

                var enumType = fieldInfo.FieldType;

                var enumNames = Enum.GetNames(enumType);

                var enumValues = Enum.GetValues(enumType);

                var validEnumNames = new System.Collections.Generic.List<string>();

                var validEnumValues = new System.Collections.Generic.List<int>();

                for (var i = 0; i < enumNames.Length; i++)
                {
                    if (enumNames[i].Equals(enumAttribute.ValueToHide, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    validEnumNames.Add(enumNames[i]);

                    validEnumValues.Add((int)enumValues.GetValue(i));
                }

                var currentIndex = Array.IndexOf(validEnumValues.ToArray(), property.enumValueIndex);

                var newIndex = EditorGUI.Popup(position, label.text, currentIndex, validEnumNames.ToArray());

                if (newIndex != currentIndex)
                {
                    property.enumValueIndex = validEnumValues[newIndex];
                }
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }
    }
}