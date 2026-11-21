using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record PosicionDto(string Id, string Cargo, string Division, string Area, string SubArea, string Ingreso, string Termino, bool Vigente);
public record DatosHeadcount(List<PosicionDto> Posiciones, Dictionary<string, int> PlanArea, Dictionary<string, int> PlanCargo);
public record PosicionesNuevas(string? Cargo, string? Division, string? Area, string? SubArea, string? Ingreso, string? Termino, int Cantidad);

/// <summary>
/// Headcount: posiciones planificadas (vacantes) y dotación esperada por área y por cargo. Crear posiciones vigentes
/// aumenta lo esperado; eliminar una vigente lo reduce.
/// </summary>
[ApiController]
[Route("api/headcount")]
[Authorize(Policy = Politicas.Administrador)]
public class HeadcountController(NexoDbContext db, Auditoria auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<DatosHeadcount> Datos()
    {
        var planes = await db.PlanesHeadcount.AsNoTracking().ToListAsync();
        var pos = await db.Posiciones.AsNoTracking().ToListAsync();
        return new DatosHeadcount(
            pos.OrderByDescending(p => p.Codigo).Select(p => new PosicionDto(p.Codigo, p.Cargo, p.Division, p.Area, p.SubArea, Fechas.Texto(p.Ingreso),
                Fechas.Texto(p.Termino) ?? "—", p.Vigente)).ToList(),
            planes.Where(p => p.Tipo == "area").OrderBy(p => p.Nombre).ToDictionary(p => p.Nombre, p => p.Cantidad),
            planes.Where(p => p.Tipo == "cargo").OrderBy(p => p.Nombre).ToDictionary(p => p.Nombre, p => p.Cantidad));
    }

    [HttpPost("posiciones")]
    public async Task<ActionResult<List<string>>> Crear(PosicionesNuevas p)
    {
        string T(string? s) => (s ?? "").Trim();
        var (cargo, division, area, subArea) = (T(p.Cargo), T(p.Division), T(p.Area), T(p.SubArea));
        if (cargo.Length == 0 || area.Length == 0 || division.Length == 0) return Error("area", "Selecciona área y cargo.");
        if (!await db.Cargos.AnyAsync(c => c.Nombre == cargo)) return Error("cargo", "El cargo no existe en el catálogo.");
        if (p.Cantidad is < 1 or > 20) return Error("cantidad", "La cantidad debe estar entre 1 y 20.");
        if (!Fechas.TryLeer(p.Ingreso, out var ingreso)) return Error("ingreso", "Ingresa la fecha de ingreso proyectada (DD/MM/AAAA).");
        DateOnly? termino = null;
        if (!string.IsNullOrWhiteSpace(p.Termino) && p.Termino.Trim() != "—")
        {
            if (!Fechas.TryLeer(p.Termino, out var t)) return Error("termino", "La fecha de término debe tener el formato DD/MM/AAAA.");
            if (t <= ingreso) return Error("termino", "La fecha de término debe ser posterior al ingreso.");
            termino = t;
        }

        var max = (await db.Posiciones.Select(x => x.Codigo).ToListAsync()).Select(c => int.TryParse(c.Replace("POS-", ""), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        var codigos = new List<string>();
        for (var i = 1; i <= p.Cantidad; i++)
        {
            var codigo = $"POS-{max + i:0000}";
            db.Posiciones.Add(new Posicion { Codigo = codigo, Cargo = cargo, Division = division, Area = area, SubArea = subArea, Ingreso = ingreso, Termino = termino });
            codigos.Add(codigo);
        }
        await Ajustar("area", area, p.Cantidad, 0);
        // Un cargo nuevo en el plan parte de la dotación actual del cargo.
        await Ajustar("cargo", cargo, p.Cantidad, await db.Colaboradores.CountAsync(c => c.Cargo == cargo));
        auditoria.Registrar($"Headcount · {area}", "Crear", actual: $"{p.Cantidad} posición(es) de {cargo}: {string.Join(", ", codigos)}");
        await db.SaveChangesAsync();
        return codigos;
    }

    [HttpDelete("posiciones/{codigo}")]
    public async Task<IActionResult> Eliminar(string codigo)
    {
        var x = await db.Posiciones.FindAsync(codigo);
        if (x is null) return NotFound();
        db.Posiciones.Remove(x);
        if (x.Vigente)
        {
            await Ajustar("area", x.Area, -1, 0, crear: false);
            await Ajustar("cargo", x.Cargo, -1, 0, crear: false);
        }
        auditoria.Registrar($"Headcount · {x.Area}", "Eliminar", actual: $"Posición {x.Codigo} ({x.Cargo})");
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task Ajustar(string tipo, string nombre, int delta, int inicial, bool crear = true)
    {
        var plan = await db.PlanesHeadcount.FindAsync(tipo, nombre);
        if (plan is null)
        {
            if (!crear) return;
            db.PlanesHeadcount.Add(plan = new PlanHeadcount { Tipo = tipo, Nombre = nombre, Cantidad = inicial });
        }
        plan.Cantidad = Math.Max(0, plan.Cantidad + delta);
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
