using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UnityREST.AI
{
    /// <summary>
    /// A conversation with history and an automatic tool loop: when the model asks for tools, they run
    /// (in parallel) and their results are sent back until the model answers or
    /// <see cref="MaxToolRounds"/> is reached.
    /// </summary>
    /// <example>
    /// <code>
    /// var chat = new ChatSession(ChatProviderSettings.Anthropic().CreateProvider(), "You are a helpful NPC.");
    /// chat.Tools.Add(Tool.Create("get_time", "Current game time", null, _ => GameClock.Now.ToString()));
    /// var reply = await chat.SendAsync("What time is it?", delta => label.text += delta);
    /// </code>
    /// </example>
    public sealed class ChatSession
    {
        private readonly List<ChatMessage> _history = new List<ChatMessage>();

        public ChatSession(IChatProvider provider, string systemPrompt = null)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                _history.Add(ChatMessage.System(systemPrompt));
            }
        }

        public IChatProvider Provider { get; set; }

        public IReadOnlyList<ChatMessage> History => _history;

        public List<ITool> Tools { get; } = new List<ITool>();

        /// <summary>Upper bound of model ↔ tool round trips per <see cref="SendAsync(ChatMessage,Action{string},CancellationToken)"/>.</summary>
        public int MaxToolRounds { get; set; } = 8;

        /// <summary>Optional gate before each tool runs (e.g. ask the player). Return false to deny.</summary>
        public Func<ToolCall, CancellationToken, UniTask<bool>> ApproveToolCall { get; set; }

        /// <summary>Raised after each tool finishes, with its result text.</summary>
        public event Action<ToolCall, string> ToolExecuted;

        /// <summary>Tokens used by every request of this session.</summary>
        public TokenUsage TotalUsage { get; private set; }

        public UniTask<ChatResponse> SendAsync(string text, Action<string> onTextDelta = null, CancellationToken cancellationToken = default) =>
            SendAsync(ChatMessage.User(text), onTextDelta, cancellationToken);

        public async UniTask<ChatResponse> SendAsync(ChatMessage message, Action<string> onTextDelta = null, CancellationToken cancellationToken = default)
        {
            var historyStart = _history.Count;
            _history.Add(message);

            try
            {
                for (var round = 0;; round++)
                {
                    var response = await Provider.CompleteAsync(_history, Tools, onTextDelta, cancellationToken);
                    TotalUsage += response.Usage;

                    if (!response.IsSuccess)
                    {
                        // Keep the history consistent: drop this exchange so it can be retried.
                        _history.RemoveRange(historyStart, _history.Count - historyStart);
                        return response;
                    }

                    _history.Add(response.Message);

                    if (response.FinishReason != FinishReason.ToolCalls || response.Message.ToolCalls.Count == 0)
                    {
                        return response;
                    }

                    var calls = response.Message.ToolCalls;

                    if (round >= MaxToolRounds)
                    {
                        // Every tool call needs a result, or the next request is rejected.
                        foreach (var call in calls)
                        {
                            _history.Add(ChatMessage.ToolResult(call, "Tool round limit reached; the call was not executed.", true));
                        }

                        return response;
                    }

                    var results = await UniTask.WhenAll(Map(calls, cancellationToken));

                    for (var i = 0; i < calls.Count; i++)
                    {
                        _history.Add(ChatMessage.ToolResult(calls[i], results[i].Content, results[i].IsError));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _history.RemoveRange(historyStart, _history.Count - historyStart);
                throw;
            }
        }

        /// <summary>Forgets the conversation, keeping the system prompt.</summary>
        public void Clear()
        {
            _history.RemoveAll(m => m.Role != ChatRole.System);
            TotalUsage = default;
        }

        private IEnumerable<UniTask<(string Content, bool IsError)>> Map(IReadOnlyList<ToolCall> calls, CancellationToken cancellationToken)
        {
            foreach (var call in calls)
            {
                yield return RunToolAsync(call, cancellationToken);
            }
        }

        private async UniTask<(string Content, bool IsError)> RunToolAsync(ToolCall call, CancellationToken cancellationToken)
        {
            var tool = Find(call.Name);

            if (tool == null)
            {
                return ($"Unknown tool '{call.Name}'.", true);
            }

            if (call.Arguments.ContainsKey("_raw"))
            {
                return ("The arguments were not valid JSON. Call the tool again with valid arguments.", true);
            }

            if (ApproveToolCall != null && !await ApproveToolCall(call, cancellationToken))
            {
                return ("The user denied this tool call.", true);
            }

            try
            {
                var result = await tool.InvokeAsync(call.Arguments ?? new JObject(), cancellationToken) ?? string.Empty;
                ToolExecuted?.Invoke(call, result);
                return (result, false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ToolExecuted?.Invoke(call, exception.Message);
                return (exception.Message, true);
            }
        }

        private ITool Find(string name)
        {
            foreach (var tool in Tools)
            {
                if (tool.Name == name)
                {
                    return tool;
                }
            }

            return null;
        }
    }
}
