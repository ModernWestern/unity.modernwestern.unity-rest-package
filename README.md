# UnityREST 2.0

Cliente REST/HTTPS para Unity pensado para no generar basura, con herramientas de inspector, pinning de certificados, Server-Sent Events y una capa de IA independiente del proveedor (compatible con OpenAI, Anthropic y Gemini) con llamadas a herramientas y cliente MCP.

- **Unity:** 2022.3 LTS o superior (probado en 2022.3.62f1 y 6000.1.5f1).
- **Dependencias:** `com.cysharp.unitask` 2.5.10 y `com.unity.nuget.newtonsoft-json` 3.2.1 (las mismas que la versión 1.x).

## Instalación

UniTask viene de OpenUPM. Agrega el registro a `Packages/manifest.json` de tu proyecto (Unity ignora `scopedRegistries` dentro de un paquete):

```json
{
  "scopedRegistries": [
    { "name": "package.openupm.com", "url": "https://package.openupm.com", "scopes": ["com.cysharp.unitask"] }
  ],
  "dependencies": {
    "com.modernwestern.unity-rest": "https://github.com/ModernWestern/unity.modernwestern.unity-rest-package.git"
  }
}
```

Los ejemplos se importan desde **Package Manager → UnityREST → Samples**.

## Inicio rápido

1. **Assets → Create → UnityREST → Api Config.**
2. Agrega los entornos (Development, Staging, Production...) con su URL base y los endpoints con nombre (`getUser · GET · users/{id}`).
3. Úsalo desde código:

```csharp
[SerializeField] ApiConfig api;
[SerializeField] EndpointRef getUser;   // se elige en el inspector

async UniTaskVoid Start()
{
    var token = this.GetCancellationTokenOnDestroy();

    // Nunca lanza excepciones por errores HTTP o de red: revisa IsSuccess.
    var user = await api.Client.Get("users/{id}").Path("id", 42).Query("expand", "teams").SendAsync<User>(token);
    if (user.IsSuccess) Debug.Log(user.Value.Name);
    else Debug.LogWarning($"{user.Error} {user.StatusCode}: {user.ErrorMessage}");

    var same = await getUser.Request().Path("id", 42).SendAsync<User>(token);
    var created = await api.Client.Endpoint("createUser").Json(new User { Name = "Ada" }).SendAsync<User>(token);
}
```

Sin asset: `var client = new RestClient("https://api.example.com/v1");`.

## Qué cubre

| Caso | API |
|---|---|
| JSON tipado (cualquier verbo) | `Get/Post/Put/Patch/Delete/Head(...)` + `.Json(obj)` + `SendAsync<T>()` |
| Texto plano | `SendAsync<string>()` devuelve el cuerpo sin deserializar |
| Parámetros de ruta y query escapados (RFC 3986, UTF-8) | `.Path("id", 5)`, `.Query("q", "café")` (string, long, double, float, bool) |
| Formularios | `.Form("k", "v")` (urlencoded), `.Multipart("file", bytes, "a.png", "image/png")` |
| JSON escrito a mano, sin asignaciones | `.Json(state, (writer, s) => ...)` |
| Bytes crudos | `.Bytes(span, contentType)`; en la respuesta, `response.Data` (sin copias) |
| Descargas | `.AsTexture()`, `.AsAudio(AudioType.MPEG)`, `.ToFile(path)` + `.Progress(progress)` |
| Streaming (SSE) | `.Stream(listener)` con `ISseListener` |
| Autenticación | `SetBearerToken`, `SetBasicAuth`, API key por entorno, `RefreshAuthorization` ante un 401 |
| Reintentos | Backoff exponencial con jitter, respeta `Retry-After`; solo reintenta lo seguro (ver abajo) |
| Timeout y cancelación | `.Timeout(segundos)` y `CancellationToken`; devuelven `HttpError.Timeout` / `Cancelled` |
| Cabeceras | Por entorno, por cliente (`SetHeader`) y por petición (`.Header`); gana la más específica |
| Interceptores | `IHttpInterceptor` para firmar (HMAC), trazas o métricas |
| Serializador propio | `client.Serializer = miIJsonSerializer` |

Las peticiones se pueden lanzar desde cualquier hilo: el envío pasa al hilo principal, como exige `UnityWebRequest`.

### Respuesta cruda

```csharp
using (var response = await client.Get("report").SendAsync(token))   // libera la memoria nativa al salir
{
    ReadOnlySpan<byte> bytes = response.Data;     // sin copiar
    var etag = response.GetHeader("ETag");
    var report = response.ReadJson<Report>();
}
```

### Reintentos

