using System.Net;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Services;

/// <summary>Configuración del correo saliente (sección Correo de appsettings).</summary>
public class OpcionesCorreo
{
    /// <summary>“Bandeja” (desarrollo: el correo solo queda guardado en el catálogo) o “Smtp”.</summary>
    public string Proveedor { get; set; } = "Bandeja";
    public string Remitente { get; set; } = "Nexo RH <no-responder@nexorh.pe>";
    public string? SmtpHost { get; set; }
    public int SmtpPuerto { get; set; } = 587;
    public string? SmtpUsuario { get; set; }
    public string? SmtpClave { get; set; }
    public bool SmtpSsl { get; set; } = true;
}

public record MensajeCorreo(string Para, string Asunto, string Titulo, string Cuerpo, string? Boton = null, string? Enlace = null, string? Pie = null);

/// <summary>
/// Envío de correos de la plataforma. Todo correo queda registrado en el catálogo (CorreoSaliente); con el proveedor
/// Smtp además se envía. Otros proveedores (SES, SendGrid…) se agregan aquí sin tocar a quien envía. En Enterprise, con el
/// dominio de correo del cliente verificado (SPF, DKIM y DMARC), sale desde su dominio y firmado con su clave DKIM.
/// </summary>
public class EnviadorCorreo(CatalogoDbContext catalogo, ContextoTenant tenant, Marcas marcas, IHttpContextAccessor http, Suscripciones suscripciones,
    DominiosTenant dominios, Microsoft.Extensions.Options.IOptions<OpcionesCorreo> opciones, ILogger<EnviadorCorreo> log)
{
    private readonly OpcionesCorreo o = opciones.Value;

    public async Task EnviarAsync(MensajeCorreo m, CancellationToken ct = default)
    {
        // Con la marca del tenant: color del botón, logo para fondo claro, remitente y firma.
        var marca = tenant.Id is { } id ? (await marcas.DeAsync(id, ct)).Datos : new MarcaDatos();
        var suscripcion = tenant.Id is { } sid ? await suscripciones.DeAsync(sid, ct) : null;
        var req = http.HttpContext?.Request;
        var logo = marca.Logos.HorizontalClaro is { } l && req is not null ? $"{req.Scheme}://{req.Host.Value}{Marcas.UrlArchivo(l)}" : null;
        var poweredBy = !(suscripcion?.Funcion(FuncionesPlan.OcultarPoweredBy) == true && !marca.Login.MostrarPoweredBy);
        var html = Plantilla(m, tenant.Resuelto ? tenant.Nombre : "Nexo RH", marca.Colores.Primario, logo, marca.Correos.Firma, poweredBy);
        var remitente = DireccionRemitente(o.Remitente);
        if (marca.Correos.Remitente is { } nombre) remitente = (nombre, remitente.Direccion);
        // Remitente desde el dominio del cliente (Enterprise, con SPF, DKIM y DMARC verificados).
        TenantCorreoDominio? propio = null;
        if (suscripcion?.Funcion(FuncionesPlan.CorreoDominio) == true)
        {
            propio = await catalogo.CorreosDominio.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenant.Id, ct);
            if (propio is { Verificado: true }) remitente = (marca.Correos.Remitente ?? tenant.Nombre, $"{propio.Remitente}@{propio.Dominio}");
            else propio = null;
        }
        var fila = new CorreoSaliente { TenantId = tenant.Id, Para = m.Para, Asunto = m.Asunto, Html = html, Enlace = m.Enlace, Proveedor = o.Proveedor,
            Remitente = remitente.Direccion, DkimDominio = propio?.Dominio };
        if (string.Equals(o.Proveedor, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var clave = propio is null ? null : dominios.ClavePrivada(propio);
                var dkim = propio is null ? null : new FirmaDkim(propio.Dominio, propio.Selector, clave!);
                var mime = MensajeMime.Construir(remitente.Nombre, remitente.Direccion, m.Para, m.Asunto, html, DateTimeOffset.UtcNow, dkim);
                await ClienteSmtp.EnviarAsync(o, remitente.Direccion, m.Para, mime, ct);
                fila.Estado = "Enviado";
            }
            catch (Exception e)
            {
                fila.Estado = "Error";
                log.LogError(e, "No se pudo enviar el correo “{Asunto}”.", m.Asunto);
            }
        }
        else log.LogInformation("Correo guardado en la bandeja de desarrollo: “{Asunto}”.", m.Asunto);
        catalogo.Correos.Add(fila);
        await catalogo.SaveChangesAsync(ct);
    }

    private static (string Nombre, string Direccion) DireccionRemitente(string r)
    {
        var i = r.IndexOf('<');
        return i < 0 ? ("Nexo RH", r.Trim()) : (r[..i].Trim(), r[(i + 1)..].TrimEnd('>', ' '));
    }

    private static string Plantilla(MensajeCorreo m, string empresa, string color, string? logo, string? firma, bool poweredBy)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        var boton = m.Boton is null || m.Enlace is null ? "" :
            $"""<p style="margin:28px 0"><a href="{E(m.Enlace)}" style="background:{E(color)};color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;display:inline-block">{E(m.Boton)}</a></p>""" +
            $"""<p style="font-size:12px;color:#6b7280">Si el botón no funciona, copia este enlace en tu navegador:<br><span style="word-break:break-all">{E(m.Enlace)}</span></p>""";
        return $"""
            <!doctype html><html lang="es"><body style="margin:0;background:#f4f5f7;font-family:Segoe UI,Arial,sans-serif;color:#1f2937">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:32px 16px">
            <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#fff;border-radius:12px;padding:32px">
            <tr><td>
            {(logo is null ? $"""<p style="font-size:13px;color:#6b7280;margin:0 0 16px">{E(empresa)}</p>""" : $"""<p style="margin:0 0 20px"><img src="{E(logo)}" alt="{E(empresa)}" height="40" style="height:40px;max-width:200px"></p>""")}
            <h1 style="font-size:20px;margin:0 0 12px">{E(m.Titulo)}</h1>
            <div style="font-size:15px;line-height:1.55">{m.Cuerpo}</div>
            {boton}
            {(firma is null ? "" : $"""<p style="font-size:14px;color:#374151;margin-top:24px;white-space:pre-line">{E(firma)}</p>""")}
            <p style="font-size:12px;color:#6b7280;margin-top:28px">{E(m.Pie ?? "Si no solicitaste este correo, puedes ignorarlo.")}</p>
            </td></tr></table>
            {(poweredBy ? """<p style="font-size:11px;color:#9ca3af">Powered by Nexo RH</p>""" : "")}
            </td></tr></table></body></html>
            """;
    }
}
