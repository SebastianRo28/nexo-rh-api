using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Registros de los módulos que no tienen tabla propia (KPI, beneficios, cursos, tickets, dashboards…): GET/POST/PUT/DELETE
/// /api/datos/{coleccion}. Cada colección define quién lee y escribe y qué módulo requiere (Services/ColeccionesModulo.cs).
/// Cada registro es un objeto JSON con "id" (y "colaboradorId" en las colecciones de registros propios).
/// </summary>
[ApiController]
[Route("api/datos")]
[Authorize]
public class DatosController(NexoDbContext db, Auditoria auditoria, ContextoTenant tenant, Suscripciones suscripciones) : ControllerBase
{
    private const int MaxBytes = 64 * 1024;
    private bool EsAdmin => User.Rol() == Rol.Administrador;
    private int? Yo => User.ColaboradorId();

    /// <summary>Varias colecciones a la vez (?c=beneficios,cursos): las que el usuario no puede leer se omiten.</summary>
    [HttpGet]
    public async Task<Dictionary<string, List<JsonObject>>> Varias([FromQuery] string? c)
    {
        var r = new Dictionary<string, List<JsonObject>>();
        foreach (var clave in (c ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct())
            if (ColeccionesModulo.Buscar(clave) is { } col && await PuedeLeer(col) is null) r[clave] = await Leer(col);
        return r;
    }

    [HttpGet("{coleccion}")]
    public async Task<ActionResult<List<JsonObject>>> Listar(string coleccion)
    {
        if (ColeccionesModulo.Buscar(coleccion) is not { } col) return NotFound();
        if (await PuedeLeer(col) is { } error) return error;
        return await Leer(col);
    }

    [HttpPost("{coleccion}")]
    public async Task<ActionResult<JsonObject>> Crear(string coleccion, JsonElement cuerpo)
    {
        if (ColeccionesModulo.Buscar(coleccion) is not { } col) return NotFound();
        if (await PuedeEscribir(col) is { } error) return error;
        if (Objeto(cuerpo, out var datos) is { } invalido) return invalido;
        int? dueno = null;
        if (col.Propios)
        {
            if (EsAdmin) dueno = datos["colaboradorId"] is JsonValue v && v.TryGetValue<int>(out var x) ? x : Yo;
            else
            {
                if (Yo is not { } yo) return Problema("Tu cuenta no está vinculada a un colaborador.", 403);
                dueno = yo;
                datos = Filtrar(datos, [.. col.EditablesPorDueno ?? [], .. col.AlCrear ?? []]);
                if (col.AlCrearPorDueno is { } completar)
                {
                    var todas = await db.RegistrosModulo.AsNoTracking().ToListAsync();
                    IEnumerable<JsonObject> De(string c, bool propios) => todas.Where(r => r.Coleccion == c && (!propios || r.ColaboradorId == yo)).Select(AJson);
                    if (completar(new CreacionPropia(datos, yo, c => De(c, false), c => De(c, true))) is { } msg) return Problema(msg, 400);
                }
            }
            if (dueno is null) return Problema("Indica el colaborador.", 400);
            datos["colaboradorId"] = dueno;
        }
        var tokenNuevo = ProtegerToken(col, datos);
        var reg = new RegistroModulo { Coleccion = col.Clave, ColaboradorId = dueno, ModificadoPor = User.Nombre() };
        datos.Remove("id");
        reg.Datos = datos.ToJsonString();
        db.RegistrosModulo.Add(reg);
        await db.SaveChangesAsync();
        auditoria.Registrar($"{col.Nombre} #{reg.Id}", "Crear", actual: Resumen(datos));
        await db.SaveChangesAsync();
        var r = AJson(reg);
        if (tokenNuevo is not null) r["tokenNuevo"] = tokenNuevo;
        return Created($"/api/datos/{col.Clave}/{reg.Id}", r);
    }

    [HttpPut("{coleccion}/{id:int}")]
    public async Task<ActionResult<JsonObject>> Modificar(string coleccion, int id, JsonElement cuerpo)
    {
        if (ColeccionesModulo.Buscar(coleccion) is not { } col) return NotFound();
        if (await PuedeEscribir(col) is { } error) return error;
        if (Objeto(cuerpo, out var datos) is { } invalido) return invalido;
        var reg = await db.RegistrosModulo.FirstOrDefaultAsync(r => r.Id == id && r.Coleccion == col.Clave);
        if (reg is null) return NotFound();
        var antes = JsonNode.Parse(reg.Datos)!.AsObject();
        if (!EsAdmin)
        {
            if (!col.Propios || reg.ColaboradorId != Yo) return Problema("Solo puedes modificar tus propios registros.", 403);
            // The owner only changes the editable fields; everything else stays as it was.
            var nuevo = antes.DeepClone().AsObject();
            foreach (var (k, v) in Filtrar(datos, col.EditablesPorDueno)) nuevo[k] = v?.DeepClone();
            datos = nuevo;
        }
        else if (col.Propios && datos["colaboradorId"] is JsonValue v && v.TryGetValue<int>(out var x)) reg.ColaboradorId = x;
        if (col.Propios) datos["colaboradorId"] = reg.ColaboradorId;
        if (col.Clave == "api-claves") { datos["tokenHash"] = antes["tokenHash"]?.DeepClone(); datos["token"] = antes["token"]?.DeepClone(); }
        datos.Remove("id");
        datos.Remove("tokenNuevo");
        reg.Datos = datos.ToJsonString();
        reg.ActualizadoEn = DateTime.UtcNow;
        reg.ModificadoPor = User.Nombre();
        auditoria.Registrar($"{col.Nombre} #{reg.Id}", "Modificar", anterior: Resumen(antes), actual: Resumen(datos));
        await db.SaveChangesAsync();
        return AJson(reg);
    }

    [HttpDelete("{coleccion}/{id:int}")]
    public async Task<IActionResult> Eliminar(string coleccion, int id)
    {
        if (ColeccionesModulo.Buscar(coleccion) is not { } col) return NotFound();
        if (await PuedeEscribir(col) is { } error) return error;
        if (!EsAdmin) return Problema("Solo un administrador puede eliminar registros.", 403);
        var reg = await db.RegistrosModulo.FirstOrDefaultAsync(r => r.Id == id && r.Coleccion == col.Clave);
        if (reg is null) return NotFound();
        db.RegistrosModulo.Remove(reg);
        auditoria.Registrar($"{col.Nombre} #{reg.Id}", "Eliminar", anterior: Resumen(JsonNode.Parse(reg.Datos)!.AsObject()));
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Reglas -------------------------------------------------------------------------------------------------------

    private async Task<List<JsonObject>> Leer(Coleccion col)
    {
        var q = db.RegistrosModulo.AsNoTracking().Where(r => r.Coleccion == col.Clave);
        if (col.Lectura == AccesoColeccion.Propios && !EsAdmin) { var yo = Yo ?? -1; q = q.Where(r => r.ColaboradorId == yo); }
        return (await q.OrderBy(r => r.Id).ToListAsync()).Select(AJson).ToList();
    }

    private async Task<ActionResult?> PuedeLeer(Coleccion col)
    {
        if (col.Lectura == AccesoColeccion.Admin && !EsAdmin) return Problema("No tienes acceso a esta información.", 403);
        return col.Lectura == AccesoColeccion.Todos ? null : await Modulo(col);
    }

    private async Task<ActionResult?> PuedeEscribir(Coleccion col)
    {
        if (col.Escritura == AccesoColeccion.Admin && !EsAdmin) return Problema("Solo un administrador puede modificar esta información.", 403);
        if (col.Escritura == AccesoColeccion.Todos) return Problema("Colección de solo lectura.", 403);
        return await Modulo(col);
    }

    /// <summary>Same answer as [RequiereModulo]: 403 module_not_enabled, which the frontend shows as the commercial modal.</summary>
    private async Task<ActionResult?> Modulo(Coleccion col)
    {
        if (col.Modulo is null || tenant.Id is not { } t) return null;
        if ((await suscripciones.DeAsync(t, HttpContext.RequestAborted)).Tiene(col.Modulo)) return null;
        var nombre = Modulos.Catalogo.FirstOrDefault(m => m.Codigo == col.Modulo)?.Nombre ?? col.Modulo;
        return new ObjectResult(new { title = $"El módulo {nombre} no está incluido en tu plan.", status = 403, codigo = "module_not_enabled", modulo = col.Modulo })
            { StatusCode = StatusCodes.Status403Forbidden };
    }

    // --- Utilidades ---------------------------------------------------------------------------------------------------

    private ActionResult? Objeto(JsonElement cuerpo, out JsonObject datos)
    {
        datos = [];
        if (cuerpo.ValueKind != JsonValueKind.Object) return Problema("El registro debe ser un objeto JSON.", 400);
        if (Encoding.UTF8.GetByteCount(cuerpo.GetRawText()) > MaxBytes) return Problema("El registro es demasiado grande (máximo 64 KB).", 400);
        datos = JsonNode.Parse(cuerpo.GetRawText())!.AsObject();
        return null;
    }

    private static JsonObject Filtrar(JsonObject datos, string[]? permitidos)
    {
        var r = new JsonObject();
        foreach (var (k, v) in datos) if (permitidos?.Contains(k) == true) r[k] = v?.DeepClone();
        return r;
    }

    private static JsonObject AJson(RegistroModulo r)
    {
        var o = JsonNode.Parse(r.Datos)?.AsObject() ?? [];
        o["id"] = r.Id;
        if (r.ColaboradorId is { } c) o["colaboradorId"] = c;
        o.Remove("tokenHash");
        return o;
    }

    /// <summary>API keys: only a SHA-256 hash and a masked version are stored; the full token is returned once.</summary>
    private static string? ProtegerToken(Coleccion col, JsonObject datos)
    {
        if (col.Clave != "api-claves") return null;
        var token = "nx_live_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        datos["tokenHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        datos["token"] = token[..12] + "…" + token[^4..];
        return token;
    }

    private static string Resumen(JsonObject o)
    {
        var s = string.Join(" · ", o.Where(kv => kv.Value is JsonValue && kv.Key is not ("tokenHash" or "token")).Take(4).Select(kv => kv.Value!.ToString()));
        return s.Length == 0 ? "—" : s.Length > 200 ? s[..200] : s;
    }

    private ObjectResult Problema(string mensaje, int estado) => Problem(mensaje, statusCode: estado);
}
