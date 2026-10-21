# 5. Respuestas y errores

> Estado a 2026-10-21.

## HttpResult&lt;T&gt;

Lo devuelve `SendAsync<T>()`. Es un struct que nunca lanza excepciones por errores HTTP, de red o de deserialización.

| Miembro | Descripción |
|---|---|
| `IsSuccess` | `true` con una respuesta 2xx que se pudo leer |
| `Value` | El objeto deserializado (`default` si falló o el cuerpo vino vacío, como en un 204) |
| `Error` | Un `HttpError`, ver la tabla de abajo |
| `StatusCode` | Código HTTP (`0` si no hubo respuesta) |
| `ErrorMessage` | El error de transporte más el comienzo del cuerpo de error del servidor (hasta 1024 caracteres) |
| `TryGetValue(out T)` | Patrón `if (result.TryGetValue(out var user))` |

```csharp
var result = await client.Get("me").SendAsync<User>(token);

switch (result.Error)
{
    case HttpError.None:
        Show(result.Value);
        break;
    case HttpError.Http when result.StatusCode == 404:
        ShowNotFound();
        break;
    case HttpError.Network:
    case HttpError.Timeout:
        ShowOfflineBanner();
        break;
    case HttpError.Cancelled:
        break;   // el objeto se destruyó o se canceló a propósito
    default:
        Debug.LogWarning(result.ErrorMessage);
        break;
}
```

## HttpError

| Valor | Cuándo |
|---|---|
| `None` | Éxito (2xx) |
| `Http` | El servidor respondió 4xx o 5xx. Mira `StatusCode` y `ErrorMessage` |
| `Network` | DNS, conexión rechazada, TLS, certificado no aceptado por el pinning |
| `Timeout` | Se superó el tiempo del intento (`Timeout`) |
| `Cancelled` | Se canceló el `CancellationToken` |
| `Deserialization` | Respuesta 2xx con un cuerpo que no encaja con `T` |
| `InsecureUrl` | Se intentó `http://` con HTTPS obligatorio; no se envió nada |

## HttpResponse (respuesta cruda)

`SendAsync()` sin tipo devuelve `HttpResponse`. El cuerpo vive en memoria nativa de Unity hasta que se libera con **`Dispose`**, así que úsala siempre con `using`:

```csharp
using (var response = await client.Get("report.csv").SendAsync(token))
{
    if (!response.IsSuccess) { Debug.LogWarning(response); return; }

    ReadOnlySpan<byte> bytes = response.Data;        // sin copiar
    string etag = response.GetHeader("ETag");
    string text = response.Text;                      // crea un string
    var report = response.ReadJson<Report>();         // deserializa desde los bytes nativos
}
```

| Miembro | Descripción |
|---|---|
| `IsSuccess`, `Error`, `StatusCode`, `ErrorMessage` | Igual que en `HttpResult<T>` |
| `Data` | `ReadOnlySpan<byte>` del cuerpo, sin copias. Vacío en descargas a archivo, textura, audio o streaming |
| `Text` | El cuerpo como texto (en streaming, los primeros 4 KB recibidos) |
| `Texture`, `AudioClip` | Con `.AsTexture()` o `.AsAudio()` |
| `GetHeader(nombre)` | Cabecera de la respuesta |
| `ReadJson<T>()` | Deserializa con Json.NET o con el serializador que pases |
| `Url`, `Attempts`, `ElapsedSeconds` | URL final, intentos usados y duración del último intento |
| `WebRequest` | La `UnityWebRequest` de fondo, para casos avanzados |

> El `ReadOnlySpan` de `Data` solo vale dentro del `using`. No lo guardes para después.

## Reintentos

La política (`ApiConfig.Retry` o `.Retries(n)` por petición) usa backoff exponencial con jitter: cada espera es entre el 50 % y el 100 % de `baseDelay × 2^intento`, con `maxDelay` como tope. Si el servidor manda `Retry-After` en segundos, se respeta, hasta 60 s.

Qué se reintenta:

| Situación | GET, PUT, DELETE, HEAD, OPTIONS | POST, PATCH |
|---|---|---|
| Error de red o timeout | Sí | Solo con `retryUnsafeMethods` |
| 408, 500, 502, 504 | Sí | Solo con `retryUnsafeMethods` |
| 429, 503 | Sí | Sí: el servidor rechazó antes de procesar |
| Otros 4xx (400, 401, 403, 404, 409, 422...) | No | No |
| Cancelado o `InsecureUrl` | No | No |

POST y PATCH no se repiten tras un error ambiguo porque el servidor pudo haberlos procesado: repetirlos podría, por ejemplo, cobrar dos veces. Activa `retryUnsafeMethods` solo si tu API usa claves de idempotencia. Las peticiones con `.Stream(...)` nunca se reintentan.

```csharp
api.Retry = new RetryPolicy { maxRetries = 4, baseDelay = 0.25f, maxDelay = 5f };
await client.Get("health").Retries(0).SendAsync();      // sin reintentos en esta petición
api.Retry = RetryPolicy.None;                            // sin reintentos en general
```

## Logs

`ApiConfig.LogLevel`:

| Nivel | Qué se imprime |
|---|---|
| `None` | Nada |
| `Errors` (por defecto) | Fallos, con el comienzo del cuerpo de error |
| `Info` | Cada petición con verbo, código, URL, duración e intentos, y cada reintento |
| `Verbose` | Lo anterior más los cuerpos de petición y respuesta (hasta 4 KB) |

Ejemplo: `[UnityREST] GET 200 https://api.example.com/v1/users/42 (83 ms, attempt 2)`. Las cabeceras nunca se imprimen.
