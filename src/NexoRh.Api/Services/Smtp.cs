using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NexoRh.Api.Services;

/// <summary>Firma DKIM con la que sale un correo desde el dominio del cliente.</summary>
public record FirmaDkim(string Dominio, string Selector, RSA Clave);

/// <summary>Mensaje MIME (HTML en UTF-8, base64) listo para SMTP, con su firma DKIM (rsa-sha256, relaxed/relaxed) si corresponde.</summary>
public static class MensajeMime
{
    private static readonly string[] Firmadas = ["From", "To", "Subject", "Date", "Message-ID", "MIME-Version", "Content-Type", "Content-Transfer-Encoding"];

    public static string Construir(string nombre, string direccion, string para, string asunto, string html, DateTimeOffset fecha, FirmaDkim? dkim = null)
    {
        var dominio = direccion.Split('@').Last();
        var cabeceras = new List<(string Nombre, string Valor)>
        {
            ("From", $"{Palabra(nombre)} <{direccion}>"),
            ("To", $"<{para}>"),
            ("Subject", Palabra(asunto)),
            ("Date", fecha.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss '+0000'", CultureInfo.InvariantCulture)),
            ("Message-ID", $"<{Guid.NewGuid():N}@{dominio}>"),
            ("MIME-Version", "1.0"),
            ("Content-Type", "text/html; charset=utf-8"),
            ("Content-Transfer-Encoding", "base64"),
        };
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));
        var cuerpo = new StringBuilder();
        for (var i = 0; i < b64.Length; i += 76) cuerpo.Append(b64, i, Math.Min(76, b64.Length - i)).Append("\r\n");
        var sb = new StringBuilder();
        if (dkim is not null) sb.Append("DKIM-Signature: ").Append(Firmar(cabeceras, cuerpo.ToString(), dkim)).Append("\r\n");
        foreach (var (n, v) in cabeceras) sb.Append(n).Append(": ").Append(v).Append("\r\n");
        return sb.Append("\r\n").Append(cuerpo).ToString();
    }

    /// <summary>Valor de la cabecera DKIM-Signature.</summary>
    public static string Firmar(List<(string Nombre, string Valor)> cabeceras, string cuerpo, FirmaDkim dkim)
    {
        var bh = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(CuerpoRelaxed(cuerpo))));
        var h = string.Join(':', Firmadas.Select(x => x.ToLowerInvariant()));
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sinFirma = $"v=1; a=rsa-sha256; c=relaxed/relaxed; d={dkim.Dominio}; s={dkim.Selector}; t={t}; h={h}; bh={bh}; b=";
        var datos = new StringBuilder();
        foreach (var nombre in Firmadas)
        {
            var c = cabeceras.First(x => x.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
            datos.Append(CabeceraRelaxed(c.Nombre, c.Valor)).Append("\r\n");
        }
        datos.Append(CabeceraRelaxed("DKIM-Signature", sinFirma));
        var firma = dkim.Clave.SignData(Encoding.UTF8.GetBytes(datos.ToString()), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return sinFirma + Convert.ToBase64String(firma);
    }

    public static string CabeceraRelaxed(string nombre, string valor) =>
        $"{nombre.Trim().ToLowerInvariant()}:{Regex.Replace(valor.Replace("\r\n", ""), "[ \t]+", " ").Trim()}";

    public static string CuerpoRelaxed(string cuerpo)
    {
        var lineas = cuerpo.Replace("\r\n", "\n").Split('\n').Select(l => Regex.Replace(l, "[ \t]+", " ").TrimEnd(' ')).ToList();
        while (lineas.Count > 0 && lineas[^1].Length == 0) lineas.RemoveAt(lineas.Count - 1);
        return lineas.Count == 0 ? "" : string.Join("\r\n", lineas) + "\r\n";
    }

    /// <summary>Texto ASCII tal cual; si no, palabras codificadas RFC 2047 (UTF-8, base64) de hasta 75 caracteres.</summary>
    private static string Palabra(string s)
    {
        if (s.All(c => c is >= ' ' and <= '~') && !s.Contains("=?")) return s.IndexOfAny(['"', ',', ':', ';', '<', '>', '@', '(', ')']) < 0 ? s : $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        var partes = new List<string>();
        var actual = new StringBuilder();
        foreach (var r in s.EnumerateRunes())
        {
            if (Encoding.UTF8.GetByteCount(actual + r.ToString()) > 45) { partes.Add(actual.ToString()); actual.Clear(); }
            actual.Append(r.ToString());
        }
        if (actual.Length > 0) partes.Add(actual.ToString());
        return string.Join("\r\n ", partes.Select(p => $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(p))}?="));
    }
}

