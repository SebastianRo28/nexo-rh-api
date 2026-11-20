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

- Define `Jwt__Clave`, `ConnectionStrings__Nexo` y `Cors__Origenes__0` como variables de entorno o en un gestor de secretos.
- `BaseDatos:MigrarAlIniciar` es `false` fuera de desarrollo: aplica las migraciones como paso del despliegue.
- No configures `Semilla:PasswordInicial` en producción; sin ese valor no se crean usuarios de ejemplo.
