# 7. HTTPS y seguridad

> Estado a 2026-10-21.

## HTTPS obligatorio

Con **Enforce Https** activado (por defecto), una URL `http://` se bloquea antes de enviarse y devuelve `HttpError.InsecureUrl`. Siempre se permiten los hosts de loopback, para el desarrollo local:

- `localhost`
- `127.x.x.x`
- `[::1]`

Otros hosts de tu red (`http://192.168.1.20:3000`) quedan bloqueados mientras la opción esté activa; el inspector lo advierte en ese entorno. Para un servidor local en otro equipo, usa HTTPS o desactiva la opción solo en ese `ApiConfig` de desarrollo.

Unity 2022+ además tiene **Player Settings → Allow downloads over HTTP**. Si lo dejas en *Not allowed* y desactivas Enforce Https, Unity lanza una excepción al enviar. Enforce Https da un error más claro y antes.

## Pinning de clave pública

Por defecto, Unity acepta cualquier certificado firmado por una autoridad de confianza del sistema. El pinning además exige que la **clave pública** del servidor sea una de las que tú esperas, lo que protege contra proxies de inspección y autoridades comprometidas.

### Obtener el pin

```bash
openssl s_client -connect api.example.com:443 -servername api.example.com </dev/null \
  | openssl x509 -pubkey -noout \
  | openssl pkey -pubin -outform der \
  | openssl dgst -sha256 -binary \
  | base64
```

El resultado, por ejemplo `lFgo/YyrgRtdCtJ+XWiMjLL3wA0Am7TODVBhr2DOZec=`, va en **Certificate Pins** del entorno. Se acepta con o sin el prefijo `sha256/`. El formato es el de OkHttp y HPKP: el SHA-256 del *SubjectPublicKeyInfo*. Las pruebas comparan el cálculo del paquete con el de openssl, con claves RSA y EC.

### Reglas para no quedarte afuera

- **Pon siempre al menos dos pines:** el actual y uno de respaldo, de una clave que ya generaste y guardas offline. Si el servidor cambia de clave y el juego publicado no la conoce, no podrá conectarse hasta que actualices el build.
- Los pines **sobreviven a las renovaciones** que conservan la clave. Muchos servicios (Let's Encrypt con su configuración por defecto, CDNs) cambian de clave al renovar: verifícalo antes de activar el pinning en producción.
- Con pines, Unity deja de validar la cadena por su cuenta. El paquete acepta el certificado solo si su pin coincide y está dentro de su período de validez.
- En **WebGL** el navegador hace la validación TLS y el pinning no aplica.

## Secretos

Todo lo que se guarda en un asset (claves de API, tokens) viaja dentro del build y se puede extraer con herramientas comunes. El inspector advierte cuando un entorno tiene una clave guardada.

- Para APIs de pago (incluidos los modelos de IA), el juego debería hablar con **tu backend**, que guarda la clave y llama al proveedor. Apunta la URL base del entorno a ese backend.
- En el editor y en herramientas internas, lee las claves de variables de entorno (el proveedor de IA lo trae: `apiKeyEnvironmentVariable`).
- Los logs del paquete nunca imprimen cabeceras, así que tokens y claves no terminan en la consola ni en los reportes de errores.

## Cuerpos y URLs

- Los parámetros de ruta y de query se escapan siempre: un valor con `/`, `?` o `&` no puede cambiar a qué recurso llamas.
- No pongas datos sensibles en la query (`?token=...`): las URLs quedan en logs de servidores y proxies. Usa cabeceras.