/// <summary>
/// Cliente SMTP mínimo: TLS implícito (puerto 465) o STARTTLS, AUTH PLAIN y DATA con el mensaje ya armado (necesario para
/// firmar con DKIM, que System.Net.Mail no permite).
/// </summary>
public static class ClienteSmtp
{
    public static async Task EnviarAsync(OpcionesCorreo o, string de, string para, string mensaje, CancellationToken ct = default)
    {
        using var tcp = new TcpClient();
        using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tiempo.CancelAfter(TimeSpan.FromSeconds(60));
        await tcp.ConnectAsync(o.SmtpHost!, o.SmtpPuerto, tiempo.Token);
        Stream s = tcp.GetStream();
        if (o.SmtpPuerto == 465) s = await TlsAsync(s, o.SmtpHost!, tiempo.Token);
        await EsperarAsync(s, 220, tiempo.Token);
        var capacidades = await ComandoAsync(s, $"EHLO {Dns.GetHostName()}", 250, tiempo.Token);
        if (o.SmtpPuerto != 465 && o.SmtpSsl)
        {
            if (!capacidades.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("El servidor SMTP no ofrece STARTTLS.");
            await ComandoAsync(s, "STARTTLS", 220, tiempo.Token);
            s = await TlsAsync(s, o.SmtpHost!, tiempo.Token);
            await ComandoAsync(s, $"EHLO {Dns.GetHostName()}", 250, tiempo.Token);
        }
        if (!string.IsNullOrEmpty(o.SmtpUsuario))
            await ComandoAsync(s, "AUTH PLAIN " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{o.SmtpUsuario}\0{o.SmtpClave}")), 235, tiempo.Token);
        await ComandoAsync(s, $"MAIL FROM:<{de}>", 250, tiempo.Token);
        await ComandoAsync(s, $"RCPT TO:<{para}>", 250, tiempo.Token, 251);
        await ComandoAsync(s, "DATA", 354, tiempo.Token);
        // Dot-stuffing: una línea que empieza con "." se envía con ".." (RFC 5321 §4.5.2).
        var cuerpo = Regex.Replace(mensaje, "(?m)^\\.", "..");
        await EscribirAsync(s, cuerpo.EndsWith("\r\n") ? cuerpo + "." : cuerpo + "\r\n.", tiempo.Token);
        await EsperarAsync(s, 250, tiempo.Token);
        try { await ComandoAsync(s, "QUIT", 221, tiempo.Token); } catch { /* el correo ya se aceptó */ }
    }

    private static async Task<Stream> TlsAsync(Stream s, string host, CancellationToken ct)
    {
        var ssl = new SslStream(s, leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);
        return ssl;
    }

    private static async Task<string> ComandoAsync(Stream s, string linea, int esperado, CancellationToken ct, int? alternativo = null)
    {
        await EscribirAsync(s, linea, ct);
        return await EsperarAsync(s, esperado, ct, alternativo);
    }

    private static Task EscribirAsync(Stream s, string linea, CancellationToken ct) => s.WriteAsync(Encoding.UTF8.GetBytes(linea + "\r\n"), ct).AsTask();

    /// <summary>Lee una respuesta (posiblemente de varias líneas "250-…") y verifica su código.</summary>
    private static async Task<string> EsperarAsync(Stream s, int esperado, CancellationToken ct, int? alternativo = null)
    {
        var todo = new StringBuilder();
        while (true)
        {
            var linea = await LineaAsync(s, ct);
            todo.AppendLine(linea);
            if (linea.Length < 4 || linea[3] != '-')
            {
                var codigo = int.TryParse(linea.AsSpan(0, Math.Min(3, linea.Length)), out var c) ? c : 0;
                if (codigo != esperado && codigo != alternativo) throw new InvalidOperationException($"El servidor SMTP respondió: {linea}");
                return todo.ToString();
            }
        }
    }

    private static async Task<string> LineaAsync(Stream s, CancellationToken ct)
    {
        var b = new List<byte>();
        var uno = new byte[1];
        while (true)
        {
            if (await s.ReadAsync(uno, ct) == 0) throw new IOException("El servidor SMTP cerró la conexión.");
            if (uno[0] == '\n') break;
            if (uno[0] != '\r') b.Add(uno[0]);
            if (b.Count > 4096) throw new IOException("Respuesta SMTP demasiado larga.");
        }
        return Encoding.UTF8.GetString(b.ToArray());
    }
}
