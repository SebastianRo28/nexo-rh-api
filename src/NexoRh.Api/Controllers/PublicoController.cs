using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record EntradaTenant(string Slug, string Nombre, string Estado, string Titulo, string Mensaje, string? Ayuda, string? PrivacidadUrl,
    bool PoweredBy, MetodosLogin Metodos);
public record MetodosLogin(bool Documento, bool Google, bool Microsoft, bool Saml);
/// <summary>tipo: tenant (login de la empresa), global (app.{dominio}), consola (admin.{dominio}) o no_encontrado.</summary>
public record Entrada(string Tipo, EntradaTenant? Tenant);
public record CuentaEncontrada(string Nombre, string Slug, string Enlace);
public record CorreoPeticion(string Correo);
public record CuentasPeticion(string Correo, string Password);

/// <summary>
/// Endpoints sin sesión y sin tenant obligatorio: qué muestra el login según el host, “¿No conoces tu enlace?” y el
/// selector de cuenta de app.{dominio} (pantalla P-MT.0).
/// </summary>
[ApiController]
[Route("api/publico")]
[AllowAnonymous]
public class PublicoController(ContextoTenant tenant, AjustesTenant ajustes, CatalogoDbContext catalogo,
    IServiceScopeFactory scopes, EnviadorCorreo correo, Enlaces enlaces, IOptions<OpcionesPlataforma> plataforma, Marcas marcas, Suscripciones suscripciones) : ControllerBase
{
    private async Task<bool> PermiteOcultar(Guid id) => (await suscripciones.DeAsync(id)).Funcion(FuncionesPlan.OcultarPoweredBy);

    /// <summary>
    /// Certificado automático de dominios propios: el proxy (p. ej. Caddy con on_demand_tls “ask”) pregunta aquí antes de
    /// emitir un certificado. 200 solo para dominios propios verificados de un tenant que no está eliminado.
    /// </summary>
    [HttpGet("tls")]
    public async Task<IActionResult> PermitirCertificado([FromQuery] string? domain)
    {
        var host = BuscadorTenants.Normalizar(domain ?? "");
        var ok = host.Length > 0 && await (from d in catalogo.Dominios.AsNoTracking()
                                           join t in catalogo.Tenants.AsNoTracking() on d.TenantId equals t.Id
                                           where d.Host == host && d.Tipo == "Personalizado" && d.Verificado && t.Estado != EstadosTenant.Eliminado
                                           select d.Id).AnyAsync();
        return ok ? Ok() : NotFound();
    }

    private const string MensajeEnlaces = "Si tu correo está registrado, te enviaremos el enlace de tu empresa.";

    private bool EsEntradaGlobal => BuscadorTenants.Normalizar(Request.Host.Value ?? "") == $"app.{plataforma.Value.Dominio.ToLowerInvariant()}";

    /// <summary>Qué pantalla de acceso corresponde al host del navegador, con la marca y los métodos del tenant.</summary>
    [HttpGet("entrada")]
    public async Task<Entrada> VerEntrada()
    {
        if (EsEntradaGlobal) return new Entrada("global", null);
        var host = BuscadorTenants.Normalizar(Request.Host.Value ?? "");
        if (host == $"admin.{plataforma.Value.Dominio.ToLowerInvariant()}") return new Entrada("consola", null);
        if (tenant.Id is not { } id) return new Entrada("no_encontrado", null);
        var a = await ajustes.DeAsync(id);
        var p = a.Acceso;
        var login = Marcas.Publica(await marcas.DeAsync(id), tenant.Nombre, await PermiteOcultar(id)).Login;
        return new Entrada("tenant", new EntradaTenant(tenant.Slug, tenant.Nombre, tenant.Estado, login.Titulo, login.Mensaje, login.Ayuda, a.PrivacidadUrl,
            login.MostrarPoweredBy, new MetodosLogin(p.LoginDocumento, p.Google, p.Microsoft, p.Saml)));
    }

    /// <summary>
    /// Marca publicada del tenant del host (tokens, logos, textos y fuente). Cacheable: el frontend la guarda con su
    /// versión y la aplica antes de mostrar la app. Sin tenant (app.{dominio}), la marca de Nexo RH.
    /// </summary>
    [HttpGet("marca")]
    public async Task<ActionResult<MarcaPublica>> VerMarca()
    {
        var v = tenant.Id is { } id ? await marcas.DeAsync(id) : new MarcaVigente(0, new MarcaDatos());
        var etag = $"\"{tenant.Id?.ToString("N") ?? "nexo"}-{v.Version}\"";
        if (Request.Headers.IfNoneMatch == etag) return StatusCode(StatusCodes.Status304NotModified);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, max-age=60";
        return Marcas.Publica(v, tenant.Resuelto ? tenant.Nombre : "Nexo RH", tenant.Id is { } tid && await PermiteOcultar(tid));
    }

    /// <summary>Logo o imagen de la marca del tenant del host. Inmutable: cada subida tiene un id nuevo.</summary>
    [HttpGet("marca/archivos/{archivoId:guid}")]
    public async Task<IActionResult> VerArchivo(Guid archivoId)
    {
        if (tenant.Id is not { } id) return NotFound();
        var a = await catalogo.ArchivosMarca.AsNoTracking().FirstOrDefaultAsync(x => x.Id == archivoId && x.TenantId == id);
        if (a is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        // Un SVG abierto directamente no ejecuta nada (además ya se limpió al subirlo).
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; img-src data:";
        return File(a.Datos, a.ContentType);
    }

    /// <summary>“¿No conoces tu enlace?”: envía por correo los enlaces de las empresas donde está registrado. La respuesta
    /// es siempre la misma, para no revelar qué cuentas existen.</summary>
    [HttpPost("enlaces")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> EnviarEnlaces(CorreoPeticion p, CancellationToken ct)
    {
        var c = (p.Correo ?? "").Trim().ToLowerInvariant();
        if (!c.Contains('@')) return Problem("Ingresa un correo válido.", statusCode: 400);
        var encontrados = await TenantsConCorreoAsync(c, ct);
        if (encontrados.Count > 0)
        {
            var items = new List<string>();
            foreach (var (t, _) in encontrados)
            {
                var url = await enlaces.DeTenantAsync(t, ct);
                items.Add($"<li style=\"margin:6px 0\"><b>{WebUtility.HtmlEncode(t.Nombre)}</b><br><a href=\"{WebUtility.HtmlEncode(url)}\">{WebUtility.HtmlEncode(url)}</a></li>");
            }
            await correo.EnviarAsync(new MensajeCorreo(c, "Los enlaces de tu empresa en Nexo RH", "Estos son tus enlaces de acceso",
                $"<p>Tu correo está registrado en {(encontrados.Count == 1 ? "esta empresa" : "estas empresas")}:</p><ul style=\"padding-left:18px\">{string.Join("", items)}</ul><p>Guarda el enlace en tus favoritos para entrar directamente.</p>",
                Enlace: await enlaces.DeTenantAsync(encontrados[0].Tenant, ct)), ct);
        }
        return Accepted(new { mensaje = MensajeEnlaces });
    }

    /// <summary>
    /// Selector de cuenta (“Elige a qué empresa entrar”): con correo y contraseña devuelve las empresas donde esa
    /// combinación es válida, cada una con un enlace de acceso de un solo uso (2 minutos).
    /// </summary>
    [HttpPost("cuentas")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<List<CuentaEncontrada>>> BuscarCuentas(CuentasPeticion p, CancellationToken ct)
    {
        if (!EsEntradaGlobal) return NotFound();
        var c = (p.Correo ?? "").Trim().ToLowerInvariant();
        if (c.Length == 0 || string.IsNullOrEmpty(p.Password)) return Problem("Ingresa tu correo y tu contraseña.", statusCode: 400);
        var r = new List<CuentaEncontrada>();
        foreach (var (t, usuarioId) in await TenantsConCorreoAsync(c, ct))
        {
            var codigo = await EnTenantAsync(t, async sp =>
            {
                var db = sp.GetRequiredService<NexoDbContext>();
                var u = await db.Usuarios.Buscar(usuarioId);
                if (u is null || !u.Activo || u.PendienteActivacion || u.BloqueadoHasta > DateTime.UtcNow) return null;
                if (!Passwords.Verificar(u, p.Password))
                {
                    var politica = (await sp.GetRequiredService<AjustesTenant>().DeAsync(t.Id, ct)).Acceso;
                    if (++u.IntentosFallidos >= Math.Max(3, politica.MaxIntentos)) { u.BloqueadoHasta = DateTime.UtcNow.AddMinutes(Math.Max(1, politica.BloqueoMinutos)); u.IntentosFallidos = 0; }
                    await db.SaveChangesAsync(ct);
                    return null;
                }
                return await sp.GetRequiredService<TokensAcceso>().CrearAsync(u, TiposToken.Acceso, TimeSpan.FromMinutes(2));
            });
            if (codigo is not null) r.Add(new CuentaEncontrada(t.Nombre, t.Slug, $"{await enlaces.DeTenantAsync(t, ct)}/acceso?codigo={Uri.EscapeDataString(codigo)}"));
        }
        return r.Count == 0 ? Problem("Correo o contraseña incorrectos.", statusCode: 401) : r.OrderBy(x => x.Nombre).ToList();
    }

    /// <summary>Tenants operativos donde hay un usuario activo con ese correo (cruza tenants a propósito: modo plataforma).</summary>
    private async Task<List<(Tenant Tenant, int UsuarioId)>> TenantsConCorreoAsync(string correoNormalizado, CancellationToken ct)
    {
        var tenants = await catalogo.Tenants.AsNoTracking()
            .Where(t => t.Estado != EstadosTenant.Suspendido && t.Estado != EstadosTenant.Cancelado && t.Estado != EstadosTenant.Eliminado)
            .ToListAsync(ct);
        var r = new List<(Tenant, int)>();
        // BD compartida: una sola consulta en modo plataforma.
        using (var scope = scopes.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ContextoTenant>().ActivarModoPlataforma();
            var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
            var compartidos = tenants.Where(t => t.CadenaConexion is null).Select(t => t.Id).ToList();
            var filas = await db.Usuarios.AsNoTracking().Where(u => u.Correo == correoNormalizado && u.Activo && compartidos.Contains(u.TenantId))
                .Select(u => new { u.TenantId, u.Id }).ToListAsync(ct);
            r.AddRange(filas.Select(f => (tenants.First(t => t.Id == f.TenantId), f.Id)));
        }
        // Tenants con BD dedicada: una consulta en cada una.
        foreach (var t in tenants.Where(t => t.CadenaConexion is not null))
        {
            var id = await EnTenantAsync(t, sp => sp.GetRequiredService<NexoDbContext>().Usuarios.AsNoTracking()
                .Where(u => u.Correo == correoNormalizado && u.Activo).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct));
            if (id is int x) r.Add((t, x));
        }
        return r;
    }

    private async Task<T> EnTenantAsync<T>(Tenant t, Func<IServiceProvider, Task<T>> f)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(t);
        return await f(scope.ServiceProvider);
    }
}
