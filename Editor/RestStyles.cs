using UnityEditor;
using UnityEngine;

namespace UnityREST.Editor
{
    /// <summary>Shared colors, icons and styles. Everything is created once so OnGUI does not allocate.</summary>
    internal static class RestStyles
    {
        // Solid accent for the selected environment and primary actions.
        public static readonly Color Accent = new Color32(124, 58, 237, 255);

        private static readonly Color[] MethodColors =
        {
            new Color32(59, 130, 246, 255), // GET
            new Color32(34, 160, 94, 255), // POST
            new Color32(217, 119, 6, 255), // PUT
            new Color32(139, 92, 246, 255), // PATCH
            new Color32(220, 38, 38, 255), // DELETE
            new Color32(100, 116, 139, 255), // HEAD
            new Color32(100, 116, 139, 255) // OPTIONS
        };

        private static readonly GUIContent[] MethodLabels =
        {
            new GUIContent("GET"), new GUIContent("POST"), new GUIContent("PUT"), new GUIContent("PATCH"),
            new GUIContent("DELETE"), new GUIContent("HEAD"), new GUIContent("OPTIONS")
        };

        private static GUIStyle _pill;
        private static GUIStyle _section;
        private static GUIStyle _mono;
        private static GUIStyle _wrap;

        public static GUIContent SecureIcon { get; } = EditorGUIUtility.IconContent("LockIcon-On");

        public static GUIContent InsecureIcon { get; } = EditorGUIUtility.IconContent("console.warnicon.sml");

        public static GUIContent PlayIcon { get; } = new GUIContent(EditorGUIUtility.IconContent("PlayButton").image, "Load into the request tester");

        public static GUIContent RevealIcon { get; } = new GUIContent(EditorGUIUtility.IconContent("animationvisibilitytoggleon").image, "Show");

        public static GUIContent HideIcon { get; } = new GUIContent(EditorGUIUtility.IconContent("animationvisibilitytoggleoff").image, "Hide");

        public static GUIStyle Pill => _pill ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            padding = new RectOffset(2, 2, 0, 0)
        };

        public static GUIStyle Section => _section ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, margin = new RectOffset(0, 0, 8, 2) };

        public static GUIStyle Mono => _mono ??= new GUIStyle(EditorStyles.textArea)
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "Courier New" }, 11),
            wordWrap = true,
            richText = false
        };

        public static GUIStyle Wrap => _wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };

        public static Color MethodColor(HttpMethod method) => MethodColors[(int)method];

        public static GUIContent MethodLabel(HttpMethod method) => MethodLabels[(int)method];

        /// <summary>Colored method badge.</summary>
        public static void DrawMethod(Rect rect, HttpMethod method)
        {
            EditorGUI.DrawRect(rect, MethodColor(method));
            GUI.Label(rect, MethodLabel(method), Pill);
        }

        /// <summary>Colored method badge that opens a method picker on click.</summary>
        public static HttpMethod MethodPopup(Rect rect, HttpMethod method)
        {
            DrawMethod(rect, method);
            return (HttpMethod)EditorGUI.EnumPopup(rect, GUIContent.none, method, GUIStyle.none);
        }

        public static void Line(float height = 1f)
        {
            var rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.12f));
        }
    }
}
