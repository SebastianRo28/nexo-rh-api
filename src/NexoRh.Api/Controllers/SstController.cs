using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record CumplimientoSstDto(int Id, string Nombre, string Normativa, string Categoria, string Descripcion, int[] Colaboradores, int[] Empresas, bool Cumple, string Actualizado);
public record RequisitoSstDto(int Id, string Nombre, int CumplimientoId, string Cumplimiento, string AplicaA, int? VigenciaMeses, int Asignaciones, int SinEvidencia,
    int Vigentes, int PorVencer, int Vencidas, string Actualizado);
public record AsignacionSstDto(int Id, int RequisitoId, string Requisito, string Cumplimiento, string AplicaA, int? ColaboradorId, string Titular, string? Documento,
    string? Cargo, string? Area, string Estado, string? Vence, bool Finalizada, string Actualizado);
public record EvidenciaSstDto(int Id, string Fecha, string? Vence, string Descripcion, string RegistradaPor, string Registrada, string? Archivo, int? Tamano);
public record AsignacionDetalle(AsignacionSstDto Asignacion, List<EvidenciaSstDto> Evidencias);
public record BibliotecaSstDto(string Nombre, string Normativa, string Categoria, bool Agregado);
public record DatosSst(List<CumplimientoSstDto> Cumplimientos, List<RequisitoSstDto> Requisitos, List<AsignacionSstDto> Asignaciones, List<BibliotecaSstDto> Biblioteca, List<string> Empresas);
public record CumplimientoNuevo(string? Nombre, string? Normativa, string? Categoria, string? Descripcion);
public record AgregarBiblioteca(List<string>? Nombres);
public record RequisitoNuevo(string? Nombre, int CumplimientoId, string? AplicaA, int? VigenciaMeses, bool Todos, List<int>? ColaboradorIds, List<string>? Empresas);
public record Asignar(List<int>? ColaboradorIds, List<string>? Empresas, bool Todos);

/// <summary>
/// SST › Registro de evidencias: cumplimientos, requisitos, asignaciones y evidencias (con archivo opcional). El estado
/// de cada asignación sale de su última evidencia: Sin evidencia, Vigente, Por vencer (30 días o menos) o Vencida.
/// Un cumplimiento "cumple" cuando todas sus asignaciones activas tienen evidencia vigente. Solo administradores.
/// </summary>
[ApiController]
[Route("api/sst")]
[Authorize(Policy = Politicas.Administrador)]
public class SstController(NexoDbContext db, Auditoria auditoria, RelojAsistencia reloj) : ControllerBase
{
    private const int DiasPorVencer = 30;
    private const int MaxArchivo = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> TiposArchivo = new() { [".pdf"] = "application/pdf", [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg" };

    private string Estado(EvidenciaSst? ultima)
    {
        if (ultima is null) return "Sin evidencia";
        if (ultima.Vence is not { } v) return "Vigente";
        if (v < reloj.Hoy) return "Vencida";
        return v.DayNumber - reloj.Hoy.DayNumber <= DiasPorVencer ? "Por vencer" : "Vigente";
    }
    private static string T(DateTime utc) => Fechas.TextoHora(utc)[..10];

    private async Task<List<AsignacionSstDto>> Asignaciones(int? soloId = null)
    {
        var requisitos = await db.RequisitosSst.AsNoTracking().ToDictionaryAsync(r => r.Id);
        var cumplimientos = await db.CumplimientosSst.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Nombre);
        var cols = await db.Colaboradores.AsNoTracking().ToDictionaryAsync(c => c.Id);
        var q = db.AsignacionesSst.AsNoTracking();
        if (soloId is { } id) q = q.Where(a => a.Id == id);
        var asignaciones = await q.ToListAsync();
        var ids = asignaciones.Select(a => a.Id).ToList();
        var ultimas = (await db.EvidenciasSst.AsNoTracking().Where(e => ids.Contains(e.AsignacionId)).ToListAsync())
            .GroupBy(e => e.AsignacionId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Fecha).ThenByDescending(e => e.Id).First());
        return asignaciones.Select(a =>
        {
            var r = requisitos[a.RequisitoId];
            var c = a.ColaboradorId is { } cid && cols.TryGetValue(cid, out var x) ? x : null;
            ultimas.TryGetValue(a.Id, out var u);
            return new AsignacionSstDto(a.Id, r.Id, r.Nombre, cumplimientos[r.CumplimientoId], r.AplicaA, a.ColaboradorId,
                c is null ? a.Empresa ?? "—" : $"{c.Apellidos}, {c.Nombres}", c?.Documento, c?.Cargo, c?.Area, Estado(u), u?.Vence is { } v ? Fechas.Texto(v) : null,
                a.Finalizada, T(u?.RegistradaEn ?? a.CreadaEn));
        }).OrderBy(a => a.Requisito).ThenBy(a => a.Titular).ToList();
    }

