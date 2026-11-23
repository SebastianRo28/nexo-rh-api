using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Auth;

/// <summary>Ajustes de acceso del tenant (catálogo), con caché de 1 minuto. Sin fila, los valores por defecto.</summary>
public class AjustesTenant(CatalogoDbContext catalogo, IMemoryCache cache)
{
    public async Task<TenantAjustes> DeAsync(Guid tenantId, CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync($"tenant-ajustes:{tenantId}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return await catalogo.Ajustes.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct) ?? new TenantAjustes { TenantId = tenantId };
        }))!;

    public void Olvidar(Guid tenantId) => cache.Remove($"tenant-ajustes:{tenantId}");

    /// <summary>La política exige doble factor a este rol.</summary>
    public static bool ExigeDobleFactor(PoliticaAcceso p, Rol rol) =>
        p.DobleFactor == ModosDobleFactor.Obligatorio || (p.DobleFactor == ModosDobleFactor.Administradores && rol == Rol.Administrador);
}

/// <summary>Tokens de un solo uso (activación, restablecer, segundo paso del login, acceso desde el selector).</summary>
public class TokensAcceso(NexoDbContext db)
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Crea el token y anula los anteriores del mismo tipo para ese usuario. Devuelve el valor a entregar.</summary>
    public async Task<string> CrearAsync(Usuario u, string tipo, TimeSpan vigencia)
    {
        var ahora = DateTime.UtcNow;
        foreach (var viejo in await db.TokensUsuario.Where(t => t.UsuarioId == u.Id && t.Tipo == tipo && t.Usado == null).ToListAsync())
            viejo.Usado = ahora;
        var valor = Base64Url(RandomNumberGenerator.GetBytes(32));
        db.TokensUsuario.Add(new TokenUsuario { UsuarioId = u.Id, Tipo = tipo, Hash = Hash(valor), Expira = ahora.Add(vigencia) });
        await db.SaveChangesAsync();
        return valor;
    }

    /// <summary>Usuario del token si está vigente y sin usar (no lo consume).</summary>
    public async Task<(TokenUsuario Token, Usuario Usuario)?> VerAsync(string? valor, string tipo)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length > 100) return null;
        var h = Hash(valor.Trim());
        var t = await db.TokensUsuario.FirstOrDefaultAsync(x => x.Hash == h && x.Tipo == tipo);
        if (t is null || t.Usado is not null || t.Expira <= DateTime.UtcNow) return null;
        var u = await db.Usuarios.Buscar(t.UsuarioId);
        return u is null || !u.Activo ? null : (t, u);
    }

    /// <summary>Como VerAsync, pero lo marca como usado (se guarda con el próximo SaveChanges).</summary>
    public async Task<Usuario?> ConsumirAsync(string? valor, string tipo)
    {
        var r = await VerAsync(valor, tipo);
        if (r is not { } x) return null;
        x.Token.Usado = DateTime.UtcNow;
        return x.Usuario;
    }

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Clave TOTP cifrada en la BD con Data Protection.</summary>
public class SecretosTotp(IDataProtectionProvider proveedor)
{
    private readonly IDataProtector p = proveedor.CreateProtector("NexoRh.Totp.v1");
    public string Cifrar(string clave) => p.Protect(clave);
    public string Descifrar(string cifrada) => p.Unprotect(cifrada);
}

/// <summary>Direcciones de la app: la del request y la de cada tenant (su subdominio o su dominio propio principal).</summary>
public class Enlaces(IHttpContextAccessor http, IOptions<OpcionesPlataforma> plataforma, CatalogoDbContext catalogo)
{
    private HttpRequest R => http.HttpContext!.Request;

    /// <summary>Origen del request, p. ej. https://grupoandino.nexorh.pe (o http://comercialandina.localhost:4200 en desarrollo).</summary>
    public string Actual => $"{R.Scheme}://{R.Host.Value}";

    public string Ruta(string ruta, string? token = null) => $"{Actual}{ruta}" + (token is null ? "" : $"?token={Uri.EscapeDataString(token)}");

    public async Task<string> DeTenantAsync(Tenant t, CancellationToken ct = default)
    {
        var propio = await catalogo.Dominios.AsNoTracking()
            .Where(d => d.TenantId == t.Id && d.Tipo == "Personalizado" && d.Verificado && d.Principal).Select(d => d.Host).FirstOrDefaultAsync(ct);
        var puerto = R.Host.Port is int p && p is not 80 and not 443 ? $":{p}" : "";
        return propio is not null ? $"{R.Scheme}://{propio}{puerto}" : $"{R.Scheme}://{t.Slug}.{plataforma.Value.Dominio}{puerto}";
    }
}

/// <summary>Invitación “Activa tu cuenta” (enlace de 72 horas para crear la contraseña).</summary>
public class Invitaciones(TokensAcceso tokens, Services.EnviadorCorreo correo, Enlaces enlaces, ContextoTenant tenant)
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(72);

    public async Task EnviarAsync(Usuario u)
    {
        static string E(string s) => System.Net.WebUtility.HtmlEncode(s);
        var token = await tokens.CrearAsync(u, TiposToken.Activacion, Vigencia);
        var nombre = u.Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? u.Nombre;
        await correo.EnviarAsync(new Services.MensajeCorreo(u.Correo, $"Activa tu cuenta de {tenant.Nombre}", "Activa tu cuenta",
            $"<p>Hola {E(nombre)}:</p><p>{E(tenant.Nombre)} te invitó a su plataforma de Recursos Humanos. Crea tu contraseña para empezar. El enlace vence en 72 horas.</p>",
            "Activar mi cuenta", enlaces.Ruta("/activar", token), "Si no esperabas esta invitación, puedes ignorar este correo."));
    }
}
