# Documentación de UnityREST

> Estado a 2026-10-21: versión 2.0.0. Probado en Unity 2022.3.62f1 y 6000.1.5f1 con 40 pruebas EditMode.

UnityREST es un cliente HTTP/HTTPS para Unity con dos partes:

- **`UnityREST`** (obligatoria): peticiones REST tipadas, reintentos, timeouts, autenticación, pinning de certificados, Server-Sent Events, descargas y un inspector con probador de peticiones.
- **`UnityREST.AI`** (opcional): chat con modelos de lenguaje de cualquier proveedor (compatible con OpenAI, Anthropic y Gemini), herramientas, streaming y cliente MCP.

## Guías

| # | Guía | Qué cubre |
|---|---|---|
| 1 | [Instalación](01-instalacion.md) | Requisitos, manifiesto, ejemplos, pruebas |
| 2 | [Inicio rápido](02-inicio-rapido.md) | Tu primera petición en cinco minutos |
| 3 | [ApiConfig: entornos y endpoints](03-apiconfig.md) | El asset de configuración, `EndpointRef`, configuración por código |
| 4 | [Construir peticiones](04-peticiones.md) | Verbos, ruta, query, cabeceras, cuerpos, opciones |
| 5 | [Respuestas y errores](05-respuestas-y-errores.md) | `HttpResult<T>`, `HttpResponse`, `HttpError`, reintentos |
| 6 | [Autenticación](06-autenticacion.md) | Bearer, Basic, API key, refresh ante 401 |
| 7 | [HTTPS y seguridad](07-seguridad.md) | HTTPS obligatorio, pinning, secretos, logs |
| 8 | [Descargas, subidas y streaming](08-descargas-y-streaming.md) | Texturas, audio, archivos, multipart, progreso, SSE |
| 9 | [Rendimiento y memoria](09-rendimiento.md) | Qué no asigna memoria, qué sí, buenas prácticas |
| 10 | [Herramientas del editor](10-editor.md) | Inspector, probador, drawers, AI Playground |
| 11 | [IA: proveedores y chat](11-ia.md) | `ChatProviderSettings`, `ChatSession`, herramientas, imágenes |
| 12 | [MCP](12-mcp.md) | Conectar servidores Model Context Protocol |
| 13 | [Extender UnityREST](13-extender.md) | Interceptores, serializador propio, proveedor de IA propio |
| 14 | [Migración desde 1.x](14-migracion.md) | Equivalencias con la API anterior |
| 15 | [Solución de problemas](15-problemas.md) | Errores comunes y cómo resolverlos |
| 16 | [Referencia de la API](16-referencia.md) | Todos los tipos públicos en una página |

## Conceptos en una línea

- Un **`ApiConfig`** describe una API: sus entornos (Development, Staging, Production) y sus endpoints con nombre.
- Su **`RestClient`** (`config.Client`) crea peticiones: `client.Get("users/{id}")`.
- Una **`HttpRequest`** se configura de forma fluida y se envía con `SendAsync`. Sale de un pool y vuelve a él al enviarse.
- **`SendAsync<T>()`** devuelve un `HttpResult<T>` que nunca lanza excepciones por errores HTTP o de red.
- **`SendAsync()`** devuelve la `HttpResponse` cruda, con el cuerpo en memoria nativa; hay que liberarla con `using`.

## Probado y descartado

- **Esperar con `UnityWebRequestAsyncOperation.ToUniTask()`** (2026-10-20): UniTask lanza `UnityWebRequestException` en cualquier 4xx/5xx, lo que obliga a usar `try/catch` para el flujo normal. Se descartó: el bucle de espera usa `UniTask.Yield` y clasifica el resultado sin excepciones.
- **`UnityWebRequest.timeout`** (2026-10-20): no distingue un timeout de otros errores de red. Se reemplazó por un plazo propio que aborta la petición y devuelve `HttpError.Timeout`.
- **Un probador visual automatizado del inspector** (2026-10-20): en proyectos temporales creados por línea de comandos, el editor no compiló sus propios shaders de interfaz y todo se veía magenta. El inspector solo se verificó compilando; falta revisarlo a ojo en un proyecto real.
