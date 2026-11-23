using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record MetodosPermitidos(bool SsoSocial, bool Saml);
public record SeguridadEdicion(PoliticaAcceso Acceso, string? PrivacidadUrl, DateTime? SoporteHasta, MetodosPermitidos Permitidos, string Plan);
public record SeguridadGuardar(PoliticaAcceso Acceso, string? PrivacidadUrl, bool PermitirSoporte, int? DiasSoporte);

/// <summary>
/// ⚙ Administración › Seguridad y acceso (P-MT.3): métodos de login, política de contraseña, doble factor, sesión,
/// política de privacidad y “Permitir acceso de soporte”.
/// </summary>
[ApiController]
[Route("api/seguridad")]
[Authorize(Policy = Politicas.Administrador)]
public class SeguridadController(CatalogoDbContext catalogo, ContextoTenant tenant, AjustesTenant ajustes, Suscripciones suscripciones, NexoDbContext db, Auditoria auditoria) : ControllerBase
{
    private Guid TenantId => tenant.Id!.Value;

    [HttpGet]
    public async Task<SeguridadEdicion> Ver()
    {
        var a = await ajustes.DeAsync(TenantId);
        var s = await suscripciones.DeAsync(TenantId);
        return new SeguridadEdicion(a.Acceso, a.PrivacidadUrl, a.SoporteHasta > DateTime.UtcNow ? a.SoporteHasta : null,
            new MetodosPermitidos(s.Funcion("SSO_SOCIAL"), s.Funcion("SAML")), s.Plan.Nombre);
    }

    [HttpPut]
    public async Task<ActionResult<SeguridadEdicion>> Guardar(SeguridadGuardar p)
    {
        if (p.Acceso is null) return Problem("Faltan los datos.", statusCode: 400);
        var x = p.Acceso;
        var s = await suscripciones.DeAsync(TenantId);
        var e = new Dictionary<string, string[]>();
        void Rango(string campo, int valor, int min, int max, string texto) { if (valor < min || valor > max) e[campo] = [$"{texto}: entre {min} y {max}."]; }
        Rango("acceso.passwordMinimo", x.PasswordMinimo, 8, 64, "Largo mínimo de la contraseña");
        Rango("acceso.maxIntentos", x.MaxIntentos, 3, 20, "Intentos antes del bloqueo");
        Rango("acceso.bloqueoMinutos", x.BloqueoMinutos, 1, 1440, "Minutos de bloqueo");
        Rango("acceso.sesionMinutos", x.SesionMinutos, 15, 1440, "Duración de la sesión en minutos");
        if (x.DobleFactor is not (ModosDobleFactor.Opcional or ModosDobleFactor.Administradores or ModosDobleFactor.Obligatorio)) e["acceso.dobleFactor"] = ["Elige una opción de doble factor."];
        if ((x.Google || x.Microsoft) && !s.Funcion("SSO_SOCIAL")) e["acceso.google"] = ["Google y Microsoft están disponibles desde el plan Pro."];
        if (x.Saml && !s.Funcion("SAML")) e["acceso.saml"] = ["SAML está disponible en el plan Enterprise."];
        var url = string.IsNullOrWhiteSpace(p.PrivacidadUrl) ? null : p.PrivacidadUrl.Trim();
        if (url is not null && (url.Length > 500 || !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https")) e["privacidadUrl"] = ["Usa una dirección https:// válida."];
        if (p.PermitirSoporte && p.DiasSoporte is not (>= 1 and <= 30)) e["diasSoporte"] = ["El acceso de soporte dura entre 1 y 30 días."];
        if (e.Count > 0) return ValidationProblem(new ValidationProblemDetails(e) { Title = "Revisa la configuración de seguridad." });

        var a = await catalogo.Ajustes.FirstOrDefaultAsync(z => z.TenantId == TenantId);
        if (a is null) catalogo.Ajustes.Add(a = new TenantAjustes { TenantId = TenantId });
        var antes = $"2FA {a.Acceso.DobleFactor} · mín. {a.Acceso.PasswordMinimo} · soporte {(a.SoporteHasta > DateTime.UtcNow ? "sí" : "no")}";
        a.Acceso = new PoliticaAcceso
        {
            LoginDocumento = x.LoginDocumento, PasswordMinimo = x.PasswordMinimo, MaxIntentos = x.MaxIntentos, BloqueoMinutos = x.BloqueoMinutos,
            DobleFactor = x.DobleFactor, SesionMinutos = x.SesionMinutos, Google = x.Google, Microsoft = x.Microsoft, Saml = x.Saml,
        };
        a.PrivacidadUrl = url;
        a.SoporteHasta = p.PermitirSoporte ? DateTime.UtcNow.AddDays(p.DiasSoporte!.Value) : null;
        a.ActualizadoEn = DateTime.UtcNow;
        await catalogo.SaveChangesAsync();
        ajustes.Olvidar(TenantId);
        auditoria.RegistrarTexto("Seguridad y acceso", "Modificar", null, null, "política de acceso", antes,
            $"2FA {x.DobleFactor} · mín. {x.PasswordMinimo} · soporte {(p.PermitirSoporte ? $"sí ({p.DiasSoporte} días)" : "no")}");
        await db.SaveChangesAsync();
        return await Ver();
    }
}
