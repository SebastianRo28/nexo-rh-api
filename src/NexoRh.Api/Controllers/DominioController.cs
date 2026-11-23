using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record DominioPropioDto(int Id, string Host, string Tipo, bool Verificado, bool Principal, string EstadoCertificado, DateTime? VerificadoEn,
    DateTime? CertificadoEn, DateTime? UltimaRevision, string? Error, List<RegistroDnsRequerido> Registros);
public record CorreoDominioDto(string Dominio, string Remitente, string Direccion, bool Verificado, bool SpfOk, bool DkimOk, bool DmarcOk, DateTime? VerificadoEn,
    DateTime? UltimaRevision, bool EnUso, List<RegistroDnsRequerido> Registros);
public record DominiosEdicion(bool PermiteDominio, bool PermiteCorreo, string Plan, string Subdominio, List<DominioPropioDto> Dominios, CorreoDominioDto? Correo,
    bool DnsSimulado, string RemitentePlataforma, int MaximoDominios);
public record DominioAlta(string Host);
public record CorreoAlta(string Dominio, string Remitente);

/// <summary>Vistas compartidas por la consola del proveedor y Administración › Dominio y correo.</summary>
public static class VistasDominio
{
    public static DominioPropioDto Dto(DominiosTenant s, Tenant t, TenantDominio d) => new(d.Id, d.Host, d.Tipo, d.Verificado, d.Principal, d.EstadoCertificado,
        d.VerificadoEn, d.CertificadoEn, d.UltimaRevision, d.ErrorVerificacion, s.Registros(t, d));

    public static CorreoDominioDto Dto(DominiosTenant s, TenantCorreoDominio c, bool permitido) => new(c.Dominio, c.Remitente, $"{c.Remitente}@{c.Dominio}", c.Verificado,
        c.SpfOk, c.DkimOk, c.DmarcOk, c.VerificadoEn, c.UltimaRevision, permitido && c.Verificado, s.RegistrosCorreo(c));
}

