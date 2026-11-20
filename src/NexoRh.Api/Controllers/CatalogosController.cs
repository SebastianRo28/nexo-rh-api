using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Catálogos genéricos de Configuración y Administración (empresas, centros de costo, localidades, feriados…).
/// Cada fila se valida con la definición del catálogo (Data/genericos.json). Solo administradores; los feriados los
/// lee cualquiera (los usa Asistencia) y al cambiarlos se actualiza la tabla de feriados de Asistencia.
/// </summary>
[ApiController]
[Route("api/catalogos")]
[Authorize]
public class CatalogosController(NexoDbContext db, Genericos genericos, Auditoria auditoria) : ControllerBase
{
    private static JsonObject Fila(FilaCatalogo f)
    {
        var o = JsonNode.Parse(f.Datos)!.AsObject();
        o["id"] = f.Id;
        return o;
    }

    /// <summary>Todos los catálogos: { clave: [filas] }.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<Dictionary<string, List<JsonObject>>> Todos()
    {
        var filas = await db.FilasCatalogo.AsNoTracking().OrderBy(f => f.Id).ToListAsync();
        return genericos.Catalogos.Keys.ToDictionary(k => k, k => filas.Where(f => f.Clave == k).Select(Fila).ToList());
    }

    [HttpGet("{clave}")]
    public async Task<ActionResult<List<JsonObject>>> Listar(string clave)
    {
        if (!genericos.Catalogos.ContainsKey(clave)) return NotFound();
        if (clave != "feriados" && !User.IsInRole("Administrador")) return Forbid();
        return (await db.FilasCatalogo.AsNoTracking().Where(f => f.Clave == clave).OrderBy(f => f.Id).ToListAsync()).Select(Fila).ToList();
    }

    [HttpPost("{clave}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<JsonObject>> Crear(string clave, JsonElement fila) => await Guardar(clave, null, fila);

    [HttpPut("{clave}/{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<JsonObject>> Modificar(string clave, int id, JsonElement fila) => await Guardar(clave, id, fila);

    private async Task<ActionResult<JsonObject>> Guardar(string clave, int? id, JsonElement entrada)
    {
        if (!genericos.Catalogos.TryGetValue(clave, out var cat)) return NotFound();
        FilaCatalogo? existente = null;
        if (id is { } i)
        {
            existente = await db.FilasCatalogo.FirstOrDefaultAsync(f => f.Id == i && f.Clave == clave);
            if (existente is null) return NotFound();
        }
        var otras = (await db.FilasCatalogo.AsNoTracking().Where(f => f.Clave == clave && f.Id != (id ?? 0)).ToListAsync()).Select(f => JsonNode.Parse(f.Datos)!.AsObject());
        var todas = await db.FilasCatalogo.AsNoTracking().ToListAsync();
        IEnumerable<string> ValoresDe(string otro)
        {
            if (!genericos.Catalogos.TryGetValue(otro, out var oc)) return [];
            var k = oc.Campos[0].Key;
            return todas.Where(f => f.Clave == otro).Select(f => Genericos.Clave(JsonNode.Parse(f.Datos)!.AsObject(), k));
        }
        var errores = genericos.ValidarFila(cat, entrada, otras, ValoresDe, out var limpia);
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores) { Title = errores.Values.First()[0] });

        await using var tx = await db.Database.BeginTransactionAsync();
        var etiqueta = limpia[cat.Campos[0].Key]?.ToString() ?? "—";
        var entidad = $"{cat.Singular} · {etiqueta}";
        if (existente is null)
        {
            existente = new FilaCatalogo { Clave = clave };
            db.FilasCatalogo.Add(existente);
            auditoria.Registrar(entidad, "Crear", actual: Resumen(cat, limpia));
        }
        else
        {
            var antes = JsonNode.Parse(existente.Datos)!.AsObject();
            foreach (var c in cat.Campos.Where(c => antes[c.Key]?.ToJsonString() != limpia[c.Key]?.ToJsonString()))
                auditoria.Registrar(entidad, "Modificar", propiedad: c.Label, anterior: Texto(antes[c.Key]), actual: Texto(limpia[c.Key]));
        }
        existente.Datos = limpia.ToJsonString();
        existente.ActualizadoEn = DateTime.UtcNow;
        existente.ModificadoPor = User.Nombre();
        await db.SaveChangesAsync();
        if (clave == "feriados") await SincronizarFeriados();
        await tx.CommitAsync();
        return Fila(existente);
    }

    [HttpDelete("{clave}/{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Eliminar(string clave, int id)
    {
        if (!genericos.Catalogos.TryGetValue(clave, out var cat)) return NotFound();
        var f = await db.FilasCatalogo.FirstOrDefaultAsync(x => x.Id == id && x.Clave == clave);
        if (f is null) return NotFound();
        var datos = JsonNode.Parse(f.Datos)!.AsObject();
        var valor = Genericos.Clave(datos, cat.Campos[0].Key);
        foreach (var r in cat.ReferenciadoPor)
        {
            var usada = (await db.FilasCatalogo.AsNoTracking().Where(x => x.Clave == r.Clave).ToListAsync())
                .Any(x => Genericos.Clave(JsonNode.Parse(x.Datos)!.AsObject(), r.Campo).Equals(valor, StringComparison.OrdinalIgnoreCase));
            if (usada)
            {
                var otro = genericos.Catalogos.TryGetValue(r.Clave, out var oc) ? oc.Singular.ToLowerInvariant() : r.Clave;
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["id"] = [$"No se puede eliminar: “{valor}” se usa en algún registro de {otro}."] })
                { Title = $"No se puede eliminar: “{valor}” se usa en algún registro de {otro}." });
            }
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        db.FilasCatalogo.Remove(f);
        auditoria.Registrar($"{cat.Singular} · {valor}", "Eliminar", anterior: Resumen(cat, datos));
        await db.SaveChangesAsync();
        if (clave == "feriados") await SincronizarFeriados();
        await tx.CommitAsync();
        return NoContent();
    }

    /// <summary>La tabla de feriados de Asistencia queda igual al catálogo.</summary>
    private async Task SincronizarFeriados()
    {
        var filas = (await db.FilasCatalogo.AsNoTracking().Where(f => f.Clave == "feriados").ToListAsync()).Select(f => JsonNode.Parse(f.Datos)!.AsObject());
        db.Feriados.RemoveRange(db.Feriados);
        foreach (var x in filas)
            if (DateOnly.TryParseExact(x["fecha"]?.ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                db.Feriados.Add(new Feriado { Fecha = d, Nombre = x["nombre"]?.ToString() ?? "", Alcance = x["alcance"]?.ToString() is { Length: > 0 } a ? a : "Nacional" });
        await db.SaveChangesAsync();
    }

    private static string Texto(JsonNode? n) => n switch
    {
        null => "—",
        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "Sí" : "No",
        _ => n.ToString() is { Length: > 0 } s ? s : "—",
    };
    private static string Resumen(Genericos.Catalogo cat, JsonObject o) =>
        string.Join(" · ", cat.Campos.Where(c => c.Tipo != "switch" && c.Tipo != "textarea").Select(c => Texto(o[c.Key])).Where(s => s != "—").Take(4));
}
