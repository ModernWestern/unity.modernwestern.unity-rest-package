using System;
using UnityEditor;
using UnityEngine;

namespace UnityREST.Editor
{
    using AI;

    [CustomEditor(typeof(ChatProviderAsset))]
    internal sealed class ChatProviderAssetEditor : UnityEditor.Editor
    {
        private static readonly (string Label, Func<ChatProviderSettings> Create)[] Presets =
        {
            ("OpenAI", () => ChatProviderSettings.OpenAI()),
            ("Anthropic (Claude)", () => ChatProviderSettings.Anthropic()),
            ("Google Gemini", () => ChatProviderSettings.Gemini()),
            ("Ollama (local)", () => ChatProviderSettings.Ollama()),
            ("LM Studio (local)", () => ChatProviderSettings.LmStudio("local-model")),
            ("OpenRouter", () => ChatProviderSettings.OpenRouter("openrouter/auto"))
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var asset = (ChatProviderAsset)target;
            var settings = asset.Settings;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Preset", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

                if (EditorGUILayout.DropdownButton(new GUIContent("Apply a preset…"), FocusType.Passive))
                {
                    var menu = new GenericMenu();

                    foreach (var preset in Presets)
                    {
                        menu.AddItem(new GUIContent(preset.Label), false, () =>
                        {
                            Undo.RecordObject(asset, "Apply chat provider preset");
                            var next = preset.Create();
                            // Same field name as ChatProviderAsset.settings, so the private field is overwritten in place.
                            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(new Wrapper { settings = next }), asset);
                            EditorUtility.SetDirty(asset);
                        });
                    }

                    menu.ShowAsContext();
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"), true);

            if (!string.IsNullOrEmpty(settings.apiKey))
            {
                EditorGUILayout.HelpBox("The API key is stored in this asset and ships with any build that includes it. " +
                                        "For shipped games, point Base Url at your own backend and keep the key there.", MessageType.Warning);
            }
            else if (string.IsNullOrEmpty(settings.ResolveApiKey()) && !IsLocal(settings.baseUrl))
            {
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(settings.apiKeyEnvironmentVariable)
                    ? "No API key. Set one, or name an environment variable that holds it."
                    : $"Environment variable {settings.apiKeyEnvironmentVariable} is not set. Restart Unity after setting it.", MessageType.Info);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Open AI Playground"))
            {
                AiPlaygroundWindow.Open(asset);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsLocal(string url) =>
            !string.IsNullOrEmpty(url) && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && RestClient.IsSecureOrLoopback(url);

        [Serializable]
        private sealed class Wrapper
        {
            public ChatProviderSettings settings;
        }
    }
}