/// <summary>
/// ⚙ Administración › Dominio y correo (Enterprise): dominio propio con certificado automático (rrhh.tuempresa.pe) y
/// correos desde el dominio de la empresa (SPF, DKIM y DMARC). En otros planes se ve, con el aviso del plan.
/// </summary>
[ApiController]
[Route("api/dominio")]
[Authorize(Policy = Politicas.Administrador)]
public class DominioController(CatalogoDbContext catalogo, ContextoTenant tenant, Suscripciones suscripciones, DominiosTenant dominios, NexoDbContext db,
    Auditoria auditoria, IOptions<OpcionesCorreo> correo) : ControllerBase
{
    private Guid TenantId => tenant.Id!.Value;

    private async Task<Tenant> TenantAsync() => await catalogo.Tenants.AsNoTracking().FirstAsync(t => t.Id == TenantId);

    private ObjectResult SinFuncion(string texto) =>
        new(new { title = texto, status = 403, codigo = "feature_not_enabled" }) { StatusCode = 403 };

    [HttpGet]
    public async Task<DominiosEdicion> Ver()
    {
        var t = await TenantAsync();
        var s = await suscripciones.DeAsync(TenantId);
        var lista = await catalogo.Dominios.AsNoTracking().Where(d => d.TenantId == TenantId).OrderByDescending(d => d.Tipo == "Subdominio").ThenBy(d => d.Id).ToListAsync();
        var c = await catalogo.CorreosDominio.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == TenantId);
        var plataforma = correo.Value.Remitente;
        return new DominiosEdicion(s.Funcion(FuncionesPlan.DominioPropio), s.Funcion(FuncionesPlan.CorreoDominio), s.Plan.Nombre, dominios.Destino(t),
            lista.Select(d => VistasDominio.Dto(dominios, t, d)).ToList(), c is null ? null : VistasDominio.Dto(dominios, c, s.Funcion(FuncionesPlan.CorreoDominio)),
            dominios.DnsSimulado, plataforma[(plataforma.IndexOf('<') + 1)..].TrimEnd('>', ' '), DominiosTenant.MaximoDominios);
    }

    [HttpPost("dominios")]
    public async Task<ActionResult<DominioPropioDto>> Agregar(DominioAlta p)
    {
        if (!(await suscripciones.DeAsync(TenantId)).Funcion(FuncionesPlan.DominioPropio)) return SinFuncion("El dominio propio está disponible en el plan Enterprise.");
        var t = await TenantAsync();
        var (host, error, estado) = await dominios.ValidarAsync(t, p.Host);
        if (host is null) return Problem(error, statusCode: estado);
        var d = await dominios.AgregarAsync(t, host);
        auditoria.RegistrarTexto("Dominio propio", "Crear", null, null, "dominio", null, host);
        await db.SaveChangesAsync();
        return Created($"/api/dominio/dominios/{d.Id}", VistasDominio.Dto(dominios, t, d));
    }

    [HttpPost("dominios/{id:int}/verificar")]
    public async Task<ActionResult<DominioPropioDto>> Verificar(int id)
    {
        var t = await TenantAsync();
        var d = await catalogo.Dominios.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && x.Tipo == "Personalizado");
        if (d is null) return NotFound();
        var antes = d.Verificado;
        await dominios.VerificarAsync(t, d);
        if (!antes && d.Verificado)
        {
            auditoria.RegistrarTexto("Dominio propio", "Modificar", null, null, d.Host, "sin verificar", "verificado");
            await db.SaveChangesAsync();
        }
        return VistasDominio.Dto(dominios, t, d);
    }

    [HttpPost("dominios/{id:int}/principal")]
    public async Task<IActionResult> Principal(int id)
    {
        if (await dominios.MarcarPrincipalAsync(TenantId, id) is { } error) return Problem(error, statusCode: 422);
        var host = await catalogo.Dominios.AsNoTracking().Where(d => d.Id == id).Select(d => d.Host).FirstAsync();
        auditoria.RegistrarTexto("Dominio propio", "Modificar", null, null, "dominio principal", null, host);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("dominios/{id:int}")]
    public async Task<IActionResult> Quitar(int id)
    {
        var host = await catalogo.Dominios.AsNoTracking().Where(d => d.Id == id && d.TenantId == TenantId).Select(d => d.Host).FirstOrDefaultAsync();
        if (host is null) return NotFound();
        if (await dominios.QuitarAsync(TenantId, id) is { } error) return Problem(error, statusCode: 422);
        auditoria.RegistrarTexto("Dominio propio", "Eliminar", null, null, "dominio", host, null);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Configura el remitente (genera una clave DKIM nueva si cambia el dominio): hay que publicar los registros y verificar.</summary>
    [HttpPut("correo")]
    public async Task<ActionResult<CorreoDominioDto>> Correo(CorreoAlta p)
    {
        var s = await suscripciones.DeAsync(TenantId);
        if (!s.Funcion(FuncionesPlan.CorreoDominio)) return SinFuncion("Los correos desde tu dominio están disponibles en el plan Enterprise.");
        if (dominios.ValidarCorreo(p.Dominio, p.Remitente) is { } error) return Problem(error, statusCode: 400);
        var c = await dominios.ConfigurarCorreoAsync(TenantId, p.Dominio, p.Remitente);
        auditoria.RegistrarTexto("Correos desde tu dominio", "Modificar", null, null, "remitente", null, $"{c.Remitente}@{c.Dominio}");
        await db.SaveChangesAsync();
        return VistasDominio.Dto(dominios, c, true);
    }

    [HttpPost("correo/verificar")]
    public async Task<ActionResult<CorreoDominioDto>> VerificarCorreo()
    {
        var c = await catalogo.CorreosDominio.FirstOrDefaultAsync(x => x.TenantId == TenantId);
        if (c is null) return NotFound();
        var antes = c.Verificado;
        await dominios.VerificarCorreoAsync(c);
        if (!antes && c.Verificado)
        {
            auditoria.RegistrarTexto("Correos desde tu dominio", "Modificar", null, null, $"{c.Remitente}@{c.Dominio}", "sin verificar", "verificado (SPF, DKIM y DMARC)");
            await db.SaveChangesAsync();
        }
        return VistasDominio.Dto(dominios, c, (await suscripciones.DeAsync(TenantId)).Funcion(FuncionesPlan.CorreoDominio));
    }

    [HttpDelete("correo")]
    public async Task<IActionResult> QuitarCorreo()
    {
        var c = await catalogo.CorreosDominio.FirstOrDefaultAsync(x => x.TenantId == TenantId);
        if (c is null) return NotFound();
        catalogo.CorreosDominio.Remove(c);
        await catalogo.SaveChangesAsync();
        auditoria.RegistrarTexto("Correos desde tu dominio", "Eliminar", null, null, "remitente", $"{c.Remitente}@{c.Dominio}", null);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
