using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record ReporteDto(int Id, string Nombre, string Plantilla, string Categoria, string Creado, string CreadoPor, bool Generable, bool RequierePeriodo);
public record EjecucionDto(int Id, string Reporte, string Plantilla, string? Periodo, string Usuario, string Fecha, int Filas);
public record DatosReportes(List<ReporteDto> Reportes, List<EjecucionDto> Historial, PlantillaReporte[] Plantillas, List<string> Periodos);
public record ReporteNuevo(string? Plantilla, string? Nombre);
public record IdsReportes(List<int>? Ids);

/// <summary>
/// Reportería › Exportadores: reportes guardados a partir de plantillas y su generación como CSV con los datos actuales.
/// Cada archivo generado queda en el Historial; volver a descargarlo desde ahí genera los datos de nuevo.
/// </summary>
[ApiController]
[Route("api/reportes")]
[Authorize(Policy = Politicas.Administrador)]
public partial class ReportesController(NexoDbContext db, Auditoria auditoria, GeneradorReportes generador, RelojAsistencia reloj) : ControllerBase
{
    [HttpGet]
    public async Task<DatosReportes> Datos()
    {
        var reps = await db.Reportes.AsNoTracking().OrderByDescending(r => r.Id).ToListAsync();
        var hist = await db.EjecucionesReporte.AsNoTracking().OrderByDescending(e => e.Id).Take(200).ToListAsync();
        // Períodos con boletas calculadas, del más reciente al más antiguo.
        var periodos = await db.Boletas.Select(b => b.Periodo).Distinct().OrderByDescending(p => p).ToListAsync();
        return new DatosReportes(reps.Select(Dto).ToList(),
            hist.Select(e => new EjecucionDto(e.Id, e.Reporte, e.Plantilla, e.Periodo is { } p ? Planilla.Texto(p) : null, e.Usuario, Fechas.TextoHora(e.Fecha), e.Filas)).ToList(),
            GeneradorReportes.Plantillas, periodos.Select(Planilla.Texto).ToList());
    }

    private static ReporteDto Dto(ReporteGuardado r)
    {
        var p = GeneradorReportes.Plantilla(r.Plantilla);
        return new(r.Id, r.Nombre, r.Plantilla, r.Categoria, Fechas.Texto(r.Creado), r.CreadoPor, p?.Generable ?? false, p?.RequierePeriodo ?? false);
    }

    [HttpPost]
    public async Task<ActionResult<ReporteDto>> Crear(ReporteNuevo n)
    {
        var p = GeneradorReportes.Plantilla((n.Plantilla ?? "").Trim());
        if (p is null) return Error("plantilla", "Elige una plantilla.");
        if (!p.Generable) return Error("plantilla", $"La plantilla “{p.Titulo}” todavía no está disponible.");
        var nombre = (n.Nombre ?? "").Trim();
        if (nombre.Length == 0) nombre = $"{p.Titulo} – {Fechas.Texto(reloj.Hoy)}";
        if (nombre.Length > 120) nombre = nombre[..120];
        var r = new ReporteGuardado { Nombre = nombre, Plantilla = p.Titulo, Categoria = p.Categoria, Creado = reloj.Hoy, CreadoPor = User.Nombre() };
        db.Reportes.Add(r);
        auditoria.Registrar($"Reporte · {nombre}", "Crear", actual: p.Titulo);
        await db.SaveChangesAsync();
        return Dto(r);
    }

    [HttpPost("{id:int}/duplicar")]
    public async Task<ActionResult<ReporteDto>> Duplicar(int id)
    {
        var r = await db.Reportes.Buscar(id);
        if (r is null) return NotFound();
        var nombre = $"{r.Nombre} (copia)";
        var c = new ReporteGuardado { Nombre = nombre.Length > 120 ? nombre[..120] : nombre, Plantilla = r.Plantilla, Categoria = r.Categoria, Creado = reloj.Hoy, CreadoPor = User.Nombre() };
        db.Reportes.Add(c);
        auditoria.Registrar($"Reporte · {c.Nombre}", "Crear", actual: $"Copia de “{r.Nombre}”");
        await db.SaveChangesAsync();
        return Dto(c);
    }

    [HttpPost("eliminar")]
    public async Task<IActionResult> Eliminar(IdsReportes p)
    {
        var ids = p.Ids ?? [];
        var reps = await db.Reportes.Where(r => ids.Contains(r.Id)).ToListAsync();
        if (reps.Count == 0) return Error("ids", "Selecciona al menos un reporte.");
        foreach (var r in reps) auditoria.Registrar($"Reporte · {r.Nombre}", "Eliminar");
        db.Reportes.RemoveRange(reps);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Genera el CSV del reporte guardado y registra la ejecución.</summary>
    [HttpGet("{id:int}/archivo")]
    public async Task<IActionResult> Archivo(int id, [FromQuery] string? periodo)
    {
        var r = await db.Reportes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        return await Generar(r.Nombre, r.Plantilla, periodo);
    }

    /// <summary>Vuelve a generar una ejecución del historial (con los datos actuales y el mismo período).</summary>
    [HttpGet("historial/{id:int}/archivo")]
    public async Task<IActionResult> ArchivoHistorial(int id)
    {
        var e = await db.EjecucionesReporte.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        return await Generar(e.Reporte, e.Plantilla, e.Periodo is { } p ? Planilla.Texto(p) : null);
    }

    private async Task<IActionResult> Generar(string nombre, string plantilla, string? periodoTexto)
    {
        var p = GeneradorReportes.Plantilla(plantilla);
        if (p is null || !p.Generable) return Error("plantilla", $"El reporte “{plantilla}” todavía no se puede generar en Nexo RH.");
        int? periodo = null;
        if (p.RequierePeriodo)
        {
            if (!Planilla.TryLeer(periodoTexto, out var per)) return Error("periodo", "Elige el período (MM/AAAA).");
            if (p.Titulo != "Ítems" && !await db.Boletas.AnyAsync(b => b.Periodo == per))
                return Error("periodo", $"El período {Planilla.Texto(per)} no tiene boletas calculadas.");
            periodo = per;
        }
        var tabla = await generador.Generar(p, periodo);
        db.EjecucionesReporte.Add(new EjecucionReporte { Reporte = nombre, Plantilla = p.Titulo, Periodo = periodo, Usuario = User.Nombre(), Filas = tabla.Filas.Count });
        await db.SaveChangesAsync();
        var archivo = Slug().Replace(nombre.ToLowerInvariant(), "-").Trim('-') + (periodo is { } x ? $"-{x}" : "") + ".csv";
        return File(GeneradorReportes.Csv(tabla), "text/csv; charset=utf-8", archivo);
    }

    [GeneratedRegex(@"[^a-z0-9áéíóúñü]+")] private static partial Regex Slug();

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
