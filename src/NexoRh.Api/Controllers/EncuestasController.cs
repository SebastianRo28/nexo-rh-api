using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record EncuestaDto(int Id, string Nombre, string Tipo, string Descripcion, bool Anonima, string Creada, string Inicio, string Termino,
    int Participantes, int Respondidas, int Avance, string Estado, int Preguntas);
public record EncuestaNueva(string? Nombre, string? Tipo, string? Plantilla, string? Inicio, string? Termino, bool? Anonima, string? Descripcion, List<int>? Destinatarios);
public record PreguntaDto(string Id, string Texto, string Tipo, List<string> Opciones, bool Obligatoria);
public record MiEncuesta(int Id, string Nombre, string Descripcion, string Termino, bool Anonima, List<PreguntaDto> Preguntas, string? Respondida, string Estado);
public record Respuestas(Dictionary<string, string>? Valores);
public record ResultadoPregunta(string Id, string Texto, string Tipo, decimal? Promedio, Dictionary<string, int> Conteo, List<string> Comentarios);
public record ResultadosEncuesta(EncuestaDto Encuesta, bool Ocultos, int Minimo, List<ResultadoPregunta> Preguntas);

/// <summary>
/// Encuestas de clima y libres. Las administra RR.HH.; cada colaborador responde las suyas una sola vez desde Mi Portal.
/// En las anónimas la respuesta no guarda quién respondió y los resultados se ocultan mientras haya menos respuestas que
/// el mínimo configurado (Encuestas › Configuraciones).
/// </summary>
[ApiController]
[Route("api/encuestas")]
[RequiereModulo(Modulos.Surveys)]
[Authorize]
public class EncuestasController(NexoDbContext db, Auditoria auditoria, Alcance alcance, RelojAsistencia reloj, Configuraciones configuraciones) : ControllerBase
{
    private string Estado(Encuesta e) => e.Cerrada || reloj.Hoy > e.Termino ? "Finalizada" : reloj.Hoy < e.Inicio ? "Pendiente" : "En curso";

    private async Task<EncuestaDto> Dto(Encuesta e, int? totalColaboradores = null, int? respondidas = null)
    {
        var participantes = e.Destinatarios.Count > 0 ? e.Destinatarios.Count : totalColaboradores ?? await db.Colaboradores.CountAsync();
        var n = respondidas ?? await db.ParticipacionesEncuesta.CountAsync(p => p.EncuestaId == e.Id);
        return new EncuestaDto(e.Id, e.Nombre, e.Tipo, e.Descripcion, e.Anonima, Fechas.Texto(e.Creada), Fechas.Texto(e.Inicio), Fechas.Texto(e.Termino),
            participantes, n, participantes == 0 ? 0 : (int)Math.Round(100.0 * n / participantes), Estado(e), e.Preguntas.Count);
    }

