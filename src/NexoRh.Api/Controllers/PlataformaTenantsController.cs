using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record KpisPlataforma(int TenantsActivos, int TenantsTrial, int TenantsSuspendidos, int TenantsMorosos, int ColaboradoresFacturables, decimal Mrr, decimal ChurnPct, int AltasMes);
public record PuntoMrr(string Periodo, decimal Monto);
public record PlanConteo(string Plan, string Nombre, int Tenants);
public record TrialPorVencer(Guid TenantId, string Nombre, string Slug, string Plan, int Colaboradores, DateOnly TrialHasta, int Dias, string? Ejecutivo);
public record SolicitudModulo(int Id, Guid TenantId, string Tenant, string Modulo, string ModuloNombre, string Usuario, string? Mensaje, DateTime CreadaEn, string Estado);
public record DashboardPlataforma(KpisPlataforma Kpis, List<PuntoMrr> SerieMrr, List<PlanConteo> Planes, List<TrialPorVencer> TrialsPorVencer, List<SolicitudModulo> Solicitudes, decimal Morosidad, int TenantsMorososVencidos);

public record TenantFila(Guid Id, string Nombre, string Slug, string DominioPrincipal, string Plan, string PlanNombre, List<string> Modulos, string Estado,
    int Colaboradores, DateTime CreadoEn, DateOnly? TrialHasta, string? Ejecutivo, bool SoporteAutorizado, decimal Precio);
public record PaginaTenants(List<TenantFila> Items, int Total, Dictionary<string, int> PorEstado, List<string> Ejecutivos);

public record AdminInicialAlta(string Nombres, string Apellidos, string Correo, string? Cargo, string? Celular);
public record OpcionesAlta(bool DatosEjemplo, bool BdDedicada);
public record AltaTenant(string RazonSocial, string Ruc, string? Nombre, string? Industria, string? Ejecutivo, string? CorreoFacturacion, string Slug,
    string Plan, List<string>? Modulos, decimal PrecioPorColaborador, DateOnly? TrialHasta, AdminInicialAlta Admin, OpcionesAlta? Opciones);

public record ModuloTenant(string Codigo, string Nombre, string Condicion, decimal Precio, bool Activo, DateOnly? Desde, DateOnly? Hasta);
public record DominioDto(int Id, string Host, string Tipo, bool Verificado, bool Principal, List<RegistroDns> Registros);
public record RegistroDns(string Tipo, string Nombre, string Valor, int Ttl);
public record DetalleTenant(Guid Id, string Slug, string Nombre, string RazonSocial, string Ruc, string? Industria, string? Ejecutivo, string? CorreoFacturacion,
    string Estado, string? MotivoSuspension, string Plan, string PlanNombre, decimal PrecioPorColaborador, decimal? PrecioPactado, DateOnly? TrialHasta, DateTime CreadoEn,
    string DominioPrincipal, string BaseDatos, string VersionEsquema, DateTime? SoporteHasta, int Colaboradores, decimal Mrr, int UsuariosActivos30d, int Usuarios,
    decimal Saldo, List<ModuloTenant> Modulos, List<PlanInfo> Planes, DateTime? CanceladoEn, DateTime? EliminacionProgramada, DateTime? EliminadoEn,
    bool EnMantenimiento, List<string> Funciones);
public record CambioTenant(string? RazonSocial, string? Nombre, string? CorreoFacturacion, string? Ejecutivo, string? Industria, string? Plan, decimal? PrecioPactado, DateOnly? TrialHasta, bool? QuitarPrecioPactado);
public record SuspenderTenant(string Motivo, string? Nota, string Confirmacion);
public record CambioModulo(string Codigo, bool Activo, DateOnly? Desde);
public record CambioModulos(List<CambioModulo> Modulos);
public record UsoTenant(List<PuntoUso> Colaboradores, List<UsoLimite> Limites, int UsuariosActivos30d, int Correos30d, int SesionesSoporte);
public record PuntoUso(string Periodo, int Colaboradores);
public record AdminTenant(int Id, string Nombre, string Correo, bool Activo, string UltimoAcceso, bool DobleFactor, bool PendienteActivacion);
public record EventoAuditoria(DateTime En, string Actor, string Origen, string Accion, string? Detalle, string? Ip);
public record NotaInternaDto(int Id, string Autor, DateTime CreadaEn, string Texto);
public record NotaInternaNueva(string Texto);
public record DominioNuevo(string Host);
public record SoporteNuevo(Guid TenantId, string Motivo, string? Ticket, string Modo, string? Justificacion);
public record SoporteCreado(Guid Id, string Url, DateTime Expira, bool Autorizada);
public record CancelarTenant(string Motivo, string Confirmacion);
public record ConfirmarSlug(string Confirmacion);

