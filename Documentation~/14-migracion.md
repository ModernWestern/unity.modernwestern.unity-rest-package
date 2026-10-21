# 14. Migración desde 1.x

> Estado a 2026-10-21. La versión 2.0.0 reemplaza la API de 1.x sin capa de compatibilidad.

## Equivalencias

| 1.x | 2.0 |
|---|---|
| Heredar de `APIManager` (MonoBehaviour) y usar `Transport` estático | `apiConfig.Client` o `new RestClient(config)`, sin MonoBehaviour |
| `APIConfig` + `APIEnvironment` + `APIPaths` (tres assets) | Un solo `ApiConfig` con entornos y endpoints |
| `EnvironmentType` (enum fijo) | Entornos con nombre libre: `SetActiveEnvironment("Staging")` |
| `StartRequest("name", path => Transport.GET<T>(path, callback))` | `await client.Endpoint("name").SendAsync<T>()` |
| `Action<WebResult<T>>` | `UniTask<HttpResult<T>>` |
| `WebResult.HasError()` | `!result.IsSuccess` |
| `WebResult.ResponseCode` | `result.StatusCode` |
| `WebResult.ResponseText` | `SendAsync<string>()` o `response.Text` |
| `WebResult<T>.data` | `result.Value` |
| `GET(uri, Dictionary/tuplas, cb)` | `.Query(clave, valor)` por cada parámetro |
| `POST<T>(uri, object, cb, args)` | `client.Post(uri).Query(...).Json(obj).SendAsync<T>()` |
| `PATCH` | `client.Patch(...)` |
| `URLBuilder.Parameters / Args / ParameterArray` | `.Query(...)`; para listas, `.Query("ids", string.Join(",", ids))` |
| `JBuilder.Object(...)` / `ArrayObject(...)` | `.Json(new { key = value })` o `.Json(estado, writer => ...)` |
| `SetAuth(token)` (guardaba en `PlayerPrefs`) | `client.SetBearerToken(token)`; guardar el token queda en manos de tu juego |
| `SignOut()` | `client.ClearAuthorization()` |
| `GetCachedToken()` | Tu propio almacenamiento seguro |
| `[ReadOnly]`, `[HideEnum]` | Se quitaron; el nuevo inspector no los necesita |

## Antes y después

**1.x**

```csharp
public class GameAPIManager : APIManager
{
    public static void GetUser(int id, Action<WebResult<User>> callback)
    {
        StartRequest("getUser", path => Transport.GET(path, new[] { ("id", id.ToString()) }, callback));
    }
}

GameAPIManager.GetUser(5, result =>
{
    if (!result.result.HasError()) Show(result.data);
});
```

**2.0**

```csharp
var result = await api.Client.Endpoint("getUser").Query("id", 5).SendAsync<User>(token);
if (result.IsSuccess) Show(result.Value);
```

## Pasos

1. Crea un `ApiConfig` y copia tus entornos (URL base y API key) y tus rutas como endpoints.
2. Quita la clase que heredaba de `APIManager` y referencia el `ApiConfig` donde hagas peticiones.
3. Reemplaza cada callback por `await ... SendAsync<T>()` y revisa `IsSuccess`.
4. Si usabas `SetAuth`/`GetCachedToken`, guarda tú el token y llama `SetBearerToken` al iniciar.
5. Revisa los endpoints `http://`: ahora se bloquean salvo en loopback.
6. Unity 2022.3 o superior.

## Comportamientos que cambian

- **Reintentos:** 1.x reintentaba cualquier fallo, incluidos 404 y POST. 2.0 reintenta solo lo seguro (ver [reintentos](05-respuestas-y-errores.md#reintentos)).
- **Escape:** 1.x no escapaba la query; 2.0 escapa todo. Si tenías valores ya escapados a mano, quita ese escape para no escapar dos veces.
- **PATCH:** 1.x enviaba el método como `Patch`; 2.0 lo envía como `PATCH`.
- **JSON:** 1.x enviaba JSON indentado; 2.0 lo envía compacto y sin propiedades `null`.
- **Endpoint inexistente:** 1.x llamaba al callback con una ruta `null`; 2.0 lanza `KeyNotFoundException`.
