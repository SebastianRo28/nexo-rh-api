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
    /// <summary>Audiencia de los tokens de la consola del proveedor.</summary>
    public string AudienciaConsola { get; set; } = "nexo-rh-consola";
}

public static class Claves
{
    public const string ColaboradorId = "colaborador_id";
    public const string Sello = "sello";
    /// <summary>Tenant del token: el servidor lo rechaza en el host de otro tenant.</summary>
    public const string Tenant = "tid";
    /// <summary>Lo agrega el servidor al validar el token cuando la contraseña es temporal.</summary>
    public const string PasswordTemporal = "pwd_temporal";
    /// <summary>Lo agrega el servidor cuando la política exige doble factor y la cuenta aún no lo configuró.</summary>
    public const string DobleFactorPendiente = "mfa_pendiente";
    /// <summary>Sesión de soporte de un usuario de plataforma (token de la app del tenant) y su modo (lectura | escritura).</summary>
    public const string SesionSoporte = "ss";
    public const string ModoSoporte = "ss_modo";
    /// <summary>Token de la consola del proveedor (audiencia propia, nunca sirve en la app de un tenant).</summary>
    public const string Alcance = "scope";
    public const string AlcancePlataforma = "plataforma";
}

/// <summary>Políticas por rol.</summary>
public static class Politicas
{
    /// <summary>Consola del proveedor: cualquier usuario de plataforma (lectura).</summary>
    public const string Plataforma = "Plataforma";
    public const string PlataformaAdmin = "PlataformaAdmin";
    public const string PlataformaSoporte = "PlataformaSoporte";
    public const string EsquemaPlataforma = "Plataforma";
    public const string Administrador = "Administrador";
    public const string Gestion = "Gestion"; // Administrador o Supervisor
}

public class TokenService(IOptions<JwtOpciones> opciones)
{
    private readonly JwtOpciones o = opciones.Value;

    /// <summary>Token de sesión; minutos = duración de la política del tenant (si no, Jwt:Minutos).</summary>
    public (string token, DateTime expira) Crear(Usuario u, int? minutos = null)
    {
        var expira = DateTime.UtcNow.AddMinutes(minutos is > 0 ? minutos.Value : o.Minutos);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, u.Nombre),
            new(ClaimTypes.Email, u.Correo),
            new(ClaimTypes.Role, u.Rol.ToString()),
            new(Claves.Sello, u.Sello),
            new(Claves.Tenant, u.TenantId.ToString()),
        };
        if (u.ColaboradorId is int c) claims.Add(new Claim(Claves.ColaboradorId, c.ToString()));
        return Firmar(claims, o.Audiencia, expira);
    }

    /// <summary>Token de una sesión de soporte en la app del tenant: rol Administrador, sin usuario del tenant detrás.</summary>
    public (string token, DateTime expira) CrearSoporte(Catalogo.SesionSoporte s, string correo) => Firmar(
    [
        new(JwtRegisteredClaimNames.Sub, $"soporte:{s.Id:N}"),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new(ClaimTypes.Name, $"{s.Agente} (soporte Nexo RH)"),
        new(ClaimTypes.Email, correo),
        new(ClaimTypes.Role, Rol.Administrador.ToString()),
        new(Claves.Tenant, s.TenantId.ToString()),
        new(Claves.SesionSoporte, s.Id.ToString()),
        new(Claves.ModoSoporte, s.Modo),
    ], o.Audiencia, s.Expira);

    /// <summary>Token de la consola del proveedor (8 horas).</summary>
    public (string token, DateTime expira) CrearPlataforma(Catalogo.UsuarioPlataforma u) => Firmar(
    [
        new(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new(ClaimTypes.Name, u.Nombre),
        new(ClaimTypes.Email, u.Correo),
        new(ClaimTypes.Role, u.Rol),
        new(Claves.Sello, u.Sello),
        new(Claves.Alcance, Claves.AlcancePlataforma),
    ], o.AudienciaConsola, DateTime.UtcNow.AddHours(8));

    private (string, DateTime) Firmar(IEnumerable<Claim> claims, string audiencia, DateTime expira)
    {
        var cred = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Clave)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(o.Emisor, audiencia, claims, expires: expira, signingCredentials: cred);
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
