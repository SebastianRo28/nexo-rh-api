using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Ubigeo para los formularios de las empresas (departamento › provincia › distrito). Lee la versión vigente del
/// catálogo global "ubigeo" que se publica desde la consola.
/// </summary>
[ApiController]
[Route("api/ubigeo")]
[Authorize]
public class UbigeoController(ServicioUbigeo ubigeo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UbigeoVigente>> Vigente()
    {
        Response.Headers.CacheControl = "private, max-age=3600";
        return await ubigeo.VigenteAsync();
    }
}

/// <summary>Versión vigente del catálogo global de ubigeo como árbol (en memoria por versión).</summary>
public class ServicioUbigeo(CatalogoDbContext catalogo, IMemoryCache cache)
{
    public async Task<UbigeoVigente> VigenteAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var v = await catalogo.VersionesCatalogo.AsNoTracking()
            .Where(x => x.Clave == Ubigeo.Clave && x.VigenteDesde <= hoy)
            .OrderByDescending(x => x.VigenteDesde).ThenByDescending(x => x.PublicadaEn)
            .Select(x => new { x.Id, x.Version }).FirstOrDefaultAsync();
        if (v is null) return new UbigeoVigente(null, []);
        var departamentos = await cache.GetOrCreateAsync($"ubigeo:{v.Id}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            var contenido = await catalogo.VersionesCatalogo.AsNoTracking().Where(x => x.Id == v.Id).Select(x => x.Contenido).FirstAsync();
            return Ubigeo.Arbol(contenido);
        });
        return new UbigeoVigente(v.Version, departamentos!);
    }

    /// <summary>Departamentos vigentes; vacío si el catálogo aún no tiene versión publicada.</summary>
    public async Task<List<UbigeoDepartamento>> DepartamentosAsync() => (await VigenteAsync()).Departamentos;
}

public record UbigeoVigente(string? Version, List<UbigeoDepartamento> Departamentos);
public record UbigeoDepartamento(string Nombre, List<UbigeoProvincia> Provincias);
public record UbigeoProvincia(string Nombre, List<UbigeoDistrito> Distritos);
public record UbigeoDistrito(string Codigo, string Nombre);

public static class Ubigeo
{
    public const string Clave = "ubigeo";
    private static readonly HashSet<string> Menores = ["de", "del", "la", "las", "los", "el", "y"];
    private static readonly CompareInfo Comparador = new CultureInfo("es-PE").CompareInfo;

    /// <summary>Compara nombres sin distinguir mayúsculas ni tildes ("Lurín" = "Lurin").</summary>
    public static bool Igual(string? a, string? b) =>
        Comparador.Compare((a ?? "").Trim(), (b ?? "").Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

    public static bool EsPeru(string? pais) => string.IsNullOrWhiteSpace(pais) || Igual(pais, "Perú");

    /// <summary>Árbol ordenado alfabéticamente desde el CSV codigo,departamento,provincia,distrito.</summary>
    public static List<UbigeoDepartamento> Arbol(string csv)
    {
        var es = StringComparer.Create(new CultureInfo("es-PE"), true);
        var filas = csv.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Select(l => l.Split(',', ';').Select(c => c.Trim()).ToArray())
            .Where(c => c.Length >= 4 && c[0].Length > 0)
            .Select(c => (Codigo: c[0], Dep: Nombre(c[1]), Prov: Nombre(c[2]), Dist: Nombre(c[3])));
        return filas.GroupBy(f => f.Dep).OrderBy(g => g.Key, es)
            .Select(d => new UbigeoDepartamento(d.Key, d.GroupBy(f => f.Prov).OrderBy(g => g.Key, es)
                .Select(p => new UbigeoProvincia(p.Key, p.OrderBy(f => f.Dist, es).Select(f => new UbigeoDistrito(f.Codigo, f.Dist)).ToList()))
                .ToList()))
            .ToList();
    }

    /// <summary>"CARMEN DE LA LEGUA-REYNOSO" → "Carmen de la Legua-Reynoso" (el INEI publica los nombres en mayúsculas).</summary>
    public static string Nombre(string s)
    {
        var palabras = s.Trim().ToLower(new CultureInfo("es-PE")).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', palabras.Select((p, i) => i > 0 && Menores.Contains(p) ? p
            : string.Join('-', p.Split('-').Select(t => t.Length == 0 ? t : char.ToUpper(t[0], CultureInfo.InvariantCulture) + t[1..]))));
    }
}
