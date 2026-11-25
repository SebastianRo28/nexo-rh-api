using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record ContextoEmpresa(Guid Id, string Slug, string Nombre, string Estado, string Plan, string PlanNombre, DateOnly? TrialHasta, int? DiasTrial, int MarcaVersion,
    string? MotivoSuspension = null, DateTime? EliminacionProgramada = null);
public record ModuloInfo(string Codigo, string Nombre, string Descripcion, string[] Beneficios, bool Contratado);
public record BannerEstado(string Tipo, string Texto, string? Accion, string? Ruta);
public record ImplementacionInfo(bool Activo, int Avance, List<PasoImplementacion> Pasos);
/// <summary>GET /api/me/context: todo lo que la app necesita para mostrarse al usuario (módulos, banners, límites, checklist).</summary>
public record ContextoUsuario(UsuarioSesionDto Usuario, ContextoEmpresa Tenant, List<string> Modulos, List<string> Funciones, List<ModuloInfo> CatalogoModulos,
    List<UsoLimite> Limites, List<BannerEstado> Banners, ImplementacionInfo? Implementacion, List<AnuncioTenant> Anuncios);
public record UsuarioSesionDto(int Id, string Nombre, string Correo, string Rol);
/// <summary>Anuncio de la consola del proveedor (P-SA.8) para este tenant: banner o modal al ingresar.</summary>
public record AnuncioTenant(int Id, string Titulo, string Cuerpo, string Tipo, string Canal);

public record PlanInfo(string Codigo, string Nombre, decimal Precio, int LimiteEmpresas, int LimiteAlmacenamientoGb, int LimiteAdmins, int LimiteApiPorMinuto);
public record ModuloCuenta(string Codigo, string Nombre, string Descripcion, string[] Beneficios, decimal Precio, string Estado, bool SolicitudPendiente);
public record EstimadoCuenta(decimal PrecioPorColaborador, decimal Subtotal, decimal Igv, decimal Total);
public record FacturaDto(string Numero, string Periodo, string Emitida, string Vence, int Colaboradores, decimal Subtotal, decimal Igv, decimal Total, string Estado, string SunatEstado,
    string? PagadaEn = null, string? MedioPago = null, string? ProximoCobro = null, string? UltimoCobro = null)
{
    /// <summary>Con el pago (fecha y medio), el próximo cobro automático y el resultado del último intento.</summary>
    public static FacturaDto De(Factura f, IReadOnlyCollection<Cobro> cobros, bool conTarjeta)
    {
        var suyos = cobros.Where(c => c.FacturaId == f.Id).OrderBy(c => c.CreadoEn).ToList();
        var ultimo = suyos.LastOrDefault();
        return new(f.Numero, f.Periodo, f.Emitida.ToString("dd/MM/yyyy"), f.Vence.ToString("dd/MM/yyyy"), f.Colaboradores, f.Subtotal, f.Igv, f.Subtotal + f.Igv, f.Estado, f.SunatEstado,
            f.PagadaEn is { } p ? Fechas.TextoHora(p) : null, f.MedioPago,
            // Un intento que ya tocaba se hace en la próxima revisión (hoy).
            conTarjeta && CobrosFacturas.ProximoIntento(f, suyos) is { } d ? (d < DateOnly.FromDateTime(DateTime.UtcNow) ? DateOnly.FromDateTime(DateTime.UtcNow) : d).ToString("dd/MM/yyyy") : null,
            ultimo is null || ultimo.Estado == EstadosCobro.Aprobado ? null : $"{ultimo.Estado}{(ultimo.Mensaje is null ? "" : $": {ultimo.Mensaje}")}");
    }

    public static async Task<List<FacturaDto>> ListaAsync(CatalogoDbContext catalogo, Guid tenantId, int max)
    {
        var facturas = await catalogo.Facturas.AsNoTracking().Where(f => f.TenantId == tenantId).OrderByDescending(f => f.Periodo).Take(max).ToListAsync();
        var ids = facturas.Select(f => f.Id).ToList();
        var cobros = await catalogo.Cobros.AsNoTracking().Where(c => ids.Contains(c.FacturaId)).ToListAsync();
        var conTarjeta = await catalogo.MediosPago.AnyAsync(m => m.TenantId == tenantId && m.Activo);
        return facturas.Select(f => De(f, cobros, conTarjeta)).ToList();
    }
}
public record DatosFacturacion(string RazonSocial, string Ruc, string? DireccionFiscal, string? CorreoFacturacion);
/// <summary>⚙ Administración › Estado de cuenta (P-MT.4).</summary>
public record EstadoCuenta(PlanInfo Plan, string Estado, DateOnly? TrialHasta, List<ModuloCuenta> Modulos, int Colaboradores, EstimadoCuenta Estimado,
    List<FacturaDto> Facturas, DatosFacturacion Facturacion, List<UsoLimite> Limites, List<PlanInfo> Planes);
