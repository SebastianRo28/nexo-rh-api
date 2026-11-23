using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace NexoRh.Api.Catalogo;

/// <summary>Configuración de la plataforma (sección Plataforma de appsettings).</summary>
public class OpcionesPlataforma
{
    /// <summary>Dominio base: {slug}.{Dominio} es la app de cada tenant (nexorh.pe en producción, localhost en desarrollo).</summary>
    public string Dominio { get; set; } = "localhost";
    /// <summary>Solo desarrollo, demo y pruebas: tenant para hosts que no son de ningún tenant (localhost:5281, el túnel…).</summary>
    public string? TenantPorDefecto { get; set; }
}

/// <summary>Subdominios que nunca son tenants (sección 3.2).</summary>
public static class Reservados
{
    public static readonly HashSet<string> Slugs = ["app", "admin", "api", "www", "mail", "soporte", "status", "demo", "static", "cdn"];
}

/// <summary>Busca el tenant de un host en el catálogo (con caché de 1 minuto).</summary>
public class BuscadorTenants(CatalogoDbContext catalogo, IMemoryCache cache, Microsoft.Extensions.Options.IOptions<OpcionesPlataforma> opciones)
{
    private readonly OpcionesPlataforma o = opciones.Value;

    public static string Normalizar(string host) => host.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();

    /// <summary>{slug}.{dominio} → por slug; cualquier otro host → por dominio propio verificado; si no, el de por defecto.</summary>
    public async Task<Tenant?> PorHostAsync(string hostCrudo, CancellationToken ct = default)
    {
        var host = Normalizar(hostCrudo);
        return await cache.GetOrCreateAsync($"tenant-host:{host}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            var sufijo = "." + o.Dominio.ToLowerInvariant();
            Tenant? t = null;
            if (host.EndsWith(sufijo) && host[..^sufijo.Length] is { Length: > 0 } slug && !slug.Contains('.') && !Reservados.Slugs.Contains(slug))
                t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug, ct);
            t ??= await (from d in catalogo.Dominios.AsNoTracking()
                         join x in catalogo.Tenants.AsNoTracking() on d.TenantId equals x.Id
                         where d.Host == host && d.Verificado
                         select x).FirstOrDefaultAsync(ct);
            // El de por defecto solo para hosts que no son subdominios de la plataforma (localhost, el túnel…):
            // un subdominio que no existe es "enlace no encontrado", nunca otra empresa.
            if (t is null && !host.EndsWith(sufijo) && o.TenantPorDefecto is { Length: > 0 } defecto)
                t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == defecto, ct);
            return t;
        });
    }

    /// <summary>app.{dominio} (entrada global) y admin.{dominio} (consola) nunca resuelven un tenant.</summary>
    public bool EsHostSinTenant(string hostCrudo)
    {
        var host = Normalizar(hostCrudo);
        var d = o.Dominio.ToLowerInvariant();
        return host == $"app.{d}" || host == $"admin.{d}" || host == $"api.{d}";
    }

    public void Olvidar(string host) => cache.Remove($"tenant-host:{Normalizar(host)}");
}

/// <summary>
/// Primer paso del pipeline: fija el tenant del request a partir del host. Los endpoints de negocio sin tenant
/// responden 404 (enlace no encontrado) y los de un tenant suspendido o cancelado, 423.
/// </summary>
public class ResolucionTenant(RequestDelegate siguiente)
{
    /// <summary>Rutas que funcionan sin tenant (salud, entrada pública, bandeja de desarrollo, documentación, frontend).</summary>
    private static bool SinTenant(PathString ruta) =>
        ruta.StartsWithSegments("/api/salud") || ruta.StartsWithSegments("/api/platform") || ruta.StartsWithSegments("/api/publico") || ruta.StartsWithSegments("/api/dev") || ruta.StartsWithSegments("/swagger") || !ruta.StartsWithSegments("/api");

    public async Task InvokeAsync(HttpContext ctx, BuscadorTenants buscador, ContextoTenant contexto, IMemoryCache cache,
        Microsoft.Extensions.Options.IOptions<OpcionesPlataforma> plataforma, DominiosTenant dominios)
    {
        var t = await buscador.PorHostAsync(ctx.Request.Host.Value ?? "", ctx.RequestAborted);
        if (t is not null) contexto.Usar(t);

        // Dominio propio: el proxy emite el certificado en el primer acceso por HTTPS (pregunta antes a /api/publico/tls).
        var host = BuscadorTenants.Normalizar(ctx.Request.Host.Value ?? "");
        if (t is not null && ctx.Request.IsHttps && !host.EndsWith("." + plataforma.Value.Dominio.ToLowerInvariant()) && !cache.TryGetValue($"certificado:{host}", out _))
        {
            await dominios.MarcarCertificadoAsync(host, ctx.RequestAborted);
            cache.Set($"certificado:{host}", true, TimeSpan.FromHours(1));
        }

        if (!SinTenant(ctx.Request.Path))
        {
            if (t is null)
            {
                await Responder(ctx, StatusCodes.Status404NotFound, "tenant_not_found", "No encontramos la empresa de este enlace.");
                return;
            }
            if (t.EnMantenimiento)
            {
                ctx.Response.Headers.RetryAfter = "60";
                await Responder(ctx, StatusCodes.Status503ServiceUnavailable, "tenant_maintenance", "Estamos mejorando la plataforma de tu empresa. Vuelve a intentarlo en unos minutos.");
                return;
            }
            // Suspendido: solo el acceso, el estado de cuenta y la exportación (los administradores ven “Cuenta suspendida”).
            // Cancelado: además, lectura de todo durante 30 días. Eliminado: nada.
            if (t.Estado is (EstadosTenant.Suspendido or EstadosTenant.Cancelado or EstadosTenant.Eliminado) && !Restringida(ctx.Request, t.Estado))
            {
                await Responder(ctx, StatusCodes.Status423Locked, t.Estado switch { EstadosTenant.Suspendido => "tenant_suspended", EstadosTenant.Eliminado => "tenant_deleted", _ => "tenant_cancelled" },
                    t.Estado == EstadosTenant.Cancelado && !HttpMethods.IsGet(ctx.Request.Method)
                        ? "La cuenta de tu empresa está cancelada: solo se puede consultar y descargar la información."
                        : t.Estado == EstadosTenant.Eliminado ? "La cuenta de esta empresa fue cerrada y sus datos eliminados."
                        : "La plataforma de tu empresa no está disponible. Contacta a tu área de RR.HH.");
                return;
            }
        }
        await siguiente(ctx);
    }

    /// <summary>Lo que sigue disponible en una cuenta suspendida o cancelada.</summary>
    public static bool Restringida(HttpRequest r, string estado)
    {
        if (estado == EstadosTenant.Eliminado) return false;
        var p = r.Path;
        if (p.StartsWithSegments("/api/auth") || p.StartsWithSegments("/api/me") || p.StartsWithSegments("/api/cuenta") || p.StartsWithSegments("/api/exportacion")) return true;
        return estado == EstadosTenant.Cancelado && HttpMethods.IsGet(r.Method);
    }

    private static Task Responder(HttpContext ctx, int estado, string codigo, string titulo)
    {
        ctx.Response.StatusCode = estado;
        return ctx.Response.WriteAsJsonAsync(new { title = titulo, status = estado, codigo });
    }
}
