# 16. Referencia de la API

> Estado a 2026-10-21, versión 2.0.0. Solo los tipos públicos.

## UnityREST

### ApiConfig (ScriptableObject)

| Miembro | Descripción |
|---|---|
| `Client` | `RestClient` compartido |
| `Environments`, `Endpoints` | Listas de solo lectura |
| `ActiveEnvironment`, `ActiveEnvironmentIndex` | Entorno activo |
| `SetActiveEnvironment(string)` | Por nombre; devuelve `false` si no existe |
| `TryGetEndpoint(string, out Endpoint)` | Busca por nombre |
| `TimeoutSeconds`, `Retry`, `EnforceHttps`, `LogLevel` | Transporte |
| `static Create(baseUrl, params Endpoint[])` | Configuración en memoria |

`ApiEnvironment`: `Name`, `BaseUrl`, `ApiKeyHeader`, `ApiKey`, `Headers`, `CertificatePins`, `IsSecure`.
`Endpoint(name, method, path)`: `Name`, `Method`, `Path`.
`EndpointRef`: `Config`, `Name`, `IsValid`, `Request()`.

### RestClient

| Miembro | Descripción |
|---|---|
| `RestClient(ApiConfig)` / `RestClient(string baseUrl)` | Constructores |
| `Get`, `Post`, `Put`, `Patch`, `Delete`, `Head(path)` | Crean una `HttpRequest` |
| `Request(HttpMethod, path)` | Verbo arbitrario |
| `Endpoint(name)` | Desde la configuración; `KeyNotFoundException` si no existe |
| `SetHeader`, `RemoveHeader` | Cabeceras del cliente |
| `SetBearerToken`, `SetBasicAuth`, `ClearAuthorization`, `HasAuthorization` | Autenticación |
| `RefreshAuthorization` | `Func<RestClient, CancellationToken, UniTask<bool>>` ante un 401 |
| `AddInterceptor`, `RemoveInterceptor` | `IHttpInterceptor` |
| `Serializer` | `IJsonSerializer` (por defecto `NewtonsoftJsonSerializer.Default`) |
| `Config` | El `ApiConfig` |

### HttpRequest (fluida, en pool)

| Grupo | Métodos |
|---|---|
| Ruta y query | `Path(name, string|long)`, `Query(key, string|long|double|float|bool)` |
| Cabeceras | `Header(name, value)`, `Accept(mediaType)` |
| Cuerpo | `Json<T>(value)`, `Json<TState>(state, Action<JsonWriter, TState>)`, `Text(text, contentType)`, `Bytes(span, contentType)`, `Form(key, value)`, `Multipart(name, value)`, `Multipart(name, bytes, fileName, contentType)` |
| Respuesta | `ToFile(path, append)`, `AsTexture(readable)`, `AsAudio(type, stream)`, `Stream(ISseListener)` |
| Opciones | `Timeout(seconds)`, `Retries(n)`, `Progress(IProgress<float>)` |
| Envío | `SendAsync(ct)` → `HttpResponse`; `SendAsync<T>(ct)` → `HttpResult<T>` |
| Estado | `Method` |

### HttpResponse (readonly struct, IDisposable)

`IsSuccess`, `Error`, `StatusCode`, `ErrorMessage`, `Url`, `Attempts`, `ElapsedSeconds`, `Data` (`ReadOnlySpan<byte>`), `Text`, `Texture`, `AudioClip`, `GetHeader(name)`, `ReadJson<T>(serializer)`, `WebRequest`, `Dispose()`.

### HttpResult&lt;T&gt; (readonly struct)

`IsSuccess`, `Value`, `Error`, `StatusCode`, `ErrorMessage`, `TryGetValue(out T)`.

### Enums y constantes

- `HttpMethod`: `Get`, `Post`, `Put`, `Patch`, `Delete`, `Head`, `Options`.
- `HttpError`: `None`, `Http`, `Network`, `Timeout`, `Cancelled`, `Deserialization`, `InsecureUrl`.
- `HttpLogLevel`: `None`, `Errors`, `Info`, `Verbose`.
- `ContentTypes`: `Json`, `TextUtf8`, `OctetStream`, `FormUrlEncoded`, `EventStream`.

### Otros

