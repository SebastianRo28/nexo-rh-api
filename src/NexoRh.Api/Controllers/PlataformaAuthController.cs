using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Controllers;

/// <summary>La consola solo responde en admin.{dominio}; en cualquier otro host, 404.</summary>
public class SoloConsolaAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext ctx)
    {
        var dominio = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<OpcionesPlataforma>>().Value.Dominio.ToLowerInvariant();
        if (BuscadorTenants.Normalizar(ctx.HttpContext.Request.Host.Value ?? "") != $"admin.{dominio}") ctx.Result = new NotFoundResult();
    }
    public void OnResourceExecuted(ResourceExecutedContext ctx) { }
}

public record UsuarioConsola(int Id, string Nombre, string Correo, string Rol);
/// <summary>Primer paso: siempre pide el segundo factor. Si la cuenta aún no lo tiene, devuelve la clave para enrolarlo.</summary>
public record LoginConsola(bool MfaRequerido, string MfaToken, bool Enrolar, string? Clave, string? Uri);
public record SesionConsola(string Token, DateTime Expira, UsuarioConsola Usuario);
public record LoginConsolaPeticion(string Correo, string Password);
public record MfaConsolaPeticion(string MfaToken, string Codigo);

/// <summary>P-SA.0 Login de plataforma: correo y contraseña del equipo Nexo RH + doble factor obligatorio.</summary>
[ApiController]
[Route("api/platform/auth")]
[SoloConsola]
public class PlataformaAuthController(CatalogoDbContext catalogo, TokenService tokens, SecretosTotp secretos, AuditoriaConsola auditoria) : ControllerBase
{
    private const int MaxIntentos = 5;
    private const string Invalido = "Correo o contraseña incorrectos.";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginConsola>> Login(LoginConsolaPeticion p)
    {
        var correo = (p.Correo ?? "").Trim().ToLowerInvariant();
        if (correo.Length == 0 || string.IsNullOrEmpty(p.Password)) return Problem("Ingresa tu correo y tu contraseña.", statusCode: 400);
        var u = await catalogo.UsuariosPlataforma.FirstOrDefaultAsync(x => x.Correo == correo);
        if (u is null || !u.Activo)
        {
            auditoria.Registrar("login.failed", detalle: correo, actor: correo);
            await catalogo.SaveChangesAsync();
            return Problem(Invalido, statusCode: 401);
        }
        if (u.BloqueadoHasta > DateTime.UtcNow) return Bloqueado(u);
        if (SemillaPlataforma.Hasher.VerifyHashedPassword(u, u.PasswordHash, p.Password) == PasswordVerificationResult.Failed)
        {
            if (++u.IntentosFallidos >= MaxIntentos) { u.BloqueadoHasta = DateTime.UtcNow.AddMinutes(15); u.IntentosFallidos = 0; }
            auditoria.Registrar("login.failed", detalle: correo, actor: correo);
            await catalogo.SaveChangesAsync();
            if (u.BloqueadoHasta > DateTime.UtcNow) return Bloqueado(u);
            var quedan = MaxIntentos - u.IntentosFallidos;
            return Problem($"{Invalido} Te quedan {quedan} {(quedan == 1 ? "intento" : "intentos")} antes del bloqueo temporal (15 min).", statusCode: 401);
        }
        u.IntentosFallidos = 0;
        string? clave = null;
        if (!u.TotpActivo)
        {
            // Primer ingreso: se enrola la app de autenticación antes de entrar (no hay login sin segundo factor).
            clave = Totp.NuevaClave();
            u.TotpSecreto = secretos.Cifrar(clave);
        }
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        catalogo.TokensPlataforma.Add(new TokenPlataforma { UsuarioId = u.Id, Hash = TokensAcceso.Hash(token), Expira = DateTime.UtcNow.AddMinutes(5) });
        await catalogo.SaveChangesAsync();
        return new LoginConsola(true, token, clave is not null, clave, clave is null ? null : Totp.Uri("Nexo RH · Consola", u.Correo, clave));
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SesionConsola>> Verificar(MfaConsolaPeticion p)
    {
        var hash = TokensAcceso.Hash((p.MfaToken ?? "").Trim());
        var t = await catalogo.TokensPlataforma.FirstOrDefaultAsync(x => x.Hash == hash);
        if (t is null || t.Usado is not null || t.Expira <= DateTime.UtcNow)
            return Problem("El paso de verificación expiró. Vuelve a iniciar sesión.", statusCode: 410);
        var u = await catalogo.UsuariosPlataforma.FirstAsync(x => x.Id == t.UsuarioId);
        if (!u.Activo || u.TotpSecreto is null) return Problem(Invalido, statusCode: 401);
        if (u.BloqueadoHasta > DateTime.UtcNow) return Bloqueado(u);
        if (!Totp.Verificar(secretos.Descifrar(u.TotpSecreto), p.Codigo))
        {
            if (++u.IntentosFallidos >= MaxIntentos) { u.BloqueadoHasta = DateTime.UtcNow.AddMinutes(15); u.IntentosFallidos = 0; }
            auditoria.Registrar("mfa.failed", actor: u.Correo);
            await catalogo.SaveChangesAsync();
            return u.BloqueadoHasta > DateTime.UtcNow ? Bloqueado(u) : Problem("Código inválido o expirado.", statusCode: 400);
        }
        t.Usado = DateTime.UtcNow;
        if (!u.TotpActivo) { u.TotpActivo = true; auditoria.Registrar("mfa.enrolled", actor: u.Correo); }
        u.IntentosFallidos = 0;
        u.UltimoAcceso = DateTime.UtcNow;
        auditoria.Registrar("login.success", actor: u.Correo);
        await catalogo.SaveChangesAsync();
        var (token, expira) = tokens.CrearPlataforma(u);
        return new SesionConsola(token, expira, new UsuarioConsola(u.Id, u.Nombre, u.Correo, u.Rol));
    }

    [HttpGet("me")]
    [Authorize(Policy = Politicas.Plataforma)]
    public async Task<ActionResult<UsuarioConsola>> Yo()
    {
        var u = int.TryParse(User.FindFirst("sub")?.Value, out var id) ? await catalogo.UsuariosPlataforma.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) : null;
        return u is null ? Unauthorized() : new UsuarioConsola(u.Id, u.Nombre, u.Correo, u.Rol);
    }

    private ObjectResult Bloqueado(UsuarioPlataforma u) =>
        Problem($"Cuenta bloqueada temporalmente. Intenta de nuevo en {Math.Ceiling((u.BloqueadoHasta!.Value - DateTime.UtcNow).TotalMinutes)} minutos.", statusCode: 423);

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
