using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityREST.AI
{
    using Serialization;
    using Streaming;

    /// <summary>
    /// Shared transport for chat providers: one <see cref="RestClient"/>, JSON written straight into the
    /// pooled request body, buffered or streamed (SSE) responses. Subclasses only map the wire format.
    /// </summary>
    public abstract class ChatProviderBase : IChatProvider
    {
        protected ChatProviderBase(ChatProviderSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            var config = ApiConfig.Create(settings.baseUrl);
            config.TimeoutSeconds = settings.timeoutSeconds;
            config.Retry = new RetryPolicy { maxRetries = 2, baseDelay = 1f, maxDelay = 20f };
            Client = new RestClient(config);

            foreach (var header in settings.headers ?? Array.Empty<HttpHeader>())
            {
                Client.SetHeader(header.Name, header.Value);
            }
        }

        public abstract string Name { get; }

        public ChatProviderSettings Settings { get; }

        public RestClient Client { get; }

        public async UniTask<ChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ITool> tools = null,
            Action<string> onTextDelta = null, CancellationToken cancellationToken = default)
        {
            var stream = onTextDelta != null;
            var request = Client.Post(GetPath(stream))
                .Json((Provider: this, Messages: messages, Tools: tools ?? Array.Empty<ITool>(), Stream: stream),
                    (writer, state) => state.Provider.WriteRequest(writer, state.Messages, state.Tools, state.Stream));

            if (stream)
            {
                var accumulator = CreateStream(onTextDelta);

                using (var response = await request.Stream(accumulator).SendAsync(cancellationToken))
                {
                    return response.IsSuccess ? accumulator.Build() : Fail(response);
                }
            }

            using (var response = await request.SendAsync(cancellationToken))
            {
                if (!response.IsSuccess)
                {
                    return Fail(response);
                }

                try
                {
                    return Parse(response.ReadJson<JObject>());
                }
                catch (Exception exception) when (exception is JsonException || exception is InvalidCastException)
                {
                    return ChatResponse.Failed(HttpError.Deserialization, response.StatusCode, exception.Message);
                }
            }
        }

        protected abstract string GetPath(bool stream);

        protected abstract void WriteRequest(JsonWriter writer, IReadOnlyList<ChatMessage> messages, IReadOnlyList<ITool> tools, bool stream);

        protected abstract ChatResponse Parse(JObject body);

        protected abstract StreamAccumulator CreateStream(Action<string> onTextDelta);

        private static ChatResponse Fail(in HttpResponse response)
        {
            var body = response.Text;
            string message = null;

            // Surface the provider's own error message when the body is JSON.
            if (!string.IsNullOrEmpty(body) && body.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    var json = JObject.Parse(body);
                    var error = json["error"];
                    message = error?.Type == JTokenType.Object ? (string)error["message"] : (string)error ?? (string)json["message"];
                }
                catch (JsonException)
                {
                }
            }

            return ChatResponse.Failed(response.Error, response.StatusCode, message ?? (string.IsNullOrEmpty(body) ? response.ErrorMessage : body));
        }

#region Helpers for subclasses

        protected static void WriteSystemText(JsonWriter writer, IReadOnlyList<ChatMessage> messages)
        {
            var builder = new StringBuilder();

            foreach (var message in messages)
            {
                if (message.Role == ChatRole.System)
                {
                    builder.Append(builder.Length > 0 ? "\n\n" : string.Empty).Append(message.Content);
                }
            }

            writer.WriteValue(builder.ToString());
        }

        protected static bool HasSystem(IReadOnlyList<ChatMessage> messages)
        {
            foreach (var message in messages)
            {
                if (message.Role == ChatRole.System)
                {
                    return true;
                }
            }

            return false;
        }

        protected static JObject ParseArguments(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new JObject();
            }

            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException)
            {
                // Truncated or invalid arguments: keep them visible to the tool instead of failing the turn.
                return new JObject { ["_raw"] = json };
            }
        }

        protected static int ReadInt(JToken token) => token != null && token.Type == JTokenType.Integer ? (int)token : 0;

#endregion

        /// <summary>Collects a streamed answer. Events arrive on the main thread.</summary>
        protected abstract class StreamAccumulator : ISseListener
        {
            private static readonly byte[] Done = Encoding.ASCII.GetBytes("[DONE]");

            protected readonly Action<string> OnTextDelta;
            protected readonly StringBuilder Text = new StringBuilder();
            protected string Error;

            protected StreamAccumulator(Action<string> onTextDelta)
            {
                OnTextDelta = onTextDelta;
            }

            public void OnEvent(ReadOnlySpan<byte> eventType, ReadOnlySpan<byte> data)
            {
                if (data.SequenceEqual(Done))
                {
                    return;
                }

                JObject json;

                try
                {
                    json = NewtonsoftJsonSerializer.Default.Deserialize<JObject>(data);
                }
                catch (JsonException exception)
                {
                    Error = exception.Message;
                    return;
                }

                if (json == null)
                {
                    return;
                }

                if (json["error"] is JToken error && error.Type != JTokenType.Null)
                {
                    Error = error.Type == JTokenType.Object ? (string)error["message"] ?? error.ToString() : error.ToString();
                    return;
                }

                OnJson(json);
            }

            protected void AppendText(string delta)
            {
                if (string.IsNullOrEmpty(delta))
                {
                    return;
                }

                Text.Append(delta);
                OnTextDelta?.Invoke(delta);
            }

            protected abstract void OnJson(JObject json);

            protected abstract ChatResponse BuildResponse();

            public ChatResponse Build() => Error != null ? ChatResponse.Failed(HttpError.Http, 200, Error) : BuildResponse();
        }
    }
}
