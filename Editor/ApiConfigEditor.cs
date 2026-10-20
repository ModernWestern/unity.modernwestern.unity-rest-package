using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityREST.Editor
{
    [CustomEditor(typeof(ApiConfig))]
    internal sealed class ApiConfigEditor : UnityEditor.Editor
    {
        // Response characters shown in the tester.
        private const int PreviewLimit = 16000;

        private SerializedProperty _active;
        private SerializedProperty _environments;
        private SerializedProperty _endpoints;
        private SerializedProperty _timeout;
        private SerializedProperty _retry;
        private SerializedProperty _enforceHttps;
        private SerializedProperty _logLevel;
        private ReorderableList _endpointList;

        private readonly List<string> _issues = new List<string>();
        private GUIContent[] _environmentNames = Array.Empty<GUIContent>();
        private bool _showEnvironments = true;
        private bool _showSettings;
        private bool _showTester;

        private HttpMethod _testMethod;
        private string _testPath = string.Empty;
        private string _testBody = string.Empty;
        private string _testResult;
        private string _testStatus;
        private bool _testSuccess;
        private bool _testBusy;
        private Vector2 _testScroll;

        private ApiConfig Config => (ApiConfig)target;

        private void OnEnable()
        {
            _active = serializedObject.FindProperty("activeEnvironment");
            _environments = serializedObject.FindProperty("environments");
            _endpoints = serializedObject.FindProperty("endpoints");
            _timeout = serializedObject.FindProperty("timeoutSeconds");
            _retry = serializedObject.FindProperty("retry");
            _enforceHttps = serializedObject.FindProperty("enforceHttps");
            _logLevel = serializedObject.FindProperty("logLevel");

            _endpointList = new ReorderableList(serializedObject, _endpoints, true, true, true, true)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"Endpoints ({_endpoints.arraySize})", EditorStyles.boldLabel),
                drawElementCallback = DrawEndpoint
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawEnvironmentBar();
            DrawIssues();

            EditorGUILayout.Space(4f);
            _showEnvironments = EditorGUILayout.BeginFoldoutHeaderGroup(_showEnvironments, "Environments");

            if (_showEnvironments)
            {
                EditorGUILayout.PropertyField(_environments, GUIContent.none, true);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4f);
            _endpointList.DoLayoutList();

            _showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showSettings, "Transport");

            if (_showSettings)
            {
                EditorGUILayout.PropertyField(_timeout);
                EditorGUILayout.PropertyField(_retry, true);
                EditorGUILayout.PropertyField(_enforceHttps);
                EditorGUILayout.PropertyField(_logLevel);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            _showTester = EditorGUILayout.BeginFoldoutHeaderGroup(_showTester, "Request tester");

            if (_showTester)
            {
                DrawTester();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEnvironmentBar()
        {
            var count = _environments.arraySize;

            if (_environmentNames.Length != count)
            {
                _environmentNames = new GUIContent[count];
            }

            for (var i = 0; i < count; i++)
            {
                var name = _environments.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                var label = string.IsNullOrEmpty(name) ? $"Environment {i}" : name;

                if (_environmentNames[i] == null || _environmentNames[i].text != label)
                {
                    _environmentNames[i] = new GUIContent(label);
                }
            }

            if (count == 0)
            {
                EditorGUILayout.HelpBox("Add an environment with the base URL of your API.", MessageType.Warning);
                return;
            }

            var color = GUI.backgroundColor;
            GUI.backgroundColor = RestStyles.Accent;
            var selected = GUILayout.Toolbar(Mathf.Clamp(_active.intValue, 0, count - 1), _environmentNames, GUILayout.Height(24f));
            GUI.backgroundColor = color;
            _active.intValue = selected;

            var baseUrl = _environments.GetArrayElementAtIndex(selected).FindPropertyRelative("baseUrl").stringValue;
            var secure = !string.IsNullOrEmpty(baseUrl) && baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(secure ? RestStyles.SecureIcon : RestStyles.InsecureIcon, GUILayout.Width(18f), GUILayout.Height(18f));
                EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(baseUrl) ? "No base URL" : baseUrl, EditorStyles.boldLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void DrawIssues()
        {
            _issues.Clear();

            for (var i = 0; i < _environments.arraySize; i++)
            {
                var environment = _environments.GetArrayElementAtIndex(i);
                var name = environment.FindPropertyRelative("name").stringValue;
                var url = environment.FindPropertyRelative("baseUrl").stringValue;

                if (string.IsNullOrEmpty(url) || !(url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                                                   url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
                {
                    _issues.Add($"'{name}' needs a base URL starting with https://");
                }
                else if (_enforceHttps.boolValue && !RestClient.IsSecureOrLoopback(url))
                {
                    _issues.Add($"'{name}' uses plain http. Requests will be blocked while Enforce Https is on.");
                }

                if (!string.IsNullOrEmpty(environment.FindPropertyRelative("apiKey").stringValue))
                {
                    _issues.Add($"'{name}' stores an API key in this asset. It ships inside the build and can be extracted.");
                }
            }

            var seen = new HashSet<string>();

            for (var i = 0; i < _endpoints.arraySize; i++)
            {
                var name = _endpoints.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;

                if (string.IsNullOrEmpty(name))
                {
                    _issues.Add($"Endpoint #{i} has no name.");
                }
                else if (!seen.Add(name))
                {
                    _issues.Add($"Endpoint name '{name}' is used more than once.");
                }
            }

            if (_issues.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", _issues), MessageType.Warning);
            }
        }

        private void DrawEndpoint(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _endpoints.GetArrayElementAtIndex(index);
            var buttonRect = new Rect(rect.xMax - 22f, rect.y + 2f, 22f, EditorGUIUtility.singleLineHeight);
            EndpointDrawer.Draw(new Rect(rect.x, rect.y + 1f, rect.width - 26f, rect.height), element);

            if (GUI.Button(buttonRect, RestStyles.PlayIcon, EditorStyles.iconButton))
            {
                _testMethod = (HttpMethod)element.FindPropertyRelative("method").enumValueIndex;
                _testPath = element.FindPropertyRelative("path").stringValue;
                _showTester = true;
            }
        }

        private void DrawTester()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var methodRect = GUILayoutUtility.GetRect(64f, EditorGUIUtility.singleLineHeight, GUILayout.Width(64f));
                _testMethod = RestStyles.MethodPopup(methodRect, _testMethod);
                _testPath = EditorGUILayout.TextField(_testPath);

                using (new EditorGUI.DisabledScope(_testBusy || Config.ActiveEnvironment == null))
                {
                    var color = GUI.backgroundColor;
                    GUI.backgroundColor = RestStyles.Accent;

                    if (GUILayout.Button(_testBusy ? "Sending…" : "Send", GUILayout.Width(72f)))
                    {
                        SendTestAsync().Forget();
                    }

                    GUI.backgroundColor = color;
                }
            }

            EditorGUILayout.LabelField("Replace {placeholders} with real values. Relative paths use the active environment.", EditorStyles.miniLabel);

            if (_testMethod == HttpMethod.Post || _testMethod == HttpMethod.Put || _testMethod == HttpMethod.Patch || _testMethod == HttpMethod.Delete)
            {
                EditorGUILayout.LabelField("JSON body", EditorStyles.miniBoldLabel);
                _testBody = EditorGUILayout.TextArea(_testBody, RestStyles.Mono, GUILayout.MinHeight(48f));
            }

            if (_testStatus == null)
            {
                return;
            }

            EditorGUILayout.HelpBox(_testStatus, _testSuccess ? MessageType.Info : MessageType.Error);
            _testScroll = EditorGUILayout.BeginScrollView(_testScroll, GUILayout.MinHeight(80f), GUILayout.MaxHeight(320f));
            EditorGUILayout.SelectableLabel(_testResult ?? string.Empty, RestStyles.Mono, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private async UniTaskVoid SendTestAsync()
        {
            _testBusy = true;

            try
            {
                var request = Config.Client.Request(_testMethod, _testPath);

                if (!string.IsNullOrWhiteSpace(_testBody) && _testMethod != HttpMethod.Get && _testMethod != HttpMethod.Head)
                {
                    request.Text(_testBody, ContentTypes.Json);
                }

                using (var response = await request.SendAsync())
                {
                    _testSuccess = response.IsSuccess;
                    var size = response.Data.Length;
                    _testStatus = response.IsSuccess
                        ? $"{response.StatusCode} · {response.ElapsedSeconds * 1000:0} ms · {FormatSize(size)} · {response.Url}"
                        : $"{response.Error} {(response.StatusCode > 0 ? response.StatusCode.ToString() : string.Empty)} · {response.ErrorMessage}\n{response.Url}";
                    _testResult = Pretty(response.Text);
                }
            }
            catch (Exception exception)
            {
                _testSuccess = false;
                _testStatus = exception.Message;
                _testResult = null;
            }
            finally
            {
                _testBusy = false;
                Repaint();
            }
        }

        private static string Pretty(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var trimmed = text.TrimStart();

            if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                try
                {
                    text = JToken.Parse(text).ToString(Formatting.Indented);
                }
                catch (JsonException)
                {
                }
            }

            return text.Length > PreviewLimit ? text.Substring(0, PreviewLimit) + "\n…" : text;
        }

        private static string FormatSize(int bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            return bytes < 1024 * 1024 ? $"{bytes / 1024f:0.0} KB" : $"{bytes / (1024f * 1024f):0.0} MB";
        }
    }
}
