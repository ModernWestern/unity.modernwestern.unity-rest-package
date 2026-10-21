# 1. Instalación

> Estado a 2026-10-21.

## Requisitos

| Requisito | Versión |
|---|---|
| Unity | 2022.3 LTS o superior (probado en 2022.3.62f1 y 6000.1.5f1) |
| `com.cysharp.unitask` | 2.5.10 (OpenUPM) |
| `com.unity.nuget.newtonsoft-json` | 3.2.1 (registro de Unity) |
| Módulos de Unity | `unitywebrequest`, `unitywebrequesttexture`, `unitywebrequestaudio` (se instalan solos) |

## Agregar el paquete

Unity no lee los `scopedRegistries` que declara un paquete, así que el registro de OpenUPM (para UniTask) va en el manifiesto **de tu proyecto**. Abre `Packages/manifest.json` y agrega:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.cysharp.unitask"]
    }
  ],
  "dependencies": {
    "com.modernwestern.unity-rest": "https://github.com/ModernWestern/unity.modernwestern.unity-rest-package.git"
  }
}
```

Para fijar una versión o una rama, agrega `#<tag o rama>` al final de la URL, por ejemplo `...unity-rest-package.git#feature/v2-revamp`.

### Desde una carpeta local

Mientras desarrollas el paquete:

```json
"com.modernwestern.unity-rest": "file:D:/My Repositories/unity-rest-package"
```

## Ensamblados

| Ensamblado | Contenido | Se referencia solo |
|---|---|---|
| `UnityREST` | Cliente HTTP, configuración, seguridad, SSE | Sí |
| `UnityREST.AI` | Proveedores de IA, `ChatSession`, MCP | Sí |
| `UnityREST.Editor` | Inspectores, drawers, AI Playground | Solo en el editor |

Si tu código vive en un `.asmdef`, agrega `UnityREST` (y `UnityREST.AI` si usas IA) y `UniTask` a sus referencias.

## Ejemplos

En **Window → Package Manager → UnityREST → Samples**:

- **REST basics**: peticiones tipadas, endpoint desde el inspector, POST, respuesta cruda, refresh de token, textura con progreso y descarga a disco.
- **AI chat with tools and MCP**: chat con cualquier proveedor, una herramienta local que lee la escena, herramientas MCP e imágenes.

## Pruebas

Para que el Test Runner muestre las pruebas del paquete, agrégalo a `testables` en el manifiesto:

```json
"testables": ["com.modernwestern.unity-rest"]
```

Luego, **Window → General → Test Runner → EditMode → Run All**. Desde la línea de comandos:

```bash
Unity.exe -batchmode -projectPath <proyecto> -runTests -testPlatform EditMode -assemblyNames UnityREST.Tests -testResults resultados.xml
```

Las pruebas de integración levantan un servidor HTTP en `127.0.0.1` con un puerto libre; no salen a internet.
