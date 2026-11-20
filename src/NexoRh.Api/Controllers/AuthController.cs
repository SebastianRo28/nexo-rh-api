using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;

namespace NexoRh.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(NexoDbContext db, TokenService tokens) : ControllerBase
{
    private const int MaxIntentos = 5;
    private static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);
    private static readonly PasswordHasher<Usuario> Hasher = new();

    /// <summary>Inicia sesión con correo y contraseña; devuelve un token JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginRespuesta>> Login(LoginPeticion p)
    {
        var correo = (p.Correo ?? "").Trim().ToLowerInvariant();
        if (correo.Length == 0 || string.IsNullOrEmpty(p.Password))
            return Problem("Ingresa tu correo y tu contraseña.", statusCode: 400);
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Correo == correo);
        // Mismo mensaje para correo inexistente o contraseña errada, para no revelar qué cuentas existen.
        const string invalido = "Correo o contraseña incorrectos.";
        if (u is null || !u.Activo) return Problem(invalido, statusCode: 401);
        if (u.BloqueadoHasta > DateTime.UtcNow)
            return Problem($"Cuenta bloqueada por intentos fallidos. Intenta de nuevo en {Math.Ceiling((u.BloqueadoHasta.Value - DateTime.UtcNow).TotalMinutes)} minutos.", statusCode: 423);
        if (Hasher.VerifyHashedPassword(u, u.PasswordHash, p.Password) == PasswordVerificationResult.Failed)
        {
            u.IntentosFallidos++;
            if (u.IntentosFallidos >= MaxIntentos) { u.BloqueadoHasta = DateTime.UtcNow.Add(Bloqueo); u.IntentosFallidos = 0; }
            await db.SaveChangesAsync();
            return Problem(invalido, statusCode: 401);
        }
        u.IntentosFallidos = 0;
        u.BloqueadoHasta = null;
        u.UltimoAcceso = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var (token, expira) = tokens.Crear(u);
        return new LoginRespuesta(token, expira, Sesion(u));
    }

    /// <summary>Datos del usuario de la sesión actual.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioSesion>> Yo()
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)) return Unauthorized();
        var u = await db.Usuarios.FindAsync(id);
        return u is null || !u.Activo ? Unauthorized() : Sesion(u);
    }

    private static UsuarioSesion Sesion(Usuario u)
    {
        var partes = u.Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ini = string.Concat(partes.Take(2).Select(x => char.ToUpperInvariant(x[0])));
        return new UsuarioSesion(u.Id, u.Nombre, u.Correo, u.Rol.ToString(), u.Perfil, u.ColaboradorId, ini);
    }
}
