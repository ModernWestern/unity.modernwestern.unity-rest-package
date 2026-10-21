# 6. Autenticación

> Estado a 2026-10-21.

## Bearer (JWT, OAuth)

```csharp
client.SetBearerToken(accessToken);   // Authorization: Bearer <token>
client.ClearAuthorization();          // al cerrar sesión
bool loggedIn = client.HasAuthorization;
```

## Basic

```csharp
client.SetBasicAuth("usuario", "contraseña");   // Authorization: Basic base64(usuario:contraseña)
```

## API key

- **Por entorno**, en el `ApiConfig` (campos *Api Key Header* y *Api Key*). Se manda en cada petición de ese entorno.
- **Por código**, si la clave no debe guardarse en el asset: `api.ActiveEnvironment.ApiKey = clave;` o `client.SetHeader("x-api-key", clave);`.

## Cabecera propia

```csharp
client.SetHeader("X-Session", sessionId);
await client.Get("x").Header("Authorization", "Token " + otro).SendAsync();   // solo esta petición
```

## Renovar el token ante un 401

`RefreshAuthorization` se llama una vez cuando una petición recibe 401. Si devuelve `true`, la petición se repite con las credenciales nuevas, sin gastar reintentos; si devuelve `false`, se entrega el 401.

```csharp
client.RefreshAuthorization = async (c, ct) =>
{
    var refreshed = await c.Post("auth/refresh")
        .Form("refresh_token", tokens.Refresh)
        .Retries(0)
        .SendAsync<TokenResponse>(ct);

    if (!refreshed.IsSuccess)
    {
        tokens.Clear();
        return false;              // el llamador recibe el 401 y puede mandar al login
    }

    tokens.Save(refreshed.Value);
    c.SetBearerToken(refreshed.Value.access_token);
    return true;
};
```

Si se disparan varias peticiones a la vez con un token vencido, cada una llama al refresco. Si tu backend invalida el refresh token al usarlo, comparte una sola tarea de refresco:

```csharp
private UniTask<bool>? _refreshing;

client.RefreshAuthorization = (c, ct) =>
{
    _refreshing ??= RefreshOnceAsync(c, ct).Preserve();
    return _refreshing.Value;
};

private async UniTask<bool> RefreshOnceAsync(RestClient c, CancellationToken ct)
{
    try { /* ...pedir el token y llamar a c.SetBearerToken... */ return true; }
    finally { _refreshing = null; }
}
```

## Dónde guardar tokens

UnityREST no guarda credenciales: eso le toca a tu juego. `PlayerPrefs` es texto plano, lo cual sirve para prototipos pero no para cuentas reales. En móviles usa el llavero del sistema (Keychain en iOS, Keystore en Android) mediante un plugin, y guarda el refresh token, no la contraseña.

## Firmas (HMAC) y esquemas propios

Para firmar cada petición con su URL y su cuerpo, usa un [interceptor](13-extender.md#interceptores).
