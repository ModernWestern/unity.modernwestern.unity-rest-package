# 12. MCP (Model Context Protocol)

> Estado a 2026-10-21. Probado contra un servidor MCP simulado; versión de protocolo `2025-06-18`.

MCP es un estándar para que un servidor exponga herramientas, recursos y prompts a modelos de lenguaje. `McpClient` se conecta a servidores remotos con el transporte **Streamable HTTP** y convierte sus herramientas en `ITool`, que funcionan con cualquier proveedor de `ChatSession`.

## Conectar y usar sus herramientas

```csharp
var mcp = new McpClient("https://tools.example.com/mcp", accessToken);   // el token es opcional
chat.Tools.AddRange(await mcp.GetToolsAsync("remote_", token));          // el prefijo evita choques de nombres
```

La conexión se hace sola en la primera llamada:

1. `initialize`, con la versión de protocolo y la información del cliente.
2. Guarda la sesión que devuelve el servidor (`Mcp-Session-Id`) y la manda en cada petición, junto con `MCP-Protocol-Version`.
3. Envía `notifications/initialized`.

Si varias llamadas arrancan a la vez, comparten un solo handshake. Si el servidor pierde la sesión (404), el cliente se reconecta y repite la petición una vez.

## API

| Miembro | Uso |
|---|---|
| `ConnectAsync()` | Conecta explícitamente, por ejemplo al cargar la escena |
| `ListToolsAsync()` | `McpToolInfo` (nombre, título, descripción, esquema), con paginación |
| `CallToolAsync(nombre, args)` | `McpToolResult`: `Content` (bloques), `StructuredContent`, `IsError`, `Text` |
| `GetToolsAsync(prefijo)` | Las herramientas como `ITool`, con nombres saneados para los proveedores |
| `ListResourcesAsync()` / `ReadResourceAsync(uri)` | Recursos del servidor |
| `RequestAsync(método, params)` | Cualquier método JSON-RPC (`prompts/list`, `prompts/get`...) |
| `NotifyAsync(método, params)` | Notificaciones sin respuesta |
| `CloseAsync()` | Cierra la sesión en el servidor (`DELETE`) |
| `ServerInfo`, `ServerCapabilities`, `Instructions`, `ProtocolVersion`, `IsConnected` | Lo que informó el servidor |
| `ClientName`, `ClientVersion` | Cómo se presenta el cliente (por defecto `UnityREST` y `2.0.0`) |

Las respuestas pueden venir como JSON o como SSE: el cliente reconoce las dos y, en SSE, ignora las notificaciones que lleguen antes de la respuesta.

## Errores

`McpClient` **lanza** `McpException` (a diferencia de las peticiones REST), con `Code` (JSON-RPC), `StatusCode` (HTTP) y `ErrorData`. Las herramientas que entrega `GetToolsAsync` convierten los errores en resultados de error para el modelo, así que en un `ChatSession` no tienes que capturarlos.

```csharp
try
{
    var result = await mcp.CallToolAsync("dice_roll", new JObject { ["sides"] = 20 });
    Debug.Log(result.Text);
}
catch (McpException e)
{
    Debug.LogWarning($"MCP {e.Code}: {e.Message}");
}
```

## Con autenticación avanzada o pinning

Usa un `RestClient` propio para el transporte:

```csharp
var config = ApiConfig.Create("https://tools.example.com");
var client = new RestClient(config);
client.SetBearerToken(token);
var mcp = new McpClient(client, "mcp");
```

## Límites actuales

- Solo cliente HTTP (Streamable HTTP). No incluye el transporte `stdio`, porque los juegos no lanzan procesos locales.
- No responde pedidos del servidor al cliente (sampling, elicitation); se ignoran.
- El flujo OAuth para obtener el token lo resuelve tu aplicación; el cliente recibe el token ya listo.
