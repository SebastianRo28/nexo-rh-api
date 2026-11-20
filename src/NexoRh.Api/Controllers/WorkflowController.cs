using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record SolicitudWfDto(int Id, string Titulo, string Estado, string Tipo, string Solicitante, string Afectado, int? AfectadoId, string Documento,
    int Progreso, string Creada, string Actualizada, string Detalle, decimal? Monto, string Comentario, string ResueltaPor, string? Proceso, string? Plantilla,
    bool EsMia, bool PuedoResolver);
public record SolicitudPortal(string? Proceso, string? Valor);
public record SolicitudDesdePlantilla(int PlantillaId, int? AfectadoId, string? Detalle);
public record ResolverWf(bool Aprobar, string? Comentario);
public record ResolucionWf(SolicitudWfDto Solicitud, int? ItemId);
public record PlantillaWfDto(int Id, string Nombre, string Tipo, string Estado, int Operaciones, string Actualizada, int Solicitudes);
public record EstadoPlantillaWf(bool Activa);

/// <summary>
/// Workflow: plantillas y solicitudes. Administradores ven todas; supervisores, las suyas y las de su equipo;
/// colaboradores, las propias. Resuelve RR.HH. o la jefatura del colaborador afectado, nunca quien la pidió.
/// Aprobar un adelanto de sueldo crea el descuento ADE-SUE en el período abierto de planilla.
/// </summary>
[ApiController]
[Route("api/workflow")]
[Authorize]
public class WorkflowController(NexoDbContext db, Auditoria auditoria, Alcance alcance, Planilla planilla) : ControllerBase
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Procesos que el colaborador inicia desde Mi Portal (mismos que el frontend).</summary>
    private record Proceso(string Nombre, string Tipo, string Campo, string[]? Opciones);
    private static readonly Dictionary<string, Proceso> Procesos = new()
    {
        ["constancia"] = new("Constancia de trabajo", "Documento", "opcion", ["A quien corresponda", "Entidad bancaria", "Embajada / consulado"]),
        ["quinta"] = new("Certificado de retenciones de 5ta", "Documento", "opcion", ["2025", "2024"]),
        ["adelanto"] = new("Adelanto de sueldo", "Flexible", "monto", null),
        ["datos"] = new("Actualización de datos personales", "Flexible", "texto", null),
        ["activo"] = new("Solicitud de activo", "Flexible", "texto", null),
    };

    private int? YoUsuario => int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    private async Task<bool> Visible(SolicitudWorkflow s) =>
        alcance.EsAdmin || s.SolicitanteUsuarioId == YoUsuario || (s.AfectadoId is { } a && (await alcance.VisiblesAsync()).Contains(a));

    private async Task<bool> PuedeResolver(SolicitudWorkflow s) =>
        s.Estado == "Pendiente" && s.SolicitanteUsuarioId != YoUsuario && (alcance.EsAdmin || (s.AfectadoId is { } a && await alcance.GestionaAsync(a)));

    private async Task<SolicitudWfDto> Dto(SolicitudWorkflow s, Dictionary<int, string> plantillas) => new(s.Id, s.Titulo, s.Estado, s.Tipo, s.Solicitante, s.Afectado,
        s.AfectadoId, s.Documento, s.Progreso, Fechas.Texto(s.Creada), Fechas.Texto(s.Actualizada), s.Detalle, s.Monto, s.Comentario, s.ResueltaPor, s.Proceso,
        s.PlantillaId is { } p && plantillas.TryGetValue(p, out var n) ? n : null, s.SolicitanteUsuarioId == YoUsuario, await PuedeResolver(s));

    private Task<Dictionary<int, string>> NombresPlantillas() => db.PlantillasWorkflow.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Nombre);

    // --- Solicitudes -------------------------------------------------------------------------------------------------
    [HttpGet("solicitudes")]
    public async Task<List<SolicitudWfDto>> Solicitudes()
    {
        var plantillas = await NombresPlantillas();
        var r = new List<SolicitudWfDto>();
        foreach (var s in await db.SolicitudesWorkflow.AsNoTracking().OrderByDescending(s => s.Id).ToListAsync())
            if (await Visible(s)) r.Add(await Dto(s, plantillas));
        return r;
    }

    /// <summary>Solicitud propia desde Mi Portal › Procesos Internos.</summary>
    [HttpPost("solicitudes/portal")]
    public async Task<ActionResult<SolicitudWfDto>> DesdePortal(SolicitudPortal p)
    {
        if (alcance.Propio is not { } yo || await db.Colaboradores.FindAsync(yo) is not { } c) return Error("proceso", "Tu usuario no está vinculado a un colaborador.");
        if (p.Proceso is null || !Procesos.TryGetValue(p.Proceso, out var proc)) return Error("proceso", "Proceso no válido.");
        var valor = (p.Valor ?? "").Trim();
        if (valor.Length == 0) return Error("valor", "Completa este dato para enviar la solicitud.");
        decimal? monto = null;
        string detalle;
        switch (proc.Campo)
        {
            case "opcion":
                if (!proc.Opciones!.Contains(valor)) return Error("valor", "Opción no válida.");
                detalle = valor;
                break;
            case "monto":
                if (!decimal.TryParse(valor.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var m) || m <= 0) return Error("valor", "Ingresa un monto mayor a 0.");
                var max = Math.Round((c.SueldoBase ?? c.SueldoNeto) * 0.5m, 2);
                if (m > max) return Error("valor", $"El adelanto no puede superar S/ {max.ToString("N2", En)} (50 % de tu sueldo base).");
                if (await db.SolicitudesWorkflow.AnyAsync(s => s.AfectadoId == yo && s.Proceso == "adelanto" && s.Estado == "Pendiente"))
                    return Error("valor", "Ya tienes un adelanto pendiente de aprobación.");
                monto = Math.Round(m, 2);
                detalle = $"S/ {monto.Value.ToString("N2", En)}";
                break;
            default:
                if (valor.Length > 300) return Error("valor", "Máximo 300 caracteres.");
                detalle = valor;
                break;
        }
        var hoy = Fechas.HoyLima();
        var nombre = $"{c.Apellidos}, {c.Nombres}";
        var s = new SolicitudWorkflow
        {
            Titulo = $"{proc.Nombre} – {(detalle.Length > 40 ? detalle[..40] + "…" : detalle)}", Tipo = proc.Tipo, Proceso = p.Proceso, SolicitanteUsuarioId = YoUsuario,
            Solicitante = nombre, AfectadoId = c.Id, Afectado = nombre, Documento = c.Documento, Detalle = detalle, Monto = monto, Creada = hoy, Actualizada = hoy,
        };
        db.SolicitudesWorkflow.Add(s);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Solicitud #{s.Id}", "Crear", c, actual: s.Titulo);
        await db.SaveChangesAsync();
        return await Dto(s, await NombresPlantillas());
    }

    /// <summary>Solicitud desde una plantilla activa (RR.HH. o una jefatura, sobre alguien de su equipo).</summary>
    [HttpPost("solicitudes")]
    [Authorize(Policy = Politicas.Gestion)]
    public async Task<ActionResult<SolicitudWfDto>> DesdePlantilla(SolicitudDesdePlantilla p)
    {
        var pl = await db.PlantillasWorkflow.FindAsync(p.PlantillaId);
        if (pl is null || pl.Estado != "Activa") return Error("plantillaId", "La plantilla no existe o no está activa.");
        Colaborador? af = null;
        if (p.AfectadoId is { } aid)
        {
            af = await db.Colaboradores.FindAsync(aid);
            if (af is null) return Error("afectadoId", "El colaborador no existe.");
            if (!await alcance.GestionaAsync(aid)) return Forbid();
        }
        else if (!alcance.EsAdmin) return Error("afectadoId", "Elige al colaborador de tu equipo.");
        var detalle = (p.Detalle ?? "").Trim();
        if (detalle.Length > 300) return Error("detalle", "Máximo 300 caracteres.");
        var hoy = Fechas.HoyLima();
        var nombreAf = af is null ? "—" : $"{af.Apellidos}, {af.Nombres}";
        var s = new SolicitudWorkflow
        {
            Titulo = af is null ? pl.Nombre : $"{pl.Nombre} – {af.Apellidos}", Tipo = pl.Tipo, PlantillaId = pl.Id, SolicitanteUsuarioId = YoUsuario, Solicitante = User.Nombre(),
            AfectadoId = af?.Id, Afectado = nombreAf, Documento = af?.Documento ?? "—", Detalle = detalle, Creada = hoy, Actualizada = hoy,
        };
        db.SolicitudesWorkflow.Add(s);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Solicitud #{s.Id}", "Crear", af, actual: s.Titulo);
        await db.SaveChangesAsync();
        return await Dto(s, await NombresPlantillas());
    }

    [HttpPost("solicitudes/{id:int}/resolver")]
    public async Task<ActionResult<ResolucionWf>> Resolver(int id, ResolverWf p)
    {
        var s = await db.SolicitudesWorkflow.FindAsync(id);
        if (s is null || !await Visible(s)) return NotFound();
        if (s.Estado != "Pendiente") return Error("estado", $"La solicitud ya fue {s.Estado.ToLowerInvariant()}.");
        if (s.SolicitanteUsuarioId == YoUsuario) return Problem("No puedes resolver una solicitud que tú creaste.", statusCode: 403);
        if (!await PuedeResolver(s)) return Problem("No puedes resolver esta solicitud.", statusCode: 403);
        var comentario = (p.Comentario ?? "").Trim();
        if (!p.Aprobar && comentario.Length == 0) return Error("comentario", "Indica el motivo del rechazo.");
        if (comentario.Length > 300) return Error("comentario", "Máximo 300 caracteres.");
        var c = s.AfectadoId is { } a ? await db.Colaboradores.FindAsync(a) : null;

        int? itemId = null;
        if (p.Aprobar && s.Proceso == "adelanto" && s.Monto is { } monto && c is not null)
        {
            if (await planilla.AbiertoAsync() is not { } periodo) return Error("estado", "No hay un período de planilla abierto para descontar el adelanto.");
            var tipo = await db.TiposItem.FirstOrDefaultAsync(t => t.Codigo == "ADE-SUE");
            if (tipo is null) return Error("estado", "Falta el ítem ADE-SUE en el catálogo de remuneraciones.");
            var item = new AsignacionItem { ColaboradorId = c.Id, TipoItemId = tipo.Id, Monto = monto, Desde = periodo, Hasta = periodo, Observacion = $"Solicitud #{s.Id}" };
            db.AsignacionesItem.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
            auditoria.Registrar($"Ítem ADE-SUE #{item.Id}", "Crear", c, actual: $"S/ {monto.ToString("N2", En)} · {Planilla.Texto(periodo)} (solicitud #{s.Id})");
        }
        var anterior = s.Estado;
        s.Estado = p.Aprobar ? "Aprobada" : "Rechazada";
        s.Progreso = 100;
        s.Comentario = comentario;
        s.ResueltaPor = User.Nombre();
        s.Actualizada = Fechas.HoyLima();
        auditoria.Registrar($"Solicitud #{s.Id}", "Modificar", c, "estado", anterior, s.Estado + (comentario.Length > 0 ? $" · {comentario}" : ""));
        await db.SaveChangesAsync();
        return new ResolucionWf(await Dto(s, await NombresPlantillas()), itemId);
    }

    // --- Plantillas --------------------------------------------------------------------------------------------------
    [HttpGet("plantillas")]
    [Authorize(Policy = Politicas.Gestion)]
    public async Task<List<PlantillaWfDto>> Plantillas()
    {
        var usos = await db.SolicitudesWorkflow.Where(s => s.PlantillaId != null).GroupBy(s => s.PlantillaId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return (await db.PlantillasWorkflow.AsNoTracking().OrderByDescending(p => p.Id).ToListAsync())
            .Select(p => new PlantillaWfDto(p.Id, p.Nombre, p.Tipo, p.Estado, p.Operaciones, Fechas.TextoHora(p.ActualizadaEn)[..10], usos.GetValueOrDefault(p.Id))).ToList();
    }

    [HttpPost("plantillas/{id:int}/estado")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<PlantillaWfDto>> EstadoPlantilla(int id, EstadoPlantillaWf p)
    {
        var pl = await db.PlantillasWorkflow.FindAsync(id);
        if (pl is null) return NotFound();
        if (pl.Estado is not ("Activa" or "Inactiva")) return Error("estado", $"Corrige la plantilla antes de activarla ({pl.Estado.ToLowerInvariant()}).");
        var nuevo = p.Activa ? "Activa" : "Inactiva";
        if (pl.Estado != nuevo)
        {
            auditoria.Registrar($"Plantilla de workflow · {pl.Nombre}", "Modificar", propiedad: "estado", anterior: pl.Estado, actual: nuevo);
            pl.Estado = nuevo;
            pl.ActualizadaEn = DateTime.UtcNow;
            pl.ModificadoPor = User.Nombre();
            await db.SaveChangesAsync();
        }
        return (await Plantillas()).First(x => x.Id == id);
    }

    [HttpPost("plantillas/{id:int}/duplicar")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<PlantillaWfDto>> Duplicar(int id)
    {
        var pl = await db.PlantillasWorkflow.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (pl is null) return NotFound();
        var nombre = $"{pl.Nombre} (copia)";
        for (var i = 2; await db.PlantillasWorkflow.AnyAsync(x => x.Nombre == nombre); i++) nombre = $"{pl.Nombre} (copia {i})";
        var copia = new PlantillaWorkflow
        {
            Id = (await db.PlantillasWorkflow.MaxAsync(x => (int?)x.Id) ?? 0) + 1, Nombre = nombre, Tipo = pl.Tipo, Estado = "Inactiva", Operaciones = pl.Operaciones, ModificadoPor = User.Nombre(),
        };
        db.PlantillasWorkflow.Add(copia);
        auditoria.Registrar($"Plantilla de workflow · {copia.Nombre}", "Crear", actual: $"Copia de “{pl.Nombre}” (inactiva)");
        await db.SaveChangesAsync();
        return (await Plantillas()).First(x => x.Id == copia.Id);
    }

    [HttpDelete("plantillas/{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> EliminarPlantilla(int id)
    {
        var pl = await db.PlantillasWorkflow.FindAsync(id);
        if (pl is null) return NotFound();
        if (await db.SolicitudesWorkflow.AnyAsync(s => s.PlantillaId == id && s.Estado == "Pendiente"))
            return Error("id", $"“{pl.Nombre}” tiene solicitudes pendientes: resuélvelas o desactiva la plantilla.");
        db.PlantillasWorkflow.Remove(pl);
        auditoria.Registrar($"Plantilla de workflow · {pl.Nombre}", "Eliminar", anterior: pl.Tipo);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