public record SolicitudPeticion(string Modulo, string? Mensaje);
public record ImplementacionPeticion(bool Activo);

[ApiController]
[Authorize]
public class CuentaController(NexoDbContext db, CatalogoDbContext catalogo, ContextoTenant tenant, Suscripciones suscripciones, LimitesPlan limites,
    Implementacion implementacion, AjustesTenant ajustes, Marcas marcas, Auditoria auditoria) : ControllerBase
{
    private Guid TenantId => tenant.Id!.Value;

    /// <summary>Contexto del usuario y del tenant: módulos contratados, catálogo para los candados, banners, límites y checklist.</summary>
    [HttpGet("api/me/context")]
    public async Task<ActionResult<ContextoUsuario>> Contexto()
    {
        // En una sesión de soporte no hay usuario del tenant: se ve como administrador.
        var soporte = User.FindFirst(Claves.SesionSoporte) is not null;
        Usuario? u;
        if (soporte) u = new Usuario { Id = 0, Nombre = User.Identity?.Name ?? "Soporte", Correo = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "", Rol = Rol.Administrador };
        else
        {
            if (!int.TryParse(User.FindFirst("sub")?.Value, out var id)) return Unauthorized();
            u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return Unauthorized();
        }
        var t = await catalogo.Tenants.AsNoTracking().FirstAsync(x => x.Id == TenantId);
        var s = await suscripciones.DeAsync(TenantId);
        var admin = u.Rol == Rol.Administrador;
        int? dias = t.Estado == EstadosTenant.Trial && t.TrialHasta is { } th ? Math.Max(0, th.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber) : null;

        var banners = new List<BannerEstado>();
        List<UsoLimite> uso = [];
        ImplementacionInfo? impl = null;
        if (admin)
        {
            const string cuenta = "/configuracion/estado-de-cuenta";
            if (dias is int d) banners.Add(new("trial", d == 1 ? "Prueba gratuita: te queda 1 día" : $"Prueba gratuita: te quedan {d} días", "Contratar", cuenta));
            if (t.Estado == EstadosTenant.Cancelado && t.CanceladoEn is { } cancelado)
                banners.Add(new("cancelado", $"La cuenta está cancelada: solo lectura hasta el {Fechas.TextoHora(cancelado.AddDays(CicloVidaTenants.DiasCancelacion))[..10]}. Descarga tus datos antes de esa fecha.", "Descargar mis datos", cuenta));
            if (t.Estado == EstadosTenant.Moroso) banners.Add(new("moroso", "Tienes facturas pendientes de pago. Regulariza para evitar la suspensión del servicio.", "Ver estado de cuenta", cuenta));
            uso = await limites.UsoAsync();
            foreach (var l in uso.Where(l => l.Estado != "ok"))
                banners.Add(new("limite", l.Estado == "bloqueado"
                    ? $"Llegaste al límite de tu plan en {l.Nombre.ToLowerInvariant()} ({l.Usado:0.###} de {l.Maximo:0.###} {l.Unidad})."
                    : $"Usaste el {l.Pct} % de {l.Nombre.ToLowerInvariant()} de tu plan.", "Ampliar plan", cuenta));
            if ((await ajustes.DeAsync(TenantId)).ModoImplementacion)
            {
                var pasos = await implementacion.PasosAsync();
                impl = new ImplementacionInfo(true, (int)Math.Round(pasos.Average(p => p.Avance)), pasos);
            }
        }
        var catalogoModulos = await catalogo.ModulosComerciales.AsNoTracking().OrderBy(m => m.Orden).ToListAsync();
        var ahora = DateTime.UtcNow;
        var anuncios = admin
            ? (await catalogo.Anuncios.AsNoTracking().Where(a => a.Publicado && (a.Desde == null || a.Desde <= ahora) && (a.Hasta == null || a.Hasta > ahora)).OrderByDescending(a => a.Id).ToListAsync())
                .Where(a => PlataformaConfigController.Aplica(a, t.PlanCodigo, s)).Select(a => new AnuncioTenant(a.Id, a.Titulo, a.Cuerpo, a.Tipo, a.Canal)).ToList()
            : [];
        return new ContextoUsuario(new UsuarioSesionDto(u.Id, u.Nombre, u.Correo, u.Rol.ToString()),
            new ContextoEmpresa(t.Id, t.Slug, t.Nombre, t.Estado, s.Plan.Codigo, s.Plan.Nombre, t.TrialHasta, dias, (await marcas.DeAsync(TenantId)).Version, t.MotivoSuspension, t.CanceladoEn?.AddDays(CicloVidaTenants.DiasCancelacion)),
            [.. s.Modulos.Order()], [.. s.Funciones.Order()],
            catalogoModulos.Select(m => new ModuloInfo(m.Codigo, m.Nombre, m.Descripcion, m.Beneficios.Split('|', StringSplitOptions.RemoveEmptyEntries), s.Tiene(m.Codigo))).ToList(),
            uso, banners, impl, anuncios);
    }

    /// <summary>Estado de cuenta: plan, módulos (contratados y disponibles), colaboradores facturables, estimado, facturas y límites.</summary>
    [HttpGet("api/cuenta")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<EstadoCuenta> Ver()
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstAsync(x => x.Id == TenantId);
        var s = await suscripciones.DeAsync(TenantId);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var contratados = await catalogo.TenantModulos.AsNoTracking()
            .Where(m => m.TenantId == TenantId && m.ActivoDesde <= hoy && (m.ActivoHasta == null || m.ActivoHasta >= hoy)).ToListAsync();
        var pendientes = await catalogo.Solicitudes.AsNoTracking().Where(x => x.TenantId == TenantId && x.Estado == "Nueva").Select(x => x.Modulo).ToListAsync();
        var incluidos = Modulos.Lista(s.Plan.ModulosIncluidos);
        var modulos = (await catalogo.ModulosComerciales.AsNoTracking().OrderBy(m => m.Orden).ToListAsync()).Select(m =>
        {
            var c = contratados.FirstOrDefault(x => x.Codigo == m.Codigo);
            var estado = incluidos.Contains(m.Codigo) ? "Incluido" : c is not null ? "Contratado" : "Disponible";
            return new ModuloCuenta(m.Codigo, m.Nombre, m.Descripcion, m.Beneficios.Split('|', StringSplitOptions.RemoveEmptyEntries),
                estado == "Incluido" ? 0 : c?.PrecioPactado ?? m.PrecioPorColaborador, estado, pendientes.Contains(m.Codigo));
        }).ToList();
        var colaboradores = await db.Colaboradores.CountAsync();
        var precio = s.Plan.PrecioPorColaborador + modulos.Where(m => m.Estado == "Contratado").Sum(m => m.Precio);
        var subtotal = colaboradores * precio;
        var igv = Math.Round(subtotal * Modulos.Igv, 2);
        var facturas = await FacturaDto.ListaAsync(catalogo, TenantId, 24);
        var planes = await catalogo.Planes.AsNoTracking().OrderBy(p => p.Orden).ToListAsync();
        return new EstadoCuenta(Info(s.Plan), t.Estado, t.TrialHasta, modulos, colaboradores, new EstimadoCuenta(precio, subtotal, igv, subtotal + igv),
            facturas,
            new DatosFacturacion(t.RazonSocial, t.Ruc, t.DireccionFiscal, t.CorreoFacturacion), await limites.UsoAsync(), planes.Select(Info).ToList());
    }

    /// <summary>Datos de facturación (razón social, RUC, dirección fiscal, correo para las facturas).</summary>
    [HttpPut("api/cuenta/facturacion")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<DatosFacturacion>> GuardarFacturacion(DatosFacturacion p)
    {
        var errores = new Dictionary<string, string[]>();
        var razon = (p.RazonSocial ?? "").Trim();
        var ruc = (p.Ruc ?? "").Trim();
        var correo = string.IsNullOrWhiteSpace(p.CorreoFacturacion) ? null : p.CorreoFacturacion.Trim().ToLowerInvariant();
        if (razon.Length is 0 or > 160) errores["razonSocial"] = ["Ingresa la razón social (máximo 160 caracteres)."];
        if (!Regex.IsMatch(ruc, "^(10|15|17|20)\\d{9}$")) errores["ruc"] = ["El RUC debe tener 11 dígitos y empezar con 10, 15, 17 o 20."];
        if (correo is not null && !MailAddress.TryCreate(correo, out _)) errores["correoFacturacion"] = ["Ingresa un correo válido."];
        if (p.DireccionFiscal?.Trim().Length > 200) errores["direccionFiscal"] = ["Máximo 200 caracteres."];
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores));
        var t = await catalogo.Tenants.FirstAsync(x => x.Id == TenantId);
        var antes = $"{t.RazonSocial} · {t.Ruc}";
        (t.RazonSocial, t.Ruc, t.DireccionFiscal, t.CorreoFacturacion) = (razon, ruc, string.IsNullOrWhiteSpace(p.DireccionFiscal) ? null : p.DireccionFiscal.Trim(), correo);
        await catalogo.SaveChangesAsync();
        auditoria.RegistrarTexto("Datos de facturación", "Modificar", null, null, "razón social · RUC", antes, $"{razon} · {ruc}");
        await db.SaveChangesAsync();
        return new DatosFacturacion(t.RazonSocial, t.Ruc, t.DireccionFiscal, t.CorreoFacturacion);
    }

    /// <summary>“Solicitar información” de un módulo (desde el modal del candado o el Estado de cuenta): crea un lead para el proveedor.</summary>
    [HttpPost("api/cuenta/solicitudes")]
    public async Task<IActionResult> Solicitar(SolicitudPeticion p)
    {
        var modulo = (p.Modulo ?? "").Trim();
        if (modulo.Length is 0 or > 80) return Problem("Indica el módulo.", statusCode: 400);
        var correo = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
        var desde = DateTime.UtcNow.AddDays(-1);
        // Una solicitud por persona y módulo al día (pulsar dos veces no crea dos leads).
        var reciente = await catalogo.Solicitudes.AnyAsync(x => x.TenantId == TenantId && x.Modulo == modulo && x.Correo == correo && x.CreadaEn > desde);
        if (!reciente)
        {
            catalogo.Solicitudes.Add(new SolicitudInformacion
            {
                TenantId = TenantId, Modulo = modulo, Usuario = User.Firma(), Correo = correo,
                Mensaje = string.IsNullOrWhiteSpace(p.Mensaje) ? null : p.Mensaje.Trim()[..Math.Min(p.Mensaje.Trim().Length, 500)],
            });
            await catalogo.SaveChangesAsync();
        }
        return Accepted(new { mensaje = "Recibimos tu solicitud. Un ejecutivo de Nexo RH te contactará en las próximas 24 horas hábiles." });
    }

    /// <summary>Modo implementación: muestra el checklist a los administradores.</summary>
    [HttpPut("api/cuenta/implementacion")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<ImplementacionInfo?>> Implementar(ImplementacionPeticion p)
    {
        var a = await catalogo.Ajustes.FirstOrDefaultAsync(x => x.TenantId == TenantId);
        if (a is null) catalogo.Ajustes.Add(a = new TenantAjustes { TenantId = TenantId });
        a.ModoImplementacion = p.Activo;
        a.ActualizadoEn = DateTime.UtcNow;
        await catalogo.SaveChangesAsync();
        ajustes.Olvidar(TenantId);
        if (!p.Activo) return Ok(null);
        var pasos = await implementacion.PasosAsync();
        return new ImplementacionInfo(true, (int)Math.Round(pasos.Average(x => x.Avance)), pasos);
    }

    private static PlanInfo Info(Plan p) => new(p.Codigo, p.Nombre, p.PrecioPorColaborador, p.LimiteEmpresas, p.LimiteAlmacenamientoGb, p.LimiteAdmins, p.LimiteApiPorMinuto);
}