Por defecto: 2 reintentos, 0.5 s de base y 8 s de tope. Se reintentan errores de red, timeouts, 408, 500, 502 y 504 **solo en métodos idempotentes** (GET, PUT, DELETE...). 429 y 503 se reintentan siempre, porque el servidor rechazó la petición antes de procesarla. POST y PATCH no se repiten tras un error ambiguo, salvo que actives `retryUnsafeMethods` (útil si tu API usa claves de idempotencia). Las peticiones de streaming no se reintentan.

## HTTPS y seguridad

- **`Enforce Https`** (activado por defecto) bloquea `http://` antes de enviar y devuelve `HttpError.InsecureUrl`. Los hosts de loopback (`localhost`, `127.x`, `[::1]`) siempre se permiten para desarrollo local.
- **Pinning de clave pública**: en cada entorno, `Certificate Pins` acepta pines SHA-256 del SubjectPublicKeyInfo del certificado del servidor, en el mismo formato que OkHttp y HPKP. Sobreviven a las renovaciones que conservan la clave. Pon siempre un pin de respaldo. Para calcularlo:

  ```bash
  openssl s_client -connect api.example.com:443 </dev/null | openssl x509 -pubkey -noout | openssl pkey -pubin -outform der | openssl dgst -sha256 -binary | base64
  ```

  Con pines, Unity deja de validar la cadena por su cuenta: el handler exige que el pin coincida y que el certificado esté vigente. En WebGL el navegador valida y el pinning no aplica.
- **Secretos**: todo lo que se guarda en un asset viaja dentro del build y se puede extraer. El inspector lo advierte. En un juego publicado, apunta la URL base a tu backend y deja las claves allí.
- Los logs nunca imprimen cabeceras, así que los tokens no terminan en la consola.

## Rendimiento y asignaciones

Hecho para que el camino caliente no genere basura:

- Las peticiones (`HttpRequest`) salen de un pool y se reutilizan con todos sus búferes.
- Ruta, query y formularios se escriben y escapan en búferes reutilizados; los números se formatean con `TryFormat`, sin `ToString()`.
- El cuerpo se serializa directo a un búfer de `ArrayPool` y se copia una sola vez a memoria nativa (`UploadHandlerRaw` es dueño de la copia).
- La respuesta se lee desde la memoria nativa de Unity (`nativeData`), sin pasar por `string` ni `byte[]`.
- Json.NET usa búferes de caracteres del pool y un lector/escritor en caché por hilo.
- El parser SSE trabaja sobre `Span<byte>` con búferes reutilizados, y el streaming usa un búfer de recepción preasignado.

Las pruebas `AllocationTests` verifican con `Is.Not.AllocatingGCMemory()` que construir URLs, escribir cuerpos y parsear SSE no asignan memoria. Lo que sí asigna, y no se puede evitar: el `string` final de la URL que pide `UnityWebRequest`, los objetos `JsonTextReader`/`JsonTextWriter` de Json.NET y los valores que deserializas.

## Inspector

- **Api Config**: barra de entornos (el seleccionado queda activo al instante), candado o advertencia según HTTPS, avisos por URLs inválidas, claves guardadas en el asset y nombres de endpoint repetidos. Lista de endpoints en una línea con el verbo en color y el **probador de peticiones**: el botón ▶ de cada endpoint lo carga, envía desde el editor y muestra estado, tiempo, tamaño y el JSON formateado.
- **EndpointRef**: selector de config + menú de sus endpoints; marca en rojo los que se renombraron o borraron.
- **[Secret]**: campo enmascarado con botón para mostrarlo.
- **Window → UnityREST → AI Playground**: chat con cualquier proveedor configurado, streaming, conexión a un servidor MCP y conteo de tokens.

## IA con cualquier proveedor

El módulo `UnityREST.AI` es opcional y usa el mismo transporte.

```csharp
var provider = ChatProviderSettings.Anthropic().CreateProvider();   // o OpenAI(), Gemini(), Ollama(), LmStudio(), OpenRouter(), OpenAICompatible(url, modelo)
var chat = new ChatSession(provider, "Eres un asistente conciso.");

chat.Tools.Add(Tool.Create("get_time", "Hora del juego",
    new ToolSchema().String("zone", "Zona horaria", required: false),
    args => GameClock.Now.ToString()));

var reply = await chat.SendAsync("¿Qué hora es?", delta => label.text += delta, token);   // con delta: streaming
```

