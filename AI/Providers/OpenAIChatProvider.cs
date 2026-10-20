using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityREST.AI
{
    /// <summary>OpenAI Chat Completions format, spoken by most hosted and local model servers.</summary>
    public sealed class OpenAIChatProvider : ChatProviderBase
    {
        public OpenAIChatProvider(ChatProviderSettings settings) : base(settings)
        {
            var key = settings.ResolveApiKey();

            if (!string.IsNullOrEmpty(key))
            {
                Client.SetBearerToken(key);
            }
        }

        public override string Name => "openai";

        protected override string GetPath(bool stream) => "chat/completions";

        protected override void WriteRequest(JsonWriter writer, IReadOnlyList<ChatMessage> messages, IReadOnlyList<ITool> tools, bool stream)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("model");
            writer.WriteValue(Settings.model);

            writer.WritePropertyName(string.IsNullOrEmpty(Settings.maxTokensParameter) ? "max_tokens" : Settings.maxTokensParameter);
            writer.WriteValue(Settings.maxTokens);

            if (Settings.overrideTemperature)
            {
                writer.WritePropertyName("temperature");
                writer.WriteValue(Settings.temperature);
            }

            if (!string.IsNullOrEmpty(Settings.effort))
            {
                writer.WritePropertyName("reasoning_effort");
                writer.WriteValue(Settings.effort);
            }

            if (stream)
            {
                writer.WritePropertyName("stream");
                writer.WriteValue(true);
                writer.WritePropertyName("stream_options");
                writer.WriteStartObject();
                writer.WritePropertyName("include_usage");
                writer.WriteValue(true);
                writer.WriteEndObject();
            }

            writer.WritePropertyName("messages");
            writer.WriteStartArray();

            foreach (var message in messages)
            {
                WriteMessage(writer, message);
            }

            writer.WriteEndArray();

            if (tools.Count > 0)
            {
                writer.WritePropertyName("tools");
                writer.WriteStartArray();

                foreach (var tool in tools)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("function");
                    writer.WritePropertyName("function");
                    writer.WriteStartObject();
                    writer.WritePropertyName("name");
                    writer.WriteValue(tool.Name);
                    writer.WritePropertyName("description");
                    writer.WriteValue(tool.Description);
                    writer.WritePropertyName("parameters");
                    tool.Parameters.WriteTo(writer);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static void WriteMessage(JsonWriter writer, ChatMessage message)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("role");

            switch (message.Role)
            {
                case ChatRole.System:
                    writer.WriteValue("system");
                    writer.WritePropertyName("content");
                    writer.WriteValue(message.Content);
                    break;

                case ChatRole.User:
                    writer.WriteValue("user");
                    writer.WritePropertyName("content");

                    if (message.Images.Count == 0)
                    {
                        writer.WriteValue(message.Content);
                        break;
                    }

                    writer.WriteStartArray();
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("text");
                    writer.WritePropertyName("text");
                    writer.WriteValue(message.Content);
                    writer.WriteEndObject();

                    foreach (var image in message.Images)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("type");
                        writer.WriteValue("image_url");
                        writer.WritePropertyName("image_url");
                        writer.WriteStartObject();
                        writer.WritePropertyName("url");
                        writer.WriteValue("data:" + image.MediaType + ";base64," + image.Base64Data);
                        writer.WriteEndObject();
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    break;

                case ChatRole.Assistant:
                    writer.WriteValue("assistant");
                    writer.WritePropertyName("content");
                    writer.WriteValue(message.Content.Length > 0 || message.ToolCalls.Count == 0 ? message.Content : null);

                    if (message.ToolCalls.Count > 0)
                    {
                        writer.WritePropertyName("tool_calls");
                        writer.WriteStartArray();

                        foreach (var call in message.ToolCalls)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("id");
                            writer.WriteValue(call.Id);
                            writer.WritePropertyName("type");
                            writer.WriteValue("function");
                            writer.WritePropertyName("function");
                            writer.WriteStartObject();
                            writer.WritePropertyName("name");
                            writer.WriteValue(call.Name);
                            writer.WritePropertyName("arguments");
                            writer.WriteValue(call.Arguments.ToString(Formatting.None));
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                    }

                    break;

                case ChatRole.Tool:
                    writer.WriteValue("tool");
                    writer.WritePropertyName("tool_call_id");
                    writer.WriteValue(message.ToolCallId);
                    writer.WritePropertyName("content");
                    writer.WriteValue(message.Content);
                    break;
            }

            writer.WriteEndObject();
        }

        protected override ChatResponse Parse(JObject body)
        {
            var choice = body["choices"]?[0];
            var message = choice?["message"];
            var calls = new List<ToolCall>();

            if (message?["tool_calls"] is JArray toolCalls)
            {
                foreach (var call in toolCalls)
                {
                    calls.Add(new ToolCall((string)call["id"], (string)call["function"]?["name"], ParseArguments((string)call["function"]?["arguments"])));
                }
            }

            var finish = (string)choice?["finish_reason"];

            return new ChatResponse
            {
                Message = ChatMessage.FromProvider((string)message?["content"], calls, Name, null),
                RawFinishReason = finish,
                FinishReason = MapFinish(finish, calls.Count),
                Usage = new TokenUsage(ReadInt(body["usage"]?["prompt_tokens"]), ReadInt(body["usage"]?["completion_tokens"]))
            };
        }

        private static FinishReason MapFinish(string finish, int toolCalls)
        {
            switch (finish)
            {
                case "length":
                    return FinishReason.Length;
                case "content_filter":
                    return FinishReason.ContentFilter;
                case "tool_calls":
                case "function_call":
                    return FinishReason.ToolCalls;
                default:
                    // Some servers report "stop" even when they return tool calls.
                    return toolCalls > 0 ? FinishReason.ToolCalls : FinishReason.Stop;
            }
        }

        protected override StreamAccumulator CreateStream(Action<string> onTextDelta) => new Accumulator(this, onTextDelta);

        private sealed class Accumulator : StreamAccumulator
        {
            private readonly OpenAIChatProvider _provider;
            private readonly List<(string Id, string Name, StringBuilder Arguments)> _calls = new List<(string, string, StringBuilder)>();
            private string _finish;
            private TokenUsage _usage;

            public Accumulator(OpenAIChatProvider provider, Action<string> onTextDelta) : base(onTextDelta)
            {
                _provider = provider;
            }

            protected override void OnJson(JObject json)
            {
                if (json["usage"] is JObject usage)
                {
                    _usage = new TokenUsage(ReadInt(usage["prompt_tokens"]), ReadInt(usage["completion_tokens"]));
                }

                var choice = json["choices"]?.Type == JTokenType.Array && json["choices"].HasValues ? json["choices"][0] : null;

                if (choice == null)
                {
                    return;
                }

                var finish = choice["finish_reason"];

                if (finish != null && finish.Type == JTokenType.String)
                {
                    _finish = (string)finish;
                }

                var delta = choice["delta"];

                if (delta == null)
                {
                    return;
                }

                if (delta["content"] is JValue content && content.Type == JTokenType.String)
                {
                    AppendText((string)content);
                }

                if (!(delta["tool_calls"] is JArray toolCalls))
                {
                    return;
                }

                foreach (var call in toolCalls)
                {
                    var index = call["index"] != null ? (int)call["index"] : _calls.Count;

                    while (_calls.Count <= index)
                    {
                        _calls.Add((null, null, new StringBuilder()));
                    }

                    var entry = _calls[index];
                    entry.Id = (string)call["id"] ?? entry.Id;
                    entry.Name = (string)call["function"]?["name"] ?? entry.Name;
                    entry.Arguments.Append((string)call["function"]?["arguments"]);
                    _calls[index] = entry;
                }
            }

            protected override ChatResponse BuildResponse()
            {
                var calls = new List<ToolCall>(_calls.Count);

                for (var i = 0; i < _calls.Count; i++)
                {
                    calls.Add(new ToolCall(_calls[i].Id ?? "call_" + i, _calls[i].Name, ParseArguments(_calls[i].Arguments.ToString())));
                }

                return new ChatResponse
                {
                    Message = ChatMessage.FromProvider(Text.ToString(), calls, _provider.Name, null),
                    RawFinishReason = _finish,
                    FinishReason = MapFinish(_finish, calls.Count),
                    Usage = _usage
                };
            }
        }
    }
}
