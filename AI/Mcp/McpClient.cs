using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityREST.AI.Mcp
{
    using Serialization;
    using Streaming;

    /// <summary>
    /// Model Context Protocol client over the Streamable HTTP transport. Lists and calls remote tools,
    /// reads resources and exposes the tools as <see cref="ITool"/> for any <see cref="IChatProvider"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var mcp = new McpClient("https://example.com/mcp", bearerToken);
    /// chat.Tools.AddRange(await mcp.GetToolsAsync());
    /// </code>
    /// </example>
    public sealed class McpClient
    {
        public const string DefaultProtocolVersion = "2025-06-18";

        private const string SessionHeader = "Mcp-Session-Id";
        private const string ProtocolHeader = "MCP-Protocol-Version";
        private const string AcceptBoth = "application/json, text/event-stream";

        private readonly RestClient _client;
        private readonly string _path;
        private long _nextId;
        private string _sessionId;
        private UniTask? _connecting;

        /// <param name="endpoint">Full MCP endpoint URL, e.g. https://host/mcp.</param>
        /// <param name="bearerToken">Optional OAuth access token or API token.</param>
        public McpClient(string endpoint, string bearerToken = null)
        {
            var config = ApiConfig.Create(endpoint);
            config.TimeoutSeconds = 60f;
            config.Retry = new RetryPolicy { maxRetries = 1, baseDelay = 0.5f, maxDelay = 4f };
            _client = new RestClient(config);
            _path = string.Empty;

            if (!string.IsNullOrEmpty(bearerToken))
            {
                _client.SetBearerToken(bearerToken);
            }
        }

        /// <summary>Uses an existing client (custom auth, pinning...) and a path relative to its base URL.</summary>
        public McpClient(RestClient client, string path)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _path = path ?? string.Empty;
        }

        public RestClient Client => _client;

        public string ClientName { get; set; } = "UnityREST";

        public string ClientVersion { get; set; } = "2.0.0";

        public string ProtocolVersion { get; private set; } = DefaultProtocolVersion;

        public bool IsConnected { get; private set; }

        public JObject ServerInfo { get; private set; }

        public JObject ServerCapabilities { get; private set; }

        /// <summary>Usage hints the server provides for the model; worth adding to the system prompt.</summary>
        public string Instructions { get; private set; }

#region Lifecycle

        public UniTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                return UniTask.CompletedTask;
            }

            // Concurrent callers share one handshake.
            _connecting ??= HandshakeAsync(cancellationToken).Preserve();
            return _connecting.Value;
        }

        private async UniTask HandshakeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var parameters = new JObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject { ["name"] = ClientName, ["version"] = ClientVersion }
                };

                var result = await SendAsync("initialize", parameters, cancellationToken);

                ProtocolVersion = (string)result["protocolVersion"] ?? ProtocolVersion;
                ServerInfo = result["serverInfo"] as JObject;
                ServerCapabilities = result["capabilities"] as JObject;
                Instructions = (string)result["instructions"];
                IsConnected = true;

                await NotifyAsync("notifications/initialized", null, cancellationToken);
            }
            finally
            {
                _connecting = null;
            }
        }

        /// <summary>Ends the session on the server (HTTP DELETE). The client can connect again afterwards.</summary>
        public async UniTask CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_sessionId != null)
            {
                using (await _client.Delete(_path).Header(SessionHeader, _sessionId).Retries(0).SendAsync(cancellationToken))
                {
                }
            }

            _sessionId = null;
            IsConnected = false;
        }

#endregion

