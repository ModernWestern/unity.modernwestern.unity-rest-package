# 9. Rendimiento y memoria

> Estado a 2026-10-21.

UnityREST busca que hacer peticiones no genere basura que dispare el recolector (GC) en medio del juego.

## Qué no asigna memoria administrada

Las pruebas `AllocationTests` lo verifican con `Is.Not.AllocatingGCMemory()` del Test Framework de Unity, después de una pasada de calentamiento:

| Operación | Cómo |
|---|---|
| Construir la URL (ruta, `{marcadores}`, query, escape UTF-8) | Búferes reutilizados del pool de peticiones; los números se formatean con `TryFormat` |
| Escribir cuerpos (texto, bytes, formularios, multipart) | `PooledBufferWriter` sobre `ArrayPool<byte>` |
| Parsear Server-Sent Events | `SseParser` trabaja sobre `Span<byte>` con búferes reutilizados |

Además, sin pruebas de asignación propias:

- **Peticiones en pool:** cada `HttpRequest` se reutiliza con todos sus búferes (hasta 32 en el pool).
- **Una sola copia del cuerpo:** de `ArrayPool` a memoria nativa, y `UploadHandlerRaw` se queda con esa copia.
- **Respuestas sin copia:** `HttpResponse.Data` y `ReadJson<T>()` leen directo de la memoria nativa de Unity, sin pasar por `string` ni `byte[]`.
- **Json.NET con búferes del pool:** caracteres de `ArrayPool<char>` y un `StreamReader`/`StreamWriter` en caché por hilo.
- **Espera sin asignaciones:** el envío espera con `UniTask.Yield`, que usa máquinas de estado en pool.
- **Streaming:** usa un búfer de recepción preasignado, reutilizado por la petición.

## Qué sí asigna (y por qué)

| Asignación | Motivo |
|---|---|
| El `string` final de la URL | `UnityWebRequest` solo acepta `string` |
| `UnityWebRequest` y sus handlers | Son objetos de Unity, uno por intento |
| `JsonTextReader` / `JsonTextWriter` | Json.NET crea uno por operación (sus búferes sí vienen del pool) |
| Los objetos que deserializas | Son tu resultado |
| `Text`, `GetHeader`, `ErrorMessage` | Crean strings cuando los pides |
| Logs con `Info` o `Verbose` | Formatean mensajes |

## Buenas prácticas

- **Libera las respuestas crudas** con `using`: su memoria nativa no la libera el GC.
- **Prefiere `SendAsync<T>`** si solo necesitas el objeto: libera la respuesta por ti.
- **Lee `Data` antes que `Text`** cuando proceses bytes.
- **En rutas calientes** (telemetría, multiplayer por HTTP), escribe el JSON con `.Json(estado, (writer, s) => ...)` y pasa los datos por el estado, no capturándolos en el lambda.
- **Usa números directos:** `.Query("page", 2)` en lugar de `.Query("page", page.ToString())`.
- **Deja el log en `Errors`** en builds de producción.
- **Reutiliza el cliente:** `api.Client` es compartido; no crees un `RestClient` por petición.

## Hilos

`UnityWebRequest` solo funciona en el hilo principal. `SendAsync` cambia solo al hilo principal si lo llamas desde otro, así que puedes construir y enviar peticiones desde tareas en segundo plano. La deserialización ocurre en el hilo principal, al terminar la respuesta. Para JSON muy grandes, recibe con `SendAsync()`, copia los bytes y deserialízalos en otro hilo.
