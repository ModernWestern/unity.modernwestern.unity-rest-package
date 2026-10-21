# Changelog

## [2.0.0] - 2026-10-20
Reescritura completa. Rompe compatibilidad con 1.x (ver "Migración" en el README).

### Añadido
- `RestClient` con peticiones fluidas y en pool (`Get/Post/Put/Patch/Delete/Head`), resultados tipados `HttpResult<T>` que no lanzan excepciones y respuestas crudas `HttpResponse` leídas desde memoria nativa.
- Cuerpos JSON, texto, bytes, `x-www-form-urlencoded` y `multipart/form-data`; descargas a textura, audio y archivo; progreso.
- Server-Sent Events con parser incremental sin asignaciones (`.Stream(ISseListener)`).
- Reintentos con backoff exponencial, jitter y `Retry-After`, solo en métodos seguros; timeout y cancelación por petición.
- Refresh automático de autorización ante 401, cabeceras por entorno, cliente y petición, e interceptores.
- HTTPS obligatorio por defecto (loopback permitido) y pinning de clave pública SHA-256 por entorno.
- Un solo asset `ApiConfig` con entornos y endpoints con nombre; `EndpointRef` para elegirlos en el inspector.
- Inspector nuevo: barra de entornos, validaciones, endpoints en una línea con el verbo en color y probador de peticiones; drawers `[Secret]`, `HttpHeader`, `Endpoint` y `EndpointRef`.
- Módulo opcional `UnityREST.AI`: proveedores compatibles con OpenAI, Anthropic y Gemini, streaming, imágenes, herramientas (`Tool`, `ToolSchema`), `ChatSession` con bucle de herramientas y cliente MCP (Streamable HTTP).
- Ventana **Window → UnityREST → AI Playground** y asset `ChatProviderAsset` con presets.
- `ChatSession.SystemPrompt` editable, `TrimHistory` (sin separar herramientas de sus resultados) y evento `RoundCompleted`; constructores públicos de `ChatResponse` y `ChatMessage.Assistant(text, toolCalls)` para proveedores propios.
- Documentación completa en `Documentation~/` (16 guías en español).
- 40 pruebas EditMode, incluidas pruebas de asignación y de integración con servidor local.

### Cambiado
- Mínimo Unity 2022.3. Se quitan `scopedRegistries` de `package.json` (Unity solo los lee del manifiesto del proyecto).

### Eliminado
- `APIManager`, `WebTransport`, `WebTransportBase`, `IWebTransport`, `WebResult`, `APIEnvironment`, `APIPaths`, `URLBuilder`, `JBuilder`, `RequestProfiler`, `HideEnum` y `ReadOnly`.

### Corregido
- Ya no se reintentan 4xx ni POST tras errores ambiguos, `PATCH` se envía en mayúsculas, query y ruta se escapan, y un endpoint inexistente ya no produce una ruta `null`.

## [1.1.4] - 2025-02-21
Support tuples for (key, value) parameters. Minor issues solved.

## [1.1.3] - 2024-11-24
An issue when a webrequest got locked was solved.

## [1.1.2] - 2024-11-24
OpenUPM scopes added.

## [1.1.1] - 2024-11-24
Implementing of the com.cysharp.unitask package.

## [1.0.0] - 2024-04-11
This is the first release of UnityRest package.
