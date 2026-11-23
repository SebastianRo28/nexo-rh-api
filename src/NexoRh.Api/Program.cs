using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var desarrollo = builder.Environment.IsDevelopment();

// --- Comprobaciones de producción ----------------------------------------------------------------------------------
// Fuera de desarrollo la API no arranca con una configuración pensada solo para pruebas.
if (!desarrollo && !builder.Environment.IsEnvironment("Pruebas"))
{
    if (!string.IsNullOrWhiteSpace(config["Asistencia:FechaCorte"]))
        throw new InvalidOperationException("Asistencia:FechaCorte fija la fecha del sistema y solo se usa en desarrollo. Déjala vacía en producción.");
    if (config.GetValue("Semilla:DatosDemo", false))
        throw new InvalidOperationException("Semilla:DatosDemo carga la empresa de ejemplo y solo se usa en desarrollo.");
    var cors = config.GetSection("Cors:Origenes").Get<string[]>() ?? [];
    if (cors.Length == 0 || cors.Any(o => !o.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Cors:Origenes debe indicar la dirección https:// del frontend en producción.");
    if (string.Equals(config["Dns:Modo"], "Simulado", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Dns:Modo = Simulado solo se usa en desarrollo y pruebas: la verificación de dominios necesita el DNS real.");
}

// Ningún pedido puede superar 10 MB (las subidas más grandes son evidencias de 5 MB e importaciones de 2 MB).
builder.WebHost.ConfigureKestrel(k => { k.Limits.MaxRequestBodySize = 10 * 1024 * 1024; k.AddServerHeader = false; });
if (!desarrollo) builder.Logging.AddJsonConsole(o => { o.IncludeScopes = false; o.UseUtcTimestamp = true; });

// --- Bases de datos y tenants --------------------------------------------------------------------------------------
// Negocio: BD compartida por los tenants (o la dedicada de un tenant, según el catálogo). Catálogo: tenants y dominios;
// si no se configura, se usa la misma instancia con el nombre de la BD de negocio + "Catalogo".
var csNegocio = config.GetConnectionString("Nexo") ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Nexo.");
var csCatalogo = config.GetConnectionString("Catalogo") ?? CadenaCatalogo(csNegocio);
builder.Services.Configure<OpcionesPlataforma>(config.GetSection("Plataforma"));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CadenasConexion>();
builder.Services.AddScoped<ContextoTenant>();
// DNS para verificar dominios propios y remitentes: el real en producción; en desarrollo y pruebas, uno en memoria.
if (string.Equals(config["Dns:Modo"] ?? (desarrollo || builder.Environment.IsEnvironment("Pruebas") ? "Simulado" : "Real"), "Simulado", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ResolutorDnsSimulado>();
    builder.Services.AddSingleton<IResolutorDns>(sp => sp.GetRequiredService<ResolutorDnsSimulado>());
}
else builder.Services.AddSingleton<IResolutorDns, ResolutorDns>();
builder.Services.AddScoped<DominiosTenant>();
builder.Services.AddScoped<BuscadorTenants>();
builder.Services.AddSingleton<AprovisionadorTenants>();
builder.Services.AddDbContext<CatalogoDbContext>(o => o.UseSqlServer(csCatalogo));
builder.Services.AddDbContext<NexoDbContext>((sp, o) => o
    .UseSqlServer(sp.GetRequiredService<ContextoTenant>().CadenaConexion ?? csNegocio)
    .AddInterceptors(SesionTenantInterceptor.Instancia));

// --- Autenticación -------------------------------------------------------------------------------------------------
var jwt = config.GetSection("Jwt").Get<JwtOpciones>() ?? new JwtOpciones();
if (Encoding.UTF8.GetByteCount(jwt.Clave) < 32)
    throw new InvalidOperationException("Jwt:Clave debe tener al menos 32 caracteres. Configúrala en appsettings.Development.json, user-secrets o una variable de entorno.");
builder.Services.Configure<JwtOpciones>(config.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AjustesTenant>();
builder.Services.AddScoped<Marcas>();
builder.Services.AddScoped<Suscripciones>();
builder.Services.AddScoped<LimitesPlan>();
builder.Services.AddScoped<Implementacion>();
builder.Services.AddScoped<TokensAcceso>();
builder.Services.AddScoped<Invitaciones>();
builder.Services.AddScoped<Enlaces>();
builder.Services.AddSingleton<SecretosTotp>();
// Las claves de Data Protection cifran el doble factor: en producción deben persistir (DataProtection:Carpeta).
var dp = builder.Services.AddDataProtection().SetApplicationName("NexoRh");
if (config["DataProtection:Carpeta"] is { Length: > 0 } carpetaDp) dp.PersistKeysToFileSystem(new DirectoryInfo(carpetaDp));
builder.Services.Configure<OpcionesCorreo>(config.GetSection("Correo"));
builder.Services.AddScoped<EnviadorCorreo>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Emisor,
        ValidateAudience = true, ValidAudience = jwt.Audiencia,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role,
    };
    // El token solo vale si la cuenta sigue activa y no cambió su sello (contraseña, rol o estado).
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            // Un token de una empresa no sirve en el enlace de otra (claim tid ≠ tenant del host).
            var tenant = ctx.HttpContext.RequestServices.GetRequiredService<ContextoTenant>();
            if (tenant.Id is not { } tid || ctx.Principal?.FindFirstValue(Claves.Tenant) != tid.ToString()) { ctx.Fail("El token es de otra empresa."); return; }
            // Sesión de soporte de un usuario de plataforma: vale mientras la sesión siga abierta y vigente (60 min).
            if (ctx.Principal?.FindFirstValue(Claves.SesionSoporte) is { } ss)
            {
                var cat = ctx.HttpContext.RequestServices.GetRequiredService<CatalogoDbContext>();
                var vigente = Guid.TryParse(ss, out var sid) && await cat.SesionesSoporte.AsNoTracking()
                    .AnyAsync(x => x.Id == sid && x.TenantId == tid && x.Fin == null && x.Expira > DateTime.UtcNow);
                if (!vigente) ctx.Fail("La sesión de soporte terminó.");
                return;
            }
            var db = ctx.HttpContext.RequestServices.GetRequiredService<NexoDbContext>();
            var sub = ctx.Principal?.FindFirstValue("sub");
            var u = int.TryParse(sub, out var id)
                ? await db.Usuarios.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Activo, x.Sello, x.DebeCambiarPassword, x.TotpActivo, x.Rol }).FirstOrDefaultAsync()
                : null;
            if (u is null || !u.Activo || u.Sello != ctx.Principal!.FindFirstValue(Claves.Sello)) { ctx.Fail("Sesión no válida."); return; }
            var identidad = (ClaimsIdentity)ctx.Principal!.Identity!;
            if (u.DebeCambiarPassword) identidad.AddClaim(new Claim(Claves.PasswordTemporal, "1"));
            if (!u.TotpActivo)
            {
                var politica = (await ctx.HttpContext.RequestServices.GetRequiredService<AjustesTenant>().DeAsync(tid)).Acceso;
                if (AjustesTenant.ExigeDobleFactor(politica, u.Rol)) identidad.AddClaim(new Claim(Claves.DobleFactorPendiente, "1"));
            }
        },
    };
});
// Consola del proveedor (admin.{dominio}): esquema propio con otra audiencia; un token de tenant nunca sirve aquí ni al revés.
builder.Services.AddAuthentication().AddJwtBearer(Politicas.EsquemaPlataforma, o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Emisor,
        ValidateAudience = true, ValidAudience = jwt.AudienciaConsola,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role,
    };
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var cat = ctx.HttpContext.RequestServices.GetRequiredService<CatalogoDbContext>();
            var u = int.TryParse(ctx.Principal?.FindFirstValue("sub"), out var id)
                ? await cat.UsuariosPlataforma.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Activo, x.Sello }).FirstOrDefaultAsync()
                : null;
            if (u is null || !u.Activo || u.Sello != ctx.Principal!.FindFirstValue(Claves.Sello)) ctx.Fail("Sesión de consola no válida.");
        },
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Administrador, p => p.RequireRole("Administrador"))
    .AddPolicy(Politicas.Gestion, p => p.RequireRole("Administrador", "Supervisor"))
    .AddPolicy(Politicas.Plataforma, p => p.AddAuthenticationSchemes(Politicas.EsquemaPlataforma).RequireClaim(Claves.Alcance, Claves.AlcancePlataforma))
    .AddPolicy(Politicas.PlataformaAdmin, p => p.AddAuthenticationSchemes(Politicas.EsquemaPlataforma).RequireClaim(Claves.Alcance, Claves.AlcancePlataforma).RequireRole(RolesPlataforma.Admin))
    .AddPolicy(Politicas.PlataformaSoporte, p => p.AddAuthenticationSchemes(Politicas.EsquemaPlataforma).RequireClaim(Claves.Alcance, Claves.AlcancePlataforma).RequireRole(RolesPlataforma.Admin, RolesPlataforma.Soporte));
