# 13. Extender UnityREST

> Estado a 2026-10-21.

## Interceptores

`IHttpInterceptor` ve cada `UnityWebRequest` justo antes de enviarse y cada respuesta al final, después de los reintentos.

```csharp
public sealed class HmacSigner : IHttpInterceptor
{
    private readonly byte[] _secret;

    public HmacSigner(byte[] secret) => _secret = secret;

    public void OnSend(UnityWebRequest request)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using (var hmac = new HMACSHA256(_secret))
        {
            var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(request.method + request.url + timestamp));
            request.SetRequestHeader("X-Timestamp", timestamp);
            request.SetRequestHeader("X-Signature", Convert.ToBase64String(signature));
        }
    }

    public void OnReceive(in HttpResponse response)
    {
    }
}

client.AddInterceptor(new HmacSigner(secret));
```

Otros usos: IDs de traza, métricas de latencia (`response.ElapsedSeconds`), contar errores 5xx o mostrar un aviso global de "sin conexión". `OnSend` corre una vez por intento.

## Serializador propio

`RestClient.Serializer` acepta cualquier `IJsonSerializer`:

```csharp
public interface IJsonSerializer
{
    void Serialize<T>(T value, Stream output);
    T Deserialize<T>(ReadOnlySpan<byte> utf8Json);
}
```

Para cambiar la configuración de Json.NET (por ejemplo, nombres en snake_case):

```csharp
client.Serializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings
{
    ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
    NullValueHandling = NullValueHandling.Ignore
});
```

El serializador por defecto omite las propiedades `null` al serializar.

## Proveedor de IA propio

Para una API que no sigue ninguno de los tres formatos, o para tu backend con su propio contrato, implementa `IChatProvider`:

```csharp
public sealed class MyBackendProvider : IChatProvider
{
    private readonly RestClient _client = new RestClient("https://game.example.com/ai");

    public string Name => "my-backend";

    public ChatProviderSettings Settings { get; } = new ChatProviderSettings();

    public async UniTask<ChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ITool> tools = null,
        Action<string> onTextDelta = null, CancellationToken cancellationToken = default)
    {
        var result = await _client.Post("chat").Json(new { messages = messages.Select(m => new { role = m.Role.ToString(), text = m.Content }) })
            .SendAsync<MyReply>(cancellationToken);

        if (!result.IsSuccess)
        {
            return ChatResponse.Failed(result.Error, result.StatusCode, result.ErrorMessage);
        }

        onTextDelta?.Invoke(result.Value.text);
        return new ChatResponse(ChatMessage.Assistant(result.Value.text), FinishReason.Stop);
    }
}
```

Para pedir herramientas, devuelve `new ChatResponse(ChatMessage.Assistant(texto, new[] { new ToolCall(id, nombre, args) }), FinishReason.ToolCalls)`. `ChatSession` las ejecuta y te devuelve los resultados como mensajes `ChatRole.Tool`.

Para un formato parecido a los existentes, también puedes heredar de `ChatProviderBase`, que ya resuelve el transporte, el streaming SSE y los errores: implementa `GetPath`, `WriteRequest`, `Parse` y `CreateStream`.

## Usar las piezas sueltas

| Pieza | Para qué |
|---|---|
| `PooledBufferWriter` | Búfer de bytes reutilizable sobre `ArrayPool` (`Stream` + `IBufferWriter<byte>`) |
| `SseParser` | Parsear `text/event-stream` desde cualquier fuente |
| `NewtonsoftJsonSerializer.Write(stream, estado, escritor)` | Escribir JSON a mano con un escritor del pool |
| `PinnedCertificateHandler` | Pinning en tus propias `UnityWebRequest` |
| `PinnedCertificateHandler.ComputeSpkiSha256(cert)` | Calcular el pin de un certificado |
