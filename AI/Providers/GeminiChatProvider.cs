using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityREST.AI
{
    /// <summary>
    /// Google Gemini generateContent API. Model turns are replayed with their original parts so
    /// thought signatures survive tool-use loops.
    /// </summary>
    public sealed class GeminiChatProvider : ChatProviderBase
    {
        // JSON Schema keywords Gemini's function declarations reject.
        private static readonly string[] UnsupportedSchemaKeys = { "$schema", "$id", "additionalProperties", "$defs", "definitions" };

        public GeminiChatProvider(ChatProviderSettings settings) : base(settings)
        {
            var key = settings.ResolveApiKey();

            if (!string.IsNullOrEmpty(key))
            {
                Client.SetHeader("x-goog-api-key", key);
            }
        }

        public override string Name => "gemini";

        protected override string GetPath(bool stream) =>
            "models/" + Settings.model + (stream ? ":streamGenerateContent?alt=sse" : ":generateContent");

        protected override void WriteRequest(JsonWriter writer, IReadOnlyList<ChatMessage> messages, IReadOnlyList<ITool> tools, bool stream)
        {
            writer.WriteStartObject();

            if (HasSystem(messages))
            {
                writer.WritePropertyName("systemInstruction");
                writer.WriteStartObject();
                writer.WritePropertyName("parts");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("text");
                WriteSystemText(writer, messages);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WritePropertyName("contents");
            writer.WriteStartArray();

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];

                switch (message.Role)
                {
                    case ChatRole.User:
                        WriteContent(writer, "user");
                        foreach (var image in message.Images)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("inlineData");
                            writer.WriteStartObject();
                            writer.WritePropertyName("mimeType");
                            writer.WriteValue(image.MediaType);
                            writer.WritePropertyName("data");
                            writer.WriteValue(image.Base64Data);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                        }

                        WriteTextPart(writer, message.Content);
                        EndContent(writer);
                        break;

                    case ChatRole.Assistant when message.HasNativeContentFor(Name):
                        WriteContent(writer, "model");
                        foreach (var part in (JArray)message.NativeContent)
                        {
                            part.WriteTo(writer);
                        }

                        EndContent(writer);
                        break;

                    case ChatRole.Assistant:
                        WriteContent(writer, "model");
                        if (message.Content.Length > 0)
                        {
                            WriteTextPart(writer, message.Content);
                        }

                        foreach (var call in message.ToolCalls)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("functionCall");
                            writer.WriteStartObject();
                            writer.WritePropertyName("name");
                            writer.WriteValue(call.Name);
                            writer.WritePropertyName("args");
                            call.Arguments.WriteTo(writer);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                        }

                        EndContent(writer);
                        break;

                    case ChatRole.Tool:
                        WriteContent(writer, "user");
                        for (; i < messages.Count && messages[i].Role == ChatRole.Tool; i++)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("functionResponse");
                            writer.WriteStartObject();
                            writer.WritePropertyName("name");
                            writer.WriteValue(messages[i].ToolName);
                            writer.WritePropertyName("response");
                            writer.WriteStartObject();
                            writer.WritePropertyName(messages[i].IsError ? "error" : "result");
                            writer.WriteValue(messages[i].Content);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                        }

                        i--;
                        EndContent(writer);
                        break;
                }
            }

            writer.WriteEndArray();

            if (tools.Count > 0)
            {
                writer.WritePropertyName("tools");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("functionDeclarations");
                writer.WriteStartArray();

                foreach (var tool in tools)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("name");
                    writer.WriteValue(tool.Name);
                    writer.WritePropertyName("description");
                    writer.WriteValue(tool.Description);

                    if (tool.Parameters["properties"] is JObject properties && properties.HasValues)
                    {
                        writer.WritePropertyName("parameters");
                        CleanSchema(tool.Parameters.DeepClone()).WriteTo(writer);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndArray();
            }

            writer.WritePropertyName("generationConfig");
            writer.WriteStartObject();
            writer.WritePropertyName("maxOutputTokens");
            writer.WriteValue(Settings.maxTokens);

            if (Settings.overrideTemperature)
            {
                writer.WritePropertyName("temperature");
                writer.WriteValue(Settings.temperature);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteContent(JsonWriter writer, string role)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("role");
            writer.WriteValue(role);
            writer.WritePropertyName("parts");
            writer.WriteStartArray();
        }

        private static void EndContent(JsonWriter writer)
        {
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteTextPart(JsonWriter writer, string text)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("text");
            writer.WriteValue(text);
            writer.WriteEndObject();
        }

        private static JToken CleanSchema(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (var key in UnsupportedSchemaKeys)
                {
                    obj.Remove(key);
                }

                foreach (var property in obj.Properties())
                {
                    CleanSchema(property.Value);
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                {
                    CleanSchema(item);
                }
            }

            return token;
        }

        protected override ChatResponse Parse(JObject body)
        {
            var accumulator = new Accumulator(Name, null);
            accumulator.Add(body);
            return accumulator.Build();
        }

        protected override StreamAccumulator CreateStream(Action<string> onTextDelta) => new Accumulator(Name, onTextDelta);

        /// <summary>Both modes share it: a non-streamed answer is a single chunk.</summary>
        private sealed class Accumulator : StreamAccumulator
        {
            private readonly string _provider;
            private readonly JArray _parts = new JArray();
            private readonly List<ToolCall> _calls = new List<ToolCall>();
            private string _finish;
            private TokenUsage _usage;

            public Accumulator(string provider, Action<string> onTextDelta) : base(onTextDelta)
            {
                _provider = provider;
            }

            public void Add(JObject json)
            {
                if (json["error"] is JObject error)
                {
                    Error = (string)error["message"] ?? error.ToString();
                    return;
                }

                OnJson(json);
            }

            protected override void OnJson(JObject json)
            {
                if (json["usageMetadata"] is JObject usage)
                {
                    _usage = new TokenUsage(ReadInt(usage["promptTokenCount"]), ReadInt(usage["candidatesTokenCount"]));
                }

                var blocked = (string)json["promptFeedback"]?["blockReason"];

                if (blocked != null)
                {
                    _finish = blocked;
                }

                var candidate = json["candidates"]?.Type == JTokenType.Array && json["candidates"].HasValues ? json["candidates"][0] : null;

                if (candidate == null)
                {
                    return;
                }

                _finish = (string)candidate["finishReason"] ?? _finish;

                if (!(candidate["content"]?["parts"] is JArray parts))
                {
                    return;
                }

                foreach (var part in parts)
                {
                    _parts.Add(part.DeepClone());

                    if (part["functionCall"] is JObject call)
                    {
                        _calls.Add(new ToolCall((string)call["id"] ?? "call_" + _calls.Count, (string)call["name"], call["args"] as JObject));
                    }
                    else if (part["text"] != null && part.Value<bool?>("thought") != true)
                    {
                        AppendText((string)part["text"]);
                    }
                }
            }

            protected override ChatResponse BuildResponse()
            {
                FinishReason reason;

                switch (_finish)
                {
                    case null:
                    case "STOP":
                        reason = _calls.Count > 0 ? FinishReason.ToolCalls : FinishReason.Stop;
                        break;
                    case "MAX_TOKENS":
                        reason = FinishReason.Length;
                        break;
                    case "SAFETY":
                    case "RECITATION":
                    case "BLOCKLIST":
                    case "PROHIBITED_CONTENT":
                    case "SPII":
                    case "IMAGE_SAFETY":
                        reason = FinishReason.ContentFilter;
                        break;
                    default:
                        reason = FinishReason.Other;
                        break;
                }

                return new ChatResponse
                {
                    Message = ChatMessage.FromProvider(Text.ToString(), _calls, _provider, _parts),
                    RawFinishReason = _finish,
                    FinishReason = reason,
                    Usage = _usage
                };
            }
        }
    }
}
