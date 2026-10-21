# 8. Descargas, subidas y streaming

> Estado a 2026-10-21.

## Texturas

```csharp
using (var image = await cdn.Get("avatars/{id}.png").Path("id", userId).AsTexture().SendAsync(token))
{
    if (image.IsSuccess)
    {
        avatar.texture = image.Texture;
    }
}
```

`AsTexture(readable: true)` deja los píxeles legibles por CPU (`GetPixels`), a costa de usar el doble de memoria. **La textura no se destruye con la respuesta**: llama a `Destroy(texture)` cuando ya no la uses.

## Audio

```csharp
using (var clip = await cdn.Get("voice/line_12.mp3").AsAudio(AudioType.MPEG).SendAsync(token))
{
    source.clip = clip.AudioClip;
    source.Play();
}
```

`AsAudio(AudioType.OGGVORBIS, streamAudio: true)` empieza a reproducir antes de terminar la descarga.

## Archivos grandes, directo a disco

```csharp
var path = Path.Combine(Application.persistentDataPath, "dlc", "level3.bundle");

using (var file = await cdn.Get("dlc/level3.bundle")
           .ToFile(path)
           .Timeout(0)                       // sin límite para descargas largas
           .Progress(new Progress<float>(p => bar.value = p))
           .SendAsync(token))
{
    Debug.Log(file.IsSuccess ? "Listo" : file.ToString());
}
```

El archivo se escribe a medida que llega, sin cargarlo en memoria. Si la descarga se aborta, el archivo parcial se borra. `ToFile(path, append: true)` agrega al final.

## Progreso

`.Progress(IProgress<float>)` informa de 0 a 1: el valor es `subida × 0.5 + descarga × 0.5`, y llega a 1 cuando la petición termina bien. Un `Progress<T>` creado en el hilo principal entrega los valores en el hilo principal, así que puede tocar la interfaz directamente.

## Subidas

| Caso | Cómo |
|---|---|
| JSON | `.Json(objeto)` |
| Un archivo como cuerpo crudo | `.Bytes(bytes, "image/png")` con `Put` o `Post` |
| Formulario con archivos | `.Multipart("file", bytes, "nombre.png", "image/png")` y campos con `.Multipart("campo", "valor")` |

El cuerpo se copia una sola vez a memoria nativa por intento; el arreglo original no se retiene.

## Server-Sent Events (SSE)

Para respuestas `text/event-stream` (notificaciones en vivo, modelos de IA, progreso de tareas). Implementa `ISseListener`:

```csharp
public sealed class LiveScores : MonoBehaviour, ISseListener
{
    private async UniTaskVoid Start()
    {
        using (var response = await api.Client.Get("scores/stream").Stream(this).Timeout(0).SendAsync(destroyCancellationToken))
        {
            Debug.Log($"Stream terminado: {response}");
        }
    }

    public void OnEvent(ReadOnlySpan<byte> eventType, ReadOnlySpan<byte> data)
    {
        // eventType vacío = "message". Los spans solo valen durante esta llamada.
        var score = NewtonsoftJsonSerializer.Default.Deserialize<Score>(data);
        board.Show(score);
    }
}
```

- `.Stream(listener)` agrega `Accept: text/event-stream` si no pusiste otro.
- Los eventos llegan en el hilo principal a medida que el servidor los manda.
- El parser respeta el estándar: líneas `data:` múltiples unidas con `\n`, `event:`, comentarios `:` y saltos LF, CR o CRLF. No asigna memoria.
- Las peticiones con streaming **no se reintentan**, para no repetir eventos.
- Para cortar el stream, cancela el token. Para streams sin fin, usa `.Timeout(0)`.

También puedes usar el parser solo, sobre bytes que ya tengas:

```csharp
var parser = new SseParser();
parser.Feed(chunk, listener);     // por cada fragmento
parser.Complete(listener);        // al final
```
