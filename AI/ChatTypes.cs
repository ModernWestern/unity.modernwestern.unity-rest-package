using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityREST.AI
{
    public enum ChatRole : byte
    {
        System,
        User,
        Assistant,
        Tool
    }

    public enum FinishReason : byte
    {
        Stop,
        Length,
        ToolCalls,
        Refusal,
        ContentFilter,
        Error,
        Other
    }

    /// <summary>Provider-agnostic chat backend. Pass <c>onTextDelta</c> to stream the answer as it is generated.</summary>
    public interface IChatProvider
    {
        string Name { get; }

        ChatProviderSettings Settings { get; }

        UniTask<ChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ITool> tools = null,
            Action<string> onTextDelta = null, CancellationToken cancellationToken = default);
    }

    /// <summary>Inline image for vision-capable models.</summary>
    public readonly struct ChatImage
    {
        public ChatImage(string mediaType, string base64Data)
        {
            MediaType = mediaType;
            Base64Data = base64Data;
        }

        public string MediaType { get; }

        public string Base64Data { get; }

        public static ChatImage FromPng(byte[] png) => new ChatImage("image/png", Convert.ToBase64String(png));

        public static ChatImage FromJpg(byte[] jpg) => new ChatImage("image/jpeg", Convert.ToBase64String(jpg));

        /// <summary>Encodes a readable texture (e.g. a screenshot) as PNG.</summary>
        public static ChatImage FromTexture(Texture2D texture) => FromPng(texture.EncodeToPNG());
    }

    public sealed class ToolCall
    {
        public ToolCall(string id, string name, JObject arguments)
        {
            Id = id;
            Name = name;
            Arguments = arguments ?? new JObject();
        }

        public string Id { get; }

        public string Name { get; }

        public JObject Arguments { get; }

        public override string ToString() => $"{Name}({Arguments.ToString(Newtonsoft.Json.Formatting.None)})";
    }

    public sealed class ChatMessage
    {
        private static readonly IReadOnlyList<ToolCall> NoToolCalls = Array.Empty<ToolCall>();
        private static readonly IReadOnlyList<ChatImage> NoImages = Array.Empty<ChatImage>();

        private ChatMessage(ChatRole role, string content)
        {
            Role = role;
            Content = content ?? string.Empty;
        }

        public ChatRole Role { get; }

        public string Content { get; }

        public IReadOnlyList<ChatImage> Images { get; private set; } = NoImages;

        /// <summary>Tools the assistant asked to run.</summary>
        public IReadOnlyList<ToolCall> ToolCalls { get; private set; } = NoToolCalls;

        /// <summary>For <see cref="ChatRole.Tool"/> messages: the call this result answers.</summary>
        public string ToolCallId { get; private set; }

        public string ToolName { get; private set; }

        public bool IsError { get; private set; }

        /// <summary>Provider that produced <see cref="NativeContent"/>.</summary>
        internal string NativeProvider { get; private set; }

        /// <summary>
        /// Exact provider payload of an assistant turn (reasoning blocks, signatures). Replayed verbatim
        /// to the same provider, as tool-use loops require.
        /// </summary>
        internal JToken NativeContent { get; private set; }

        public static ChatMessage System(string text) => new ChatMessage(ChatRole.System, text);

        public static ChatMessage User(string text, params ChatImage[] images) =>
            new ChatMessage(ChatRole.User, text) { Images = images == null || images.Length == 0 ? NoImages : images };

        public static ChatMessage Assistant(string text) => new ChatMessage(ChatRole.Assistant, text);

        /// <summary>Assistant turn that requests tools. For custom <see cref="IChatProvider"/> implementations and tests.</summary>
        public static ChatMessage Assistant(string text, IReadOnlyList<ToolCall> toolCalls) =>
            new ChatMessage(ChatRole.Assistant, text) { ToolCalls = toolCalls == null || toolCalls.Count == 0 ? NoToolCalls : toolCalls };

        public static ChatMessage ToolResult(ToolCall call, string content, bool isError = false) =>
            new ChatMessage(ChatRole.Tool, content) { ToolCallId = call.Id, ToolName = call.Name, IsError = isError };

        internal static ChatMessage FromProvider(string text, List<ToolCall> toolCalls, string provider, JToken native) =>
            new ChatMessage(ChatRole.Assistant, text)
            {
                ToolCalls = toolCalls == null || toolCalls.Count == 0 ? NoToolCalls : toolCalls,
                NativeProvider = provider,
                NativeContent = native
            };

        internal bool HasNativeContentFor(string provider) => NativeContent != null && NativeProvider == provider;

        public override string ToString() => $"{Role}: {Content}";
    }

    public readonly struct TokenUsage
    {
        public TokenUsage(int inputTokens, int outputTokens)
        {
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
        }

        public int InputTokens { get; }

        public int OutputTokens { get; }

        public static TokenUsage operator +(TokenUsage a, TokenUsage b) => new TokenUsage(a.InputTokens + b.InputTokens, a.OutputTokens + b.OutputTokens);

        public override string ToString() => $"in {InputTokens} / out {OutputTokens}";
    }

    public sealed class ChatResponse
    {
        internal ChatResponse()
        {
        }

        /// <summary>Successful response. For custom <see cref="IChatProvider"/> implementations and tests.</summary>
        public ChatResponse(ChatMessage message, FinishReason finishReason, TokenUsage usage = default, string rawFinishReason = null)
        {
            Message = message;
            FinishReason = finishReason;
            Usage = usage;
            RawFinishReason = rawFinishReason;
        }

        public ChatMessage Message { get; internal set; }

        public FinishReason FinishReason { get; internal set; }

        /// <summary>The provider's own stop reason, e.g. "end_turn" or "tool_calls".</summary>
        public string RawFinishReason { get; internal set; }

        public TokenUsage Usage { get; internal set; }

        public HttpError Error { get; internal set; }

        public long StatusCode { get; internal set; }

        public string ErrorMessage { get; internal set; }

        public bool IsSuccess => Error == HttpError.None && ErrorMessage == null;

        public string Text => Message?.Content;

        public static ChatResponse Failed(HttpError error, long status, string message) => new ChatResponse
        {
            Error = error == HttpError.None ? HttpError.Http : error,
            StatusCode = status,
            ErrorMessage = message ?? error.ToString(),
            FinishReason = FinishReason.Error
        };

        public override string ToString() => IsSuccess ? $"[{FinishReason}] {Text}" : $"[{Error} {StatusCode}] {ErrorMessage}";
    }
}