#region JSON-RPC

        /// <summary>Sends any JSON-RPC request and returns its <c>result</c>. Throws <see cref="McpException"/> on errors.</summary>
        public async UniTask<JToken> RequestAsync(string method, JObject parameters = null, CancellationToken cancellationToken = default)
        {
            await ConnectAsync(cancellationToken);

            try
            {
                return await SendAsync(method, parameters, cancellationToken);
            }
            catch (McpException exception) when (exception.StatusCode == 404 && _sessionId != null)
            {
                // The server dropped the session: start a new one and retry once.
                _sessionId = null;
                IsConnected = false;
                await ConnectAsync(cancellationToken);
                return await SendAsync(method, parameters, cancellationToken);
            }
        }

        public async UniTask NotifyAsync(string method, JObject parameters = null, CancellationToken cancellationToken = default)
        {
            using (var response = await Prepare(method, null, parameters).SendAsync(cancellationToken))
            {
                if (!response.IsSuccess)
                {
                    throw McpException.FromHttp(response);
                }
            }
        }

        private async UniTask<JToken> SendAsync(string method, JObject parameters, CancellationToken cancellationToken)
        {
            var id = Interlocked.Increment(ref _nextId);

            using (var response = await Prepare(method, id, parameters).SendAsync(cancellationToken))
            {
                if (!response.IsSuccess)
                {
                    throw McpException.FromHttp(response);
                }

                var session = response.GetHeader(SessionHeader);

                if (!string.IsNullOrEmpty(session))
                {
                    _sessionId = session;
                }

                var message = (response.GetHeader("Content-Type") ?? string.Empty).IndexOf(ContentTypes.EventStream, StringComparison.OrdinalIgnoreCase) >= 0
                    ? FindInEventStream(response.Data, id)
                    : FindInJson(NewtonsoftJsonSerializer.Default.Deserialize<JToken>(response.Data), id);

                if (message == null)
                {
                    throw new McpException(-32603, $"No response to '{method}' from the MCP server.", response.StatusCode);
                }

                if (message["error"] is JObject error)
                {
                    throw new McpException((int?)error["code"] ?? -32603, (string)error["message"] ?? "MCP error", response.StatusCode, error["data"]);
                }

                return message["result"] ?? new JObject();
            }
        }

        private HttpRequest Prepare(string method, long? id, JObject parameters)
        {
            var request = _client.Post(_path).Accept(AcceptBoth)
                .Json((Method: method, Id: id, Params: parameters), (writer, state) =>
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("jsonrpc");
                    writer.WriteValue("2.0");

                    if (state.Id.HasValue)
                    {
                        writer.WritePropertyName("id");
                        writer.WriteValue(state.Id.Value);
                    }

                    writer.WritePropertyName("method");
                    writer.WriteValue(state.Method);

                    if (state.Params != null)
                    {
                        writer.WritePropertyName("params");
                        state.Params.WriteTo(writer);
                    }

                    writer.WriteEndObject();
                });

            if (_sessionId != null)
            {
                request.Header(SessionHeader, _sessionId);
            }

            if (IsConnected)
            {
                request.Header(ProtocolHeader, ProtocolVersion);
            }

            return request;
        }

        private static JToken FindInJson(JToken body, long id)
        {
            if (body is JArray batch)
            {
                foreach (var item in batch)
                {
                    if (IsResponseTo(item, id))
                    {
                        return item;
                    }
                }

                return null;
            }

            return IsResponseTo(body, id) ? body : null;
        }

        private static JToken FindInEventStream(ReadOnlySpan<byte> data, long id)
        {
            var collector = new ResponseCollector(id);
            var parser = new SseParser();
            parser.Feed(data, collector);
            parser.Complete(collector);
            return collector.Message;
        }

        private static bool IsResponseTo(JToken token, long id) =>
            token is JObject obj && obj["id"] is JValue value && value.Type == JTokenType.Integer && (long)value == id &&
            (obj["result"] != null || obj["error"] != null);

        /// <summary>Picks our response out of an SSE body; server notifications and requests are ignored.</summary>
        private sealed class ResponseCollector : ISseListener
        {
            private readonly long _id;

            public ResponseCollector(long id)
            {
                _id = id;
            }

            public JToken Message { get; private set; }

            public void OnEvent(ReadOnlySpan<byte> eventType, ReadOnlySpan<byte> data)
            {
                if (Message != null)
                {
                    return;
                }

                try
                {
                    Message = FindInJson(NewtonsoftJsonSerializer.Default.Deserialize<JToken>(data), _id);
                }
                catch (JsonException)
                {
                }
            }
        }

#endregion

