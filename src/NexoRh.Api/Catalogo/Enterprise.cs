using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexoRh.Api.Auth;

namespace NexoRh.Api.Catalogo;

/// <summary>Funciones finas de los planes (Plan.Funciones y flags por tenant).</summary>
public static class FuncionesPlan
{
    public const string DominioPropio = "CUSTOM_DOMAIN", CorreoDominio = "CUSTOM_EMAIL", OcultarPoweredBy = "HIDE_POWERED_BY", BdDedicada = "DEDICATED_DB";
}

// --- DNS ------------------------------------------------------------------------------------------------------------

/// <summary>Consultas DNS para verificar dominios propios y el remitente de correo (SPF, DKIM, DMARC).</summary>
public interface IResolutorDns
{
    /// <summary>true en desarrollo y pruebas: los registros se publican en memoria (POST /api/dev/dns) y no en un DNS real.</summary>
    bool Simulado { get; }
    Task<List<string>> TxtAsync(string nombre, CancellationToken ct = default);
    Task<List<string>> CnameAsync(string nombre, CancellationToken ct = default);
    Task<List<string>> DireccionesAsync(string nombre, CancellationToken ct = default);
}

/// <summary>
/// DNS real: consulta UDP (con EDNS0, y TCP si la respuesta viene truncada) a los servidores DNS del sistema o a los de
/// Dns:Servidores. Sin dependencias externas.
/// </summary>
public class ResolutorDns(IConfiguration config, ILogger<ResolutorDns> log) : IResolutorDns
{
    private const ushort Txt = 16, Cname = 5;
    public bool Simulado => false;

    public Task<List<string>> TxtAsync(string nombre, CancellationToken ct = default) => ConsultarAsync(nombre, Txt, ct);
    public Task<List<string>> CnameAsync(string nombre, CancellationToken ct = default) => ConsultarAsync(nombre, Cname, ct);

    public async Task<List<string>> DireccionesAsync(string nombre, CancellationToken ct = default)
    {
        try { return (await Dns.GetHostAddressesAsync(nombre, ct)).Select(a => a.ToString()).ToList(); }
        catch (SocketException) { return []; }
    }

