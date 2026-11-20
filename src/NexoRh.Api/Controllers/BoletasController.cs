using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>Boletas de pago. Administradores: todas (también los borradores del período abierto). Los demás: solo sus boletas definitivas.</summary>
[ApiController]
[Route("api/remuneraciones/boletas")]
[Authorize]
public class BoletasController(NexoDbContext db, Alcance alcance) : ControllerBase
{
    // -----------------------------------------------------------------------------------------------------
    private static BoletaResumenDto Resumen(Boleta b) => new(b.Id, Planilla.Texto(b.Periodo), b.ColaboradorId, b.Nombre, b.Documento, b.Cargo, b.Regimen,
        b.DiasLaborados, b.DiasVacaciones, b.DiasNoTrabajados, b.Ingresos, b.Descuentos, b.Aportes, b.Neto,
        b.Lineas.Where(l => l.Codigo.StartsWith("HEX-")).Sum(l => l.Monto), b.Lineas.Where(l => l.Codigo == "REC-DOM").Sum(l => l.Monto), b.Definitiva, b.Observaciones);

    /// <summary>
    /// Administradores: todas (también los borradores del período abierto, con ?periodo=MM/AAAA). Los demás: solo sus
    /// boletas definitivas.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BoletaResumenDto>>> Boletas(string? periodo, int? colaboradorId)
    {
        var q = db.Boletas.AsNoTracking();
        if (!alcance.EsAdmin)
        {
            if (alcance.Propio is not { } yo) return new List<BoletaResumenDto>();
            q = q.Where(b => b.ColaboradorId == yo && b.Definitiva);
        }
        if (colaboradorId is { } cid) q = q.Where(b => b.ColaboradorId == cid);
        if (periodo is not null)
        {
            if (!Planilla.TryLeer(periodo, out var p)) return Error("periodo", "El período debe tener el formato MM/AAAA.");
            q = q.Where(b => b.Periodo == p);
        }
        return (await q.OrderByDescending(b => b.Periodo).ThenBy(b => b.Nombre).ToListAsync()).Select(Resumen).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BoletaDto>> Boleta(int id)
    {
        var b = await db.Boletas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null || (!alcance.EsAdmin && (b.ColaboradorId != alcance.Propio || !b.Definitiva))) return NotFound();
        return new BoletaDto(Resumen(b), b.Lineas.Select(l => new LineaBoletaDto(l.Codigo, l.Nombre, l.Clase, l.Detalle, l.Monto)).ToList());
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