#region Tools and resources

        public async UniTask<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default)
        {
            var tools = new List<McpToolInfo>();
            string cursor = null;

            do
            {
                var result = await RequestAsync("tools/list", cursor == null ? null : new JObject { ["cursor"] = cursor }, cancellationToken);

                if (result["tools"] is JArray page)
                {
                    foreach (var tool in page)
                    {
                        tools.Add(new McpToolInfo((string)tool["name"], (string)tool["title"], (string)tool["description"],
                            tool["inputSchema"] as JObject ?? new JObject { ["type"] = "object" }));
                    }
                }

                cursor = (string)result["nextCursor"];
            } while (!string.IsNullOrEmpty(cursor));

            return tools;
        }

        public async UniTask<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken cancellationToken = default)
        {
            var result = await RequestAsync("tools/call", new JObject { ["name"] = name, ["arguments"] = arguments ?? new JObject() }, cancellationToken);
            return new McpToolResult(result["content"] as JArray ?? new JArray(), result["structuredContent"], (bool?)result["isError"] ?? false);
        }

        /// <summary>The server's tools as <see cref="ITool"/>. Names are sanitized for model APIs and optionally prefixed.</summary>
        public async UniTask<IReadOnlyList<ITool>> GetToolsAsync(string namePrefix = null, CancellationToken cancellationToken = default)
        {
            var infos = await ListToolsAsync(cancellationToken);
            var tools = new List<ITool>(infos.Count);

            foreach (var info in infos)
            {
                tools.Add(new McpTool(this, info, namePrefix));
            }

            return tools;
        }

        public async UniTask<JArray> ListResourcesAsync(CancellationToken cancellationToken = default)
        {
            var result = await RequestAsync("resources/list", null, cancellationToken);
            return result["resources"] as JArray ?? new JArray();
        }

        /// <summary>Text of a resource; binary contents are returned as their base64 blob.</summary>
        public async UniTask<string> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
        {
            var result = await RequestAsync("resources/read", new JObject { ["uri"] = uri }, cancellationToken);
            var builder = new StringBuilder();

            foreach (var content in result["contents"] as JArray ?? new JArray())
            {
                builder.Append((string)content["text"] ?? (string)content["blob"]);
            }

            return builder.ToString();
        }

#endregion
    }

    public sealed class McpToolInfo
    {
        public McpToolInfo(string name, string title, string description, JObject inputSchema)
        {
            Name = name;
            Title = title;
            Description = description;
            InputSchema = inputSchema;
        }

        public string Name { get; }

        public string Title { get; }

        public string Description { get; }

        public JObject InputSchema { get; }
    }

    public sealed class McpToolResult
    {
        public McpToolResult(JArray content, JToken structuredContent, bool isError)
        {
            Content = content;
            StructuredContent = structuredContent;
            IsError = isError;
        }

        /// <summary>Content blocks: text, image, audio, resource_link, resource.</summary>
        public JArray Content { get; }

        public JToken StructuredContent { get; }

        public bool IsError { get; }

        /// <summary>Text blocks joined; falls back to the structured content as JSON.</summary>
        public string Text
        {
            get
            {
                var builder = new StringBuilder();

                foreach (var block in Content)
                {
                    if ((string)block["type"] == "text")
                    {
                        builder.Append(builder.Length > 0 ? "\n" : string.Empty).Append((string)block["text"]);
                    }
                }

                return builder.Length == 0 && StructuredContent != null ? StructuredContent.ToString(Formatting.None) : builder.ToString();
            }
        }
    }

    public sealed class McpException : Exception
    {
        public McpException(int code, string message, long statusCode = 0, JToken data = null) : base(message)
        {
            Code = code;
            StatusCode = statusCode;
            ErrorData = data;
        }

        /// <summary>JSON-RPC error code.</summary>
        public int Code { get; }

        public long StatusCode { get; }

        /// <summary>Optional <c>error.data</c> from the server.</summary>
        public JToken ErrorData { get; }

        internal static McpException FromHttp(in HttpResponse response) =>
            new McpException(-32000, $"MCP transport error: {response.ErrorMessage} {response.Text}".Trim(), response.StatusCode);
    }

    /// <summary>Adapts a remote MCP tool to <see cref="ITool"/>.</summary>
    internal sealed class McpTool : ITool
    {
        // Tool names accepted by every provider: ^[a-zA-Z0-9_-]{1,64}$.
        private static readonly Regex InvalidName = new Regex("[^a-zA-Z0-9_-]", RegexOptions.Compiled);

        private readonly McpClient _client;
        private readonly string _remoteName;

        public McpTool(McpClient client, McpToolInfo info, string prefix)
        {
            _client = client;
            _remoteName = info.Name;

            var name = InvalidName.Replace((prefix ?? string.Empty) + info.Name, "_");
            Name = name.Length > 64 ? name.Substring(0, 64) : name;
            Description = info.Description ?? info.Title ?? info.Name;
            Parameters = info.InputSchema;
        }

        public string Name { get; }

        public string Description { get; }

        public JObject Parameters { get; }

        public async UniTask<string> InvokeAsync(JObject arguments, CancellationToken cancellationToken)
        {
            var result = await _client.CallToolAsync(_remoteName, arguments, cancellationToken);

            if (result.IsError)
            {
                throw new InvalidOperationException(result.Text);
            }

            return result.Text;
        }
    }
}