| Tipo | Miembros |
|---|---|
| `RetryPolicy` (struct) | `maxRetries`, `baseDelay`, `maxDelay`, `retryUnsafeMethods`, `Default`, `None` |
| `HttpHeader` (struct) | `Name`, `Value` |
| `IHttpInterceptor` | `OnSend(UnityWebRequest)`, `OnReceive(in HttpResponse)` |
| `SecretAttribute` | Enmascara un `string` en el inspector |
| `UnityREST.Serialization.IJsonSerializer` | `Serialize<T>(value, Stream)`, `Deserialize<T>(ReadOnlySpan<byte>)` |
| `UnityREST.Serialization.NewtonsoftJsonSerializer` | `Default`, `Serializer`, ctor con `JsonSerializerSettings`, `static Write(stream, state, write)` |
| `UnityREST.Streaming.ISseListener` | `OnEvent(eventType, data)` |
| `UnityREST.Streaming.SseParser` | `Feed(chunk, listener)`, `Complete(listener)`, `Reset()` |
| `UnityREST.Security.PinnedCertificateHandler` | ctor con pines, `PinCount`, `static ComputeSpkiSha256(X509Certificate2)` |
| `UnityREST.Buffers.PooledBufferWriter` | `Stream` e `IBufferWriter<byte>`: `WrittenSpan`, `WrittenCount`, `Clear()`, `WriteUtf8(span)` |

## UnityREST.AI

| Tipo | Miembros |
|---|---|
| `IChatProvider` | `Name`, `Settings`, `CompleteAsync(messages, tools, onTextDelta, ct)` |
| `ChatProviderBase` | Base de los proveedores: `Client`, `GetPath`, `WriteRequest`, `Parse`, `CreateStream` |
| `OpenAIChatProvider`, `AnthropicChatProvider`, `GeminiChatProvider` | Los tres formatos |
| `ChatProviderSettings` | Campos `kind`, `baseUrl`, `model`, `apiKey`, `apiKeyEnvironmentVariable`, `maxTokens`, `overrideTemperature`, `temperature`, `effort`, `maxTokensParameter`, `timeoutSeconds`, `headers`; `ResolveApiKey()`, `CreateProvider()`; presets `OpenAI`, `Anthropic`, `Gemini`, `Ollama`, `LmStudio`, `OpenRouter`, `OpenAICompatible` |
| `ChatProviderAsset` | `Settings`, `CreateProvider()` |
| `ChatMessage` | `Role`, `Content`, `Images`, `ToolCalls`, `ToolCallId`, `ToolName`, `IsError`; `System`, `User(text, images)`, `Assistant(text)`, `Assistant(text, toolCalls)`, `ToolResult(call, content, isError)` |
| `ChatRole` | `System`, `User`, `Assistant`, `Tool` |
| `ChatImage` | `MediaType`, `Base64Data`, `FromPng`, `FromJpg`, `FromTexture` |
| `ToolCall` | `Id`, `Name`, `Arguments` (`JObject`) |
| `ChatResponse` | `Message`, `Text`, `FinishReason`, `RawFinishReason`, `Usage`, `Error`, `StatusCode`, `ErrorMessage`, `IsSuccess`; ctor público; `Failed(...)` |
| `FinishReason` | `Stop`, `Length`, `ToolCalls`, `Refusal`, `ContentFilter`, `Error`, `Other` |
| `TokenUsage` | `InputTokens`, `OutputTokens`, `+` |
| `ChatSession` | `Provider`, `History`, `SystemPrompt`, `Tools`, `MaxToolRounds`, `ApproveToolCall`, `ToolExecuted`, `RoundCompleted`, `TotalUsage`, `SendAsync(text|message, onTextDelta, ct)`, `TrimHistory(n)`, `Clear()` |
| `ITool` | `Name`, `Description`, `Parameters`, `InvokeAsync(args, ct)` |
| `Tool` | `Create(name, description, schema, Func<JObject, string>)`, `Create(..., Func<JObject, CancellationToken, UniTask<string>>)` |
| `ToolSchema` | `String`, `Number`, `Integer`, `Boolean`, `Enum`, `Array`, `Object`, `Build()`, conversión implícita a `JObject` |

### UnityREST.AI.Mcp

| Tipo | Miembros |
|---|---|
| `McpClient` | ctor `(endpoint, bearerToken)` y `(RestClient, path)`; `ConnectAsync`, `CloseAsync`, `RequestAsync`, `NotifyAsync`, `ListToolsAsync`, `CallToolAsync`, `GetToolsAsync(prefix)`, `ListResourcesAsync`, `ReadResourceAsync`; `ServerInfo`, `ServerCapabilities`, `Instructions`, `ProtocolVersion`, `IsConnected`, `ClientName`, `ClientVersion`, `Client`; `DefaultProtocolVersion` |
| `McpToolInfo` | `Name`, `Title`, `Description`, `InputSchema` |
| `McpToolResult` | `Content`, `StructuredContent`, `IsError`, `Text` |
| `McpException` | `Code`, `StatusCode`, `ErrorData` |
