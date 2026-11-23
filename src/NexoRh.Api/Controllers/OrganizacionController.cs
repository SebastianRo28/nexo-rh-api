using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>Estructura organizacional: cargos, familias de cargo, competencias y árbol división › área › sub-área.</summary>
[ApiController]
[Route("api/organizacion")]
[Authorize]
public class OrganizacionController(NexoDbContext db, Auditoria auditoria) : ControllerBase
{
    [HttpGet("cargos")]
    public async Task<IEnumerable<CargoDto>> Cargos()
    {
        var conteo = await db.Colaboradores.GroupBy(c => c.Cargo).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return (await db.Cargos.AsNoTracking().OrderBy(c => c.Id).ToListAsync())
            .Select(c => new CargoDto(c.Id, c.Nombre, c.Codigo, c.SubAreas, conteo.GetValueOrDefault(c.Nombre), c.Familia, c.Activo, c.Descripcion));
    }

    [HttpPost("cargos")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<CargoDto>> CrearCargo(CargoGuardar p)
    {
        var errores = await ValidarCargo(p, null);
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores) { Title = "Revisa los datos del cargo." });
        var c = new Cargo
        {
            Id = (await db.Cargos.MaxAsync(x => (int?)x.Id) ?? -1) + 1, Nombre = p.Nombre!.Trim(),
            Codigo = string.IsNullOrWhiteSpace(p.Codigo) ? Semilla.CodigoDe(p.Nombre!) : p.Codigo.Trim().ToUpperInvariant(),
            Familia = p.Familia!.Trim(), SubAreas = p.SubAreas ?? 0, Descripcion = p.Descripcion?.Trim(), Activo = p.Activo ?? true,
        };
        db.Cargos.Add(c);
        auditoria.Registrar($"Cargo · {c.Nombre}", "Crear", actual: $"{c.Codigo} · {c.Familia}");
        await db.SaveChangesAsync();
        return Created($"/api/organizacion/cargos/{c.Id}", new CargoDto(c.Id, c.Nombre, c.Codigo, c.SubAreas, 0, c.Familia, c.Activo, c.Descripcion));
    }

    [HttpPut("cargos/{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<CargoDto>> ModificarCargo(int id, CargoGuardar p)
    {
        var c = await db.Cargos.Buscar(id);
        if (c is null) return NotFound();
        var errores = await ValidarCargo(p, c);
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores) { Title = "Revisa los datos del cargo." });
        var nombreAnterior = c.Nombre;
        var activoAntes = c.Activo;
        c.Nombre = p.Nombre!.Trim();
        c.Codigo = string.IsNullOrWhiteSpace(p.Codigo) ? c.Codigo : p.Codigo.Trim().ToUpperInvariant();
        c.Familia = p.Familia!.Trim();
        c.SubAreas = p.SubAreas ?? c.SubAreas;
        c.Descripcion = p.Descripcion?.Trim();
        c.Activo = p.Activo ?? c.Activo;
        // Renombrar el cargo actualiza a quienes lo tienen, para no dejar colaboradores con un cargo inexistente.
        if (nombreAnterior != c.Nombre)
        {
            await db.Colaboradores.Where(x => x.Cargo == nombreAnterior).ExecuteUpdateAsync(s => s.SetProperty(x => x.Cargo, c.Nombre));
            auditoria.Registrar($"Cargo · {c.Nombre}", "Modificar", propiedad: "nombre", anterior: nombreAnterior, actual: c.Nombre);
        }
        if (activoAntes != c.Activo) auditoria.Registrar($"Cargo · {c.Nombre}", "Modificar", propiedad: "estado", anterior: activoAntes ? "Activo" : "Inactivo", actual: c.Activo ? "Activo" : "Inactivo");
        await db.SaveChangesAsync();
        var n = await db.Colaboradores.CountAsync(x => x.Cargo == c.Nombre);
        return new CargoDto(c.Id, c.Nombre, c.Codigo, c.SubAreas, n, c.Familia, c.Activo, c.Descripcion);
    }

    private async Task<Dictionary<string, string[]>> ValidarCargo(CargoGuardar p, Cargo? actual)
    {
        var e = new Dictionary<string, string[]>();
        var nombre = (p.Nombre ?? "").Trim();
        if (nombre.Length is 0 or > 80) e["nombre"] = ["Ingresa el nombre del cargo (máximo 80 caracteres)."];
        else if (await db.Cargos.AnyAsync(x => x.Nombre == nombre && x.Id != (actual == null ? -1 : actual.Id))) e["nombre"] = ["Ya existe un cargo con ese nombre."];
        if ((p.Codigo ?? "").Trim().Length > 30) e["codigo"] = ["El código admite hasta 30 caracteres."];
        var familia = (p.Familia ?? "").Trim();
        if (!await db.FamiliasCargo.AnyAsync(x => x.Nombre == familia && x.Activo)) e["familia"] = ["Elige una familia de cargo activa."];
        if (p.SubAreas is < 0 or > 100) e["subAreas"] = ["La cantidad de sub-áreas no es válida."];
        if (actual != null && p.Activo == false && actual.Activo && await db.Colaboradores.AnyAsync(x => x.Cargo == actual.Nombre))
            e["activo"] = ["No se puede desactivar un cargo que tienen colaboradores vigentes."];
        return e;
    }

    [HttpGet("familias")]
    public async Task<IEnumerable<FamiliaDto>> Familias()
    {
        var conteo = await db.Cargos.GroupBy(c => c.Familia).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return (await db.FamiliasCargo.AsNoTracking().OrderBy(f => f.Id).ToListAsync()).Select(f => new FamiliaDto(f.Id, f.Nombre, conteo.GetValueOrDefault(f.Nombre), f.Activo));
    }

    [HttpGet("competencias")]
    public async Task<IEnumerable<CompetenciaDto>> Competencias() =>
        await db.Competencias.AsNoTracking().OrderBy(c => c.Id).Select(c => new CompetenciaDto(c.Id, c.Nombre, c.Descripcion, c.Activo)).ToListAsync();

    /// <summary>Árbol división › área › sub-área con la dotación de cada nivel, calculado a partir de los colaboradores.</summary>
    [HttpGet("divisiones")]
    public async Task<IEnumerable<DivisionDto>> Divisiones()
    {
        var cs = await db.Colaboradores.AsNoTracking().Select(c => new { c.Division, c.Area, c.SubArea }).ToListAsync();
        return cs.GroupBy(c => c.Division).OrderBy(g => g.Key).Select(d => new DivisionDto(d.Key, d.Count(),
            d.GroupBy(c => c.Area).OrderBy(g => g.Key).Select(a => new AreaDto(a.Key, a.Count(),
                a.GroupBy(c => c.SubArea).OrderBy(g => g.Key).Select(s => new SubAreaDto(s.Key, s.Count())).ToList())).ToList())).ToList();
    }
}
