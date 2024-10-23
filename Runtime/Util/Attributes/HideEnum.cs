using UnityEngine;

namespace UnityREST.Editor
{
    public class HideEnum : PropertyAttribute
    {
        public readonly string ValueToHide;

        public HideEnum(string valueToHide)
        {
            ValueToHide = valueToHide;
        }
    }
}