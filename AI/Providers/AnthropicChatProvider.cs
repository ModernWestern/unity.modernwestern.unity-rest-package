using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityREST.AI
{
    /// <summary>
    /// Claude Messages API. Assistant turns are replayed with their original content blocks (thinking
    /// blocks and signatures included), which multi-turn tool use requires.
    /// </summary>
    public sealed class AnthropicChatProvider : ChatProviderBase
    {
        // Wire format version of the Messages API.
        private const string ApiVersion = "2023-06-01";

        public AnthropicChatProvider(ChatProviderSettings settings) : base(settings)
        {
            var key = settings.ResolveApiKey();

            if (!string.IsNullOrEmpty(key))
            {
                Client.SetHeader("x-api-key", key);
            }

            Client.SetHeader("anthropic-version", ApiVersion);
#if UNITY_WEBGL && !UNITY_EDITOR
            // Required for CORS when calling the API from a browser. Use a proxy instead of shipping a key.
            Client.SetHeader("anthropic-dangerous-direct-browser-access", "true");
#endif
        }

        public override string Name => "anthropic";

        protected override string GetPath(bool stream) => "messages";

        protected override void WriteRequest(JsonWriter writer, IReadOnlyList<ChatMessage> messages, IReadOnlyList<ITool> tools, bool stream)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("model");
            writer.WriteValue(Settings.model);
            writer.WritePropertyName("max_tokens");
            writer.WriteValue(Settings.maxTokens);

            if (Settings.overrideTemperature)
            {
                writer.WritePropertyName("temperature");
                writer.WriteValue(Settings.temperature);
            }

            if (!string.IsNullOrEmpty(Settings.effort))
            {
                writer.WritePropertyName("output_config");
                writer.WriteStartObject();
                writer.WritePropertyName("effort");
                writer.WriteValue(Settings.effort);
                writer.WriteEndObject();
            }

            if (stream)
            {
                writer.WritePropertyName("stream");
                writer.WriteValue(true);
            }

            if (HasSystem(messages))
            {
                writer.WritePropertyName("system");
                WriteSystemText(writer, messages);
            }

            writer.WritePropertyName("messages");
            writer.WriteStartArray();

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];

                switch (message.Role)
                {
                    case ChatRole.User:
                        WriteUser(writer, message);
                        break;
                    case ChatRole.Assistant:
                        WriteAssistant(writer, message);
                        break;
                    case ChatRole.Tool:
                        // All results of one turn go back together in a single user message.
                        writer.WriteStartObject();
                        writer.WritePropertyName("role");
                        writer.WriteValue("user");
                        writer.WritePropertyName("content");
                        writer.WriteStartArray();

                        for (; i < messages.Count && messages[i].Role == ChatRole.Tool; i++)
                        {
                            WriteToolResult(writer, messages[i]);
                        }

                        i--;
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                        break;
                }
            }

            writer.WriteEndArray();

            if (tools.Count > 0)
            {
                writer.WritePropertyName("tools");
                writer.WriteStartArray();

                foreach (var tool in tools)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("name");
                    writer.WriteValue(tool.Name);
                    writer.WritePropertyName("description");
                    writer.WriteValue(tool.Description);
                    writer.WritePropertyName("input_schema");
                    tool.Parameters.WriteTo(writer);

                    if (stream)
                    {
                        // Stream large tool inputs as they are generated instead of in one burst.
                        writer.WritePropertyName("eager_input_streaming");
                        writer.WriteValue(true);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static void WriteUser(JsonWriter writer, ChatMessage message)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("role");
            writer.WriteValue("user");
            writer.WritePropertyName("content");

            if (message.Images.Count == 0)
            {
                writer.WriteValue(message.Content);
            }
            else
            {
                writer.WriteStartArray();

                foreach (var image in message.Images)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("image");
                    writer.WritePropertyName("source");
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("base64");
                    writer.WritePropertyName("media_type");
                    writer.WriteValue(image.MediaType);
                    writer.WritePropertyName("data");
                    writer.WriteValue(image.Base64Data);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteStartObject();
                writer.WritePropertyName("type");
                writer.WriteValue("text");
                writer.WritePropertyName("text");
                writer.WriteValue(message.Content);
                writer.WriteEndObject();
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private void WriteAssistant(JsonWriter writer, ChatMessage message)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("role");
            writer.WriteValue("assistant");
            writer.WritePropertyName("content");

            if (message.HasNativeContentFor(Name))
            {
                message.NativeContent.WriteTo(writer);
            }
            else
            {
                writer.WriteStartArray();

                if (message.Content.Length > 0)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("text");
                    writer.WritePropertyName("text");
                    writer.WriteValue(message.Content);
                    writer.WriteEndObject();
                }

                foreach (var call in message.ToolCalls)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("tool_use");
                    writer.WritePropertyName("id");
                    writer.WriteValue(call.Id);
                    writer.WritePropertyName("name");
                    writer.WriteValue(call.Name);
                    writer.WritePropertyName("input");
                    call.Arguments.WriteTo(writer);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static void WriteToolResult(JsonWriter writer, ChatMessage result)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("tool_result");
            writer.WritePropertyName("tool_use_id");
            writer.WriteValue(result.ToolCallId);
            writer.WritePropertyName("content");
            writer.WriteValue(result.Content);

            if (result.IsError)
            {
                writer.WritePropertyName("is_error");
                writer.WriteValue(true);
            }

            writer.WriteEndObject();
        }

        protected override ChatResponse Parse(JObject body)
        {
            var content = body["content"] as JArray ?? new JArray();
            var stop = (string)body["stop_reason"];

            return new ChatResponse
            {
                Message = BuildMessage(content, Name),
                RawFinishReason = stop,
                FinishReason = MapStop(stop),
                Usage = new TokenUsage(ReadInt(body["usage"]?["input_tokens"]), ReadInt(body["usage"]?["output_tokens"]))
            };
        }

        private static ChatMessage BuildMessage(JArray content, string provider)
        {
            var text = new StringBuilder();
            var calls = new List<ToolCall>();

            foreach (var block in content)
            {
                switch ((string)block["type"])
                {
                    case "text":
                        text.Append((string)block["text"]);
                        break;
                    case "tool_use":
                        calls.Add(new ToolCall((string)block["id"], (string)block["name"], block["input"] as JObject));
                        break;
                }
            }

            return ChatMessage.FromProvider(text.ToString(), calls, provider, content);
        }

        private static FinishReason MapStop(string stop)
        {
            switch (stop)
            {
                case "end_turn":
                case "stop_sequence":
                    return FinishReason.Stop;
                case "max_tokens":
                    return FinishReason.Length;
                case "tool_use":
                    return FinishReason.ToolCalls;
                case "refusal":
                    return FinishReason.Refusal;
                default:
                    return FinishReason.Other;
            }
        }

        protected override StreamAccumulator CreateStream(Action<string> onTextDelta) => new Accumulator(Name, onTextDelta);

        /// <summary>Rebuilds the exact content blocks from stream events so the turn can be replayed.</summary>
        private sealed class Accumulator : StreamAccumulator
        {
            private sealed class Block
            {
                public JObject Json;
                public StringBuilder Text;
                public StringBuilder Thinking;
                public StringBuilder Input;
            }

            private readonly string _provider;
            private readonly List<Block> _blocks = new List<Block>();
            private string _stop;
            private int _inputTokens;
            private int _outputTokens;

            public Accumulator(string provider, Action<string> onTextDelta) : base(onTextDelta)
            {
                _provider = provider;
            }

            protected override void OnJson(JObject json)
            {
                switch ((string)json["type"])
                {
                    case "message_start":
                        _inputTokens = ReadInt(json["message"]?["usage"]?["input_tokens"]);
                        break;

                    case "content_block_start":
                        var index = (int)json["index"];

                        while (_blocks.Count <= index)
                        {
                            _blocks.Add(new Block());
                        }

                        _blocks[index].Json = (JObject)json["content_block"].DeepClone();
                        break;

                    case "content_block_delta":
                        OnDelta(_blocks[(int)json["index"]], json["delta"]);
                        break;

                    case "content_block_stop":
                        Finish(_blocks[(int)json["index"]]);
                        break;

                    case "message_delta":
                        _stop = (string)json["delta"]?["stop_reason"] ?? _stop;
                        _outputTokens = ReadInt(json["usage"]?["output_tokens"]);
                        break;
                }
            }

            private void OnDelta(Block block, JToken delta)
            {
                switch ((string)delta["type"])
                {
                    case "text_delta":
                        var text = (string)delta["text"];
                        (block.Text ??= new StringBuilder()).Append(text);
                        AppendText(text);
                        break;
                    case "thinking_delta":
                        (block.Thinking ??= new StringBuilder()).Append((string)delta["thinking"]);
                        break;
                    case "signature_delta":
                        block.Json["signature"] = (string)delta["signature"];
                        break;
                    case "input_json_delta":
                        (block.Input ??= new StringBuilder()).Append((string)delta["partial_json"]);
                        break;
                }
            }

            private static void Finish(Block block)
            {
                if (block.Text != null)
                {
                    block.Json["text"] = block.Json.Value<string>("text") + block.Text;
                }

                if (block.Thinking != null)
                {
                    block.Json["thinking"] = block.Json.Value<string>("thinking") + block.Thinking;
                }

                if (block.Input != null)
                {
                    block.Json["input"] = ParseArguments(block.Input.ToString());
                }
            }

            protected override ChatResponse BuildResponse()
            {
                var content = new JArray();

                foreach (var block in _blocks)
                {
                    if (block.Json != null)
                    {
                        content.Add(block.Json);
                    }
                }

                return new ChatResponse
                {
                    Message = BuildMessage(content, _provider),
                    RawFinishReason = _stop,
                    FinishReason = MapStop(_stop),
                    Usage = new TokenUsage(_inputTokens, _outputTokens)
                };
            }
        }
    }
}
