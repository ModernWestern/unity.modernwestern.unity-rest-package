using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UnityREST.Tests
{
    using AI;

    public sealed class ChatSessionTests
    {
        /// <summary>Replies from a script: the first reply asks for a tool, the rest answer with text.</summary>
        private sealed class ScriptedProvider : IChatProvider
        {
            public readonly List<string> SystemPrompts = new List<string>();
            public bool AskForTool;

            public string Name => "scripted";

            public ChatProviderSettings Settings { get; } = new ChatProviderSettings();

            public UniTask<ChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ITool> tools = null,
                Action<string> onTextDelta = null, CancellationToken cancellationToken = default)
            {
                SystemPrompts.Add(messages[0].Role == ChatRole.System ? messages[0].Content : null);

                if (AskForTool && messages[messages.Count - 1].Role == ChatRole.User)
                {
                    var calls = new List<ToolCall> { new ToolCall("t1", "ping", new JObject()) };
                    return UniTask.FromResult(new ChatResponse(ChatMessage.Assistant(string.Empty, calls), FinishReason.ToolCalls));
                }

                onTextDelta?.Invoke("ok");
                return UniTask.FromResult(new ChatResponse(ChatMessage.Assistant("ok"), FinishReason.Stop));
            }
        }

        [UnityTest]
        public IEnumerator SystemPromptCanChangeBetweenTurns() => UniTask.ToCoroutine(async () =>
        {
            var provider = new ScriptedProvider();
            var chat = new ChatSession(provider, "first");

            await chat.SendAsync("a");
            chat.SystemPrompt = "second";
            await chat.SendAsync("b");
            chat.SystemPrompt = null;
            await chat.SendAsync("c");

            CollectionAssert.AreEqual(new[] { "first", "second", null }, provider.SystemPrompts);
            Assert.AreEqual(6, chat.History.Count);
        });

        [UnityTest]
        public IEnumerator RoundCompletedFiresForEveryModelResponse() => UniTask.ToCoroutine(async () =>
        {
            var chat = new ChatSession(new ScriptedProvider { AskForTool = true });
            chat.Tools.Add(Tool.Create("ping", "Ping", null, _ => "pong"));
            var rounds = new List<FinishReason>();
            chat.RoundCompleted += response => rounds.Add(response.FinishReason);

            await chat.SendAsync("hi");

            CollectionAssert.AreEqual(new[] { FinishReason.ToolCalls, FinishReason.Stop }, rounds);
        });

        [UnityTest]
        public IEnumerator TrimHistoryKeepsToolCallsWithTheirResults() => UniTask.ToCoroutine(async () =>
        {
            var chat = new ChatSession(new ScriptedProvider { AskForTool = true }, "system");
            chat.Tools.Add(Tool.Create("ping", "Ping", null, _ => "pong"));

            // Each turn: user, assistant tool call, tool result, assistant text.
            await chat.SendAsync("one");
            await chat.SendAsync("two");
            await chat.SendAsync("three");
            Assert.AreEqual(13, chat.History.Count);

            var removed = chat.TrimHistory(5);

            Assert.AreEqual(8, removed.Count);
            Assert.AreEqual("system", chat.SystemPrompt);
            Assert.AreEqual(ChatRole.User, chat.History[1].Role);
            Assert.AreEqual("three", chat.History[1].Content);
            Assert.AreEqual(5, chat.History.Count);

            // A limit inside the last turn keeps that whole turn.
            chat.TrimHistory(1);
            Assert.AreEqual("three", chat.History[1].Content);
            Assert.AreEqual(5, chat.History.Count);

            chat.TrimHistory(0);
            Assert.AreEqual(5, chat.History.Count);
            Assert.IsTrue(removed.All(m => m.Role != ChatRole.System));
        });
    }
}