builder.Services.AddScoped<AuditoriaConsola>();
builder.Services.AddSingleton<MetricasTenants>();
builder.Services.AddSingleton<TrabajosAprovisionamiento>();
builder.Services.AddScoped<ConsultasPlataforma>();
builder.Services.AddScoped<CicloVidaTenants>();
builder.Services.AddSingleton<MudanzaBaseDedicada>();
builder.Services.AddHostedService<CicloVidaEnSegundoPlano>();

// Máximo 10 intentos de inicio de sesión por minuto por IP (además del bloqueo de la cuenta tras 5 fallos).
var loginPorMinuto = config.GetValue("Seguridad:LoginPorMinuto", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

// --- Web -----------------------------------------------------------------------------------------------------------
// Detrás de un proxy inverso (nginx, IIS, App Service…) la IP real del cliente y el esquema https llegan en X-Forwarded-*.
// Sin esto, el límite de intentos de login contaría a todos los usuarios como una sola IP (la del proxy).
var detrasDeProxy = config.GetValue("Proxy:Habilitado", false);
if (detrasDeProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1;
        // Por defecto solo se confía en un proxy de la misma máquina (127.0.0.1 / ::1: nginx local, cloudflared…).
        var ips = config.GetSection("Proxy:IpsConfiables").Get<string[]>() ?? [];
        if (ips.Length > 0)
        {
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
            foreach (var ip in ips) o.KnownProxies.Add(IPAddress.Parse(ip));
        }
    });
}
builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; });
var origenes = config.GetSection("Cors:Origenes").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Auditoria>();
builder.Services.AddScoped<Alcance>();
builder.Services.AddSingleton<Genericos>();
builder.Services.AddScoped<Configuraciones>();
builder.Services.AddScoped<Planilla>();
builder.Services.AddScoped<GeneradorReportes>();
builder.Services.Configure<RelojAsistencia.Opciones>(config.GetSection("Asistencia"));
builder.Services.AddSingleton<RelojAsistencia>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Nexo RH API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Token de POST /api/auth/login",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>(),
    });
});

