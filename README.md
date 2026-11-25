# Nexo RH API

Backend de la plataforma de RR.HH. **Nexo RH** (el frontend Angular está en el repositorio `ventas-microservicios-front`, rama `nexo-rh`).

- **ASP.NET Core 8** Web API (C#)
- **SQL Server** con **Entity Framework Core 8** (migraciones en `src/NexoRh.Api/Data/Migraciones`)
- Autenticación con **JWT** y roles: `Administrador`, `Supervisor`, `Colaborador`

## Fase 1 (esta versión)

| Endpoint | Descripción | Acceso |
|---|---|---|
| `POST /api/auth/login` | Inicia sesión y devuelve el token | Público (máx. 10 intentos/min por IP; la cuenta se bloquea 15 min tras 5 fallos) |
| `GET /api/auth/me` | Usuario de la sesión | Autenticado |
| `GET /api/colaboradores` · `GET /api/colaboradores/{id}` | Directorio y fichas | Autenticado. Datos personales y sueldo solo para administradores, la propia persona y su cadena de jefaturas |
| `POST /api/colaboradores` · `PUT /api/colaboradores/{id}` | Crear y modificar, con validaciones | Administrador |
| `GET /api/organizacion/cargos` · `POST` · `PUT /{id}` | Cargos | Lectura: autenticado; escritura: Administrador |
| `GET /api/organizacion/familias` · `competencias` · `divisiones` | Estructura organizacional | Autenticado |
| `GET /api/cambios` | Historial de cambios (filtros `q`, `accion`, `desde`, `hasta`) | Administrador |
| `POST /api/cambios` | Registra un cambio hecho en un módulo del frontend sin API propia | Autenticado (el servidor firma usuario y fecha) |
| `GET /api/salud` | Estado del servicio | Público |

Cada creación o modificación queda en el historial de cambios con el usuario que la hizo.

## Puesta en marcha

Requisitos: .NET SDK 8 y SQL Server (la cadena por defecto usa `.\SQLEXPRESS` con autenticación de Windows).

1. Copia `src/NexoRh.Api/appsettings.Development.example.json` como `appsettings.Development.json` y reemplaza:
   - `Jwt:Clave`: una clave aleatoria de al menos 32 caracteres.
   - `Semilla:PasswordInicial`: la contraseña inicial de los usuarios de ejemplo (mínimo 10 caracteres).

   Ese archivo está en `.gitignore`; nunca lo subas al repositorio.
2. Si tu SQL Server no es `.\SQLEXPRESS`, cambia `ConnectionStrings:Nexo` en `appsettings.json` (o en el archivo de desarrollo).
3. Ejecuta:

   ```bash
   dotnet tool restore
   dotnet run --project src/NexoRh.Api --launch-profile http
   ```

   En desarrollo la API crea la base `NexoRh`, aplica las migraciones y carga los datos de ejemplo al iniciar.
   Queda en `http://localhost:5281` y la documentación en `http://localhost:5281/swagger`.

### Usuarios de ejemplo

Todos usan la contraseña de `Semilla:PasswordInicial`:

| Correo | Rol |
|---|---|
| r.paredes@comercialandina.pe | Administrador |
| l.caceres@comercialandina.pe | Administrador |
| g.navarro@comercialandina.pe | Supervisor (ve completo a su equipo de Finanzas) |
| d.benavides@comercialandina.pe | Supervisor (equipo de Operaciones) |
| f.alamos@comercialandina.pe | Colaborador |

### Migraciones

```bash
dotnet ef migrations add NombreDelCambio --project src/NexoRh.Api --output-dir Data/Migraciones
dotnet ef database update --project src/NexoRh.Api
```

## Producción

En producción (`ASPNETCORE_ENVIRONMENT=Production`) la API **no carga la empresa de ejemplo**: crea solo los datos base
(niveles de cargo, competencias, perfiles predefinidos, feriados, catálogo de ítems y parámetros legales de planilla,
catálogos genéricos), abre la planilla del mes en curso y crea el primer administrador, que debe cambiar su contraseña
al entrar.

### Configuración (variables de entorno o gestor de secretos; nunca en archivos del repositorio)

| Variable | Valor |
|---|---|
| `ConnectionStrings__Nexo` | Cadena de SQL Server de producción (usuario propio de la aplicación, no `sa`) |
| `Jwt__Clave` | Clave aleatoria de al menos 32 caracteres (p. ej. `openssl rand -base64 48`) |
| `Cors__Origenes__0` | Dirección `https://` del frontend. Si el frontend y la API van en el mismo dominio, la del sitio |
| `BaseDatos__MigrarAlIniciar` | `true` para que la API aplique las migraciones y los datos base al arrancar (una sola instancia) |
| `Semilla__AdminCorreo` · `Semilla__AdminNombre` · `Semilla__PasswordInicial` | Primer administrador (contraseña de 12+ caracteres). Solo se usan si aún no hay usuarios; bórralas después del primer arranque |
| `Proxy__Habilitado` | `true` si la API está detrás de nginx, IIS u otro proxy inverso (lee la IP real del cliente) |
| `Proxy__IpsConfiables__0` | IP del proxy, si no está en la misma máquina |
| `Seguridad__ForzarHttps` | `true` por defecto (HSTS y redirección a https). `false` solo si el proxy ya termina el TLS y la API no recibe tráfico externo |
| `Plataforma__Dominio` | Dominio base de la plataforma (`nexorh.pe`): cada empresa entra por `{slug}.nexorh.pe` y `app.nexorh.pe` es la entrada global |
| `Correo__Proveedor` | `Bandeja` (por defecto: los correos solo quedan en el catálogo, sin enviarse) o `Smtp` |
| `Correo__Remitente` · `Correo__SmtpHost` · `Correo__SmtpPuerto` · `Correo__SmtpUsuario` · `Correo__SmtpClave` | Datos del servidor SMTP cuando el proveedor es `Smtp` (invitaciones, recuperación de contraseña, enlaces de empresa) |
| `Plataforma__AdminCorreo` · `Plataforma__AdminPassword` | Primer usuario de la consola del proveedor (contraseña de 12+ caracteres); solo se usa si aún no hay usuarios de plataforma |
| `DataProtection__Carpeta` | Carpeta persistente para las claves que cifran el doble factor, las claves DKIM y las cadenas de las BD dedicadas. **Si se pierden, las BD dedicadas quedan sin conexión** y las cuentas con doble factor deben configurarlo de nuevo: respáldala |
| `Dns__Modo` | `Real` (por defecto fuera de desarrollo; verifica dominios y remitentes con el DNS). `Simulado` solo en desarrollo y pruebas (la API no arranca con él en producción) |
| `Dns__Servidores__0` | Opcional: servidor DNS para las verificaciones (por defecto, los del sistema) |
| `Correo__SpfInclude` | Dominio que los clientes incluyen en su SPF para enviar desde su dominio (por defecto `spf.{dominio}`) |
| `BaseDatos__ServidorDedicadas` | Opcional: cadena de la instancia donde se crean las BD dedicadas (por defecto, la misma que `ConnectionStrings__Nexo`) |
| `Izipay__Modo` | `Simulado` (por defecto: tarjetas de prueba internas, sin Izipay; no funciona en producción), `Sandbox` o `Produccion` |
| `Izipay__CodigoComercio` · `Izipay__ClaveApi` · `Izipay__ClaveHash` · `Izipay__ClavePublicaRsa` | Del panel de Izipay (Desarrolladores › Claves): código de comercio, “Clave de API del nuevo botón de pagos”, “Clave Hash” y “Clave pública izipay” (RSA) |
| `Izipay__UrlIpn` | URL pública de las notificaciones: `https://{api}/api/publico/pagos/izipay/ipn` |
| `Izipay__IpServidor` · `Izipay__RellenoRsa` | Opcionales: IP pública del servidor para el antifraude en los cobros automáticos; relleno RSA (`Pkcs1` u `OaepSha1`) |

La API **no arranca** fuera de desarrollo si `Asistencia:FechaCorte` (fecha fija de los datos de ejemplo) o
`Semilla:DatosDemo` están configurados, o si `Cors:Origenes` no es una dirección `https://`.

### Qué ya trae la API

- Inicio de sesión limitado a 10 intentos por minuto por IP y bloqueo de la cuenta 15 minutos tras 5 fallos.
- Todos los endpoints exigen sesión salvo los pasos del acceso (login, doble factor, recuperación, activación,
  `/api/publico/*`) y `/api/salud` (lo verifica una prueba automática).
- Acceso por empresa: login con la marca del tenant, “¿Olvidaste tu contraseña?” (enlace de 1 hora), invitación
  “Activa tu cuenta” (72 horas), doble factor con app de autenticación (TOTP, la política del tenant puede exigirlo) y
  selector de cuenta en `app.{dominio}`. En desarrollo, los correos se ven en `/dev/correos` del frontend.
- Marca por empresa (Administración › Personaliza tu marca): logos, imagen del login, color primario con paleta OKLCH y
  validación de contraste WCAG AA, tipografía, bordes, terminología y correos. Cada publicación es una versión que se
  puede restaurar; `GET /api/publico/marca` la entrega al frontend (cacheable). Las imágenes se guardan en el catálogo y
  los SVG se limpian de scripts al subirlos.
- Planes y módulos: cada tenant tiene un plan (Básico, Pro, Enterprise) y módulos adicionales en el catálogo. Los
  endpoints de un módulo no contratado responden 403 `module_not_enabled` y el frontend muestra el modal comercial
  (“Solicitar información” crea un lead). `GET /api/me/context` entrega módulos, banners (prueba gratuita, facturas
  pendientes, límites) y el checklist de implementación. Los límites del plan (empresas, administradores) bloquean al
  100 %. Estado de cuenta, Seguridad y acceso, e Implementación están en Administración.
- Consola del proveedor en `admin.{dominio}` (en desarrollo `admin.localhost:4200`): solo el equipo de Nexo RH, con doble
  factor obligatorio (se configura en el primer ingreso). Dashboard, tenants (alta con aprovisionamiento, suspender,
  módulos con vigencia, dominios, notas), sesiones de soporte de 60 minutos auditadas (solo lectura o escritura
  justificada), planes y precios, catálogos globales versionados, novedades, feature flags y salud. El primer usuario
  se crea con `Plataforma__AdminCorreo` y `Plataforma__AdminPassword` (en desarrollo, con la contraseña de la semilla).
- Ciclo de vida de los tenants (cada hora; `CicloVida__Habilitado`, apagado por defecto en desarrollo y pruebas, y
  `CicloVida__Minutos`): prueba gratuita vencida → suspendido; factura vencida → moroso, y a los 30 días → suspendido;
  al pagar vuelve a activo; mide el uso del mes y emite la factura del mes anterior. Suspendido: solo los
  administradores entran, a “Cuenta suspendida” (facturas pendientes y descarga de datos); el resto recibe 423.
  Cancelado: 30 días de solo lectura y exportación, y luego se eliminan sus datos de negocio y del catálogo (se
  conservan las facturas y la auditoría) con un certificado de eliminación. `GET /api/exportacion` (administradores)
  entrega un ZIP con un CSV por tabla y los archivos; queda en la auditoría. En la consola: cancelar, eliminar,
  marcar factura pagada y “Revisar estado ahora”; en Salud, “Ejecutar ciclo de vida” para todos los tenants.
- Enterprise (Administración › Dominio y correo, y la pestaña Dominios de la consola):
  - **Dominio propio** (`CUSTOM_DOMAIN`): el cliente agrega `rrhh.suempresa.pe` y publica un CNAME hacia
    `{slug}.{dominio}` y un TXT `_nexorh.{host}` de propiedad; al verificarse, la app abre en ese host. El certificado
    se emite automáticamente en el proxy: con Caddy, `on_demand_tls { ask http://api:8080/api/publico/tls }` (responde 200
    solo para dominios propios verificados) y el primer acceso por HTTPS lo marca “Activo”. El dominio principal se usa
    en los enlaces de los correos. Frontend y API deben servirse en el mismo host (el proxy envía `/api` a la API).
  - **Correos desde su dominio** (`CUSTOM_EMAIL`): remitente `rrhh@suempresa.pe` con SPF, DKIM (clave RSA propia por
    tenant, cifrada) y DMARC verificados en su DNS. El correo sale firmado con DKIM por el cliente SMTP de la API.
  - **Ocultar “Powered by Nexo RH”** (`HIDE_POWERED_BY`) en el login y los correos; si baja de plan, vuelve a mostrarse.
  - **BD dedicada** (`DEDICATED_DB`): en el alta (opción del wizard) o moviendo un tenant existente desde la consola
    (“Mover a BD dedicada”: modo mantenimiento con 503, copia, comparación de conteos, cambio de cadena en el catálogo y
    borrado de sus filas de la compartida). Se llama `{BD compartida}_{slug}`; al arrancar se migra después de la
    compartida (se detiene ante el primer error y Salud la muestra pendiente) y la eliminación definitiva la borra.
  - En desarrollo el DNS es simulado: los registros se publican con `POST /api/dev/dns` (`{ tipo, nombre, valor }`) y
    se pueden probar dominios `*.*.localhost` (p. ej. `rrhh.empresa.localhost:4200`).
- Cabeceras de seguridad (`nosniff`, `X-Frame-Options: DENY`, CSP, `no-store` en `/api`) y sin cabecera `Server`.
- Pedidos de máximo 10 MB; Swagger solo en desarrollo; registros en formato JSON por la consola.
- `GET /api/salud` devuelve 200 si la API llega a la base de datos y 503 si no: úsalo en el monitor del servidor.
- Cobro automático con Izipay (`Catalogo/Pagos.cs`): el administrador registra su tarjeta en el formulario de Izipay
  (Estado de cuenta) y autoriza el cargo recurrente; solo se guarda el token, cifrado. El ciclo de vida cobra cada factura
  el día de su emisión y reintenta a los 3 y 7 días; después sigue la morosidad. “Pagar ahora” (cliente) y “Cobrar ahora”
  (consola) cobran fuera de ese calendario. Un índice único impide dos cobros vigentes de una misma factura. Si Izipay no
  responde, el cobro queda “Por confirmar” hasta su notificación (`/api/publico/pagos/izipay/ipn`, firma HMAC-SHA256) o
  hasta resolverlo en la consola. **Antes de producción, valida en Sandbox el cobro `pay_token` + `mitrecurrent`, el
  relleno RSA y el `userScoring` del antifraude con Izipay.**
- Módulos sin tabla propia (KPI, beneficios y canjes, cursos y su avance, evaluaciones, tickets, dashboards, niveles de
  banda, correos automáticos, portadas y accesos del portal, valores de reconocimiento, archivos de pago, claves de API,
  IPs, webhooks e integración contable): `GET/POST/PUT/DELETE /api/datos/{coleccion}`, guardados como JSON por empresa
  (tabla `RegistrosModulo`, con RLS). Cada colección define en `Services/ColeccionesModulo.cs` quién lee y escribe, qué
  módulo requiere y qué campos puede enviar un colaborador en sus registros propios (el servidor fija, p. ej., el costo
  de un canje). Las claves de API guardan solo el hash: el token completo se devuelve una vez, al crearla.
- Datos de ejemplo: solo la empresa demo los recibe (`Semilla*.cs` y `Data/demo-modulos.json`); una empresa nueva
  empieza vacía. Asistencia, remuneraciones y parámetros generales se guardan en `/api/configuracion/{clave}`.

### Migraciones como paso del despliegue (alternativa a `MigrarAlIniciar`)

```bash
dotnet ef migrations script --idempotent --project src/NexoRh.Api -o migraciones.sql
```

El script se puede aplicar varias veces sin riesgo. Con esta opción, arranca la API una vez con
`BaseDatos__MigrarAlIniciar=true` para que cree los datos base y el primer administrador.

### Copias de seguridad

`tools/respaldo-sql.ps1` hace una copia completa verificada y borra las de más de N días:

```powershell
.\tools\respaldo-sql.ps1 -Servidor '.\SQLEXPRESS' -Carpeta 'D:\Respaldos\NexoRh' -DiasRetencion 30
```

Prográmalo a diario y copia los `.bak` fuera del servidor. Prueba restaurar una copia de vez en cuando:
una copia que nunca se restauró no está comprobada.

### Integración continua

`.github/workflows/ci.yml` compila y ejecuta todas las pruebas contra un SQL Server temporal en cada push a `master`
y en cada pull request.

## Demo temporal para un cliente (sin publicar)

`tools/demo.ps1` compila el frontend y lo sirve desde la API en `http://localhost:5281`, con los datos de ejemplo,
Swagger apagado y la IP real de cada visitante (para el límite de intentos de login):

```powershell
.\tools\demo.ps1 -Limpia      # -Limpia: empieza con los datos de ejemplo intactos
```

En otra ventana, compártelo con un túnel temporal de Cloudflare (no requiere cuenta):

```powershell
cloudflared tunnel --url http://localhost:5281
```

El túnel muestra una dirección `https://….trycloudflare.com` que funciona mientras las dos ventanas estén abiertas.
Crea usuarios propios para el cliente en Configuración › Usuarios (no compartas los de ejemplo) y desactívalos al terminar.