    // --- Lectura -----------------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<DatosSst> Datos()
    {
        var asig = await Asignaciones();
        var activas = asig.Where(a => !a.Finalizada).ToList();
        bool Ok(AsignacionSstDto a) => a.Estado is "Vigente" or "Por vencer";
        var requisitos = (await db.RequisitosSst.AsNoTracking().ToListAsync());
        var cumplimientos = await db.CumplimientosSst.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync();
        var reqDto = requisitos.Select(r =>
        {
            var l = activas.Where(a => a.RequisitoId == r.Id).ToList();
            return new RequisitoSstDto(r.Id, r.Nombre, r.CumplimientoId, cumplimientos.First(c => c.Id == r.CumplimientoId).Nombre, r.AplicaA, r.VigenciaMeses, l.Count,
                l.Count(a => a.Estado == "Sin evidencia"), l.Count(a => a.Estado == "Vigente"), l.Count(a => a.Estado == "Por vencer"), l.Count(a => a.Estado == "Vencida"), T(r.ActualizadoEn));
        }).OrderBy(r => r.Nombre).ToList();
        var cuDto = cumplimientos.Select(c =>
        {
            var l = activas.Where(a => requisitos.Any(r => r.Id == a.RequisitoId && r.CumplimientoId == c.Id)).ToList();
            var col = l.Where(a => a.AplicaA == "Colaboradores").ToList();
            var emp = l.Where(a => a.AplicaA == "Empresas").ToList();
            return new CumplimientoSstDto(c.Id, c.Nombre, c.Normativa, c.Categoria, c.Descripcion, [col.Count(Ok), col.Count], [emp.Count(Ok), emp.Count],
                l.Count > 0 && l.All(Ok), T(c.ActualizadoEn));
        }).ToList();
        var nombres = cumplimientos.Select(c => c.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var empresas = (await db.FilasCatalogo.AsNoTracking().Where(f => f.Clave == "empresas").ToListAsync())
            .Select(f => JsonNode.Parse(f.Datos)!.AsObject()).Where(o => o["activo"]?.GetValue<bool>() != false).Select(o => o["razon"]?.ToString() ?? "").Where(s => s.Length > 0).ToList();
        if (empresas.Count == 0) empresas = [SemillaSst.Empresa];
        return new DatosSst(cuDto, reqDto, asig, SemillaSst.Biblioteca.Select(b => new BibliotecaSstDto(b.Nombre, b.Normativa, b.Categoria, nombres.Contains(b.Nombre))).ToList(), empresas);
    }

    [HttpGet("asignaciones/{id:int}")]
    public async Task<ActionResult<AsignacionDetalle>> Asignacion(int id)
    {
        var a = (await Asignaciones(id)).FirstOrDefault();
        if (a is null) return NotFound();
        var ev = await db.EvidenciasSst.AsNoTracking().Where(e => e.AsignacionId == id).OrderByDescending(e => e.Fecha).ThenByDescending(e => e.Id).ToListAsync();
        return new AsignacionDetalle(a, ev.Select(e => new EvidenciaSstDto(e.Id, Fechas.Texto(e.Fecha), e.Vence is { } v ? Fechas.Texto(v) : null, e.Descripcion,
            e.RegistradaPor, T(e.RegistradaEn), e.ArchivoNombre, e.ArchivoTamano)).ToList());
    }

    [HttpGet("evidencias/{id:int}/archivo")]
    public async Task<IActionResult> Archivo(int id)
    {
        var e = await db.EvidenciasSst.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        var a = await db.ArchivosEvidenciaSst.AsNoTracking().FirstOrDefaultAsync(x => x.EvidenciaId == id);
        if (e is null || a is null) return NotFound();
        return File(a.Contenido, e.ArchivoTipo ?? "application/octet-stream", e.ArchivoNombre);
    }

    // --- Cumplimientos -----------------------------------------------------------------------------------------------
    [HttpPost("cumplimientos")]
    public async Task<IActionResult> CrearCumplimiento(CumplimientoNuevo p)
    {
        var nombre = (p.Nombre ?? "").Trim();
        if (nombre.Length is 0 or > 120) return Error("nombre", "Ingresa el nombre del cumplimiento (máximo 120 caracteres).");
        if (await db.CumplimientosSst.AnyAsync(c => c.Nombre == nombre)) return Error("nombre", "Ya existe un cumplimiento con ese nombre.");
        var c = new CumplimientoSst
        {
            Nombre = nombre, Normativa = Corto(p.Normativa, 80), Categoria = Corto(p.Categoria, 80),
            Descripcion = Corto(p.Descripcion, 500) is { Length: > 0 } d ? d : $"Obligaciones de {nombre.ToLowerInvariant()} según la normativa vigente.",
        };
        if (c.Normativa.Length == 0) return Error("normativa", "Indica la normativa (p. ej. Ley N° 29783).");
        db.CumplimientosSst.Add(c);
        auditoria.Registrar($"Cumplimiento SST · {c.Nombre}", "Crear", actual: c.Normativa);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("cumplimientos/biblioteca")]
    public async Task<ActionResult<int>> DesdeBiblioteca(AgregarBiblioteca p)
    {
        var existentes = (await db.CumplimientosSst.Select(c => c.Nombre).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nuevos = SemillaSst.Biblioteca.Where(b => (p.Nombres ?? []).Contains(b.Nombre) && !existentes.Contains(b.Nombre)).ToList();
        if (nuevos.Count == 0) return Error("nombres", "Elige cumplimientos de la biblioteca que aún no hayas agregado.");
        foreach (var b in nuevos)
        {
            db.CumplimientosSst.Add(new CumplimientoSst { Nombre = b.Nombre, Normativa = b.Normativa, Categoria = b.Categoria, Descripcion = $"Obligaciones de {b.Nombre.ToLowerInvariant()} según la normativa vigente." });
            auditoria.Registrar($"Cumplimiento SST · {b.Nombre}", "Crear", actual: $"Desde la biblioteca · {b.Normativa}");
        }
        await db.SaveChangesAsync();
        return nuevos.Count;
    }

    [HttpDelete("cumplimientos/{id:int}")]
    public async Task<IActionResult> EliminarCumplimiento(int id)
    {
        var c = await db.CumplimientosSst.FindAsync(id);
        if (c is null) return NotFound();
        if (await db.RequisitosSst.AnyAsync(r => r.CumplimientoId == id)) return Error("id", $"“{c.Nombre}” tiene requisitos: elimínalos primero.");
        db.CumplimientosSst.Remove(c);
        auditoria.Registrar($"Cumplimiento SST · {c.Nombre}", "Eliminar", anterior: c.Normativa);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Requisitos y asignaciones -----------------------------------------------------------------------------------
    [HttpPost("requisitos")]
    public async Task<IActionResult> CrearRequisito(RequisitoNuevo p)
    {
        var nombre = (p.Nombre ?? "").Trim();
        if (nombre.Length is 0 or > 120) return Error("nombre", "Ingresa el nombre del requisito (máximo 120 caracteres).");
        if (await db.RequisitosSst.AnyAsync(r => r.Nombre == nombre)) return Error("nombre", "Ya existe un requisito con ese nombre.");
        if (!await db.CumplimientosSst.AnyAsync(c => c.Id == p.CumplimientoId)) return Error("cumplimientoId", "Elige el cumplimiento asociado.");
        if (p.AplicaA is not ("Colaboradores" or "Empresas")) return Error("aplicaA", "Indica si aplica a colaboradores o a empresas.");
        if (p.VigenciaMeses is < 1 or > 120) return Error("vigenciaMeses", "La vigencia debe estar entre 1 y 120 meses (o vacía si no vence).");
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = new RequisitoSst { Nombre = nombre, CumplimientoId = p.CumplimientoId, AplicaA = p.AplicaA!, VigenciaMeses = p.VigenciaMeses };
        db.RequisitosSst.Add(r);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Requisito SST · {r.Nombre}", "Crear", actual: $"{r.AplicaA}{(r.VigenciaMeses is { } m ? $" · vigencia {m} meses" : " · no vence")}");
        if (await AgregarAsignaciones(r, p.Todos, p.ColaboradorIds, p.Empresas) is { } err) return err;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpDelete("requisitos/{id:int}")]
    public async Task<IActionResult> EliminarRequisito(int id)
    {
        var r = await db.RequisitosSst.FindAsync(id);
        if (r is null) return NotFound();
        var asig = await db.AsignacionesSst.Where(a => a.RequisitoId == id).Select(a => a.Id).ToListAsync();
        if (await db.EvidenciasSst.AnyAsync(e => asig.Contains(e.AsignacionId))) return Error("id", $"“{r.Nombre}” ya tiene evidencias registradas: finaliza sus asignaciones en lugar de eliminarlo.");
        db.RequisitosSst.Remove(r);
        auditoria.Registrar($"Requisito SST · {r.Nombre}", "Eliminar", anterior: $"{asig.Count} asignación(es)");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("requisitos/{id:int}/asignaciones")]
    public async Task<IActionResult> AsignarRequisito(int id, Asignar p)
    {
        var r = await db.RequisitosSst.FindAsync(id);
        if (r is null) return NotFound();
        if (await AgregarAsignaciones(r, p.Todos, p.ColaboradorIds, p.Empresas) is { } err) return err;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Asigna a colaboradores (o a todos) o a empresas; quien ya lo tiene activo no se duplica.</summary>
    private async Task<IActionResult?> AgregarAsignaciones(RequisitoSst r, bool todos, List<int>? colaboradorIds, List<string>? empresas)
    {
        var activas = await db.AsignacionesSst.Where(a => a.RequisitoId == r.Id && !a.Finalizada).ToListAsync();
        var n = 0;
        if (r.AplicaA == "Colaboradores")
        {
            var ids = todos ? await db.Colaboradores.Select(c => c.Id).ToListAsync() : (colaboradorIds ?? []).Distinct().ToList();
            if (ids.Count > 0 && await db.Colaboradores.CountAsync(c => ids.Contains(c.Id)) != ids.Count) return Error("colaboradorIds", "Algún colaborador no existe.");
            foreach (var cid in ids.Where(i => activas.All(a => a.ColaboradorId != i))) { db.AsignacionesSst.Add(new AsignacionSst { RequisitoId = r.Id, ColaboradorId = cid }); n++; }
        }
        else
        {
            foreach (var e in (empresas ?? []).Select(x => x.Trim()).Where(x => x.Length is > 0 and <= 120).Distinct().Where(x => activas.All(a => a.Empresa != x)))
            { db.AsignacionesSst.Add(new AsignacionSst { RequisitoId = r.Id, Empresa = e }); n++; }
        }
        if (n > 0) auditoria.Registrar($"Requisito SST · {r.Nombre}", "Modificar", propiedad: "asignaciones", actual: $"+{n}");
        return null;
    }

    [HttpPost("asignaciones/{id:int}/finalizar")]
    public async Task<IActionResult> Finalizar(int id)
    {
        var a = await db.AsignacionesSst.FindAsync(id);
        if (a is null) return NotFound();
        if (a.Finalizada) return Error("id", "La asignación ya está finalizada.");
        a.Finalizada = true;
        var r = await db.RequisitosSst.FindAsync(a.RequisitoId);
        var c = a.ColaboradorId is { } cid ? await db.Colaboradores.FindAsync(cid) : null;
        auditoria.Registrar($"Asignación SST · {r!.Nombre}", "Modificar", c, "estado", "Activa", "Finalizada");
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Registra una evidencia (multipart: fecha, descripcion y archivo opcional PDF/JPG/PNG de hasta 5 MB).</summary>
    [HttpPost("asignaciones/{id:int}/evidencias")]
    [RequestSizeLimit(MaxArchivo + 64 * 1024)]
    public async Task<ActionResult<AsignacionDetalle>> Evidencia(int id, [FromForm] string? fecha, [FromForm] string? descripcion, IFormFile? archivo)
    {
        var a = await db.AsignacionesSst.FindAsync(id);
        if (a is null) return NotFound();
        if (a.Finalizada) return Error("id", "La asignación está finalizada.");
        var r = (await db.RequisitosSst.FindAsync(a.RequisitoId))!;
        if (!Fechas.TryLeer(fecha, out var f)) return Error("fecha", "La fecha debe tener el formato DD/MM/AAAA.");
        if (f > reloj.Hoy) return Error("fecha", "La fecha de la evidencia no puede ser futura.");
        var desc = Corto(descripcion, 500);
        if (desc.Length == 0) return Error("descripcion", "Describe la evidencia (p. ej. N° de acta o certificado).");
        byte[]? contenido = null;
        string? tipo = null;
        if (archivo is not null)
        {
            if (archivo.Length is 0 or > MaxArchivo) return Error("archivo", "El archivo debe pesar como máximo 5 MB.");
            var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!TiposArchivo.TryGetValue(ext, out tipo)) return Error("archivo", "Solo se aceptan archivos PDF, JPG o PNG.");
            using var ms = new MemoryStream();
            await archivo.CopyToAsync(ms);
            contenido = ms.ToArray();
            if (!FirmaValida(contenido, tipo)) return Error("archivo", "El contenido del archivo no corresponde a su extensión.");
        }
        var e = new EvidenciaSst
        {
            AsignacionId = id, Fecha = f, Vence = r.VigenciaMeses is { } m ? f.AddMonths(m) : null, Descripcion = desc, RegistradaPor = User.Nombre(),
            ArchivoNombre = archivo is null ? null : Path.GetFileName(archivo.FileName)[..Math.Min(200, Path.GetFileName(archivo.FileName).Length)], ArchivoTipo = tipo,
            ArchivoTamano = contenido?.Length,
        };
        db.EvidenciasSst.Add(e);
        await db.SaveChangesAsync();
        if (contenido is not null) db.ArchivosEvidenciaSst.Add(new ArchivoEvidenciaSst { EvidenciaId = e.Id, Contenido = contenido });
        var c = a.ColaboradorId is { } cid ? await db.Colaboradores.FindAsync(cid) : null;
        auditoria.Registrar($"Evidencia SST · {r.Nombre}", "Crear", c, actual: $"{desc} · {Fechas.Texto(f)}{(e.Vence is { } v ? $" · vence {Fechas.Texto(v)}" : "")}{(contenido is null ? "" : " · con archivo")}");
        await db.SaveChangesAsync();
        return await Asignacion(id);
    }

    /// <summary>Comprueba los primeros bytes del archivo (PDF, PNG o JPEG).</summary>
    private static bool FirmaValida(byte[] b, string tipo) => tipo switch
    {
        "application/pdf" => b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46,
        "image/png" => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        "image/jpeg" => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        _ => false,
    };

    private static string Corto(string? s, int n) { var x = (s ?? "").Trim(); return x.Length > n ? x[..n] : x; }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