var app = builder.Build();

// Crea o actualiza las bases (por defecto solo en Development): catálogo y negocio (con Row-Level Security).
// Con Semilla:DatosDemo (por defecto en Development) asegura el tenant de ejemplo "Comercial Andina" con sus datos.
// Sin ella, en una instalación nueva crea el primer tenant (Semilla:TenantSlug y TenantNombre) con sus datos base y su
// primer administrador; los demás tenants se crean desde la consola del proveedor.
if (config.GetValue("BaseDatos:MigrarAlIniciar", desarrollo))
{
    using (var scope = app.Services.CreateScope())
    {
        var cat = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        await cat.Database.MigrateAsync();
        await SemillaPlataforma.CargarAsync(cat, config, desarrollo || app.Environment.IsEnvironment("Pruebas"), app.Logger);
    }
    var aprovisionador = app.Services.GetRequiredService<AprovisionadorTenants>();
    await aprovisionador.MigrarNegocioAsync();
    if (config.GetValue("Semilla:DatosDemo", desarrollo))
        await aprovisionador.AsegurarAsync(AprovisionadorTenants.TenantDemo(), datosDemo: true);
    else
    {
        using var scope = app.Services.CreateScope();
        if (!await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Tenants.AnyAsync())
        {
            var slug = (config["Semilla:TenantSlug"] ?? "").Trim().ToLowerInvariant();
            if (slug.Length == 0) app.Logger.LogWarning("No hay tenants: configura Semilla:TenantSlug y Semilla:TenantNombre para crear el primero.");
            else await aprovisionador.AsegurarAsync(new Tenant { Slug = slug, Nombre = config["Semilla:TenantNombre"] ?? slug, RazonSocial = config["Semilla:TenantNombre"] ?? slug }, datosDemo: false);
        }
    }
}

if (detrasDeProxy) app.UseForwardedHeaders();
// Antes que todo lo demás: el tenant del request sale del host.
app.UseMiddleware<ResolucionTenant>();
// Requests, latencia y errores por tenant (Salud de la consola).
app.Use(MetricasTenants.Medir);
app.UseExceptionHandler();
app.UseStatusCodePages();
if (config.GetValue("Swagger:Habilitado", desarrollo))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (!desarrollo && config.GetValue("Seguridad:ForzarHttps", true))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
// Cabeceras de seguridad. La API solo devuelve JSON y archivos; el frontend (si la API lo sirve) solo carga sus propios
// archivos y la fuente de Google Fonts. Angular inserta los estilos de cada componente, por eso style-src admite inline.
const string CspApi = "default-src 'none'; frame-ancestors 'none'";
const string CspWeb = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
               "font-src 'self' https://fonts.gstatic.com; img-src 'self' data: blob:; connect-src 'self'; object-src 'none'; " +
               "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    var ruta = ctx.Request.Path;
    if (ruta.StartsWithSegments("/api")) { h.ContentSecurityPolicy = CspApi; h.CacheControl = "no-store"; }
    else if (!ruta.StartsWithSegments("/swagger")) h.ContentSecurityPolicy = CspWeb;
    await next();
});

