using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NexoRh.Api.Catalogo;

/// <summary>Escribe en la auditoría de la plataforma con el usuario de la consola, la IP y el navegador.</summary>
public class AuditoriaConsola(CatalogoDbContext catalogo, IHttpContextAccessor http)
{
    public void Registrar(string accion, Guid? tenantId = null, string? detalle = null, string? actor = null)
    {
        var ctx = http.HttpContext;
        catalogo.AuditoriaPlataforma.Add(new AuditoriaPlataforma
        {
            Accion = accion, TenantId = tenantId, Detalle = detalle?[..Math.Min(detalle.Length, 1000)],
            Actor = actor ?? ctx?.User.Identity?.Name ?? "Sistema",
            Ip = ctx?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ctx?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 300)] : null,
        });
    }
}

/// <summary>
/// Primer usuario de la consola: si no hay ninguno, crea el administrador de plataforma con Plataforma:AdminCorreo y
/// Plataforma:AdminPassword (en desarrollo y pruebas, la contraseña de la semilla). El 2FA se enrola en el primer ingreso.
/// </summary>
public static class SemillaPlataforma
{
    public static readonly PasswordHasher<UsuarioPlataforma> Hasher = new();

    public static async Task CargarAsync(CatalogoDbContext catalogo, IConfiguration config, bool desarrollo, ILogger log)
    {
        if (await catalogo.UsuariosPlataforma.AnyAsync()) return;
        var correo = (config["Plataforma:AdminCorreo"] ?? "plataforma@nexorh.pe").Trim().ToLowerInvariant();
        var nombre = (config["Plataforma:AdminNombre"] ?? "Administrador de plataforma").Trim();
        var password = config["Plataforma:AdminPassword"] ?? (desarrollo ? config["Semilla:PasswordInicial"] : null);
        if (string.IsNullOrEmpty(password) || password.Length < 12)
        {
            log.LogWarning("No hay usuarios de la consola: configura Plataforma:AdminCorreo y Plataforma:AdminPassword (mínimo 12 caracteres).");
            return;
        }
        var u = new UsuarioPlataforma { Correo = correo, Nombre = nombre, Rol = RolesPlataforma.Admin };
        u.PasswordHash = Hasher.HashPassword(u, password);
        catalogo.UsuariosPlataforma.Add(u);
        await catalogo.SaveChangesAsync();
        log.LogInformation("Se creó el administrador de la consola ({Correo}); enrolará el doble factor en su primer ingreso.", correo);
    }

    /// <summary>
    /// Publica la primera versión del catálogo global "ubigeo" con la lista oficial del INEI (Lista_Ubigeos_INEI.csv de
    /// datosabiertos.gob.pe, en Catalogo/Datos) si todavía no tiene ninguna. Las siguientes se publican desde la consola.
    /// </summary>
    public static async Task CargarUbigeoAsync(CatalogoDbContext catalogo, ILogger log)
    {
        if (await catalogo.VersionesCatalogo.AnyAsync(v => v.Clave == Controllers.Ubigeo.Clave)) return;
        var ruta = Path.Combine(AppContext.BaseDirectory, "Catalogo", "Datos", "ubigeo-inei.csv");
        if (!File.Exists(ruta)) { log.LogWarning("No se encontró {Ruta}: el catálogo de ubigeo queda sin publicar.", ruta); return; }
        var lineas = (await File.ReadAllTextAsync(ruta)).Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        catalogo.VersionesCatalogo.Add(new VersionCatalogo
        {
            Clave = Controllers.Ubigeo.Clave, Version = "v2026.1", VigenteDesde = new DateOnly(2026, 1, 1), Registros = lineas.Length - 1,
            Notas = "Lista oficial de ubigeos del INEI (Plataforma Nacional de Datos Abiertos, Lista_Ubigeos_INEI.csv).",
            Contenido = string.Join("\n", lineas), PublicadaPor = "Semilla",
        });
        await catalogo.SaveChangesAsync();
        log.LogInformation("Se publicó el catálogo de ubigeo v2026.1 ({Registros} distritos).", lineas.Length - 1);
    }
}