- **Proveedores**: formato OpenAI Chat Completions (OpenAI, Azure OpenAI, Ollama, LM Studio, OpenRouter, Groq, Mistral, DeepSeek, vLLM...), Anthropic Messages y Google Gemini.
- **Bucle de herramientas**: `ChatSession` ejecuta las herramientas que pide el modelo (en paralelo), devuelve los resultados y repite hasta tener respuesta o llegar a `MaxToolRounds`. `ApproveToolCall` permite pedir confirmación antes de cada una. Si un turno falla o se cancela, se retira del historial para poder reintentarlo.
- **Razonamiento conservado**: las respuestas de Anthropic (bloques `thinking` con firma) y de Gemini (`thoughtSignature`) se reenvían tal cual al mismo proveedor, como exige el uso de herramientas en varios turnos.
- **Sesión editable**: `chat.SystemPrompt` se puede cambiar a mitad de la conversación, `chat.TrimHistory(n)` recorta los turnos viejos sin separar una herramienta de su resultado (y devuelve lo recortado para resumirlo) y `RoundCompleted` avisa de cada respuesta del modelo, incluidas las que solo piden herramientas.
- **Imágenes**: `ChatMessage.User("¿Qué ves?", ChatImage.FromTexture(captura))`.
- **Configuración en asset**: **Assets → Create → UnityREST → AI Chat Provider**, con presets. La clave puede venir de una variable de entorno (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `GEMINI_API_KEY`...) para no guardarla en el asset.
- **Modelos por defecto**: `claude-opus-5-5`, `gpt-5-mini`, `gemini-2.5-flash`; cámbialos en `model`. Los modelos actuales de Claude rechazan `temperature`, así que solo se envía si activas `overrideTemperature`. `effort` se envía como `output_config.effort` (Anthropic) o `reasoning_effort` (OpenAI).

### MCP (Model Context Protocol)

Cliente para servidores remotos con el transporte Streamable HTTP: handshake `initialize`, sesión `Mcp-Session-Id`, cabecera de versión de protocolo, respuestas JSON o SSE, paginación y reconexión si el servidor pierde la sesión.

```csharp
var mcp = new McpClient("https://example.com/mcp", accessToken);
chat.Tools.AddRange(await mcp.GetToolsAsync("remoto_"));   // las herramientas MCP funcionan con cualquier proveedor
var texto = await mcp.ReadResourceAsync("file:///readme.md");
var cualquiera = await mcp.RequestAsync("prompts/list");
```

### Personajes y NPC

Este paquete se limita a hablar con las APIs. La capa de personajes (personalidad, contexto del juego, acciones, emoción, memoria y afinidad) vive en un paquete aparte que depende de este: `com.modernwestern.ai-npc`.

## Migración desde 1.x

La 2.0 reemplaza la API anterior.

| 1.x | 2.0 |
|---|---|
| `APIManager` (MonoBehaviour) + `WebTransport` estático | `apiConfig.Client` o `new RestClient(config)`, sin MonoBehaviour |
| `APIConfig` + `APIEnvironment` + `APIPaths` (tres assets) | Un solo `ApiConfig` con entornos y endpoints |
| `StartRequest("name", path => Transport.GET<T>(path, callback))` | `await client.Endpoint("name").SendAsync<T>()` |
| Callbacks `Action<WebResult<T>>` | `UniTask<HttpResult<T>>` con `IsSuccess`, `Error`, `StatusCode`, `ErrorMessage` |
| `URLBuilder.Parameters(...)`, `URLBuilder.Args(...)` | `.Query(...)`, `.Path(...)` con escape correcto |
| `JBuilder.Object(...)` | `.Json(obj)` o `.Json(state, writer => ...)` |
| Token en `PlayerPrefs` (`SetAuth`) | `SetBearerToken`; el almacenamiento seguro queda en manos de tu juego |

Fallos de la 1.x que desaparecen:

- Se reintentaba cualquier respuesta no exitosa, incluidos 404 y POST, y se esperaba el retardo incluso después del último intento.
- `PATCH` se enviaba con el método `"Patch"`, y hay servidores que distinguen mayúsculas.
- Los valores de query y de ruta no se escapaban.
- `StartRequest` llamaba al callback con una ruta `null` cuando el endpoint no existía (`is var` siempre es verdadero).
- `APIManager.Awake` sumaba una suscripción al evento estático en cada carga de escena.

## Pruebas

`Tests/Editor` incluye 40 pruebas (EditMode): URLs, SSE, serialización, asignaciones, pinning contra hashes de openssl e integración con un servidor HTTP local (reintentos, timeout, cancelación, 401 con refresh, formularios, multipart, SSE, los tres proveedores de IA con herramientas, la sesión de chat y un servidor MCP).

Para correrlas, agrega el paquete a `testables` en el `manifest.json`:

```json
"testables": ["com.modernwestern.unity-rest"]
```