    // --- Administración ----------------------------------------------------------------------------------------------
    [HttpGet]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<List<EncuestaDto>> Listar(string? tipo)
    {
        var total = await db.Colaboradores.CountAsync();
        var conteo = await db.ParticipacionesEncuesta.GroupBy(p => p.EncuestaId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var q = db.Encuestas.AsNoTracking();
        if (tipo is not null) q = q.Where(e => e.Tipo == tipo);
        var r = new List<EncuestaDto>();
        foreach (var e in await q.OrderByDescending(e => e.Inicio).ToListAsync()) r.Add(await Dto(e, total, conteo.GetValueOrDefault(e.Id)));
        return r;
    }

    [HttpGet("plantillas")]
    [Authorize(Policy = Politicas.Administrador)]
    public object Plantillas() => new { clima = PlantillasEncuesta.Clima, libre = PlantillasEncuesta.Libres };

    [HttpPost]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<EncuestaDto>> Crear(EncuestaNueva p)
    {
        if (p.Tipo is not ("Clima" or "Libre")) return Error("tipo", "El tipo debe ser Clima o Libre.");
        var plantillas = p.Tipo == "Clima" ? PlantillasEncuesta.Clima : PlantillasEncuesta.Libres;
        if (!plantillas.Contains(p.Plantilla)) return Error("plantilla", "Elige una plantilla de la lista.");
        if (!Fechas.TryLeer(p.Inicio, out var inicio)) return Error("inicio", "La fecha de inicio debe tener el formato DD/MM/AAAA.");
        if (!Fechas.TryLeer(p.Termino, out var termino) || termino < inicio) return Error("termino", "La fecha de término debe ser igual o posterior al inicio.");
        if (termino.DayNumber - inicio.DayNumber > 366) return Error("termino", "Una encuesta puede durar como máximo un año.");
        var nombre = (p.Nombre ?? "").Trim() is { Length: > 0 } n ? n : p.Plantilla == "En blanco" ? "Nueva encuesta" : p.Plantilla!;
        if (nombre.Length > 120) return Error("nombre", "El nombre admite hasta 120 caracteres.");
        if (await db.Encuestas.AnyAsync(e => e.Nombre == nombre && e.Termino >= reloj.Hoy)) return Error("nombre", "Ya hay una encuesta vigente con ese nombre.");
        var dest = (p.Destinatarios ?? []).Distinct().ToList();
        if (dest.Count > 0 && await db.Colaboradores.CountAsync(c => dest.Contains(c.Id)) != dest.Count) return Error("destinatarios", "Algún colaborador no existe.");
        var e = new Encuesta
        {
            Nombre = nombre, Tipo = p.Tipo!, Anonima = p.Anonima ?? p.Tipo == "Clima", Inicio = inicio, Termino = termino, Creada = Fechas.HoyLima(),
            CreadaPor = User.Nombre(), Preguntas = PlantillasEncuesta.Preguntas(p.Plantilla!), Destinatarios = dest, Descripcion = (p.Descripcion ?? "").Trim(),
        };
        if (e.Descripcion.Length > 500) e.Descripcion = e.Descripcion[..500];
        db.Encuestas.Add(e);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Encuesta #{e.Id}", "Crear", actual: $"{e.Nombre} ({e.Tipo}, {Fechas.Texto(inicio)} al {Fechas.Texto(termino)}{(e.Anonima ? ", anónima" : "")})");
        await db.SaveChangesAsync();
        return await Dto(e);
    }

    [HttpPost("{id:int}/cerrar")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<EncuestaDto>> Cerrar(int id)
    {
        var e = await db.Encuestas.Buscar(id);
        if (e is null) return NotFound();
        if (Estado(e) == "Finalizada") return Error("id", "La encuesta ya terminó.");
        e.Cerrada = true;
        auditoria.Registrar($"Encuesta #{e.Id}", "Modificar", propiedad: "estado", anterior: "En curso", actual: "Finalizada (cerrada antes del término)");
        await db.SaveChangesAsync();
        return await Dto(e);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var e = await db.Encuestas.Buscar(id);
        if (e is null) return NotFound();
        var n = await db.ParticipacionesEncuesta.CountAsync(p => p.EncuestaId == id);
        if (n > 0) return Error("id", $"No se puede eliminar: ya tiene {n} respuesta(s). Ciérrala en su lugar.");
        db.Encuestas.Remove(e);
        auditoria.Registrar($"Encuesta #{e.Id}", "Eliminar", anterior: e.Nombre);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Resultados agregados. En las anónimas no se muestran si hay menos respuestas que el mínimo.</summary>
    [HttpGet("{id:int}/resultados")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<ResultadosEncuesta>> Resultados(int id)
    {
        var e = await db.Encuestas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        var dto = await Dto(e);
        var minimo = (int)(decimal)(await configuraciones.LeerAsync("encuestas"))["minimo"];
        if (e.Anonima && dto.Respondidas < minimo) return new ResultadosEncuesta(dto, true, minimo, []);
        var resp = await db.RespuestasEncuesta.AsNoTracking().Where(r => r.EncuestaId == id).Select(r => r.Valores).ToListAsync();
        var preguntas = e.Preguntas.Select(p =>
        {
            var valores = resp.Select(v => v.GetValueOrDefault(p.Codigo)).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList();
            var conteo = p.Tipo == "texto" ? [] : p.Opciones.ToDictionary(o => o, o => valores.Count(v => v == o));
            decimal? promedio = p.Tipo == "escala" && valores.Count > 0 ? Math.Round((decimal)valores.Average(v => int.TryParse(v, out var x) ? x : 0), 2) : null;
            // Los comentarios se barajan para no poder relacionarlos con el orden de las respuestas.
            var comentarios = p.Tipo == "texto" ? valores.OrderBy(v => v.GetHashCode()).ToList() : [];
            return new ResultadoPregunta(p.Codigo, p.Texto, p.Tipo, promedio, conteo, comentarios);
        }).ToList();
        return new ResultadosEncuesta(dto, false, minimo, preguntas);
    }

    // --- Mi Portal ---------------------------------------------------------------------------------------------------
    private static bool EsDestinatario(Encuesta e, int colaboradorId) => e.Destinatarios.Count == 0 || e.Destinatarios.Contains(colaboradorId);

    /// <summary>Encuestas del colaborador: las vigentes y las que ya respondió.</summary>
    [HttpGet("mias")]
    public async Task<List<MiEncuesta>> Mias()
    {
        if (alcance.Propio is not { } yo) return [];
        var mias = await db.ParticipacionesEncuesta.AsNoTracking().Where(p => p.ColaboradorId == yo).ToDictionaryAsync(p => p.EncuestaId, p => p.Fecha);
        return (await db.Encuestas.AsNoTracking().OrderBy(e => e.Termino).ToListAsync())
            .Where(e => EsDestinatario(e, yo) && (mias.ContainsKey(e.Id) || Estado(e) == "En curso"))
            .Select(e => new MiEncuesta(e.Id, e.Nombre, e.Descripcion, Fechas.Texto(e.Termino), e.Anonima,
                e.Preguntas.Select(q => new PreguntaDto(q.Codigo, q.Texto, q.Tipo, [.. q.Opciones], q.Obligatoria)).ToList(),
                mias.TryGetValue(e.Id, out var f) ? Fechas.Texto(f) : null, Estado(e))).ToList();
    }

    [HttpPost("{id:int}/responder")]
    public async Task<IActionResult> Responder(int id, Respuestas p)
    {
        if (alcance.Propio is not { } yo) return Error("id", "Tu usuario no está vinculado a un colaborador.");
        var e = await db.Encuestas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null || !EsDestinatario(e, yo)) return NotFound();
        if (Estado(e) != "En curso") return Error("id", "La encuesta no está abierta.");
        if (await db.ParticipacionesEncuesta.AnyAsync(x => x.EncuestaId == id && x.ColaboradorId == yo)) return Error("id", "Ya respondiste esta encuesta.");
        var entrada = p.Valores ?? [];
        var errores = new Dictionary<string, string[]>();
        var limpias = new Dictionary<string, string>();
        foreach (var k in entrada.Keys.Where(k => e.Preguntas.All(q => q.Codigo != k))) errores[k] = ["Pregunta desconocida."];
        foreach (var q in e.Preguntas)
        {
            var v = (entrada.GetValueOrDefault(q.Codigo) ?? "").Trim();
            if (v.Length == 0) { if (q.Obligatoria) errores[q.Codigo] = ["Responde esta pregunta."]; continue; }
            if (q.Tipo != "texto" && !q.Opciones.Contains(v)) { errores[q.Codigo] = ["Opción no válida."]; continue; }
            if (v.Length > 500) { errores[q.Codigo] = ["Máximo 500 caracteres."]; continue; }
            limpias[q.Codigo] = v;
        }
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores) { Title = "Revisa tus respuestas." });
        db.ParticipacionesEncuesta.Add(new ParticipacionEncuesta { EncuestaId = id, ColaboradorId = yo, Fecha = reloj.Hoy });
        db.RespuestasEncuesta.Add(new RespuestaEncuesta { EncuestaId = id, ColaboradorId = e.Anonima ? null : yo, Valores = limpias });
        await db.SaveChangesAsync();
        return NoContent();
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