// Frontend compilado (ng build): con Web:Carpeta la API sirve también la aplicación web en el mismo dominio, sin CORS.
var carpetaWeb = config["Web:Carpeta"];
PhysicalFileProvider? archivosWeb = null;
if (!string.IsNullOrWhiteSpace(carpetaWeb))
{
    var ruta = Path.GetFullPath(carpetaWeb, app.Environment.ContentRootPath);
    if (!File.Exists(Path.Combine(ruta, "index.html")))
        throw new InvalidOperationException($"Web:Carpeta no contiene index.html ({ruta}). Ejecuta “ng build” en el frontend.");
    archivosWeb = new PhysicalFileProvider(ruta);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = archivosWeb,
        // Los archivos de Angular llevan un hash en el nombre: se pueden guardar en caché un año.
        OnPrepareResponse = c => c.Context.Response.Headers.CacheControl =
            c.File.Name == "index.html" ? "no-cache" : "public, max-age=31536000, immutable",
    });
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
// Con contraseña temporal solo se puede usar /api/auth (para cambiarla).
app.Use(async (ctx, next) =>
{
    if (ctx.User.HasClaim(Claves.PasswordTemporal, "1") && !ctx.Request.Path.StartsWithSegments("/api/auth") && !ctx.Request.Path.StartsWithSegments("/api/me"))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { title = "Debes cambiar tu contraseña temporal antes de continuar.", status = 403, codigo = "password_temporal" });
        return;
    }
    // La política de la empresa exige doble factor y la cuenta aún no lo configuró: solo /api/auth (para hacerlo).
    // Cuenta suspendida o cancelada: los tokens que no son de administrador dejan de servir.
    if (ctx.User.Identity?.IsAuthenticated == true && !ctx.User.IsInRole("Administrador") && !ctx.Request.Path.StartsWithSegments("/api/auth")
        && ctx.RequestServices.GetRequiredService<ContextoTenant>().Estado is EstadosTenant.Suspendido or EstadosTenant.Cancelado)
    {
        ctx.Response.StatusCode = StatusCodes.Status423Locked;
        await ctx.Response.WriteAsJsonAsync(new { title = "La plataforma de tu empresa no está disponible temporalmente.", status = 423, codigo = "tenant_suspended" });
        return;
    }
    // Sesión de soporte en modo lectura: ninguna escritura (salvo terminar la sesión).
    if (ctx.User.FindFirstValue(Claves.ModoSoporte) == ModosSoporte.Lectura && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method)
        && !HttpMethods.IsOptions(ctx.Request.Method) && !ctx.Request.Path.StartsWithSegments("/api/auth/soporte"))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { title = "Sesión de soporte en modo solo lectura: no se pueden guardar cambios.", status = 403, codigo = "SUPPORT_READ_ONLY" });
        return;
    }
    if (ctx.User.HasClaim(Claves.DobleFactorPendiente, "1") && !ctx.Request.Path.StartsWithSegments("/api/auth") && !ctx.Request.Path.StartsWithSegments("/api/me"))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { title = "Tu empresa exige el doble factor. Configúralo para continuar.", status = 403, codigo = "doble_factor_requerido" });
        return;
    }
    await next();
});
app.UseAuthorization();
app.MapControllers();
// Para el monitor del servidor: 200 si la API responde y llega a la base de datos, 503 si no.
app.MapGet("/api/salud", async (NexoDbContext db, CancellationToken ct) =>
{
    bool baseOk;
    try { baseOk = await db.Database.CanConnectAsync(ct); } catch { baseOk = false; }
    return baseOk ? Results.Ok(new { estado = "ok", baseDatos = "ok" })
                  : Results.Json(new { estado = "degradado", baseDatos = "sin conexión" }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

// Rutas del frontend (p. ej. /admin/activos/inventario al recargar): devuelven index.html y Angular hace el resto.
// Las rutas /api inexistentes siguen respondiendo 404.
if (archivosWeb is not null)
{
    app.MapFallback(async ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api") || !HttpMethods.IsGet(ctx.Request.Method)) { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.Headers.CacheControl = "no-cache";
        await ctx.Response.SendFileAsync(archivosWeb.GetFileInfo("index.html"));
    }).AllowAnonymous();
}

app.Run();

public partial class Program
{
    /// <summary>BD catálogo por defecto: la misma instancia y el nombre de la BD de negocio + "Catalogo".</summary>
    public static string CadenaCatalogo(string csNegocio)
    {
        var b = new SqlConnectionStringBuilder(csNegocio);
        if (string.IsNullOrEmpty(b.InitialCatalog)) throw new InvalidOperationException("Configura ConnectionStrings:Catalogo.");
        b.InitialCatalog += "Catalogo";
        return b.ConnectionString;
    }
}