/// <summary>
/// Consola del proveedor (admin.{dominio}): P-SA.1 Dashboard, P-SA.2 Tenants, P-SA.3 alta con aprovisionamiento,
/// P-SA.4 detalle del tenant y P-SA.5 sesiones de soporte. Lee el catálogo; entra a un tenant solo para lo puntual.
/// </summary>
[ApiController]
[Route("api/platform")]
[SoloConsola]
[Authorize(Policy = Politicas.Plataforma)]
public class PlataformaTenantsController(CatalogoDbContext catalogo, ConsultasPlataforma consultas, AuditoriaConsola auditoria, BuscadorTenants buscador,
    TrabajosAprovisionamiento trabajos, IServiceScopeFactory scopes, IOptions<OpcionesPlataforma> plataforma, CicloVidaTenants cicloVida, DominiosTenant dominios,
    Suscripciones suscripciones, MudanzaBaseDedicada mudanza) : ControllerBase
{
    private string Dominio => plataforma.Value.Dominio.ToLowerInvariant();
    private static readonly HashSet<string> SlugsExtra = ["ayuda"];

    // --- P-SA.1 Dashboard --------------------------------------------------------------------------------------------

    [HttpGet("dashboard")]
    public async Task<DashboardPlataforma> Dashboard()
    {
        var tenants = await catalogo.Tenants.AsNoTracking().Where(t => t.Estado != EstadosTenant.Eliminado).ToListAsync();
        var colab = await consultas.ColaboradoresAsync(tenants);
        var precios = await consultas.PreciosAsync(tenants);
        var pagan = tenants.Where(t => t.Estado is EstadosTenant.Activo or EstadosTenant.Moroso).ToList();
        var mrr = Math.Round(pagan.Sum(t => colab.GetValueOrDefault(t.Id) * precios[t.Id]), 2);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cancelados = await catalogo.AuditoriaPlataforma.CountAsync(a => a.Accion == "tenant.cancelled" && a.En >= inicioMes);
        var churn = pagan.Count + cancelados == 0 ? 0 : Math.Round(cancelados * 100m / (pagan.Count + cancelados), 1);

        var desde = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-11);
        var periodoDesde = $"{desde.Year:D4}-{desde.Month:D2}";
        var facturado = await catalogo.Facturas.AsNoTracking().Where(f => string.Compare(f.Periodo, periodoDesde) >= 0)
            .GroupBy(f => f.Periodo).Select(g => new { g.Key, Monto = g.Sum(f => f.Subtotal) }).ToListAsync();
        var serie = Enumerable.Range(0, 12).Select(i => desde.AddMonths(i)).Select(m => $"{m.Year:D4}-{m.Month:D2}")
            .Select(p => new PuntoMrr(p, facturado.FirstOrDefault(x => x.Key == p)?.Monto ?? 0)).ToList();
        serie[^1] = serie[^1] with { Monto = Math.Max(serie[^1].Monto, mrr) };

        var planes = await catalogo.Planes.AsNoTracking().OrderBy(p => p.Orden).ToListAsync();
        var trials = tenants.Where(t => t.Estado == EstadosTenant.Trial && t.TrialHasta is { } th && th >= hoy && th.DayNumber - hoy.DayNumber <= 7)
            .OrderBy(t => t.TrialHasta).Select(t => new TrialPorVencer(t.Id, t.Nombre, t.Slug, t.PlanCodigo, colab.GetValueOrDefault(t.Id), t.TrialHasta!.Value,
                t.TrialHasta!.Value.DayNumber - hoy.DayNumber, t.EjecutivoCuenta)).ToList();
        var vencidas = await catalogo.Facturas.AsNoTracking().Where(f => f.Estado != "Pagada" && f.Vence < hoy.AddDays(-30)).ToListAsync();
        return new DashboardPlataforma(
            new KpisPlataforma(tenants.Count(t => t.Estado == EstadosTenant.Activo), tenants.Count(t => t.Estado == EstadosTenant.Trial),
                tenants.Count(t => t.Estado == EstadosTenant.Suspendido), tenants.Count(t => t.Estado == EstadosTenant.Moroso),
                pagan.Sum(t => colab.GetValueOrDefault(t.Id)), mrr, churn, tenants.Count(t => t.CreadoEn >= inicioMes)),
            serie, planes.Select(p => new PlanConteo(p.Codigo, p.Nombre, pagan.Concat(tenants.Where(t => t.Estado == EstadosTenant.Trial)).Count(t => t.PlanCodigo == p.Codigo))).ToList(),
            trials, await Solicitudes(), vencidas.Sum(f => f.Subtotal + f.Igv), vencidas.Select(f => f.TenantId).Distinct().Count());
    }

    private async Task<List<SolicitudModulo>> Solicitudes()
    {
        var nombres = await catalogo.Tenants.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Nombre);
        var modulos = await catalogo.ModulosComerciales.AsNoTracking().ToDictionaryAsync(m => m.Codigo, m => m.Nombre);
        return (await catalogo.Solicitudes.AsNoTracking().Where(s => s.Estado == "Nueva").OrderByDescending(s => s.CreadaEn).Take(20).ToListAsync())
            .Select(s => new SolicitudModulo(s.Id, s.TenantId, nombres.GetValueOrDefault(s.TenantId) ?? "—", s.Modulo, modulos.GetValueOrDefault(s.Modulo) ?? s.Modulo,
                s.Usuario, s.Mensaje, s.CreadaEn, s.Estado)).ToList();
    }

    /// <summary>Aprobar una solicitud contrata el módulo desde hoy (o desde la fecha indicada en Plan y módulos).</summary>
    [HttpPost("solicitudes/{id:int}/aprobar")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> Aprobar(int id) => await Resolver(id, true);

    [HttpPost("solicitudes/{id:int}/rechazar")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> Rechazar(int id) => await Resolver(id, false);

    private async Task<IActionResult> Resolver(int id, bool aprobar)
    {
        var s = await catalogo.Solicitudes.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (s.Estado != "Nueva") return Problem("La solicitud ya fue atendida.", statusCode: 409);
        if (aprobar)
        {
            if (!await catalogo.ModulosComerciales.AnyAsync(m => m.Codigo == s.Modulo)) return Problem("Ese producto no es un módulo contratable.", statusCode: 422);
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!await catalogo.TenantModulos.AnyAsync(m => m.TenantId == s.TenantId && m.Codigo == s.Modulo && (m.ActivoHasta == null || m.ActivoHasta >= hoy)))
                catalogo.TenantModulos.Add(new TenantModulo { TenantId = s.TenantId, Codigo = s.Modulo, ActivoDesde = hoy });
        }
        s.Estado = aprobar ? "Aprobada" : "Rechazada";
        auditoria.Registrar(aprobar ? "module_request.approved" : "module_request.rejected", s.TenantId, s.Modulo);
        await catalogo.SaveChangesAsync();
        Olvidar(s.TenantId);
        return NoContent();
    }

    // --- P-SA.2 Tenants ----------------------------------------------------------------------------------------------

    [HttpGet("tenants")]
    public async Task<PaginaTenants> Tenants([FromQuery] string? q, [FromQuery] string? estado, [FromQuery] string? plan, [FromQuery] string? ejecutivo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? sort = null)
    {
        var todos = await catalogo.Tenants.AsNoTracking().Where(t => t.Estado != EstadosTenant.Eliminado).ToListAsync();
        var porEstado = EstadosTenant.Todos.Where(e => e != EstadosTenant.Eliminado).ToDictionary(e => e, e => todos.Count(t => t.Estado == e));
        var texto = (q ?? "").Trim().ToLowerInvariant();
        var filtrados = todos.Where(t => (estado is null or "" || t.Estado == estado) && (plan is null or "" || t.PlanCodigo == plan)
            && (ejecutivo is null or "" || t.EjecutivoCuenta == ejecutivo)
            && (texto.Length == 0 || t.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) || t.Slug.Contains(texto) || t.RazonSocial.Contains(texto, StringComparison.OrdinalIgnoreCase) || t.Ruc.Contains(texto))).ToList();
        var colab = await consultas.ColaboradoresAsync(filtrados);
        var precios = await consultas.PreciosAsync(filtrados);
        var mods = await ModulosVigentes(filtrados.Select(t => t.Id).ToList());
        var dominios = await catalogo.Dominios.AsNoTracking().Where(d => d.Principal && d.Verificado).ToListAsync();
        var soporte = await catalogo.Ajustes.AsNoTracking().Where(a => a.SoporteHasta > DateTime.UtcNow).Select(a => a.TenantId).ToListAsync();
        var planes = await catalogo.Planes.AsNoTracking().ToDictionaryAsync(p => p.Codigo, p => p.Nombre);
        var filas = filtrados.Select(t => new TenantFila(t.Id, t.Nombre, t.Slug,
            dominios.Where(d => d.TenantId == t.Id && d.Verificado).OrderByDescending(d => d.Principal).Select(d => d.Host).FirstOrDefault() ?? $"{t.Slug}.{Dominio}",
            t.PlanCodigo, planes.GetValueOrDefault(t.PlanCodigo) ?? t.PlanCodigo, mods.GetValueOrDefault(t.Id) ?? [], t.Estado, colab.GetValueOrDefault(t.Id), t.CreadoEn,
            t.TrialHasta, t.EjecutivoCuenta, soporte.Contains(t.Id), precios[t.Id]));
        filas = sort switch
        {
            "colaboradores" => filas.OrderByDescending(f => f.Colaboradores),
            "alta" => filas.OrderByDescending(f => f.CreadoEn),
            "trial" => filas.OrderBy(f => f.TrialHasta ?? DateOnly.MaxValue),
            _ => filas.OrderBy(f => f.Nombre),
        };
        var lista = filas.ToList();
        pageSize = Math.Clamp(pageSize, 1, 100);
        return new PaginaTenants(lista.Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToList(), lista.Count, porEstado,
            todos.Select(t => t.EjecutivoCuenta).OfType<string>().Distinct().Order().ToList());
    }

    private async Task<Dictionary<Guid, List<string>>> ModulosVigentes(List<Guid> ids)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var planes = await catalogo.Planes.AsNoTracking().ToDictionaryAsync(p => p.Codigo, p => Modulos.Lista(p.ModulosIncluidos));
        var tenants = await catalogo.Tenants.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.PlanCodigo }).ToListAsync();
        var filas = await catalogo.TenantModulos.AsNoTracking().Where(m => ids.Contains(m.TenantId) && m.ActivoDesde <= hoy && (m.ActivoHasta == null || m.ActivoHasta >= hoy)).ToListAsync();
        return tenants.ToDictionary(t => t.Id, t => (planes.GetValueOrDefault(t.PlanCodigo) ?? []).Concat(filas.Where(f => f.TenantId == t.Id).Select(f => f.Codigo)).Distinct().Order().ToList());
    }

    [HttpPost("tenants/{id:guid}/suspend")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> Suspender(Guid id, SuspenderTenant p)
    {
        var t = await catalogo.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (t.Estado is EstadosTenant.Suspendido or EstadosTenant.Cancelado or EstadosTenant.Eliminado) return Problem($"El tenant ya está {t.Estado.ToLowerInvariant()}.", statusCode: 409);
        if (string.IsNullOrWhiteSpace(p.Motivo)) return Problem("Indica el motivo de la suspensión.", statusCode: 400);
        if ((p.Confirmacion ?? "").Trim() != t.Slug) return Problem($"Escribe {t.Slug} para confirmar.", statusCode: 400);
        t.Estado = EstadosTenant.Suspendido;
        t.MotivoSuspension = p.Motivo.Trim()[..Math.Min(p.Motivo.Trim().Length, 300)];
        auditoria.Registrar("tenant.suspended", t.Id, $"{t.MotivoSuspension}{(string.IsNullOrWhiteSpace(p.Nota) ? "" : $" · {p.Nota.Trim()}")}");
        await catalogo.SaveChangesAsync();
        Olvidar(t.Id);
        return Ok(new { id = t.Id, estado = t.Estado });
    }

    [HttpPost("tenants/{id:guid}/reactivate")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> Reactivar(Guid id)
    {
        var t = await catalogo.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (t.Estado is not (EstadosTenant.Suspendido or EstadosTenant.Cancelado or EstadosTenant.Moroso)) return Problem(t.Estado == EstadosTenant.Eliminado ? "El tenant fue eliminado." : "El tenant ya está operativo.", statusCode: 409);
        var antes = t.Estado;
        t.CanceladoEn = null;
        // Si estaba en prueba gratuita y aún no vence, vuelve a Trial.
        t.Estado = t.TrialHasta > DateOnly.FromDateTime(DateTime.UtcNow) ? EstadosTenant.Trial : EstadosTenant.Activo;
        t.MotivoSuspension = null;
        auditoria.Registrar("tenant.reactivated", t.Id, $"Desde {antes}");
        await catalogo.SaveChangesAsync();
        Olvidar(t.Id);
        return Ok(new { id = t.Id, estado = t.Estado });
    }

    /// <summary>Cancelar: 30 días de solo lectura y exportación para el cliente; luego el ciclo de vida elimina sus datos.</summary>
    [HttpPost("tenants/{id:guid}/cancel")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> Cancelar(Guid id, CancelarTenant p)
    {
        var t = await catalogo.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (t.Estado is EstadosTenant.Cancelado or EstadosTenant.Eliminado) return Problem($"El tenant ya está {t.Estado.ToLowerInvariant()}.", statusCode: 409);
        if (string.IsNullOrWhiteSpace(p.Motivo)) return Problem("Indica el motivo de la cancelación.", statusCode: 400);
        if ((p.Confirmacion ?? "").Trim() != t.Slug) return Problem($"Escribe {t.Slug} para confirmar.", statusCode: 400);
        t.Estado = EstadosTenant.Cancelado;
        t.CanceladoEn = DateTime.UtcNow;
        t.MotivoSuspension = p.Motivo.Trim()[..Math.Min(p.Motivo.Trim().Length, 300)];
        auditoria.Registrar("tenant.cancelled", t.Id, $"{t.MotivoSuspension} · datos se eliminan el {t.CanceladoEn.Value.AddDays(CicloVidaTenants.DiasCancelacion):dd/MM/yyyy}");
        await catalogo.SaveChangesAsync();
        Olvidar(t.Id);
        return Ok(new { id = t.Id, estado = t.Estado, eliminacionProgramada = t.CanceladoEn.Value.AddDays(CicloVidaTenants.DiasCancelacion) });
    }

    /// <summary>Eliminación definitiva inmediata (solo de un tenant cancelado), con certificado.</summary>
    [HttpPost("tenants/{id:guid}/delete")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<ActionResult<CertificadoEliminacion>> Eliminar(Guid id, ConfirmarSlug p)
    {
        var t = await catalogo.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (t.Estado != EstadosTenant.Cancelado) return Problem("Solo se elimina un tenant cancelado.", statusCode: 409);
        if ((p.Confirmacion ?? "").Trim() != t.Slug) return Problem($"Escribe {t.Slug} para confirmar.", statusCode: 400);
        var cert = await cicloVida.EliminarAsync(t, User.Identity?.Name ?? "Consola", DateTime.UtcNow);
        Olvidar(t.Id);
        return cert;
    }

    [HttpGet("tenants/{id:guid}/certificado")]
    public async Task<IActionResult> Certificado(Guid id)
    {
        var json = await catalogo.Tenants.AsNoTracking().Where(t => t.Id == id).Select(t => t.CertificadoEliminacion).FirstOrDefaultAsync();
        return json is null ? NotFound() : Content(json, "application/json");
    }

    /// <summary>Registra el pago de una factura (hasta integrar los pagos) y revisa el estado del tenant.</summary>
    [HttpPost("tenants/{id:guid}/invoices/{numero}/pay")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> Pagar(Guid id, string numero)
    {
        var f = await catalogo.Facturas.FirstOrDefaultAsync(x => x.TenantId == id && x.Numero == numero);
        if (f is null) return NotFound();
        if (f.Estado == "Pagada") return Problem("La factura ya está pagada.", statusCode: 409);
        f.Estado = "Pagada";
        auditoria.Registrar("invoice.paid", id, $"{numero} · S/ {f.Subtotal + f.Igv:0.00}");
        await catalogo.SaveChangesAsync();
        var r = await cicloVida.EjecutarAsync(DateTime.UtcNow, id, User.Identity?.Name ?? "Consola");
        Olvidar(id);
        return Ok(r);
    }

    /// <summary>Corre el ciclo de vida ahora (para un tenant o para todos).</summary>
    [HttpPost("lifecycle/run")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<ActionResult<ResumenCicloVida>> EjecutarCicloVida([FromQuery] Guid? tenantId)
    {
        var r = await cicloVida.EjecutarAsync(DateTime.UtcNow, tenantId, User.Identity?.Name ?? "Consola");
        foreach (var id in tenantId is { } x ? [x] : catalogo.Tenants.AsNoTracking().Select(t => t.Id).ToList()) Olvidar(id);
        return r;
    }

    // --- P-SA.3 Alta -------------------------------------------------------------------------------------------------

    [HttpGet("slug-availability")]
    public async Task<IActionResult> Slug([FromQuery] string? slug)
    {
        var s = (slug ?? "").Trim().ToLowerInvariant();
        if (s.Length == 0) return Problem("Indica el slug.", statusCode: 400);
        return Ok(new { slug = s, status = await EstadoSlug(s) });
    }

    private async Task<string> EstadoSlug(string s) =>
        !Regex.IsMatch(s, "^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$") ? "invalid"
        : Reservados.Slugs.Contains(s) || SlugsExtra.Contains(s) ? "reserved"
        : await catalogo.Tenants.AnyAsync(t => t.Slug == s) ? "taken" : "available";

    /// <summary>Alta asíncrona (202): catálogo, BD y datos iniciales, marca por defecto e invitación al administrador inicial.</summary>
    [HttpPost("tenants")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> Crear(AltaTenant p)
    {
        var e = new Dictionary<string, string[]>();
        var razon = (p.RazonSocial ?? "").Trim();
        var ruc = (p.Ruc ?? "").Trim();
        var slug = (p.Slug ?? "").Trim().ToLowerInvariant();
        if (razon.Length is 0 or > 160) e["razonSocial"] = ["Ingresa la razón social."];
        if (!Regex.IsMatch(ruc, "^(10|20)\\d{9}$")) e["ruc"] = ["El RUC debe tener 11 dígitos y empezar con 10 o 20."];
        if (p.CorreoFacturacion is { Length: > 0 } cf && !MailAddress.TryCreate(cf.Trim(), out _)) e["correoFacturacion"] = ["Ingresa un correo válido."];
        var plan = await catalogo.Planes.AsNoTracking().FirstOrDefaultAsync(x => x.Codigo == p.Plan);
        if (plan is null) e["plan"] = ["Elige un plan."];
        var catalogoModulos = await catalogo.ModulosComerciales.AsNoTracking().Select(m => m.Codigo).ToListAsync();
        var incluidos = Modulos.Lista(plan?.ModulosIncluidos);
        var modulos = (p.Modulos ?? []).Distinct().Where(m => !incluidos.Contains(m)).ToList();
        if (modulos.Any(m => !catalogoModulos.Contains(m))) e["modulos"] = ["Hay un módulo que no existe."];
        if (modulos.Count + incluidos.Count == 0) e["modulos"] = ["Elige al menos un módulo."];
        if (p.PrecioPorColaborador <= 0) e["precioPorColaborador"] = ["El precio por colaborador debe ser mayor que cero."];
        if (p.TrialHasta is { } th && th <= DateOnly.FromDateTime(DateTime.UtcNow)) e["trialHasta"] = ["El fin del trial debe ser una fecha futura."];
        var a = p.Admin;
        if (a is null || string.IsNullOrWhiteSpace(a.Nombres) || string.IsNullOrWhiteSpace(a.Apellidos)) e["admin.nombres"] = ["Ingresa los nombres y apellidos del administrador."];
        if (a is null || !MailAddress.TryCreate((a.Correo ?? "").Trim(), out _)) e["admin.correo"] = ["Ingresa un correo válido."];
        if (e.Count > 0) return ValidationProblem(new ValidationProblemDetails(e));
        var bdDedicada = p.Opciones?.BdDedicada == true;
        if (bdDedicada && !Modulos.Lista(plan!.Funciones).Contains(FuncionesPlan.BdDedicada))
            return Problem("La base de datos dedicada solo está disponible en el plan Enterprise.", statusCode: 422);
        var estado = await EstadoSlug(slug);
        if (estado != "available")
            return Problem(estado == "taken" ? "Ese slug ya está en uso." : estado == "reserved" ? "Ese slug está reservado por la plataforma." : "El slug no es válido.", statusCode: estado == "taken" ? 409 : 400);

        var tenant = new Tenant
        {
            Slug = slug, Nombre = string.IsNullOrWhiteSpace(p.Nombre) ? razon : p.Nombre.Trim(), RazonSocial = razon, Ruc = ruc, Industria = p.Industria?.Trim(),
            EjecutivoCuenta = p.Ejecutivo?.Trim(), CorreoFacturacion = p.CorreoFacturacion?.Trim().ToLowerInvariant(), PlanCodigo = plan!.Codigo,
            PrecioPactado = p.PrecioPorColaborador == plan.PrecioPorColaborador ? null : p.PrecioPorColaborador,
            Estado = p.TrialHasta is null ? EstadosTenant.Activo : EstadosTenant.Trial, TrialHasta = p.TrialHasta,
        };
        var trabajo = trabajos.Nuevo();
        var actor = User.Identity?.Name ?? "Consola";
        var puerto = Request.Host.Port is int pt && pt is not 80 and not 443 ? $":{pt}" : "";
        var baseUrl = $"{Request.Scheme}://{slug}.{Dominio}{puerto}";
        var admin = new AdminInicialAlta(a!.Nombres.Trim(), a.Apellidos.Trim(), (a.Correo ?? "").Trim().ToLowerInvariant(), a.Cargo?.Trim(), a.Celular?.Trim());
        var datosEjemplo = p.Opciones?.DatosEjemplo == true;
        // El trabajo corre fuera del request (sin su HttpContext): el front consulta GET provisioning/{id} cada 2 s.
        using (ExecutionContext.SuppressFlow())
            _ = Task.Run(() => Aprovisionar(trabajo.Id, tenant, modulos, datosEjemplo, admin, baseUrl, actor, bdDedicada));
        return Accepted(new { provisioningJobId = trabajo.Id });
    }

    private async Task Aprovisionar(Guid trabajo, Tenant tenant, List<string> modulos, bool datosEjemplo, AdminInicialAlta admin, string baseUrl, string actor, bool bdDedicada = false)
    {
        var paso = "catalog";
        try
        {
            trabajos.Paso(trabajo, paso, "running");
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var t = await sp.GetRequiredService<AprovisionadorTenants>().AsegurarAsync(tenant, datosEjemplo, modulos: modulos, adminDesdeConfig: false, bdDedicada: bdDedicada);
            trabajos.Paso(trabajo, "catalog", "succeeded", tenantId: t.Id);
            trabajos.Paso(trabajo, "database", "succeeded");
            trabajos.Paso(trabajo, "profiles", "succeeded");
            // La marca por defecto de Nexo RH aplica sin fila propia (versión 0) hasta que el cliente publique la suya.
            trabajos.Paso(trabajo, "branding", "succeeded");
            paso = "invitation";
            trabajos.Paso(trabajo, paso, "running");
            await consultas.EnTenantAsync(t, async tsp =>
            {
                var db = tsp.GetRequiredService<NexoDbContext>();
                var perfil = await db.Perfiles.FirstAsync(x => x.Predefinido && x.Rol == Rol.Administrador);
                var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Correo == admin.Correo);
                if (u is null)
                {
                    u = new Usuario { Nombre = $"{admin.Nombres} {admin.Apellidos}", Correo = admin.Correo, Rol = Rol.Administrador, Perfil = perfil.Nombre, PerfilId = perfil.Id, PendienteActivacion = true };
                    Passwords.Asignar(u, Passwords.Temporal(), temporal: false);
                    db.Usuarios.Add(u);
                    await db.SaveChangesAsync();
                }
                var token = await tsp.GetRequiredService<TokensAcceso>().CrearAsync(u, TiposToken.Activacion, Invitaciones.Vigencia);
                await tsp.GetRequiredService<EnviadorCorreo>().EnviarAsync(new MensajeCorreo(u.Correo, $"Activa tu cuenta de {t.Nombre} en Nexo RH", "Bienvenido a Nexo RH",
                    $"<p>Hola {WebUtility.HtmlEncode(admin.Nombres)}:</p><p>Creamos la plataforma de Recursos Humanos de {WebUtility.HtmlEncode(t.Nombre)}. Eres su administrador: crea tu contraseña para configurar la empresa. El enlace vence en 72 horas.</p><p>Tu enlace de acceso: {WebUtility.HtmlEncode(baseUrl)}</p>",
                    "Activar mi cuenta", $"{baseUrl}/activar?token={Uri.EscapeDataString(token)}"));
                return 0;
            });
            trabajos.Paso(trabajo, paso, "succeeded");
            using var s2 = scopes.CreateScope();
            var cat = s2.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            cat.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = actor, Accion = "tenant.created", TenantId = t.Id,
                Detalle = $"{t.Nombre} · {t.Slug} · {t.PlanCodigo} · {string.Join(", ", modulos)} · admin {admin.Correo}{(datosEjemplo ? " · con datos de ejemplo" : "")}{(bdDedicada ? " · BD dedicada" : "")}" });
            await cat.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            trabajos.Paso(trabajo, paso, "failed", ex.Message);
        }
    }

    [HttpGet("provisioning/{id:guid}")]
    public ActionResult<TrabajoAprovisionamiento> Trabajo(Guid id) => trabajos.Ver(id) is { } t ? t : NotFound();

    // --- P-SA.4 Detalle ----------------------------------------------------------------------------------------------

    [HttpGet("tenants/{id:guid}")]
    public async Task<ActionResult<DetalleTenant>> Detalle(Guid id)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var planes = await catalogo.Planes.AsNoTracking().OrderBy(p => p.Orden).ToListAsync();
        var plan = planes.FirstOrDefault(p => p.Codigo == t.PlanCodigo);
        var colab = (await consultas.ColaboradoresAsync([t])).GetValueOrDefault(t.Id);
        var precio = (await consultas.PreciosAsync([t]))[t.Id];
        var ajustes = await catalogo.Ajustes.AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == id);
        var dominio = await catalogo.Dominios.AsNoTracking().Where(d => d.TenantId == id && d.Verificado).OrderByDescending(d => d.Principal).ThenByDescending(d => d.Tipo == "Personalizado").Select(d => d.Host).FirstOrDefaultAsync();
        var saldo = await catalogo.Facturas.AsNoTracking().Where(f => f.TenantId == id && f.Estado != "Pagada").SumAsync(f => (decimal?)(f.Subtotal + f.Igv)) ?? 0;
        var hace30 = DateTime.UtcNow.AddDays(-30);
        var (usuarios, activos, version) = t.Estado == EstadosTenant.Eliminado ? (0, 0, "—") : await consultas.EnTenantAsync(t, async sp =>
        {
            var db = sp.GetRequiredService<NexoDbContext>();
            var ultima = (await db.Database.GetAppliedMigrationsAsync()).LastOrDefault() ?? "—";
            return (await db.Usuarios.CountAsync(u => u.Activo), await db.Usuarios.CountAsync(u => u.Activo && u.UltimoAcceso >= hace30), ultima);
        });
        return new DetalleTenant(t.Id, t.Slug, t.Nombre, t.RazonSocial, t.Ruc, t.Industria, t.EjecutivoCuenta, t.CorreoFacturacion, t.Estado, t.MotivoSuspension,
            t.PlanCodigo, plan?.Nombre ?? t.PlanCodigo, precio, t.PrecioPactado, t.TrialHasta, t.CreadoEn, dominio ?? $"{t.Slug}.{Dominio}",
            t.CadenaConexion is null ? "Compartida" : "Dedicada", version, ajustes?.SoporteHasta > DateTime.UtcNow ? ajustes.SoporteHasta : null,
            colab, Math.Round(colab * precio, 2), activos, usuarios, saldo, await ModulosDe(t, plan),
            planes.Select(x => new PlanInfo(x.Codigo, x.Nombre, x.PrecioPorColaborador, x.LimiteEmpresas, x.LimiteAlmacenamientoGb, x.LimiteAdmins, x.LimiteApiPorMinuto)).ToList(),
            t.CanceladoEn, t.CanceladoEn?.AddDays(CicloVidaTenants.DiasCancelacion), t.EliminadoEn,
            t.EnMantenimiento, [.. (await suscripciones.DeAsync(t.Id)).Funciones.Order()]);
    }

    private async Task<List<ModuloTenant>> ModulosDe(Tenant t, Plan? plan)
    {
        var incluidos = Modulos.Lista(plan?.ModulosIncluidos);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var filas = await catalogo.TenantModulos.AsNoTracking().Where(m => m.TenantId == t.Id).ToListAsync();
        return (await catalogo.ModulosComerciales.AsNoTracking().OrderBy(m => m.Orden).ToListAsync()).Select(m =>
        {
            if (incluidos.Contains(m.Codigo)) return new ModuloTenant(m.Codigo, m.Nombre, $"Incluido en {plan!.Nombre}", 0, true, null, null);
            var f = filas.Where(x => x.Codigo == m.Codigo).OrderByDescending(x => x.ActivoDesde).FirstOrDefault();
            var activo = f is not null && (f.ActivoHasta == null || f.ActivoHasta >= hoy);
            return new ModuloTenant(m.Codigo, m.Nombre, "Adicional", f?.PrecioPactado ?? m.PrecioPorColaborador, activo, f?.ActivoDesde, f?.ActivoHasta);
        }).ToList();
    }

    [HttpPatch("tenants/{id:guid}")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<ActionResult<DetalleTenant>> Modificar(Guid id, CambioTenant p)
    {
        var t = await catalogo.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var cambios = new List<string>();
        if (p.RazonSocial is { } rs) { if (rs.Trim().Length is 0 or > 160) return Problem("La razón social no es válida.", statusCode: 400); t.RazonSocial = rs.Trim(); cambios.Add("razón social"); }
        if (p.Nombre is { } n) { if (n.Trim().Length is 0 or > 120) return Problem("El nombre comercial no es válido.", statusCode: 400); t.Nombre = n.Trim(); cambios.Add("nombre"); }
        if (p.CorreoFacturacion is { } cf) { if (cf.Length > 0 && !MailAddress.TryCreate(cf.Trim(), out _)) return Problem("El correo de facturación no es válido.", statusCode: 400); t.CorreoFacturacion = cf.Length == 0 ? null : cf.Trim().ToLowerInvariant(); cambios.Add("correo de facturación"); }
        if (p.Ejecutivo is { } ej) { t.EjecutivoCuenta = ej.Trim().Length == 0 ? null : ej.Trim(); cambios.Add("ejecutivo"); }
        if (p.Industria is { } ind) { t.Industria = ind.Trim().Length == 0 ? null : ind.Trim(); cambios.Add("industria"); }
        if (p.Plan is { } pl)
        {
            if (!await catalogo.Planes.AnyAsync(x => x.Codigo == pl)) return Problem("Ese plan no existe.", statusCode: 400);
            cambios.Add($"plan {t.PlanCodigo} → {pl}");
            t.PlanCodigo = pl;
        }
        if (p.QuitarPrecioPactado == true) { t.PrecioPactado = null; cambios.Add("precio del plan"); }
        else if (p.PrecioPactado is { } pp) { if (pp <= 0) return Problem("El precio debe ser mayor que cero.", statusCode: 400); t.PrecioPactado = pp; cambios.Add($"precio pactado {pp}"); }
        if (p.TrialHasta is { } th) { t.TrialHasta = th; if (t.Estado == EstadosTenant.Trial || th > DateOnly.FromDateTime(DateTime.UtcNow)) t.Estado = EstadosTenant.Trial; cambios.Add($"trial hasta {th:dd/MM/yyyy}"); }
        auditoria.Registrar("tenant.updated", t.Id, string.Join(" · ", cambios));
        await catalogo.SaveChangesAsync();
        Olvidar(t.Id);
        return await Detalle(id);
    }

    /// <summary>Módulos adicionales con fecha de efecto. Apagar un módulo oculta sus pantallas al tenant; no borra datos.</summary>
    [HttpPut("tenants/{id:guid}/modules")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<ActionResult<List<ModuloTenant>>> Modulos_(Guid id, CambioModulos p)
    {
        var t = await catalogo.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var plan = await catalogo.Planes.AsNoTracking().FirstAsync(x => x.Codigo == t.PlanCodigo);
        var incluidos = Modulos.Lista(plan.ModulosIncluidos);
        var catalogoModulos = await catalogo.ModulosComerciales.AsNoTracking().Select(m => m.Codigo).ToListAsync();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var resumen = new List<string>();
        foreach (var c in p.Modulos ?? [])
        {
            if (!catalogoModulos.Contains(c.Codigo)) return Problem($"El módulo {c.Codigo} no existe.", statusCode: 422);
            if (incluidos.Contains(c.Codigo)) continue;
            var desde = c.Desde ?? hoy;
            if (desde < inicioMes) return Problem("La vigencia no puede ser anterior al período facturado abierto.", statusCode: 400);
            var vigente = await catalogo.TenantModulos.Where(m => m.TenantId == id && m.Codigo == c.Codigo && (m.ActivoHasta == null || m.ActivoHasta >= hoy))
                .OrderByDescending(m => m.ActivoDesde).FirstOrDefaultAsync();
            if (c.Activo && vigente is null) { catalogo.TenantModulos.Add(new TenantModulo { TenantId = id, Codigo = c.Codigo, ActivoDesde = desde }); resumen.Add($"+{c.Codigo} desde {desde:dd/MM/yyyy}"); }
            else if (!c.Activo && vigente is not null)
            {
                if (vigente.ActivoDesde >= desde) catalogo.TenantModulos.Remove(vigente);
                else vigente.ActivoHasta = desde.AddDays(-1);
                resumen.Add($"-{c.Codigo} desde {desde:dd/MM/yyyy}");
            }
        }
        if (resumen.Count > 0) auditoria.Registrar("tenant.modules", id, string.Join(" · ", resumen));
        await catalogo.SaveChangesAsync();
        Olvidar(id);
        return await ModulosDe(t, plan);
    }

    [HttpGet("tenants/{id:guid}/usage")]
    public async Task<ActionResult<UsoTenant>> Uso(Guid id)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var historia = await catalogo.Usos.AsNoTracking().Where(u => u.TenantId == id).OrderBy(u => u.Periodo).Select(u => new PuntoUso(u.Periodo, u.Colaboradores)).ToListAsync();
        var facturadas = await catalogo.Facturas.AsNoTracking().Where(f => f.TenantId == id).Select(f => new PuntoUso(f.Periodo, f.Colaboradores)).ToListAsync();
        historia = historia.Concat(facturadas.Where(f => !historia.Any(h => h.Periodo == f.Periodo))).OrderBy(h => h.Periodo).ToList();
        if (t.Estado == EstadosTenant.Eliminado) return new UsoTenant(historia, [], 0, 0, 0);
        var hace30 = DateTime.UtcNow.AddDays(-30);
        var (limites, colab, activos) = await consultas.EnTenantAsync(t, async sp =>
        {
            var db = sp.GetRequiredService<NexoDbContext>();
            return (await sp.GetRequiredService<LimitesPlan>().UsoAsync(), await db.Colaboradores.CountAsync(), await db.Usuarios.CountAsync(u => u.Activo && u.UltimoAcceso >= hace30));
        });
        var actual = $"{DateTime.UtcNow.Year:D4}-{DateTime.UtcNow.Month:D2}";
        if (!historia.Any(h => h.Periodo == actual)) historia.Add(new PuntoUso(actual, colab));
        return new UsoTenant(historia.TakeLast(12).ToList(), limites, activos,
            await catalogo.Correos.CountAsync(c => c.TenantId == id && c.CreadoEn >= hace30), await catalogo.SesionesSoporte.CountAsync(s => s.TenantId == id));
    }

    [HttpGet("tenants/{id:guid}/invoices")]
    public async Task<List<FacturaDto>> Facturas(Guid id) =>
        (await catalogo.Facturas.AsNoTracking().Where(f => f.TenantId == id).OrderByDescending(f => f.Periodo).ToListAsync())
            .Select(f => new FacturaDto(f.Numero, f.Periodo, f.Emitida.ToString("dd/MM/yyyy"), f.Vence.ToString("dd/MM/yyyy"), f.Colaboradores, f.Subtotal, f.Igv, f.Subtotal + f.Igv, f.Estado, f.SunatEstado)).ToList();

    [HttpGet("tenants/{id:guid}/admins")]
    public async Task<ActionResult<List<AdminTenant>>> Admins(Guid id)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        return await consultas.EnTenantAsync(t, async sp => (await sp.GetRequiredService<NexoDbContext>().Usuarios.AsNoTracking().Where(u => u.Rol == Rol.Administrador).OrderBy(u => u.Nombre).ToListAsync())
            .Select(u => new AdminTenant(u.Id, u.Nombre, u.Correo, u.Activo, u.UltimoAcceso is { } a ? Fechas.TextoHora(a) : "—", u.TotpActivo, u.PendienteActivacion)).ToList());
    }

    [HttpGet("tenants/{id:guid}/branding")]
    public async Task<ActionResult<MarcaPublica>> MarcaTenant(Guid id)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        return await consultas.EnTenantAsync(t, async sp => Marcas.Publica(await sp.GetRequiredService<Marcas>().DeAsync(t.Id), t.Nombre,
            (await sp.GetRequiredService<Suscripciones>().DeAsync(t.Id)).Funcion(FuncionesPlan.OcultarPoweredBy)));
    }

    /// <summary>Auditoría: la de la plataforma sobre el tenant, o el historial de cambios del propio tenant (lectura).</summary>
    [HttpGet("tenants/{id:guid}/audit")]
    public async Task<ActionResult<List<EventoAuditoria>>> Auditoria(Guid id, [FromQuery] string origen = "platform")
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (origen == "tenant")
            return await consultas.EnTenantAsync(t, async sp => (await sp.GetRequiredService<NexoDbContext>().Cambios.AsNoTracking().OrderByDescending(c => c.Id).Take(100).ToListAsync())
                .Select(c => new EventoAuditoria(c.Fecha, c.Usuario, "tenant", $"{c.Entidad} · {c.Accion}", string.Join(" → ", new[] { c.Anterior, c.Actual }.Where(x => !string.IsNullOrEmpty(x) && x != "—")), null)).ToList());
        return (await catalogo.AuditoriaPlataforma.AsNoTracking().Where(a => a.TenantId == id).OrderByDescending(a => a.Id).Take(100).ToListAsync())
            .Select(a => new EventoAuditoria(a.En, a.Actor, "platform", a.Accion, a.Detalle, a.Ip)).ToList();
    }

    [HttpGet("tenants/{id:guid}/notes")]
    public async Task<List<NotaInternaDto>> Notas(Guid id) =>
        await catalogo.Notas.AsNoTracking().Where(n => n.TenantId == id).OrderByDescending(n => n.Id).Select(n => new NotaInternaDto(n.Id, n.Autor, n.CreadaEn, n.Texto)).ToListAsync();

    [HttpPost("tenants/{id:guid}/notes")]
    public async Task<ActionResult<NotaInternaDto>> NuevaNota(Guid id, NotaInternaNueva p)
    {
        if (!await catalogo.Tenants.AnyAsync(t => t.Id == id)) return NotFound();
        var texto = (p.Texto ?? "").Trim();
        if (texto.Length is 0 or > 2000) return Problem("La nota es obligatoria (máximo 2,000 caracteres).", statusCode: 400);
        var n = new NotaTenant { TenantId = id, Autor = User.Identity?.Name ?? "—", Texto = texto };
        catalogo.Notas.Add(n);
        auditoria.Registrar("tenant.note", id, texto.Length > 80 ? texto[..80] + "…" : texto);
        await catalogo.SaveChangesAsync();
        return Created($"/api/platform/tenants/{id}/notes", new NotaInternaDto(n.Id, n.Autor, n.CreadaEn, n.Texto));
    }

    [HttpGet("tenants/{id:guid}/domains")]
    public async Task<ActionResult<List<DominioPropioDto>>> Dominios(Guid id)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        return (await catalogo.Dominios.AsNoTracking().Where(d => d.TenantId == id).OrderByDescending(d => d.Tipo == "Subdominio").ThenBy(d => d.Id).ToListAsync())
            .Select(d => VistasDominio.Dto(dominios, t, d)).ToList();
    }

    /// <summary>Dominio propio (plan Enterprise o flag CUSTOM_DOMAIN): queda pendiente hasta verificar el DNS.</summary>
    [HttpPost("tenants/{id:guid}/domains")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<ActionResult<DominioPropioDto>> NuevoDominio(Guid id, DominioNuevo p)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (!(await suscripciones.DeAsync(id)).Funcion(FuncionesPlan.DominioPropio)) return Problem("El dominio propio es del plan Enterprise (o activa el flag CUSTOM_DOMAIN).", statusCode: 422);
        var (host, error, estado) = await dominios.ValidarAsync(t, p.Host);
        if (host is null) return Problem(error, statusCode: estado);
        var d = await dominios.AgregarAsync(t, host);
        auditoria.Registrar("tenant.domain.added", id, host);
        await catalogo.SaveChangesAsync();
        return Created($"/api/platform/tenants/{id}/domains/{d.Id}", VistasDominio.Dto(dominios, t, d));
    }

    /// <summary>Verifica el TXT de propiedad y el CNAME (o las IP) hacia el subdominio del tenant.</summary>
    [HttpPost("tenants/{id:guid}/domains/{dominioId:int}/verify")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> VerificarDominio(Guid id, int dominioId)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        var d = await catalogo.Dominios.FirstOrDefaultAsync(x => x.Id == dominioId && x.TenantId == id && x.Tipo == "Personalizado");
        if (t is null || d is null) return NotFound();
        var antes = d.Verificado;
        var v = await dominios.VerificarAsync(t, d);
        if (!antes && d.Verificado) { auditoria.Registrar("tenant.domain.verified", id, d.Host); await catalogo.SaveChangesAsync(); }
        return Ok(new { status = d.Verificado ? "verified" : "pending", v.Propiedad, v.Ruteo, error = v.Error, certificateStatus = d.EstadoCertificado, checkedAt = DateTime.UtcNow,
            dominio = VistasDominio.Dto(dominios, t, d) });
    }

    [HttpPost("tenants/{id:guid}/domains/{dominioId:int}/primary")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> DominioPrincipal(Guid id, int dominioId)
    {
        if (await dominios.MarcarPrincipalAsync(id, dominioId) is { } error) return Problem(error, statusCode: 422);
        auditoria.Registrar("tenant.domain.primary", id, await catalogo.Dominios.Where(d => d.Id == dominioId).Select(d => d.Host).FirstAsync());
        await catalogo.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("tenants/{id:guid}/domains/{dominioId:int}")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> QuitarDominio(Guid id, int dominioId)
    {
        var host = await catalogo.Dominios.AsNoTracking().Where(d => d.Id == dominioId && d.TenantId == id).Select(d => d.Host).FirstOrDefaultAsync();
        if (host is null) return NotFound();
        if (await dominios.QuitarAsync(id, dominioId) is { } error) return Problem(error, statusCode: 422);
        auditoria.Registrar("tenant.domain.removed", id, host);
        await catalogo.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Remitente de correo desde el dominio del cliente (lo configura el cliente; aquí se ve y se revisa).</summary>
    [HttpGet("tenants/{id:guid}/email-domain")]
    public async Task<ActionResult<CorreoDominioDto?>> CorreoDominio(Guid id)
    {
        var c = await catalogo.CorreosDominio.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == id);
        if (c is null) return NoContent();
        return VistasDominio.Dto(dominios, c, (await suscripciones.DeAsync(id)).Funcion(FuncionesPlan.CorreoDominio));
    }

    [HttpPost("tenants/{id:guid}/email-domain/verify")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<ActionResult<CorreoDominioDto>> VerificarCorreoDominio(Guid id)
    {
        var c = await catalogo.CorreosDominio.FirstOrDefaultAsync(x => x.TenantId == id);
        if (c is null) return NotFound();
        await dominios.VerificarCorreoAsync(c);
        return VistasDominio.Dto(dominios, c, (await suscripciones.DeAsync(id)).Funcion(FuncionesPlan.CorreoDominio));
    }

    /// <summary>
    /// Mueve el tenant a su BD dedicada (Enterprise). Asíncrono como el alta: GET provisioning/{id} muestra los pasos. Durante
    /// la copia la app del tenant responde “en mantenimiento”.
    /// </summary>
    [HttpPost("tenants/{id:guid}/database/dedicate")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> Dedicar(Guid id, ConfirmarSlug p)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (p.Confirmacion?.Trim() != t.Slug) return Problem($"Escribe {t.Slug} para confirmar.", statusCode: 400);
        if (t.CadenaConexion is not null) return Problem("El tenant ya tiene su base de datos dedicada.", statusCode: 409);
        if (t.Estado is EstadosTenant.Cancelado or EstadosTenant.Eliminado) return Problem("El tenant está cancelado.", statusCode: 409);
        if (t.EnMantenimiento) return Problem("Ya hay una operación en curso para este tenant.", statusCode: 409);
        if (!(await suscripciones.DeAsync(id)).Funcion(FuncionesPlan.BdDedicada)) return Problem("La base de datos dedicada es del plan Enterprise (o activa el flag DEDICATED_DB).", statusCode: 422);
        var trabajo = trabajos.Nuevo(MudanzaBaseDedicada.Pasos);
        var actor = User.Identity?.Name ?? "Consola";
        auditoria.Registrar("tenant.database.dedicate.started", id, t.Slug);
        await catalogo.SaveChangesAsync();
        using (ExecutionContext.SuppressFlow())
            _ = Task.Run(() => mudanza.MoverAsync(trabajo.Id, id, actor));
        return Accepted(new { provisioningJobId = trabajo.Id });
    }


    // --- P-SA.5 Soporte ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Abre una sesión de soporte (60 min) en la app del tenant: con su autorización o con un ticket. Devuelve un enlace de
    /// un solo uso; se notifica a los administradores del tenant y todo queda en ambas auditorías.
    /// </summary>
    [HttpPost("support-sessions")]
    [Authorize(Policy = Politicas.PlataformaSoporte)]
    public async Task<ActionResult<SoporteCreado>> AbrirSoporte(SoporteNuevo p)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == p.TenantId);
        if (t is null) return NotFound();
        if (t.Estado is EstadosTenant.Cancelado or EstadosTenant.Eliminado) return Problem("El tenant está cancelado.", statusCode: 423);
        var autorizada = await catalogo.Ajustes.AnyAsync(a => a.TenantId == t.Id && a.SoporteHasta > DateTime.UtcNow);
        var e = new Dictionary<string, string[]>();
        var motivo = (p.Motivo ?? "").Trim();
        if (motivo.Length < 15) e["motivo"] = ["El motivo es obligatorio (mínimo 15 caracteres)."];
        if (!autorizada && string.IsNullOrWhiteSpace(p.Ticket)) e["ticket"] = ["El cliente no autorizó accesos de soporte: indica el N° de ticket."];
        if (p.Modo is not (ModosSoporte.Lectura or ModosSoporte.Escritura)) e["modo"] = ["Elige el modo."];
        if (p.Modo == ModosSoporte.Escritura && (p.Justificacion ?? "").Trim().Length < 10) e["justificacion"] = ["Justifica la escritura (mínimo 10 caracteres)."];
        if (e.Count > 0) return ValidationProblem(new ValidationProblemDetails(e));
        var uid = int.Parse(User.FindFirst("sub")!.Value);
        var abierta = await catalogo.SesionesSoporte.FirstOrDefaultAsync(s => s.UsuarioPlataformaId == uid && s.TenantId == t.Id && s.Fin == null && s.Expira > DateTime.UtcNow);
        if (abierta is not null)
            return Problem("Ya tienes una sesión de soporte activa en este tenant. Termínala antes de abrir otra.", statusCode: 409);
        var valor = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var s = new SesionSoporte
        {
            UsuarioPlataformaId = uid, Agente = User.Identity?.Name ?? "Soporte", TenantId = t.Id, Motivo = motivo, Ticket = p.Ticket?.Trim(), Modo = p.Modo,
            Justificacion = p.Justificacion?.Trim(), Autorizada = autorizada, TokenHash = TokensAcceso.Hash(valor), Expira = DateTime.UtcNow.AddMinutes(60),
        };
        catalogo.SesionesSoporte.Add(s);
        auditoria.Registrar("support.started", t.Id, $"{s.Modo} · {motivo}{(s.Ticket is null ? "" : $" · ticket {s.Ticket}")}{(autorizada ? "" : " · sin autorización del cliente")}");
        await catalogo.SaveChangesAsync();
        await NotificarSoporte(t, s, iniciada: true);
        var puerto = Request.Host.Port is int pt && pt is not 80 and not 443 ? $":{pt}" : "";
        return Created($"/api/platform/support-sessions/{s.Id}", new SoporteCreado(s.Id, $"{Request.Scheme}://{t.Slug}.{Dominio}{puerto}/soporte?token={Uri.EscapeDataString(valor)}", s.Expira, autorizada));
    }

    [HttpDelete("support-sessions/{id:guid}")]
    [Authorize(Policy = Politicas.PlataformaSoporte)]
    public async Task<IActionResult> CerrarSoporte(Guid id)
    {
        var s = await catalogo.SesionesSoporte.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null || s.Fin is not null) return NotFound();
        s.Fin = DateTime.UtcNow;
        auditoria.Registrar("support.ended", s.TenantId, s.Motivo);
        await catalogo.SaveChangesAsync();
        var t = await catalogo.Tenants.AsNoTracking().FirstAsync(x => x.Id == s.TenantId);
        await NotificarSoporte(t, s, iniciada: false);
        return NoContent();
    }

    /// <summary>Historial de cambios del tenant y correo a sus administradores (“Notificación de acceso de soporte”).</summary>
    private async Task NotificarSoporte(Tenant t, SesionSoporte s, bool iniciada) => await consultas.EnTenantAsync(t, async sp =>
    {
        var db = sp.GetRequiredService<NexoDbContext>();
        sp.GetRequiredService<Auditoria>().RegistrarTexto("Acceso de soporte", iniciada ? "Crear" : "Modificar", null, null, "sesión de soporte",
            null, $"{(iniciada ? "Iniciada" : "Terminada")} por {s.Agente} (Nexo RH) · {s.Modo} · {s.Motivo}{(s.Ticket is null ? "" : $" · ticket {s.Ticket}")}");
        await db.SaveChangesAsync();
        var correos = await db.Usuarios.AsNoTracking().Where(u => u.Rol == Rol.Administrador && u.Activo && !u.PendienteActivacion).Select(u => u.Correo).ToListAsync();
        var enviador = sp.GetRequiredService<EnviadorCorreo>();
        foreach (var c in correos)
            await enviador.EnviarAsync(new MensajeCorreo(c, iniciada ? $"Acceso de soporte a {t.Nombre}" : $"Terminó el acceso de soporte a {t.Nombre}",
                iniciada ? "Notificación de acceso de soporte" : "Acceso de soporte terminado",
                $"<p>{WebUtility.HtmlEncode(s.Agente)}, del equipo de Nexo RH, {(iniciada ? "inició" : "terminó")} una sesión de soporte en modo <b>{(s.Modo == ModosSoporte.Lectura ? "solo lectura" : "escritura")}</b>.</p><p>Motivo: {WebUtility.HtmlEncode(s.Motivo)}{(s.Ticket is null ? "" : $"<br>Ticket: {WebUtility.HtmlEncode(s.Ticket)}")}</p>{(s.Autorizada ? "" : "<p><b>Tu empresa no tenía autorizado el acceso de soporte</b>: se abrió con un ticket.</p>")}<p>Cada acción queda en el historial de cambios.</p>",
                Pie: "Puedes autorizar o revocar los accesos de soporte en Administración › Seguridad y acceso."));
        return 0;
    });

    private void Olvidar(Guid tenantId)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<Suscripciones>().Olvidar(tenantId);
        foreach (var host in catalogo.Dominios.AsNoTracking().Where(d => d.TenantId == tenantId).Select(d => d.Host).ToList()) buscador.Olvidar(host);
    }
}