    private IEnumerable<IPAddress> Servidores()
    {
        var propios = config.GetSection("Dns:Servidores").Get<string[]>()?.Select(IPAddress.Parse).ToList();
        if (propios is { Count: > 0 }) return propios;
        return NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().DnsAddresses)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork || (a.AddressFamily == AddressFamily.InterNetworkV6 && !a.IsIPv6SiteLocal))
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).Distinct().Take(3);
    }

    private async Task<List<string>> ConsultarAsync(string nombre, ushort tipo, CancellationToken ct)
    {
        var consulta = Consulta(nombre, tipo);
        foreach (var servidor in Servidores())
        {
            try
            {
                using var udp = new UdpClient(servidor.AddressFamily);
                using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
                tiempo.CancelAfter(TimeSpan.FromSeconds(3));
                await udp.SendAsync(consulta, new IPEndPoint(servidor, 53), tiempo.Token);
                var r = (await udp.ReceiveAsync(tiempo.Token)).Buffer;
                if ((r[2] & 0x02) != 0) r = await PorTcpAsync(servidor, consulta, ct);
                return Leer(r, tipo);
            }
            catch (Exception e) when (e is SocketException or OperationCanceledException or IndexOutOfRangeException && !ct.IsCancellationRequested)
            {
                log.LogDebug(e, "El servidor DNS {Servidor} no respondió la consulta de {Nombre}.", servidor, nombre);
            }
        }
        return [];
    }

    private static async Task<byte[]> PorTcpAsync(IPAddress servidor, byte[] consulta, CancellationToken ct)
    {
        using var tcp = new TcpClient(servidor.AddressFamily);
        using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tiempo.CancelAfter(TimeSpan.FromSeconds(5));
        await tcp.ConnectAsync(servidor, 53, tiempo.Token);
        var s = tcp.GetStream();
        await s.WriteAsync(new[] { (byte)(consulta.Length >> 8), (byte)consulta.Length }.Concat(consulta).ToArray(), tiempo.Token);
        var largo = new byte[2];
        await s.ReadExactlyAsync(largo, tiempo.Token);
        var r = new byte[(largo[0] << 8) | largo[1]];
        await s.ReadExactlyAsync(r, tiempo.Token);
        return r;
    }

    /// <summary>Pregunta DNS con recursión y un registro OPT (EDNS0) que acepta respuestas UDP de hasta 4096 bytes.</summary>
    public static byte[] Consulta(string nombre, ushort tipo)
    {
        var m = new List<byte>();
        var id = RandomNumberGenerator.GetInt32(ushort.MaxValue);
        m.AddRange([(byte)(id >> 8), (byte)id, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 1]);
        foreach (var etiqueta in nombre.TrimEnd('.').Split('.'))
        {
            var b = Encoding.ASCII.GetBytes(etiqueta);
            m.Add((byte)b.Length);
            m.AddRange(b);
        }
        m.Add(0);
        m.AddRange([(byte)(tipo >> 8), (byte)tipo, 0, 1]);
        m.AddRange([0, 0, 41, 0x10, 0x00, 0, 0, 0, 0, 0, 0]);
        return m.ToArray();
    }

    /// <summary>Respuestas del tipo pedido (TXT: las cadenas de cada registro unidas; CNAME: el nombre de destino).</summary>
    public static List<string> Leer(byte[] r, ushort tipo)
    {
        var preguntas = (r[4] << 8) | r[5];
        var respuestas = (r[6] << 8) | r[7];
        var i = 12;
        for (var q = 0; q < preguntas; q++) { Nombre(r, ref i); i += 4; }
        var lista = new List<string>();
        for (var a = 0; a < respuestas; a++)
        {
            Nombre(r, ref i);
            var t = (ushort)((r[i] << 8) | r[i + 1]);
            var largo = (r[i + 8] << 8) | r[i + 9];
            i += 10;
            if (t == tipo && tipo == Txt)
            {
                var sb = new StringBuilder();
                for (var j = i; j < i + largo;) { var n = r[j]; sb.Append(Encoding.UTF8.GetString(r, j + 1, n)); j += n + 1; }
                lista.Add(sb.ToString());
            }
            else if (t == tipo && tipo == Cname)
            {
                var j = i;
                lista.Add(Nombre(r, ref j).ToLowerInvariant());
            }
            i += largo;
        }
        return lista;
    }

    private static string Nombre(byte[] r, ref int i)
    {
        var partes = new List<string>();
        var j = i;
        var salto = false;
        for (var guardia = 0; guardia < 128; guardia++)
        {
            var n = r[j];
            if (n == 0) { j++; break; }
            if ((n & 0xC0) == 0xC0)
            {
                if (!salto) i = j + 2;
                salto = true;
                j = ((n & 0x3F) << 8) | r[j + 1];
                continue;
            }
            partes.Add(Encoding.ASCII.GetString(r, j + 1, n));
            j += n + 1;
        }
        if (!salto) i = j;
        return string.Join('.', partes);
    }
}

/// <summary>DNS de desarrollo y pruebas: registros publicados en memoria (no sale nada a internet).</summary>
public class ResolutorDnsSimulado : IResolutorDns
{
    private readonly ConcurrentDictionary<(string Tipo, string Nombre), List<string>> registros = new();
    public bool Simulado => true;

    public void Publicar(string tipo, string nombre, string valor) =>
        registros.AddOrUpdate((tipo.ToUpperInvariant(), Normal(nombre)), _ => [valor], (_, l) => { lock (l) { if (!l.Contains(valor)) l.Add(valor); } return l; });
    public void Quitar(string tipo, string nombre) => registros.TryRemove((tipo.ToUpperInvariant(), Normal(nombre)), out _);

    public Task<List<string>> TxtAsync(string nombre, CancellationToken ct = default) => Task.FromResult(De("TXT", nombre));
    public Task<List<string>> CnameAsync(string nombre, CancellationToken ct = default) => Task.FromResult(De("CNAME", nombre).Select(Normal).ToList());
    public Task<List<string>> DireccionesAsync(string nombre, CancellationToken ct = default) => Task.FromResult(De("A", nombre));

    private List<string> De(string tipo, string nombre) => registros.TryGetValue((tipo, Normal(nombre)), out var l) ? [.. l] : [];
    private static string Normal(string s) => s.Trim().TrimEnd('.').ToLowerInvariant();
}

