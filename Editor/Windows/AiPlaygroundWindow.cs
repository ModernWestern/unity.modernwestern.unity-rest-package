using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityREST.Editor
{
    using AI;
    using AI.Mcp;

    /// <summary>Chat with any configured provider from the editor, optionally with tools from an MCP server.</summary>
    internal sealed class AiPlaygroundWindow : EditorWindow
    {
        [SerializeField]
        private ChatProviderAsset provider;

        [SerializeField]
        private string systemPrompt = "You are a helpful assistant.";

        [SerializeField]
        private string mcpUrl = string.Empty;

        [SerializeField]
        private string input = string.Empty;

        private string _mcpToken = string.Empty;
        private ChatSession _session;
        private ChatProviderAsset _sessionProvider;
        private McpClient _mcp;
        private string _mcpStatus;
        private readonly StringBuilder _streaming = new StringBuilder();
        private string _status;
        private bool _busy;
        private bool _showSetup = true;
        private Vector2 _scroll;
        private CancellationTokenSource _cancel;

        private GUIStyle _bubble;

        [MenuItem("Window/UnityREST/AI Playground")]
        public static void Open() => Open(null);

        public static void Open(ChatProviderAsset asset)
        {
            var window = GetWindow<AiPlaygroundWindow>("AI Playground");
            window.minSize = new Vector2(360f, 420f);

            if (asset != null)
            {
                window.provider = asset;
            }
        }

        private void OnDisable() => _cancel?.Cancel();

        private void OnGUI()
        {
            _bubble ??= new GUIStyle(EditorStyles.helpBox) { wordWrap = true, richText = false, fontSize = 12, padding = new RectOffset(8, 8, 6, 6) };

            DrawSetup();
            RestStyles.Line();
            DrawTranscript();
            DrawInput();
        }

        private void DrawSetup()
        {
            _showSetup = EditorGUILayout.BeginFoldoutHeaderGroup(_showSetup, provider != null ? $"{provider.name} · {provider.Settings.model}" : "Setup");

            if (_showSetup)
            {
                provider = (ChatProviderAsset)EditorGUILayout.ObjectField("Provider", provider, typeof(ChatProviderAsset), false);

                if (provider == null)
                {
                    EditorGUILayout.HelpBox("Create one with Assets > Create > UnityREST > AI Chat Provider.", MessageType.Info);
                }

                EditorGUILayout.LabelField("System prompt", EditorStyles.miniBoldLabel);
                systemPrompt = EditorGUILayout.TextArea(systemPrompt, GUILayout.MinHeight(36f));

                using (new EditorGUILayout.HorizontalScope())
                {
                    mcpUrl = EditorGUILayout.TextField("MCP server", mcpUrl);

                    using (new EditorGUI.DisabledScope(_busy || string.IsNullOrWhiteSpace(mcpUrl)))
                    {
                        if (GUILayout.Button(_mcp == null ? "Connect" : "Reconnect", GUILayout.Width(80f)))
                        {
                            ConnectMcpAsync().Forget();
                        }
                    }
                }

                _mcpToken = EditorGUILayout.PasswordField("MCP token", _mcpToken);

                if (_mcpStatus != null)
                {
                    EditorGUILayout.LabelField(_mcpStatus, EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("New conversation", GUILayout.Width(130f)))
                    {
                        _session = null;
                        _status = null;
                    }
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawTranscript()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (_session != null)
            {
                foreach (var message in _session.History)
                {
                    switch (message.Role)
                    {
                        case ChatRole.User:
                            Bubble("You", message.Content);
                            break;
                        case ChatRole.Assistant:
                            if (message.Content.Length > 0)
                            {
                                Bubble("Assistant", message.Content);
                            }

                            foreach (var call in message.ToolCalls)
                            {
                                Bubble("Tool call", call.ToString());
                            }

                            break;
                        case ChatRole.Tool:
                            Bubble(message.IsError ? $"{message.ToolName} failed" : message.ToolName, message.Content);
                            break;
                    }
                }
            }

            if (_busy && _streaming.Length > 0)
            {
                Bubble("Assistant", _streaming.ToString());
            }

            EditorGUILayout.EndScrollView();

            if (_status != null)
            {
                EditorGUILayout.HelpBox(_status, MessageType.Warning);
            }
        }

        private void Bubble(string title, string text)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(text, _bubble, GUILayout.Height(_bubble.CalcHeight(new GUIContent(text), position.width - 32f)));
        }

        private void DrawInput()
        {
            RestStyles.Line();
            input = EditorGUILayout.TextArea(input, GUILayout.MinHeight(48f));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(_session != null ? $"Tokens: {_session.TotalUsage}" : string.Empty, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (_busy)
                {
                    if (GUILayout.Button("Stop", GUILayout.Width(80f)))
                    {
                        _cancel?.Cancel();
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(provider == null || string.IsNullOrWhiteSpace(input)))
                    {
                        var color = GUI.backgroundColor;
                        GUI.backgroundColor = RestStyles.Accent;

                        var send = GUILayout.Button("Send", GUILayout.Width(80f)) ||
                                   (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && Event.current.control);
                        GUI.backgroundColor = color;

                        if (send && provider != null && !string.IsNullOrWhiteSpace(input))
                        {
                            SendAsync(input.Trim()).Forget();
                            input = string.Empty;
                            GUI.FocusControl(null);
                        }
                    }
                }
            }
        }

        private async UniTaskVoid SendAsync(string text)
        {
            if (_session == null || _sessionProvider != provider)
            {
                _session = new ChatSession(provider.CreateProvider(), systemPrompt);
                _sessionProvider = provider;

                if (_mcp != null)
                {
                    _session.Tools.AddRange(await _mcp.GetToolsAsync());
                }
            }

            _busy = true;
            _status = null;
            _streaming.Clear();
            _cancel = new CancellationTokenSource();

            try
            {
                var response = await _session.SendAsync(text, delta =>
                {
                    _streaming.Append(delta);
                    _scroll.y = float.MaxValue;
                    Repaint();
                }, _cancel.Token);

                if (!response.IsSuccess)
                {
                    _status = $"{response.Error} {response.StatusCode}: {response.ErrorMessage}";
                }
                else if (response.FinishReason != FinishReason.Stop && response.FinishReason != FinishReason.ToolCalls)
                {
                    _status = $"Stopped: {response.FinishReason} ({response.RawFinishReason})";
                }
            }
            catch (OperationCanceledException)
            {
                _status = "Cancelled.";
            }
            catch (Exception exception)
            {
                _status = exception.Message;
            }
            finally
            {
                _busy = false;
                _streaming.Clear();
                _cancel.Dispose();
                _cancel = null;
                _scroll.y = float.MaxValue;
                Repaint();
            }
        }

        private async UniTaskVoid ConnectMcpAsync()
        {
            _busy = true;
            _mcpStatus = "Connecting…";

            try
            {
                var client = new McpClient(mcpUrl.Trim(), string.IsNullOrEmpty(_mcpToken) ? null : _mcpToken);
                var tools = await client.ListToolsAsync();
                _mcp = client;
                _session = null;
                _mcpStatus = $"{client.ServerInfo?["name"] ?? "Server"} · {tools.Count} tools: {string.Join(", ", ToNames(tools))}";
            }
            catch (Exception exception)
            {
                _mcp = null;
                _mcpStatus = "Could not connect: " + exception.Message;
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        private static string[] ToNames(System.Collections.Generic.IReadOnlyList<McpToolInfo> tools)
        {
            var names = new string[tools.Count];

            for (var i = 0; i < names.Length; i++)
            {
                names[i] = tools[i].Name;
            }

            return names;
        }
    }
}
