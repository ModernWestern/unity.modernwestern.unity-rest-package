# 2. Inicio rápido

> Estado a 2026-10-21.

Este ejemplo usa la API pública de prueba `https://jsonplaceholder.typicode.com`.

## 1. Crea la configuración

1. **Assets → Create → UnityREST → Api Config**.
2. En **Environments**, deja un entorno llamado `Production` con **Base Url** `https://jsonplaceholder.typicode.com`.
3. En **Endpoints**, agrega:
   - `getPost` · GET · `posts/{id}`
   - `createPost` · POST · `posts`
4. Abre **Request tester**, pulsa ▶ junto a `getPost`, cambia `{id}` por `1` y pulsa **Send**. Deberías ver `200` y el JSON formateado.

## 2. Define el modelo

```csharp
[System.Serializable]
public sealed class Post
{
    public int id;
    public int userId;
    public string title;
    public string body;
}
```

Json.NET lee campos y propiedades públicas, sin atributos. Para nombres distintos usa `[JsonProperty("user_id")]`.

## 3. Llama a la API

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityREST;

public sealed class PostsDemo : MonoBehaviour
{
    [SerializeField] private ApiConfig api;

    private async UniTaskVoid Start()
    {
        var token = this.GetCancellationTokenOnDestroy();

        // GET con parámetro de ruta.
        var result = await api.Client.Endpoint("getPost").Path("id", 1).SendAsync<Post>(token);

        if (!result.IsSuccess)
        {
            Debug.LogWarning($"{result.Error} {result.StatusCode}: {result.ErrorMessage}");
            return;
        }

        Debug.Log(result.Value.title);

        // POST con cuerpo JSON.
        var created = await api.Client.Endpoint("createPost")
            .Json(new Post { userId = 1, title = "Hola", body = "Desde Unity" })
            .SendAsync<Post>(token);

        Debug.Log($"Creado con id {created.Value?.id} ({created.StatusCode})");
    }
}
```

## Lo que conviene saber desde el principio

- **Nunca hay excepciones por errores HTTP o de red.** Revisa `IsSuccess`. Las excepciones quedan para errores de programación, como pedir un endpoint que no existe.
- **Cancela con el objeto.** `this.GetCancellationTokenOnDestroy()` aborta la petición si el GameObject se destruye; en ese caso, `Error` vale `Cancelled`.
- **No guardes la `HttpRequest`.** Después de `SendAsync` vuelve al pool y se reutiliza; si la usas otra vez, lanza `InvalidOperationException`.
- **Sin asset también funciona:** `var client = new RestClient("https://jsonplaceholder.typicode.com");`.

Siguiente: [ApiConfig](03-apiconfig.md) o [Construir peticiones](04-peticiones.md).
