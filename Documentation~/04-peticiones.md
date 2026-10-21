# 4. Construir peticiones

> Estado a 2026-10-21.

Toda petición empieza en un `RestClient` y se configura con métodos encadenados:

```csharp
var result = await client.Get("users/{id}/posts")
    .Path("id", 42)
    .Query("page", 2)
    .Query("q", "café con leche")
    .Header("X-Trace-Id", traceId)
    .Timeout(5f)
    .SendAsync<Post[]>(token);
```

## Verbos

| Método | Verbo |
|---|---|
| `client.Get(path)` | GET |
| `client.Post(path)` | POST |
| `client.Put(path)` | PUT |
| `client.Patch(path)` | PATCH |
| `client.Delete(path)` | DELETE |
| `client.Head(path)` | HEAD (sin cuerpo de respuesta) |
| `client.Request(HttpMethod.Options, path)` | Cualquiera |
| `client.Endpoint("nombre")` | El verbo y la ruta del endpoint |

`path` puede ser relativa a la URL base del entorno activo (`users/1`, `/users/1`) o absoluta (`https://otro.host/x`); las absolutas ignoran la URL base. Las barras sobrantes entre base y ruta se unifican.

## Ruta y query

```csharp
.Path("id", 42)                 // reemplaza {id}; string o long
.Path("slug", "año nuevo")      // se escapa: a%C3%B1o%20nuevo
.Query("q", "café & té")        // string, long, double, float o bool
.Query("ratio", 0.75)           // números con punto decimal, sin depender del idioma del sistema
.Query("active", true)          // "true" / "false"
```

- Todo se escapa con RFC 3986 en UTF-8: espacios, `&`, `/`, `ñ`, emojis.
- Si la ruta ya trae `?` (`items?sort=name`), los nuevos parámetros se agregan con `&`.
- Un `{marcador}` sin valor queda tal cual en la URL, para que el error sea visible.
- Para arrays, repite la clave (`.Query("tag", "a").Query("tag", "b")`) o únelos tú (`.Query("ids", "1,2,3")`), según lo que espere tu API.

## Cabeceras

Hay tres niveles. Cuando se repite un nombre, gana el más específico:

1. **Entorno** (`ApiConfig`): la API key y las cabeceras fijas.
2. **Cliente**: `client.SetHeader("Accept-Language", "es")`, `client.RemoveHeader(...)`, autenticación.
3. **Petición**: `.Header(nombre, valor)` y `.Accept("application/xml")`.

Si nadie fija `Accept`, las respuestas en memoria piden `application/json`. Los logs nunca imprimen cabeceras.

## Cuerpos

Un cuerpo por petición. Mezclar tipos (por ejemplo JSON y `Form`) lanza `InvalidOperationException`.

| Método | Content-Type | Uso |
|---|---|---|
| `.Json(objeto)` | `application/json` | Serializa con el serializador del cliente (Json.NET por defecto, omite `null`) |
| `.Json(estado, (writer, s) => ...)` | `application/json` | JSON escrito a mano, sin asignaciones |
| `.Text(texto, contentType)` | `text/plain; charset=utf-8` | Texto, XML, GraphQL... |
| `.Bytes(span, contentType)` | `application/octet-stream` | Binario |
| `.Form(clave, valor)` | `application/x-www-form-urlencoded` | Formularios clásicos, OAuth |
| `.Multipart(nombre, valor)` / `.Multipart(nombre, bytes, archivo, tipo)` | `multipart/form-data` | Subir archivos |

### JSON escrito a mano

Útil en rutas calientes o para formas que no tienen clase. Pasa los datos por `estado`, sin capturar variables, para que el lambda no asigne memoria:

```csharp
client.Post("scores").Json((Player: playerId, Score: score), (writer, s) =>
{
    writer.WriteStartObject();
    writer.WritePropertyName("player"); writer.WriteValue(s.Player);
    writer.WritePropertyName("score");  writer.WriteValue(s.Score);
    writer.WriteEndObject();
});
```

### Formularios

```csharp
await client.Post("oauth/token")
    .Form("grant_type", "password")
    .Form("username", user)
    .Form("password", password)
    .SendAsync<TokenResponse>();
```

### Multipart

```csharp
var png = screenshot.EncodeToPNG();

await client.Post("uploads")
    .Multipart("title", "Captura")
    .Multipart("file", png, "captura.png", "image/png")
    .SendAsync<UploadResult>();
```

## Opciones por petición

| Método | Efecto |
|---|---|
| `.Timeout(segundos)` | Reemplaza `ApiConfig.TimeoutSeconds` en esta petición. `0` = sin límite |
| `.Retries(n)` | Reemplaza la cantidad de reintentos de la política |
| `.Progress(IProgress<float>)` | Progreso de 0 a 1: la mitad para la subida y la mitad para la descarga |
| `.ToFile`, `.AsTexture`, `.AsAudio`, `.Stream` | Cambian cómo se recibe la respuesta: ver [descargas y streaming](08-descargas-y-streaming.md) |

## Enviar

| Método | Devuelve | Cuándo |
|---|---|---|
| `SendAsync<T>(token)` | `HttpResult<T>` | Lo normal: deserializa el JSON a `T` |
| `SendAsync<string>(token)` | `HttpResult<string>` | Cuerpo como texto, sin JSON |
| `SendAsync(token)` | `HttpResponse` | Bytes, cabeceras, texturas, audio. **Usa `using`** |

Se puede llamar desde cualquier hilo: el envío pasa al hilo principal, como exige `UnityWebRequest`.

## Ciclo de vida

Cada `HttpRequest` sale de un pool. Al terminar `SendAsync`, vuelve al pool con sus búferes intactos para la próxima. Por eso:

- No guardes la petición en un campo ni la envíes dos veces.
- Si construyes una petición y no la envías, no pasa nada: el recolector de basura la libera.
- Para repetir una llamada, crea otra con `client.Get(...)`. Es barato.
