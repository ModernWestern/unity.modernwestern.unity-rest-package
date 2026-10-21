# 10. Herramientas del editor

> Estado a 2026-10-21. El inspector se verificó compilando y con pruebas de la lógica, pero todavía no se revisó a ojo en un proyecto real.

## Inspector de ApiConfig

- **Barra de entornos:** un botón por entorno; el seleccionado queda activo al instante. Debajo, la URL base con un candado (HTTPS) o una advertencia (HTTP).
- **Avisos:**
  - un entorno sin URL base o con una URL inválida;
  - un entorno `http://` que va a quedar bloqueado por Enforce Https;
  - una API key guardada en el asset;
  - endpoints sin nombre o con nombres repetidos.
- **Environments:** la lista de entornos con sus cabeceras y pines.
- **Endpoints:** una línea por endpoint, con el verbo en color (se cambia con un clic), el nombre y la ruta. El botón ▶ lo carga en el probador.
- **Transport:** timeout, reintentos, HTTPS obligatorio y nivel de log.
- **Request tester:** verbo, ruta (relativa al entorno activo o absoluta), cuerpo JSON para POST, PUT, PATCH y DELETE, y **Send**. Muestra código, tiempo, tamaño, URL final y el cuerpo, con el JSON formateado (hasta 16 000 caracteres). Usa el cliente real, con autenticación, cabeceras y pines.

## Drawers

| Tipo o atributo | Cómo se ve |
|---|---|
| `[Secret]` en un `string` | Campo enmascarado con un botón de ojo para mostrarlo |
| `HttpHeader` | Nombre y valor en una línea |
| `Endpoint` | Verbo en color, nombre y ruta en una línea |
| `EndpointRef` | Selector de `ApiConfig` y menú de sus endpoints; en rojo si el endpoint ya no existe |

`[Secret]` sirve en tus propios scripts:

```csharp
[SerializeField, Secret] private string serviceToken;
```

## AI Chat Provider

**Assets → Create → UnityREST → AI Chat Provider**. El inspector trae **Apply a preset…** (OpenAI, Anthropic, Gemini, Ollama, LM Studio, OpenRouter) y avisa cuando hay una clave guardada en el asset o cuando falta la variable de entorno indicada.

## AI Playground

**Window → UnityREST → AI Playground**, o el botón *Open AI Playground* de un proveedor:

- **Provider:** el asset de proveedor.
- **System prompt:** las instrucciones de la conversación.
- **MCP server / MCP token:** conecta un servidor MCP y suma sus herramientas al chat.
- La conversación con streaming, las llamadas a herramientas y sus resultados.
- **Ctrl+Enter** o **Send** para enviar, **Stop** para cancelar y **New conversation** para empezar de cero. Abajo, los tokens usados.
