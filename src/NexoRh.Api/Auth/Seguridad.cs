using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Auth;

public class JwtOpciones
{
    public string Clave { get; set; } = "";
    public string Emisor { get; set; } = "nexo-rh-api";
    public string Audiencia { get; set; } = "nexo-rh-web";
    public int Minutos { get; set; } = 480;
}

public static class Claves
{
    public const string ColaboradorId = "colaborador_id";
    public const string Sello = "sello";
    /// <summary>Lo agrega el servidor al validar el token cuando la contraseña es temporal.</summary>
    public const string PasswordTemporal = "pwd_temporal";
}

/// <summary>Políticas por rol.</summary>
public static class Politicas
{
    public const string Administrador = "Administrador";
    public const string Gestion = "Gestion"; // Administrador o Supervisor
}

public class TokenService(IOptions<JwtOpciones> opciones)
{
    private readonly JwtOpciones o = opciones.Value;

    public (string token, DateTime expira) Crear(Usuario u)
    {
        var expira = DateTime.UtcNow.AddMinutes(o.Minutos);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, u.Nombre),
            new(ClaimTypes.Email, u.Correo),
            new(ClaimTypes.Role, u.Rol.ToString()),
            new(Claves.Sello, u.Sello),
        };
        if (u.ColaboradorId is int c) claims.Add(new Claim(Claves.ColaboradorId, c.ToString()));
        var cred = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Clave)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(o.Emisor, o.Audiencia, claims, expires: expira, signingCredentials: cred);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}

public static class UsuarioActual
{
    public static string Nombre(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.Name) ?? "—";
    public static Rol Rol(this ClaimsPrincipal u) => Enum.TryParse<Rol>(u.FindFirstValue(ClaimTypes.Role), out var r) ? r : Domain.Rol.Colaborador;
    public static int? ColaboradorId(this ClaimsPrincipal u) => int.TryParse(u.FindFirstValue(Claves.ColaboradorId), out var c) ? c : null;
    /// <summary>Texto del usuario en el historial: "Nombre · Rol".</summary>
    public static string Firma(this ClaimsPrincipal u) => $"{u.Nombre()} · {u.Rol()}";
}
