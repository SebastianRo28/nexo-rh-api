using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record VersionMarca(int Version, string PublicadoPor, string PublicadoEn, string? Nota);
public record MarcaEdicion(int Version, MarcaDatos Datos, Dictionary<string, string> Archivos, List<VersionMarca> Versiones, MarcaDatos PorDefecto,
    string[] Fuentes, double ContrasteMinimo, string Empresa);
public record PublicarMarca(MarcaDatos Datos, string? Nota);
public record ArchivoSubido(Guid Id, string Url, string ContentType);

/// <summary>
/// ⚙ Administración › Personaliza tu marca (P-MT.2): edición con vista previa en el frontend, publicación por
/// versiones, volver a una versión anterior y subida de logos e imagen del login.
/// </summary>
[ApiController]
[Route("api/marca")]
[Authorize(Policy = Politicas.Administrador)]
public class MarcaController(CatalogoDbContext catalogo, Marcas marcas, ContextoTenant tenant, NexoDbContext db, Auditoria auditoria, Suscripciones suscripciones) : ControllerBase
{
    private Guid TenantId => tenant.Id!.Value;

    [HttpGet]
    public async Task<MarcaEdicion> Ver()
    {
        var v = await marcas.DeAsync(TenantId);
        var versiones = await catalogo.Marcas.AsNoTracking().Where(m => m.TenantId == TenantId).OrderByDescending(m => m.Version).Take(30)
            .Select(m => new { m.Version, m.PublicadoPor, m.PublicadoEn, m.Nota }).ToListAsync();
        return new MarcaEdicion(v.Version, v.Datos, Urls(v.Datos),
            versiones.Select(x => new VersionMarca(x.Version, x.PublicadoPor, Fechas.TextoHora(x.PublicadoEn), x.Nota)).ToList(),
            new MarcaDatos(), ReglasMarca.Fuentes, ReglasMarca.ContrasteMinimo, tenant.Nombre);
    }

    /// <summary>Publica una versión nueva. Rechaza colores sin contraste suficiente (con el tono sugerido) y archivos ajenos.</summary>
    [HttpPost]
    public async Task<ActionResult<MarcaEdicion>> Publicar(PublicarMarca p)
    {
        if (p.Datos is null) return Problem("Faltan los datos de la marca.", statusCode: 400);
        var datos = ReglasMarca.Normalizar(p.Datos, (await suscripciones.DeAsync(TenantId)).Funcion(FuncionesPlan.OcultarPoweredBy));
        var errores = ReglasMarca.Validar(datos);
        var ids = new[] { datos.Logos.Horizontal, datos.Logos.HorizontalClaro, datos.Logos.Cuadrado, datos.Login.Imagen }.OfType<Guid>().Distinct().ToList();
        var propios = await catalogo.ArchivosMarca.Where(a => a.TenantId == TenantId && ids.Contains(a.Id)).Select(a => a.Id).ToListAsync();
        if (propios.Count != ids.Count) errores["logos"] = "Alguna imagen no existe. Vuelve a subirla.";
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores.ToDictionary(x => x.Key, x => new[] { x.Value })) { Title = "Revisa la marca antes de publicarla." });
        return await GuardarVersion(datos, string.IsNullOrWhiteSpace(p.Nota) ? null : p.Nota.Trim()[..Math.Min(p.Nota.Trim().Length, 200)]);
    }

    /// <summary>Vuelve a una versión anterior (se publica como una versión nueva, así el historial no se pierde).</summary>
    [HttpPost("versiones/{version:int}/restaurar")]
    public async Task<ActionResult<MarcaEdicion>> Restaurar(int version)
    {
        var fila = await catalogo.Marcas.AsNoTracking().FirstOrDefaultAsync(m => m.TenantId == TenantId && m.Version == version);
        if (fila is null) return NotFound();
        return await GuardarVersion(ReglasMarca.Leer(fila.Json), $"Restaurada desde la versión {version}");
    }

    /// <summary>Sube un logo o la imagen del login (PNG, JPG, WebP o SVG de hasta 2 MB; los SVG se limpian de scripts).</summary>
    [HttpPost("archivos")]
    [RequestSizeLimit(ArchivosMarca.MaximoBytes + 64 * 1024)]
    public async Task<ActionResult<ArchivoSubido>> Subir([FromForm] IFormFile? archivo, [FromForm] string? tipo)
    {
        if (tipo is null || !ReglasMarca.TiposArchivo.Contains(tipo)) return Problem("Tipo de imagen no válido.", statusCode: 400);
        if (archivo is null || archivo.Length == 0) return Problem("Elige una imagen.", statusCode: 400);
        if (archivo.Length > ArchivosMarca.MaximoBytes) return Problem("La imagen supera 2 MB.", statusCode: 400);
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms);
        var datos = ms.ToArray();
        var ct = ArchivosMarca.Detectar(datos);
        if (ct is null) return Problem("Usa una imagen PNG, JPG, WebP o SVG.", statusCode: 400);
        if (tipo == "login" && ct == "image/svg+xml") return Problem("La imagen del login debe ser PNG, JPG o WebP.", statusCode: 400);
        if (ct == "image/svg+xml")
        {
            datos = ArchivosMarca.LimpiarSvg(datos)!;
            if (datos is null) return Problem("El archivo SVG no es válido.", statusCode: 400);
        }
        var a = new ArchivoMarca { TenantId = TenantId, Tipo = tipo, ContentType = ct, Datos = datos };
        catalogo.ArchivosMarca.Add(a);
        await catalogo.SaveChangesAsync();
        return new ArchivoSubido(a.Id, Marcas.UrlArchivo(a.Id), ct);
    }

    private async Task<ActionResult<MarcaEdicion>> GuardarVersion(MarcaDatos datos, string? nota)
    {
        var anterior = await catalogo.Marcas.Where(m => m.TenantId == TenantId).MaxAsync(m => (int?)m.Version) ?? 0;
        catalogo.Marcas.Add(new TenantMarca { TenantId = TenantId, Version = anterior + 1, Json = ReglasMarca.Serializar(datos), PublicadoPor = User.Firma(), Nota = nota });
        await catalogo.SaveChangesAsync();
        marcas.Olvidar(TenantId);
        auditoria.RegistrarTexto("Marca", "Modificar", null, null, "versión publicada", anterior.ToString(), $"{anterior + 1}{(nota is null ? "" : $" · {nota}")}");
        await db.SaveChangesAsync();
        return await Ver();
    }

    private static Dictionary<string, string> Urls(MarcaDatos m) =>
        new[] { m.Logos.Horizontal, m.Logos.HorizontalClaro, m.Logos.Cuadrado, m.Login.Imagen }.OfType<Guid>().Distinct()
            .ToDictionary(id => id.ToString(), Marcas.UrlArchivo);
}