// --- Dominios propios y remitente de correo ---------------------------------------------------------------------------

public record RegistroDnsRequerido(string Tipo, string Nombre, string Valor, int Ttl, bool? Ok);
public record VerificacionDominio(bool Verificado, bool Propiedad, bool Ruteo, string? Error);

/// <summary>
/// Dominio propio (Enterprise): se agrega sin verificar, el cliente publica un CNAME hacia su subdominio y un TXT de
/// propiedad, y al verificarse se habilita el certificado automático (el proxy pregunta a /api/publico/tls).
/// Remitente de correo: SPF, DKIM (clave RSA propia por tenant) y DMARC en el dominio del cliente.
/// </summary>
public class DominiosTenant(CatalogoDbContext catalogo, IResolutorDns dns, BuscadorTenants buscador, IOptions<OpcionesPlataforma> plataforma,
    IHostEnvironment entorno, IDataProtectionProvider proteccion, IConfiguration config)
{
    public const int MaximoDominios = 5;
    private string Dominio => plataforma.Value.Dominio.ToLowerInvariant();
    private IDataProtector Clave => proteccion.CreateProtector("NexoRh.Dkim.v1");
    public string SpfInclude => config["Correo:SpfInclude"] ?? $"spf.{(Dominio == "localhost" ? "nexorh.pe" : Dominio)}";
    public bool DnsSimulado => dns.Simulado;

    public string Destino(Tenant t) => $"{t.Slug}.{Dominio}";
    public static string Token(Tenant t, string host) => $"nexorh-verificacion={TokensAcceso.Hash($"{t.Id}:{host}")[..24].ToLowerInvariant()}";

    public List<RegistroDnsRequerido> Registros(Tenant t, TenantDominio d) => d.Tipo != "Personalizado" ? [] :
    [
        new("CNAME", d.Host, Destino(t), 3600, d.Verificado ? true : null),
        new("TXT", $"_nexorh.{d.Host}", Token(t, d.Host), 3600, d.Verificado ? true : null),
    ];

    /// <summary>Valida el host y que el tenant pueda agregar otro dominio. Devuelve el host normalizado o un error.</summary>
    public async Task<(string? Host, string? Error, int Estado)> ValidarAsync(Tenant t, string? crudo, CancellationToken ct = default)
    {
        var texto = (crudo ?? "").Trim();
        if (Uri.TryCreate(texto, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") texto = uri.Host;
        var host = BuscadorTenants.Normalizar(texto);
        if (!Regex.IsMatch(host, "^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z]{2,63}$"))
            return (null, "Ingresa un dominio válido, p. ej. rrhh.tuempresa.pe.", 400);
        // En desarrollo se permiten nombres *.*.localhost (el navegador los resuelve a 127.0.0.1) para probar sin DNS.
        var sufijo = "." + Dominio;
        var deLaPlataforma = host == Dominio || host.EndsWith(sufijo);
        if (deLaPlataforma && !(entorno.IsDevelopment() && Dominio == "localhost" && host[..^sufijo.Length].Contains('.')))
            return (null, $"Los subdominios de {Dominio} no se agregan como dominio propio.", 400);
        if (await catalogo.Dominios.AnyAsync(d => d.Host == host, ct)) return (null, "Ese dominio ya está registrado.", 409);
        if (await catalogo.Dominios.CountAsync(d => d.TenantId == t.Id && d.Tipo == "Personalizado", ct) >= MaximoDominios)
            return (null, $"Se permiten hasta {MaximoDominios} dominios propios.", 422);
        return (host, null, 200);
    }

    public async Task<TenantDominio> AgregarAsync(Tenant t, string host, CancellationToken ct = default)
    {
        var d = new TenantDominio { TenantId = t.Id, Host = host, Tipo = "Personalizado", Verificado = false, Principal = false, EstadoCertificado = EstadosCertificado.Pendiente };
        catalogo.Dominios.Add(d);
        await catalogo.SaveChangesAsync(ct);
        return d;
    }

    /// <summary>
    /// Propiedad: TXT _nexorh.{host} con el token del tenant. Ruteo: CNAME hacia {slug}.{dominio} (o, en un dominio raíz,
    /// las mismas IP). Al verificarse, el certificado pasa a “Emitiendo” hasta el primer acceso por HTTPS.
    /// </summary>
    public async Task<VerificacionDominio> VerificarAsync(Tenant t, TenantDominio d, CancellationToken ct = default)
    {
        var token = Token(t, d.Host);
        var propiedad = (await dns.TxtAsync($"_nexorh.{d.Host}", ct)).Any(x => x.Trim() == token);
        var destino = Destino(t);
        var cname = await dns.CnameAsync(d.Host, ct);
        var ruteo = cname.Contains(destino);
        if (!ruteo)
        {
            var nuestras = await dns.DireccionesAsync(destino, ct);
            var suyas = await dns.DireccionesAsync(d.Host, ct);
            ruteo = nuestras.Count > 0 && suyas.Count > 0 && suyas.Intersect(nuestras).Any();
        }
        var error = (propiedad, ruteo) switch
        {
            (false, false) => $"No encontramos el CNAME hacia {destino} ni el registro TXT de verificación.",
            (false, true) => $"Falta el registro TXT _nexorh.{d.Host}.",
            (true, false) => cname.Count > 0 ? $"El CNAME apunta a {cname[0]} y debe apuntar a {destino}." : $"Falta el CNAME hacia {destino}.",
            _ => null,
        };
        d.UltimaRevision = DateTime.UtcNow;
        d.ErrorVerificacion = error;
        if (error is null && !d.Verificado)
        {
            d.Verificado = true;
            d.VerificadoEn = DateTime.UtcNow;
            d.EstadoCertificado = EstadosCertificado.Emitiendo;
        }
        await catalogo.SaveChangesAsync(ct);
        buscador.Olvidar(d.Host);
        return new VerificacionDominio(d.Verificado, propiedad, ruteo, error);
    }

    /// <summary>El dominio principal se usa en los enlaces de los correos. Solo un dominio propio verificado.</summary>
    public async Task<string?> MarcarPrincipalAsync(Guid tenantId, int dominioId, CancellationToken ct = default)
    {
        var todos = await catalogo.Dominios.Where(d => d.TenantId == tenantId).ToListAsync(ct);
        var d = todos.FirstOrDefault(x => x.Id == dominioId);
        if (d is null) return "No existe ese dominio.";
        if (!d.Verificado) return "Verifica el dominio antes de hacerlo principal.";
        foreach (var x in todos) x.Principal = x.Id == d.Id;
        await catalogo.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> QuitarAsync(Guid tenantId, int dominioId, CancellationToken ct = default)
    {
        var todos = await catalogo.Dominios.Where(d => d.TenantId == tenantId).ToListAsync(ct);
        var d = todos.FirstOrDefault(x => x.Id == dominioId);
        if (d is null) return "No existe ese dominio.";
        if (d.Tipo != "Personalizado") return "El subdominio de la plataforma no se puede quitar.";
        catalogo.Dominios.Remove(d);
        if (d.Principal && todos.FirstOrDefault(x => x.Tipo != "Personalizado") is { } sub) sub.Principal = true;
        await catalogo.SaveChangesAsync(ct);
        buscador.Olvidar(d.Host);
        return null;
    }

    /// <summary>Primer request por HTTPS de un dominio propio verificado: el proxy ya emitió su certificado.</summary>
    public async Task MarcarCertificadoAsync(string host, CancellationToken ct = default)
    {
        var d = await catalogo.Dominios.FirstOrDefaultAsync(x => x.Host == host && x.Verificado && x.EstadoCertificado == EstadosCertificado.Emitiendo, ct);
        if (d is null) return;
        d.EstadoCertificado = EstadosCertificado.Activo;
        d.CertificadoEn = DateTime.UtcNow;
        await catalogo.SaveChangesAsync(ct);
    }

    // --- Remitente de correo ----------------------------------------------------------------------------------------

    public string? ValidarCorreo(string? dominio, string? remitente)
    {
        var d = BuscadorTenants.Normalizar(dominio ?? "");
        if (!Regex.IsMatch(d, "^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z]{2,63}$")) return "Ingresa el dominio de tu correo, p. ej. tuempresa.pe.";
        if (d == Dominio || d.EndsWith("." + Dominio)) return $"Usa un dominio de tu empresa, no de {Dominio}.";
        if (!Regex.IsMatch((remitente ?? "").Trim().ToLowerInvariant(), "^[a-z0-9]([a-z0-9._-]{0,62}[a-z0-9])?$")) return "El remitente es la parte antes de la @, p. ej. rrhh.";
        return null;
    }

    /// <summary>Configura (o cambia) el dominio de correo con una clave DKIM nueva: hay que volver a verificar.</summary>
    public async Task<TenantCorreoDominio> ConfigurarCorreoAsync(Guid tenantId, string dominio, string remitente, CancellationToken ct = default)
    {
        var d = BuscadorTenants.Normalizar(dominio);
        var c = await catalogo.CorreosDominio.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (c is null) catalogo.CorreosDominio.Add(c = new TenantCorreoDominio { TenantId = tenantId });
        var nuevaClave = c.Dominio != d || c.ClavePublica.Length == 0;
        c.Dominio = d;
        c.Remitente = remitente.Trim().ToLowerInvariant();
        if (nuevaClave)
        {
            using var rsa = RSA.Create(2048);
            c.Selector = $"nexorh{DateTime.UtcNow:yyyyMM}";
            c.ClavePublica = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            c.ClavePrivada = Clave.Protect(Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
            (c.SpfOk, c.DkimOk, c.DmarcOk, c.VerificadoEn) = (false, false, false, null);
        }
        await catalogo.SaveChangesAsync(ct);
        return c;
    }

    public List<RegistroDnsRequerido> RegistrosCorreo(TenantCorreoDominio c) =>
    [
        new("TXT", c.Dominio, $"v=spf1 include:{SpfInclude} ~all", 3600, c.UltimaRevision is null ? null : c.SpfOk),
        new("TXT", $"{c.Selector}._domainkey.{c.Dominio}", $"v=DKIM1; k=rsa; p={c.ClavePublica}", 3600, c.UltimaRevision is null ? null : c.DkimOk),
        new("TXT", $"_dmarc.{c.Dominio}", $"v=DMARC1; p=quarantine; adkim=r; aspf=r", 3600, c.UltimaRevision is null ? null : c.DmarcOk),
    ];

    public async Task VerificarCorreoAsync(TenantCorreoDominio c, CancellationToken ct = default)
    {
        static string Sin(string s) => Regex.Replace(s, "\\s+", "");
        var spf = await dns.TxtAsync(c.Dominio, ct);
        c.SpfOk = spf.Any(x => x.TrimStart().StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase) && x.Contains($"include:{SpfInclude}", StringComparison.OrdinalIgnoreCase));
        var dkim = await dns.TxtAsync($"{c.Selector}._domainkey.{c.Dominio}", ct);
        c.DkimOk = dkim.Any(x => Sin(x).Contains($"p={c.ClavePublica}"));
        var dmarc = await dns.TxtAsync($"_dmarc.{c.Dominio}", ct);
        c.DmarcOk = dmarc.Any(x => x.TrimStart().StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase));
        c.UltimaRevision = DateTime.UtcNow;
        if (c.Verificado) c.VerificadoEn ??= DateTime.UtcNow; else c.VerificadoEn = null;
        await catalogo.SaveChangesAsync(ct);
    }

    public RSA ClavePrivada(TenantCorreoDominio c)
    {
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(Clave.Unprotect(c.ClavePrivada)), out _);
        return rsa;
    }
}

// --- BD dedicada ----------------------------------------------------------------------------------------------------

/// <summary>Cifra las cadenas de conexión de las BD dedicadas en el catálogo (connection_string cifrada de la especificación).</summary>
public class CadenasConexion(IDataProtectionProvider proteccion)
{
    private readonly IDataProtector p = proteccion.CreateProtector("NexoRh.CadenaConexion.v1");
    public string Proteger(string cadena) => p.Protect(cadena);

    /// <summary>La cadena en claro. Acepta también cadenas guardadas sin cifrar (configuradas a mano en el catálogo).</summary>
    public string? Descifrar(string? guardada)
    {
        if (string.IsNullOrWhiteSpace(guardada)) return null;
        if (guardada.Contains('=') && guardada.Contains(';')) return guardada;
        return p.Unprotect(guardada);
    }
}
