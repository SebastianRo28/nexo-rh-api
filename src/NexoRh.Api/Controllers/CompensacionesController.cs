using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record BandaDto(int Id, string Cargo, decimal Objetivo, decimal PctMin, decimal PctMax, bool Activo);
public record RemuneracionDto(int ColaboradorId, decimal SueldoBase, string UltimaModificacion, string Evaluacion, decimal? CompTeorica);
public record CargoMercadoDto(int Id, string Cargo, string? Interno, string Descripcion, string Industria, string Tamano, decimal P25, decimal P50, decimal P75, decimal P90);
public record DatosCompensaciones(List<BandaDto> Bandas, List<RemuneracionDto> Remuneraciones, List<CargoMercadoDto> Estudio);
public record BandaEntrada(string? Cargo, decimal Objetivo, decimal PctMin, decimal PctMax, bool? Activo);
public record CompTeoricaEntrada(decimal Monto, string? Comentario);
public record EmparejarEntrada(string? Interno);

/// <summary>
/// Compensaciones: bandas salariales por cargo, compensación teórica de cada colaborador y emparejamiento del estudio
/// salarial de mercado con los cargos propios. El sueldo base sale de la ficha (sueldo base, o neto si no hay base).
/// </summary>
[ApiController]
[Route("api/compensaciones")]
[Authorize(Policy = Politicas.Administrador)]
public class CompensacionesController(NexoDbContext db, Auditoria auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<DatosCompensaciones> Datos()
    {
        var comp = await db.Compensaciones.AsNoTracking().ToDictionaryAsync(c => c.ColaboradorId);
        var cols = await db.Colaboradores.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        return new DatosCompensaciones(
            (await db.Bandas.AsNoTracking().OrderBy(b => b.Cargo).ToListAsync()).Select(Dto).ToList(),
            cols.Select(c =>
            {
                var x = comp.GetValueOrDefault(c.Id);
                return new RemuneracionDto(c.Id, c.SueldoBase ?? c.SueldoNeto, Fechas.Texto(x?.UltimaModificacion ?? c.Ingreso),
                    x?.Evaluacion is { Length: > 0 } e ? e : "—", x?.CompTeorica);
            }).ToList(),
            (await db.EstudioSalarial.AsNoTracking().OrderBy(m => m.Id).ToListAsync())
                .Select(m => new CargoMercadoDto(m.Id, m.Cargo, m.Interno, m.Descripcion, m.Industria, m.Tamano, m.P25, m.P50, m.P75, m.P90)).ToList());
    }

    private static BandaDto Dto(BandaSalarial b) => new(b.Id, b.Cargo, b.Objetivo, b.PctMin, b.PctMax, b.Activo);

    [HttpPost("bandas")]
    public async Task<ActionResult<BandaDto>> CrearBanda(BandaEntrada p) => await GuardarBanda(null, p);

    [HttpPut("bandas/{id:int}")]
    public async Task<ActionResult<BandaDto>> ModificarBanda(int id, BandaEntrada p) => await GuardarBanda(id, p);

    private async Task<ActionResult<BandaDto>> GuardarBanda(int? id, BandaEntrada p)
    {
        var cargo = (p.Cargo ?? "").Trim();
        if (cargo.Length == 0) return Error("cargo", "Selecciona un cargo.");
        if (!await db.Cargos.AnyAsync(c => c.Nombre == cargo)) return Error("cargo", "El cargo no existe en el catálogo.");
        if (await db.Bandas.AnyAsync(b => b.Cargo == cargo && b.Id != (id ?? 0))) return Error("cargo", "Ese cargo ya tiene una banda salarial.");
        if (p.Objetivo <= 0 || p.Objetivo > 1_000_000) return Error("objetivo", "Ingresa un sueldo objetivo mayor a 0.");
        if (p.PctMin is <= 0 or > 100) return Error("pctMin", "El mínimo debe estar entre 1 % y 100 % del objetivo.");
        if (p.PctMax is < 100 or > 300) return Error("pctMax", "El máximo debe estar entre 100 % y 300 % del objetivo.");
        BandaSalarial b;
        if (id is { } i)
        {
            b = (await db.Bandas.FindAsync(i))!;
            if (b is null) return NotFound();
            auditoria.Registrar($"Banda salarial · {b.Cargo}", "Modificar", propiedad: "banda",
                anterior: $"{b.Cargo} · objetivo {b.Objetivo:0.00} · {b.PctMin:0.##}%–{b.PctMax:0.##}%", actual: $"{cargo} · objetivo {p.Objetivo:0.00} · {p.PctMin:0.##}%–{p.PctMax:0.##}%");
        }
        else
        {
            b = new BandaSalarial();
            db.Bandas.Add(b);
            auditoria.Registrar($"Banda salarial · {cargo}", "Crear", actual: $"objetivo {p.Objetivo:0.00} · {p.PctMin:0.##}%–{p.PctMax:0.##}%");
        }
        b.Cargo = cargo;
        b.Objetivo = Math.Round(p.Objetivo, 2);
        b.PctMin = p.PctMin;
        b.PctMax = p.PctMax;
        if (p.Activo is { } a) b.Activo = a;
        await db.SaveChangesAsync();
        return Dto(b);
    }

    [HttpDelete("bandas/{id:int}")]
    public async Task<IActionResult> EliminarBanda(int id)
    {
        var b = await db.Bandas.FindAsync(id);
        if (b is null) return NotFound();
        db.Bandas.Remove(b);
        auditoria.Registrar($"Banda salarial · {b.Cargo}", "Eliminar");
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Fija la compensación teórica (la que se usa para ubicar al colaborador en su banda).</summary>
    [HttpPut("remuneraciones/{colaboradorId:int}/teorica")]
    public async Task<IActionResult> CompTeorica(int colaboradorId, CompTeoricaEntrada p)
    {
        var c = await db.Colaboradores.FindAsync(colaboradorId);
        if (c is null) return NotFound();
        if (p.Monto <= 0 || p.Monto > 1_000_000) return Error("monto", "Ingresa un monto mayor a 0.");
        var x = await db.Compensaciones.FindAsync(colaboradorId);
        if (x is null) db.Compensaciones.Add(x = new CompensacionColaborador { ColaboradorId = colaboradorId });
        var antes = x.CompTeorica is { } v ? v.ToString("0.00") : "—";
        x.CompTeorica = Math.Round(p.Monto, 2);
        x.Comentario = (p.Comentario ?? "").Trim() is var s && s.Length > 300 ? s[..300] : s;
        x.ActualizadaEn = DateTime.UtcNow;
        auditoria.Registrar("Compensación teórica", "Modificar", c, "compTeorica", antes, x.CompTeorica.Value.ToString("0.00"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Empareja (o desempareja con null) un cargo de mercado con un cargo propio; cada cargo propio se empareja una vez.</summary>
    [HttpPut("estudio/{id:int}/interno")]
    public async Task<IActionResult> Emparejar(int id, EmparejarEntrada p)
    {
        var m = await db.EstudioSalarial.FindAsync(id);
        if (m is null) return NotFound();
        var interno = string.IsNullOrWhiteSpace(p.Interno) ? null : p.Interno.Trim();
        if (interno is not null)
        {
            if (!await db.Cargos.AnyAsync(c => c.Nombre == interno)) return Error("interno", "El cargo no existe en el catálogo.");
            if (await db.EstudioSalarial.AnyAsync(x => x.Interno == interno && x.Id != id)) return Error("interno", $"“{interno}” ya está emparejado con otro cargo de mercado.");
        }
        auditoria.Registrar($"Estudio salarial · {m.Cargo}", "Modificar", propiedad: "cargo interno", anterior: m.Interno ?? "—", actual: interno ?? "—");
        m.Interno = interno;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
