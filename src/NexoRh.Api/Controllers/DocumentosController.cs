using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record DocumentoDto(int Id, int ColaboradorId, string Colaborador, string Empresa, string Archivo, string Fecha, string Revision, string FirmaColaborador, string FirmaEmpresa, bool Oculto);
public record DocumentoEliminadoDto(int Id, string Colaborador, string Empresa, string Archivo, string Fecha, string Eliminado, string EliminadoPor, string DisponibleHasta);
public record PlantillaDto(int Id, string Nombre, string Tipo, string Carpeta, bool Activa);
public record DatosDocumentos(List<DocumentoDto> Documentos, List<DocumentoEliminadoDto> Papelera, List<PlantillaDto> Plantillas);
public record IdsDocumentos(List<int>? Ids);
public record OcultarDocumentos(List<int>? Ids, bool Oculto);
public record EstadoPlantilla(bool Activa);
public record ResultadoLote(int Procesados, int Omitidos);

/// <summary>
/// Documentos y Firma: plantillas (activar o desactivar), documentos de los colaboradores con su circuito de revisión y
/// firmas, visibilidad para el colaborador y papelera (60 días). La firma de la empresa la registra el administrador.
/// </summary>
[ApiController]
[Route("api/documentos")]
[Authorize(Policy = Politicas.Administrador)]
public class DocumentosController(NexoDbContext db, Auditoria auditoria, RelojAsistencia reloj) : ControllerBase
{
    public const int DiasPapelera = 60;

    [HttpGet]
    public async Task<DatosDocumentos> Datos()
    {
        var limite = reloj.Hoy.ToDateTime(TimeOnly.MinValue).AddDays(-DiasPapelera);
        // Los documentos que superaron los 60 días en la papelera ya no se muestran ni se pueden restaurar.
        var vencidos = await db.Documentos.Where(d => d.EliminadoEn != null && d.EliminadoEn < limite).ToListAsync();
        if (vencidos.Count > 0) { db.Documentos.RemoveRange(vencidos); await db.SaveChangesAsync(); }

        var cols = await db.Colaboradores.AsNoTracking().ToDictionaryAsync(c => c.Id, c => $"{c.Apellidos}, {c.Nombres}");
        var docs = await db.Documentos.AsNoTracking().OrderByDescending(d => d.Fecha).ThenByDescending(d => d.Id).ToListAsync();
        string N(int id) => cols.TryGetValue(id, out var n) ? n : "—";
        return new DatosDocumentos(
            docs.Where(d => d.EliminadoEn is null).Select(d => new DocumentoDto(d.Id, d.ColaboradorId, N(d.ColaboradorId), SemillaSst.Empresa, d.Archivo,
                Fechas.Texto(d.Fecha), d.Revision, d.FirmaColaborador, d.FirmaEmpresa, d.Oculto)).ToList(),
            docs.Where(d => d.EliminadoEn is not null).OrderByDescending(d => d.EliminadoEn).Select(d => new DocumentoEliminadoDto(d.Id, N(d.ColaboradorId), SemillaSst.Empresa,
                d.Archivo, Fechas.Texto(d.Fecha), Fechas.TextoHora(d.EliminadoEn!.Value)[..10], d.EliminadoPor,
                Fechas.TextoHora(d.EliminadoEn!.Value.AddDays(DiasPapelera))[..10])).ToList(),
            (await db.PlantillasDocumento.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync()).Select(p => new PlantillaDto(p.Id, p.Nombre, p.Tipo, p.Carpeta, p.Activa)).ToList());
    }

    [HttpPost("plantillas/{id:int}/estado")]
    public async Task<IActionResult> EstadoDePlantilla(int id, EstadoPlantilla p)
    {
        var x = await db.PlantillasDocumento.FindAsync(id);
        if (x is null) return NotFound();
        if (x.Activa != p.Activa)
        {
            auditoria.Registrar($"Plantilla de documento · {x.Nombre}", "Modificar", propiedad: "estado", anterior: x.Activa ? "Activa" : "Inactiva", actual: p.Activa ? "Activa" : "Inactiva");
            x.Activa = p.Activa;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    /// <summary>Firma de la empresa: solo documentos que la esperan y cuya revisión no está pendiente.</summary>
    [HttpPost("firmar")]
    public async Task<ActionResult<ResultadoLote>> Firmar(IdsDocumentos p) =>
        await Lote(p.Ids, d => d.FirmaEmpresa == "no" && d.Revision != "no", d => d.FirmaEmpresa = "si", "firma de la empresa", "Firmado");

    [HttpPost("revisar")]
    public async Task<ActionResult<ResultadoLote>> Revisar(IdsDocumentos p) =>
        await Lote(p.Ids, d => d.Revision == "no", d => d.Revision = "si", "revisión", "Revisado");

    [HttpPost("ocultar")]
    public async Task<ActionResult<ResultadoLote>> Ocultar(OcultarDocumentos p) =>
        await Lote(p.Ids, d => d.Oculto != p.Oculto, d => d.Oculto = p.Oculto, "visibilidad", p.Oculto ? "Oculto al colaborador" : "Visible para el colaborador");

    /// <summary>Envía los documentos a la papelera (se pueden restaurar durante 60 días).</summary>
    [HttpPost("eliminar")]
    public async Task<ActionResult<ResultadoLote>> Eliminar(IdsDocumentos p) =>
        await Lote(p.Ids, _ => true, d => { d.EliminadoEn = DateTime.UtcNow; d.EliminadoPor = User.Nombre(); }, "papelera", "Enviado a la papelera");

    [HttpPost("papelera/{id:int}/restaurar")]
    public async Task<IActionResult> Restaurar(int id)
    {
        var d = await db.Documentos.FirstOrDefaultAsync(x => x.Id == id && x.EliminadoEn != null);
        if (d is null) return NotFound();
        d.EliminadoEn = null;
        d.EliminadoPor = "";
        auditoria.Registrar($"Documento · {d.Archivo}", "Modificar", await db.Colaboradores.FindAsync(d.ColaboradorId), "papelera", "En la papelera", "Restaurado");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("papelera/{id:int}")]
    public async Task<IActionResult> EliminarDefinitivo(int id)
    {
        var d = await db.Documentos.FirstOrDefaultAsync(x => x.Id == id && x.EliminadoEn != null);
        if (d is null) return NotFound();
        db.Documentos.Remove(d);
        auditoria.Registrar($"Documento · {d.Archivo}", "Eliminar", await db.Colaboradores.FindAsync(d.ColaboradorId), "papelera", "En la papelera", "Eliminado definitivamente");
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult<ResultadoLote>> Lote(List<int>? ids, Func<DocumentoColaborador, bool> aplica, Action<DocumentoColaborador> cambio, string propiedad, string actual)
    {
        var lista = (ids ?? []).Distinct().ToList();
        if (lista.Count == 0)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["ids"] = ["Selecciona al menos un documento."] }) { Title = "Selecciona al menos un documento." });
        var docs = await db.Documentos.Where(d => lista.Contains(d.Id) && d.EliminadoEn == null).ToListAsync();
        var cols = await db.Colaboradores.Where(c => docs.Select(d => d.ColaboradorId).Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var hechos = 0;
        foreach (var d in docs.Where(aplica))
        {
            cambio(d);
            auditoria.Registrar($"Documento · {d.Archivo}", "Modificar", cols.GetValueOrDefault(d.ColaboradorId), propiedad, "—", actual);
            hechos++;
        }
        await db.SaveChangesAsync();
        return new ResultadoLote(hechos, lista.Count - hechos);
    }
}
