# 15. Solución de problemas

> Estado a 2026-10-21.

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| `Cannot resolve package com.cysharp.unitask` | Falta el registro de OpenUPM | Agrega `scopedRegistries` al manifiesto del proyecto ([instalación](01-instalacion.md)) |
| `HttpError.InsecureUrl` | URL `http://` con Enforce Https | Usa HTTPS, o loopback (`localhost`/`127.0.0.1`) para servidores locales |
| `InvalidOperationException: Insecure connection not allowed` | Player Settings bloquea HTTP | **Allow downloads over HTTP**, o usa HTTPS |
| `HttpError.Network` contra tu servidor de pruebas con certificado propio | Unity no confía en ese certificado | Agrega su pin en **Certificate Pins** de ese entorno, o usa un certificado válido |
| `HttpError.Network` tras activar el pinning | El pin no coincide (el servidor cambió de clave, o fijaste el de un intermedio) | Recalcula el pin del certificado final del host ([seguridad](07-seguridad.md)) |
| `HttpError.Deserialization` | El JSON no coincide con `T` | Mira `ErrorMessage`; prueba con `SendAsync<string>()` para ver el cuerpo; revisa nombres (`[JsonProperty]`) |
| `Value` es `null` con `IsSuccess` | Cuerpo vacío (204) | Es lo esperado; no hay contenido |
| `InvalidOperationException: This HttpRequest was already sent` | Se reutilizó una petición | Crea una nueva con `client.Get(...)` en cada envío |
| `KeyNotFoundException: Endpoint 'x' is not defined` | El nombre no existe en el `ApiConfig` | Revisa el nombre; usa `EndpointRef` para elegirlo en el inspector |
| El placeholder `{id}` aparece en la URL | Falta `.Path("id", ...)` o el nombre no coincide | El nombre distingue mayúsculas: `{userId}` necesita `.Path("userId", ...)` |
| `HttpError.Timeout` en descargas grandes | El timeout aplica al intento completo | `.Timeout(0)` o un valor mayor en esa petición |
| `HttpError.Cancelled` sin cancelar a mano | Se destruyó el objeto dueño del token | Es lo esperado con `GetCancellationTokenOnDestroy()` |
| Memoria nativa que crece | Respuestas crudas sin liberar | Usa `using` con `SendAsync()`, o `SendAsync<T>()` |
| IA: `400 ... temperature` | El modelo no acepta temperatura | Desactiva `overrideTemperature` |
| IA: `400 ... max_tokens` con OpenAI | El modelo pide `max_completion_tokens` | `maxTokensParameter = "max_completion_tokens"` (el preset `OpenAI()` ya lo trae) |
| IA: `401` | Falta la clave o la variable de entorno | Revisa `apiKey` o `apiKeyEnvironmentVariable`; reinicia Unity después de crear la variable |
| IA: la respuesta termina con `FinishReason.Length` | Llegó a `maxTokens` | Sube `maxTokens` o pide respuestas más cortas |
| IA: `Refusal` | El modelo rechazó el pedido | Revisa el contenido o el system prompt |
| MCP: `McpException` con 401/403 | Token faltante o vencido | Pasa el token al crear `McpClient` |
| MCP: `McpException` con 404 repetido | URL incorrecta | La URL debe ser el endpoint MCP completo, como `https://host/mcp` |

## Diagnóstico

1. Pon `ApiConfig.LogLevel = Verbose` y repite la petición: verás URL, código, intentos y cuerpos.
2. Prueba la misma ruta en el **Request tester** del inspector.
3. Con `SendAsync()`, mira `response.WebRequest.error` y las cabeceras de respuesta.
4. Para IA, el **AI Playground** muestra el error del proveedor tal cual.