/// <summary>Requests por tenant en la última hora (latencia p95 y errores 5xx) para Salud (P-SA.10). En memoria.</summary>
public class MetricasTenants
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<(DateTime En, double Ms, bool Error)>> datos = new();
    public DateTime Inicio { get; } = DateTime.UtcNow;

    public void Registrar(Guid tenant, double ms, bool error)
    {
        var q = datos.GetOrAdd(tenant, _ => new());
        q.Enqueue((DateTime.UtcNow, ms, error));
        var limite = DateTime.UtcNow.AddHours(-1);
        while (q.TryPeek(out var x) && (x.En < limite || q.Count > 20_000)) q.TryDequeue(out _);
    }

    public IEnumerable<(Guid Tenant, double Rpm, double P95, double PctErrores, int Total)> Resumen()
    {
        var limite = DateTime.UtcNow.AddHours(-1);
        var minutos = Math.Max(1, Math.Min(60, (DateTime.UtcNow - Inicio).TotalMinutes));
        foreach (var (t, q) in datos)
        {
            var l = q.Where(x => x.En >= limite).ToList();
            if (l.Count == 0) continue;
            var ord = l.Select(x => x.Ms).Order().ToList();
            var p95 = ord[(int)Math.Min(ord.Count - 1, Math.Ceiling(ord.Count * 0.95) - 1)];
            yield return (t, Math.Round(l.Count / minutos, 1), Math.Round(p95), Math.Round(l.Count(x => x.Error) * 100.0 / l.Count, 2), l.Count);
        }
    }

    /// <summary>Middleware: mide cada request de /api que tenga tenant.</summary>
    public static async Task Medir(HttpContext ctx, Func<Task> siguiente)
    {
        var sw = Stopwatch.StartNew();
        try { await siguiente(); }
        finally
        {
            if (ctx.Request.Path.StartsWithSegments("/api") && ctx.RequestServices.GetService<ContextoTenant>()?.Id is { } t)
                ctx.RequestServices.GetRequiredService<MetricasTenants>().Registrar(t, sw.Elapsed.TotalMilliseconds, ctx.Response.StatusCode >= 500);
        }
    }
}

public record PasoAprovisionamiento(string Clave, string Titulo, string Estado, string? Error);
public record TrabajoAprovisionamiento(Guid Id, string Estado, int Progreso, List<PasoAprovisionamiento> Pasos, Guid? TenantId);

/// <summary>Trabajos de alta de tenants (P-SA.3): pasos con su estado, consultados por la consola cada 2 segundos.</summary>
public class TrabajosAprovisionamiento
{
    private readonly ConcurrentDictionary<Guid, TrabajoAprovisionamiento> trabajos = new();
    public static readonly (string Clave, string Titulo)[] Pasos =
        [("catalog", "Registro en el catálogo y subdominio"), ("database", "Base de datos y datos iniciales"), ("profiles", "Perfiles y catálogos por defecto"),
         ("branding", "Marca por defecto de Nexo RH"), ("invitation", "Invitación al administrador inicial")];

    public TrabajoAprovisionamiento Nuevo() => Nuevo(Pasos);

    public TrabajoAprovisionamiento Nuevo((string Clave, string Titulo)[] pasos)
    {
        var t = new TrabajoAprovisionamiento(Guid.NewGuid(), "running", 0, pasos.Select(p => new PasoAprovisionamiento(p.Clave, p.Titulo, "pending", null)).ToList(), null);
        trabajos[t.Id] = t;
        return t;
    }

    public TrabajoAprovisionamiento? Ver(Guid id) => trabajos.TryGetValue(id, out var t) ? t : null;

    public void Paso(Guid id, string clave, string estado, string? error = null, Guid? tenantId = null)
    {
        trabajos.AddOrUpdate(id, _ => throw new InvalidOperationException(), (_, t) =>
        {
            var pasos = t.Pasos.Select(p => p.Clave == clave ? p with { Estado = estado, Error = error } : p).ToList();
            var hechos = pasos.Count(p => p.Estado == "succeeded");
            var global = pasos.Any(p => p.Estado == "failed") ? "failed" : hechos == pasos.Count ? "succeeded" : "running";
            return t with { Pasos = pasos, Progreso = hechos * 100 / pasos.Count, Estado = global, TenantId = tenantId ?? t.TenantId };
        });
    }
}
