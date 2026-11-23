using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace NexoRh.Api.Catalogo;

public record MarcaVigente(int Version, MarcaDatos Datos);

public record LogosPublicos(string? Horizontal, string? HorizontalClaro, string? Cuadrado);
public record LoginPublico(string? Imagen, string Titulo, string Mensaje, string? Ayuda, bool MostrarPoweredBy);
/// <summary>GET /api/publico/marca: lo que el frontend aplica antes de mostrar la app (tokens, logos, textos, fuente).</summary>
public record MarcaPublica(int Version, string Empresa, string NombrePortal, LogosPublicos Logos, LoginPublico Login, ColoresMarca Colores,
    string Tipografia, string Radio, TerminologiaMarca Terminologia);

/// <summary>Marca publicada de cada tenant (con caché de 1 minuto) y su versión pública.</summary>
public class Marcas(CatalogoDbContext catalogo, IMemoryCache cache)
{
    public static string UrlArchivo(Guid id) => $"/api/publico/marca/archivos/{id}";

    public async Task<MarcaVigente> DeAsync(Guid tenantId, CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync($"tenant-marca:{tenantId}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            var fila = await catalogo.Marcas.AsNoTracking().Where(m => m.TenantId == tenantId).OrderByDescending(m => m.Version).FirstOrDefaultAsync(ct);
            return fila is null ? new MarcaVigente(0, new MarcaDatos()) : new MarcaVigente(fila.Version, ReglasMarca.Leer(fila.Json));
        }))!;

    public void Olvidar(Guid tenantId) => cache.Remove($"tenant-marca:{tenantId}");

    /// <param name="permiteOcultarPoweredBy">Si el plan vigente lo permite (si el cliente bajó de plan, vuelve a mostrarse).</param>
    public static MarcaPublica Publica(MarcaVigente v, string empresa, bool permiteOcultarPoweredBy = false)
    {
        var m = v.Datos;
        static string? U(Guid? id) => id is { } x ? UrlArchivo(x) : null;
        return new MarcaPublica(v.Version, empresa, m.NombrePortal ?? "Mi Portal",
            new LogosPublicos(U(m.Logos.Horizontal), U(m.Logos.HorizontalClaro), U(m.Logos.Cuadrado)),
            new LoginPublico(U(m.Login.Imagen), m.Login.Titulo ?? $"¡Bienvenido a {empresa}!",
                m.Login.Mensaje ?? "Ingresa para ver tu información, tus boletas y tus solicitudes.", m.Login.Ayuda, m.Login.MostrarPoweredBy || !permiteOcultarPoweredBy),
            m.Colores, m.Tipografia, m.Radio, m.Terminologia);
    }
}

/// <summary>Validación de las imágenes de marca: tipo real por contenido, tamaño y SVG sin scripts.</summary>
public static class ArchivosMarca
{
    public const int MaximoBytes = 2 * 1024 * 1024;

    /// <summary>Content-Type según los primeros bytes (no según la extensión), o null si no es PNG, JPG, WebP ni SVG.</summary>
    public static string? Detectar(byte[] d)
    {
        if (d.Length > 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) return "image/png";
        if (d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length > 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return "image/webp";
        var inicio = System.Text.Encoding.UTF8.GetString(d, 0, Math.Min(d.Length, 512)).TrimStart('﻿', ' ', '\r', '\n', '\t');
        if (inicio.StartsWith("<?xml") || inicio.StartsWith("<svg") || inicio.StartsWith("<!--")) return "image/svg+xml";
        return null;
    }

    private static readonly HashSet<string> Prohibidos = new(StringComparer.OrdinalIgnoreCase)
        { "script", "foreignObject", "iframe", "embed", "object", "audio", "video", "handler", "listener", "set", "animate" };

    /// <summary>SVG sin scripts, manejadores on*, recursos externos ni DTD. Devuelve null si no es un SVG válido.</summary>
    public static byte[]? LimpiarSvg(byte[] datos)
    {
        XDocument doc;
        try
        {
            using var lector = XmlReader.Create(new MemoryStream(datos), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            doc = XDocument.Load(lector);
        }
        catch (XmlException) { return null; }
        if (doc.Root?.Name.LocalName != "svg") return null;
        foreach (var el in doc.Descendants().Where(e => Prohibidos.Contains(e.Name.LocalName)).ToList()) el.Remove();
        foreach (var el in doc.Descendants())
            foreach (var at in el.Attributes().ToList())
            {
                var nombre = at.Name.LocalName;
                var valor = at.Value.Trim();
                var esEnlace = nombre is "href" or "src";
                if (nombre.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    || (esEnlace && !(valor.StartsWith('#') || valor.StartsWith("data:image/png") || valor.StartsWith("data:image/jpeg")))
                    || valor.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
                    at.Remove();
            }
        foreach (var estilo in doc.Descendants().Where(e => e.Name.LocalName == "style").ToList())
            if (estilo.Value.Contains("url(", StringComparison.OrdinalIgnoreCase) || estilo.Value.Contains("@import", StringComparison.OrdinalIgnoreCase)) estilo.Remove();
        var salida = new MemoryStream();
        using (var w = XmlWriter.Create(salida, new XmlWriterSettings { OmitXmlDeclaration = false, Encoding = new System.Text.UTF8Encoding(false) })) doc.Save(w);
        return salida.ToArray();
    }
}
