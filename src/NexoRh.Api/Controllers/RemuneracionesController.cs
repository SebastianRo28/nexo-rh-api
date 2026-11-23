using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Remuneraciones: parámetros legales, catálogo de ítems, ítems asignados, períodos de planilla y boletas.
/// Todo es de administradores, salvo las boletas: cada persona ve las suyas ya cerradas (definitivas).
/// </summary>
[ApiController]
[Route("api/remuneraciones")]
[RequiereModulo(Modulos.Payroll)]
[Authorize(Policy = Politicas.Administrador)]
public partial class RemuneracionesController(NexoDbContext db, Auditoria auditoria, Planilla planilla) : ControllerBase
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static string Soles(decimal x) => "S/ " + x.ToString("N2", En);
    [GeneratedRegex(@"^[A-Z0-9]{2,4}-[A-Z0-9]{2,4}$")] private static partial Regex CodigoItem();

    // --- Parámetros --------------------------------------------------------------------------------------------------
    [HttpGet("parametros")]
    public async Task<ParametrosRem> Parametros() => new(
        (await db.ParametrosRem.AsNoTracking().ToListAsync()).Select(p => new ParametroRemDto(p.Clave, p.Nombre, p.Valor, p.Unidad, p.VigenteDesde, p.Fuente)).ToList(),
        (await db.ComisionesAfp.AsNoTracking().OrderBy(a => a.Afp).ToListAsync()).Select(a => new ComisionAfpDto(a.Afp, a.ComisionFlujoPct, a.ComisionSaldoPct)).ToList(),
        (await db.TramosRenta.AsNoTracking().OrderBy(t => t.Desde).ToListAsync()).Select(t => new TramoRentaDto(t.Id, t.Desde, t.Hasta, t.TasaPct)).ToList());

    [HttpPut("parametros/{clave}")]
    public async Task<ActionResult<ParametroRemDto>> Parametro(string clave, ParametroGuardar g)
    {
        var p = await db.ParametrosRem.Buscar(clave);
        if (p is null) return NotFound();
        var max = p.Unidad switch { "%" => 100m, "UIT" => 50m, _ => 1_000_000m };
        if (g.Valor <= 0 || g.Valor > max) return Error("valor", $"El valor debe ser mayor a 0 y como máximo {max.ToString("N0", En)}.");
        if (!Fechas.TryLeer(g.VigenteDesde, out _)) return Error("vigenteDesde", "La fecha de vigencia debe tener el formato DD/MM/AAAA.");
        var anterior = $"{p.Valor.ToString("0.####", En)} {p.Unidad} (desde {p.VigenteDesde})";
        p.Valor = g.Valor;
        p.VigenteDesde = g.VigenteDesde!.Trim();
        auditoria.Registrar($"Parámetro {p.Nombre}", "Modificar", propiedad: "valor", anterior: anterior, actual: $"{p.Valor.ToString("0.####", En)} {p.Unidad} (desde {p.VigenteDesde})");
        await db.SaveChangesAsync();
        return new ParametroRemDto(p.Clave, p.Nombre, p.Valor, p.Unidad, p.VigenteDesde, p.Fuente);
    }

    [HttpPut("afp/{afp}")]
    public async Task<ActionResult<ComisionAfpDto>> Afp(string afp, ComisionAfpDto g)
    {
        var a = await db.ComisionesAfp.Buscar(afp);
        if (a is null) return NotFound();
        if (g.ComisionFlujoPct is < 0 or > 10 || g.ComisionSaldoPct is < 0 or > 10) return Error("comision", "Las comisiones deben estar entre 0 % y 10 %.");
        var anterior = $"{a.ComisionFlujoPct:0.##} % flujo · {a.ComisionSaldoPct:0.##} % saldo";
        a.ComisionFlujoPct = g.ComisionFlujoPct;
        a.ComisionSaldoPct = g.ComisionSaldoPct;
        auditoria.Registrar($"Comisión AFP {a.Afp}", "Modificar", propiedad: "valor", anterior: anterior, actual: $"{a.ComisionFlujoPct:0.##} % flujo · {a.ComisionSaldoPct:0.##} % saldo");
        await db.SaveChangesAsync();
        return new ComisionAfpDto(a.Afp, a.ComisionFlujoPct, a.ComisionSaldoPct);
    }

    [HttpPut("tramos/{id:int}")]
    public async Task<ActionResult<TramoRentaDto>> Tramo(int id, TramoGuardar g)
    {
        var t = await db.TramosRenta.Buscar(id);
        if (t is null) return NotFound();
        if (g.TasaPct is < 0 or > 50) return Error("tasaPct", "La tasa debe estar entre 0 % y 50 %.");
        var anterior = $"{t.TasaPct:0.##} %";
        t.TasaPct = g.TasaPct;
        auditoria.Registrar($"Tramo 5ta {t.Desde:0.##}–{(t.Hasta is { } h ? h.ToString("0.##") : "∞")} UIT", "Modificar", propiedad: "valor", anterior: anterior, actual: $"{t.TasaPct:0.##} %");
        await db.SaveChangesAsync();
        return new TramoRentaDto(t.Id, t.Desde, t.Hasta, t.TasaPct);
    }

    // --- Tipos de ítem -----------------------------------------------------------------------------------------------
    private static TipoItemDto Dto(TipoItem t) => new(t.Id, t.Codigo, t.Nombre, t.Clase, t.Plame, t.Essalud, t.Pension, t.Quinta, t.Automatico, t.Activo,
        Planilla.CodigosCalculados.Contains(t.Codigo));

    [HttpGet("tipos-item")]
    public async Task<List<TipoItemDto>> Tipos() => (await db.TiposItem.AsNoTracking().OrderBy(t => t.Id).ToListAsync()).Select(Dto).ToList();

    [HttpPost("tipos-item")]
    public async Task<ActionResult<TipoItemDto>> CrearTipo(TipoItemGuardar g)
    {
        if (await ValidarTipo(g, null) is { } err) return err;
        var t = new TipoItem { Id = (await db.TiposItem.MaxAsync(x => (int?)x.Id) ?? 0) + 1 };
        AplicarTipo(t, g);
        db.TiposItem.Add(t);
        auditoria.Registrar($"Tipo de ítem {t.Codigo}", "Crear", actual: $"{t.Nombre} ({t.Clase})");
        await db.SaveChangesAsync();
        return Dto(t);
    }

    [HttpPut("tipos-item/{id:int}")]
    public async Task<ActionResult<TipoItemDto>> ModificarTipo(int id, TipoItemGuardar g)
    {
        var t = await db.TiposItem.Buscar(id);
        if (t is null) return NotFound();
        if (Planilla.CodigosCalculados.Contains(t.Codigo)) return Error("codigo", "Este ítem lo calcula la planilla: no se puede modificar.");
        if (await ValidarTipo(g, t) is { } err) return err;
        var antes = $"{t.Codigo} · {t.Nombre} · {t.Clase} · PLAME {t.Plame} · EsSalud {(t.Essalud ? "sí" : "no")} · pensión {(t.Pension ? "sí" : "no")} · 5ta {(t.Quinta ? "sí" : "no")}";
        AplicarTipo(t, g);
        var despues = $"{t.Codigo} · {t.Nombre} · {t.Clase} · PLAME {t.Plame} · EsSalud {(t.Essalud ? "sí" : "no")} · pensión {(t.Pension ? "sí" : "no")} · 5ta {(t.Quinta ? "sí" : "no")}";
        if (antes != despues) auditoria.Registrar($"Tipo de ítem {t.Codigo}", "Modificar", anterior: antes, actual: despues);
        await db.SaveChangesAsync();
        return Dto(t);
    }

    [HttpDelete("tipos-item/{id:int}")]
    public async Task<IActionResult> EliminarTipo(int id)
    {
        var t = await db.TiposItem.Buscar(id);
        if (t is null) return NotFound();
        if (t.Automatico) return Error("id", "Los ítems automáticos no se pueden eliminar.");
        var usos = await db.AsignacionesItem.CountAsync(a => a.TipoItemId == id);
        if (usos > 0) return Error("id", $"No se puede eliminar: está asignado {usos} vez/veces. Desactívalo en su lugar.");
        db.TiposItem.Remove(t);
        auditoria.Registrar($"Tipo de ítem {t.Codigo}", "Eliminar", anterior: t.Nombre);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> ValidarTipo(TipoItemGuardar g, TipoItem? actual)
    {
        var e = new Dictionary<string, string[]>();
        var codigo = (g.Codigo ?? "").Trim().ToUpperInvariant();
        if (!CodigoItem().IsMatch(codigo)) e["codigo"] = ["El código debe tener el formato ABC-DEF (letras o números)."];
        else if (await db.TiposItem.AnyAsync(x => x.Codigo == codigo && x.Id != (actual == null ? 0 : actual.Id))) e["codigo"] = ["Ya existe un ítem con ese código."];
        if ((g.Nombre ?? "").Trim().Length is 0 or > 80) e["nombre"] = ["Ingresa el nombre (máximo 80 caracteres)."];
        if (g.Clase is not ("Haber" or "Descuento" or "Aporte empleador")) e["clase"] = ["La clase debe ser Haber, Descuento o Aporte empleador."];
        if (!Regex.IsMatch((g.Plame ?? "").Trim(), @"^\d{4}$")) e["plame"] = ["El código PLAME tiene 4 dígitos."];
        return e.Count > 0 ? ValidationProblem(new ValidationProblemDetails(e) { Title = e.Values.First()[0] }) : null;
    }

    private static void AplicarTipo(TipoItem t, TipoItemGuardar g)
    {
        t.Codigo = g.Codigo!.Trim().ToUpperInvariant();
        t.Nombre = g.Nombre!.Trim();
        t.Clase = g.Clase!;
        t.Plame = g.Plame!.Trim();
        // Los descuentos y aportes no son base de EsSalud, pensión ni 5ta.
        t.Essalud = g.Clase == "Haber" && g.Essalud;
        t.Pension = g.Clase == "Haber" && g.Pension;
        t.Quinta = g.Clase == "Haber" && g.Quinta;
        t.Activo = g.Activo;
    }

    // --- Ítems asignados ---------------------------------------------------------------------------------------------
    private static AsignacionItemDto Dto(AsignacionItem a) =>
        new(a.Id, a.ColaboradorId, a.TipoItemId, a.Monto, Planilla.Texto(a.Desde), a.Hasta is { } h ? Planilla.Texto(h) : "", a.Observacion);

    [HttpGet("asignaciones")]
    public async Task<List<AsignacionItemDto>> Asignaciones() =>
        (await db.AsignacionesItem.AsNoTracking().OrderByDescending(a => a.Id).ToListAsync()).Select(Dto).ToList();

    /// <summary>Solo desde el período abierto: lo ya pagado no se modifica.</summary>
    [HttpPost("asignaciones")]
    public async Task<ActionResult<AsignacionItemDto>> Asignar(AsignacionItemNueva p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        var t = await db.TiposItem.Buscar(p.TipoId);
        if (t is null || !t.Activo) return Error("tipoId", "El ítem no existe o está inactivo.");
        if (Planilla.CodigosCalculados.Contains(t.Codigo)) return Error("tipoId", $"{t.Codigo} lo calcula la planilla: no se asigna a mano.");
        if (p.Monto <= 0 || p.Monto > 1_000_000) return Error("monto", "El monto debe ser mayor a 0 y menor a S/ 1,000,000.");
        if (!Planilla.TryLeer(p.Desde, out var desde)) return Error("desde", "El período inicial debe tener el formato MM/AAAA.");
        int? hasta = null;
        if (!string.IsNullOrWhiteSpace(p.Hasta))
        {
            if (!Planilla.TryLeer(p.Hasta, out var h)) return Error("hasta", "El período final debe tener el formato MM/AAAA.");
            if (h < desde) return Error("hasta", "El período final debe ser igual o posterior al inicial.");
            hasta = h;
        }
        var abierto = await planilla.AbiertoAsync();
        if (abierto is { } ab && desde < ab) return Error("desde", $"El período {Planilla.Texto(desde)} ya está cerrado: asigna desde {Planilla.Texto(ab)}.");
        var a = new AsignacionItem { ColaboradorId = c.Id, TipoItemId = t.Id, Monto = Math.Round(p.Monto, 2), Desde = desde, Hasta = hasta, Observacion = (p.Observacion ?? "").Trim() };
        if (a.Observacion.Length > 300) a.Observacion = a.Observacion[..300];
        db.AsignacionesItem.Add(a);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Ítem {t.Codigo} #{a.Id}", "Crear", c, actual: $"{Soles(a.Monto)} · {Planilla.Texto(desde)}{(hasta is { } x ? " a " + Planilla.Texto(x) : " en adelante")}");
        await db.SaveChangesAsync();
        return Dto(a);
    }

    /// <summary>Quita el ítem. Si ya se pagó en períodos cerrados, solo se le pone fin en el último período cerrado.</summary>
    [HttpDelete("asignaciones/{id:int}")]
    public async Task<IActionResult> Quitar(int id)
    {
        var a = await db.AsignacionesItem.Buscar(id);
        if (a is null) return NotFound();
        var c = await db.Colaboradores.Buscar(a.ColaboradorId);
        var t = await db.TiposItem.Buscar(a.TipoItemId);
        var abierto = await planilla.AbiertoAsync() ?? int.MaxValue;
        if (a.Desde < abierto)
        {
            if (a.Hasta is { } h && h < abierto) return Error("id", "Este ítem ya se pagó en períodos cerrados y no tiene efecto en el período abierto.");
            var fin = abierto % 100 == 1 ? (abierto / 100 - 1) * 100 + 12 : abierto - 1;
            a.Hasta = fin;
            auditoria.Registrar($"Ítem {t?.Codigo} #{a.Id}", "Modificar", c, "hasta", "—", Planilla.Texto(fin));
        }
        else
        {
            db.AsignacionesItem.Remove(a);
            auditoria.Registrar($"Ítem {t?.Codigo} #{a.Id}", "Eliminar", c, anterior: Soles(a.Monto));
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Períodos ----------------------------------------------------------------------------------------------------
    [HttpGet("periodos")]
    public async Task<List<PeriodoDto>> Periodos()
    {
        var totales = await db.Boletas.GroupBy(b => b.Periodo).Select(g => new { g.Key, N = g.Count(), I = g.Sum(b => b.Ingresos), D = g.Sum(b => b.Descuentos), A = g.Sum(b => b.Aportes), Ne = g.Sum(b => b.Neto) })
            .ToDictionaryAsync(x => x.Key);
        return (await db.Periodos.AsNoTracking().OrderByDescending(p => p.Id).ToListAsync()).Select(p =>
        {
            totales.TryGetValue(p.Id, out var t);
            return new PeriodoDto(Planilla.Texto(p.Id), p.Cerrado, p.ProcesadoEn is { } x ? Fechas.TextoHora(x) : null, p.ProcesadoPor,
                p.CerradoEn is { } y ? Fechas.TextoHora(y) : null, p.CerradoPor, t?.N ?? 0, t?.I ?? 0, t?.D ?? 0, t?.A ?? 0, t?.Ne ?? 0);
        }).ToList();
    }

    [HttpPost("periodos/{mes:int}-{anio:int}/procesar")]
    public async Task<ActionResult<PeriodoDto>> Procesar(int mes, int anio)
    {
        var p = anio * 100 + mes;
        if (await planilla.AbiertoAsync() != p) return Error("periodo", "Solo se puede procesar el período abierto.");
        var boletas = await planilla.ProcesarAsync(p, User.Nombre());
        auditoria.Registrar($"Planilla {Planilla.Texto(p)}", "Modificar", propiedad: "proceso", actual: $"{boletas.Count} boletas · neto {Soles(boletas.Sum(b => b.Neto))}");
        await db.SaveChangesAsync();
        return (await Periodos()).First(x => x.Periodo == Planilla.Texto(p));
    }

    [HttpPost("periodos/{mes:int}-{anio:int}/cerrar")]
    public async Task<ActionResult<PeriodoDto>> Cerrar(int mes, int anio)
    {
        var p = anio * 100 + mes;
        if (await planilla.AbiertoAsync() != p) return Error("periodo", "Solo se puede cerrar el período abierto.");
        var boletas = await planilla.CerrarAsync(p, User.Nombre());
        auditoria.Registrar($"Planilla {Planilla.Texto(p)}", "Modificar", propiedad: "estado", anterior: "Abierto", actual: $"Cerrado · {boletas.Count} boletas definitivas");
        await db.SaveChangesAsync();
        return (await Periodos()).First(x => x.Periodo == Planilla.Texto(p));
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
