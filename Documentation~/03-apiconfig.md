# 3. ApiConfig: entornos y endpoints

> Estado a 2026-10-21.

`ApiConfig` es un ScriptableObject que describe una API completa. Se crea con **Assets → Create → UnityREST → Api Config**.

## Entornos

Cada entorno tiene:

| Campo | Uso |
|---|---|
| **Name** | Nombre visible: `Development`, `Staging`, `Production`... |
| **Base Url** | Esquema y host, con ruta base opcional: `https://api.example.com/v1` |
| **Api Key Header** | Cabecera donde va la clave (por defecto `x-api-key`) |
| **Api Key** | Valor de la clave. Se guarda en el asset y **viaja dentro del build** |
| **Headers** | Cabeceras fijas de ese entorno, como `X-Client-Version` |
| **Certificate Pins** | Pines SHA-256 de la clave pública del servidor (ver [seguridad](07-seguridad.md)) |

El entorno activo se elige con la barra superior del inspector y se aplica al instante, también en Play Mode. Por código:

```csharp
api.SetActiveEnvironment("Staging");     // por nombre, sin distinguir mayúsculas
api.ActiveEnvironmentIndex = 2;          // por índice
var url = api.ActiveEnvironment.BaseUrl;
```

Un patrón habitual es elegir el entorno según el tipo de build:

```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    api.SetActiveEnvironment("Staging");
#else
    api.SetActiveEnvironment("Production");
#endif
```

## Endpoints

Un endpoint es un nombre, un verbo y una ruta relativa a la URL base. La ruta admite marcadores `{nombre}` que se rellenan con `.Path(...)`, y también una query fija:

| Name | Method | Path |
|---|---|---|
| `getUser` | GET | `users/{id}` |
| `listItems` | GET | `items?sort=name` |
| `updateUser` | PATCH | `users/{id}` |

```csharp
var user = await api.Client.Endpoint("getUser").Path("id", 42).SendAsync<User>();
```

`Endpoint(nombre)` lanza `KeyNotFoundException` si el nombre no existe: es un error de configuración, no de red. El inspector avisa de nombres vacíos o repetidos.

## EndpointRef: elegir un endpoint en el inspector

```csharp
[SerializeField] private EndpointRef getUser;

var user = await getUser.Request().Path("id", 42).SendAsync<User>();
```

El drawer muestra un selector de `ApiConfig` y un menú con sus endpoints (`GET  getUser`). Si el endpoint se renombra o se borra, el campo queda en rojo como `Missing: getUser`. `getUser.IsValid` permite comprobarlo por código.

## Transporte

| Campo | Por defecto | Efecto |
|---|---|---|
| **Timeout Seconds** | 30 | Tiempo máximo por intento. `0` lo desactiva |
| **Retry** | 2 reintentos, 0.5 s, tope 8 s | Ver [reintentos](05-respuestas-y-errores.md#reintentos) |
| **Enforce Https** | Activado | Bloquea `http://` salvo en loopback |
| **Log Level** | Errors | `None`, `Errors`, `Info` (todas las peticiones) o `Verbose` (con cuerpos) |

Todo se puede cambiar por código: `api.TimeoutSeconds`, `api.Retry`, `api.EnforceHttps`, `api.LogLevel`.

## El cliente

`api.Client` devuelve un `RestClient` compartido, creado la primera vez que se usa. Lee la configuración en cada petición, así que los cambios de entorno o de timeout se aplican sin recrearlo.

Puedes crear clientes propios sobre la misma configuración, por ejemplo con otra autenticación:

```csharp
var admin = new RestClient(api);
admin.SetBearerToken(adminToken);
```

## Configuración solo por código

```csharp
var config = ApiConfig.Create("https://api.example.com/v1",
    new Endpoint("getUser", HttpMethod.Get, "users/{id}"),
    new Endpoint("createUser", HttpMethod.Post, "users"));

config.TimeoutSeconds = 10f;
config.Retry = new RetryPolicy { maxRetries = 3, baseDelay = 1f, maxDelay = 10f };

var client = config.Client;          // o new RestClient("https://api.example.com/v1")
```

`ApiConfig.Create` crea una instancia en memoria (`HideFlags.DontSave`) que no se guarda como asset.

## Varias APIs

Crea un `ApiConfig` por API (tu backend, un CDN, un servicio de analítica). Cada uno tiene su cliente, sus cabeceras y su política de reintentos.
