using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Services;

/// <summary>
/// Cálculo de la planilla mensual (régimen laboral general, Perú). Por colaborador:
/// <list type="bullet">
/// <item>Remuneración básica por días laborados (mes de 30 días): sueldo base, o jornal diario; menos días sin goce
/// (permiso sin goce, falta injustificada); los días de vacaciones se pagan como remuneración vacacional.</item>
/// <item>Ítems asignados del período (asignación familiar, movilidad, bonos, adelantos…).</item>
/// <item>Horas extras aprobadas y enviadas a planilla desde Control de Asistencia (25 % y 35 %) y trabajo en día de
/// descanso o feriado sin descanso sustitutorio (recargo de 100 %). Valor hora = valor día / 8.</item>
/// <item>Pensión: ONP 13 %, o AFP (aporte 10 % + prima de seguro hasta la remuneración máxima asegurable + comisión sobre
/// el flujo si corresponde). Las modalidades formativas no aportan.</item>
/// <item>Renta de 5ta categoría: proyección anual (remuneraciones del año + meses restantes + gratificaciones con
/// bonificación extraordinaria) − 7 UIT, impuesto por tramos y retención mensual según el mes (divisores 12, 9, 8, 5 y 4).</item>
/// <item>EsSalud 9 % a cargo del empleador (base mínima: la RMV proporcional a los días del vínculo).</item>
/// </list>
/// Los parámetros (RMV, UIT, tasas, comisiones, tramos) se leen de la base y los edita un administrador.
/// Montos redondeados a 2 decimales por línea.
/// </summary>
public class Planilla(NexoDbContext db)
{
    /// <summary>Códigos que calcula el motor: no se pueden asignar a mano.</summary>
    public static readonly string[] CodigosCalculados =
        ["SUE-BAS", "VAC-REM", "HEX-25", "HEX-35", "REC-DOM", "DSC-INA", "RET-5TA", "APO-ESS", "GRA-FPN", "BON-EXT", "APO-ONP", "AFP-APO", "AFP-COM", "AFP-PRI"];

    // --- Períodos ----------------------------------------------------------------------------------------------------
    public static string Texto(int periodo) => $"{periodo % 100:00}/{periodo / 100}";
    public static bool TryLeer(string? s, out int periodo)
    {
        periodo = 0;
        var p = (s ?? "").Trim().Split('/');
        if (p.Length != 2 || !int.TryParse(p[0], out var m) || !int.TryParse(p[1], out var a) || m is < 1 or > 12 || a is < 2000 or > 2100 || p[0].Length != 2) return false;
        periodo = a * 100 + m;
        return true;
    }
    public static int De(DateOnly d) => d.Year * 100 + d.Month;
    public static int Siguiente(int p) => p % 100 == 12 ? (p / 100 + 1) * 100 + 1 : p + 1;
    private static DateOnly Inicio(int p) => new(p / 100, p % 100, 1);
    private static DateOnly Fin(int p) => Inicio(p).AddMonths(1).AddDays(-1);

    // --- Régimen pensionario -----------------------------------------------------------------------------------------
    public record Regimen(string Tipo, string? Afp, bool ComisionFlujo, string Texto);

    /// <summary>
    /// Lee el régimen del campo Pensión de la ficha: "ONP", o "AFP {nombre} · comisión Flujo|Saldo|Mixta".
    /// Las modalidades formativas (convenio de prácticas) no aportan.
    /// </summary>
    public static Regimen LeerRegimen(string? pension, string? contrato)
    {
        if (string.Equals(contrato, "Convenio de prácticas", StringComparison.OrdinalIgnoreCase)) return new("Ninguno", null, false, "Modalidad formativa (sin aporte pensionario)");
        var t = (pension ?? "").Trim();
        if (t.StartsWith("ONP", StringComparison.OrdinalIgnoreCase)) return new("ONP", null, false, "ONP");
        if (t.StartsWith("AFP", StringComparison.OrdinalIgnoreCase))
        {
            var nombre = t[3..].Split('·')[0].Trim();
            var flujo = !t.Contains("saldo", StringComparison.OrdinalIgnoreCase);
            return new("AFP", nombre, flujo, $"AFP {nombre} · comisión {(flujo ? "flujo" : "saldo")}");
        }
        return new("Sin registrar", null, false, "Sin régimen pensionario registrado");
    }

    private static decimal R(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    private static int Dias(DateOnly d, DateOnly h, DateOnly ini, DateOnly fin)
    {
        var a = d > ini ? d : ini;
        var b = h < fin ? h : fin;
        return a <= b ? b.DayNumber - a.DayNumber + 1 : 0;
    }

    /// <summary>Impuesto anual por tramos progresivos (en UIT).</summary>
    public static decimal Impuesto(decimal rentaNeta, decimal uit, IEnumerable<TramoRenta> tramos)
    {
        decimal imp = 0;
        foreach (var t in tramos.OrderBy(t => t.Desde))
        {
            var desde = t.Desde * uit;
            if (rentaNeta <= desde) break;
            var hasta = t.Hasta is { } h ? h * uit : decimal.MaxValue;
            imp += (Math.Min(rentaNeta, hasta) - desde) * t.TasaPct / 100;
        }
        return imp;
    }

    /// <summary>
    /// Retención del mes según el procedimiento de SUNAT: enero–marzo ÷ 12; abril (− retenido ene–mar) ÷ 9;
    /// mayo–julio (− retenido ene–abr) ÷ 8; agosto (− retenido ene–jul) ÷ 5; setiembre–noviembre (− retenido ene–ago) ÷ 4;
    /// diciembre: el saldo.
    /// </summary>
    public static decimal RetencionMes(int mes, decimal impuestoAnual, IReadOnlyDictionary<int, decimal> retenidoPorMes)
    {
        decimal Hasta(int m) => retenidoPorMes.Where(kv => kv.Key <= m).Sum(kv => kv.Value);
        var r = mes switch
        {
            <= 3 => impuestoAnual / 12,
            4 => (impuestoAnual - Hasta(3)) / 9,
            <= 7 => (impuestoAnual - Hasta(4)) / 8,
            8 => (impuestoAnual - Hasta(7)) / 5,
            <= 11 => (impuestoAnual - Hasta(8)) / 4,
            _ => impuestoAnual - Hasta(11),
        };
        return Math.Max(0, R(r));
    }

    /// <summary>Calcula las boletas del período (no las guarda).</summary>
    public async Task<List<Boleta>> CalcularAsync(int periodo)
    {
        var ini = Inicio(periodo);
        var fin = Fin(periodo);
        var mes = periodo % 100;
        var par = await db.ParametrosRem.AsNoTracking().ToDictionaryAsync(x => x.Clave, x => x.Valor);
        decimal P(string k) => par.TryGetValue(k, out var v) ? v : throw new InvalidOperationException($"Falta el parámetro {k}.");
        var afps = await db.ComisionesAfp.AsNoTracking().ToDictionaryAsync(x => x.Afp, StringComparer.OrdinalIgnoreCase);
        var tramos = await db.TramosRenta.AsNoTracking().ToListAsync();
        var tipos = await db.TiposItem.AsNoTracking().ToListAsync();
        var porCodigo = tipos.ToDictionary(t => t.Codigo);
        var cols = await db.Colaboradores.AsNoTracking().Where(c => c.Ingreso <= fin).OrderBy(c => c.Apellidos).ToListAsync();
        var asignaciones = await db.AsignacionesItem.AsNoTracking().Where(a => a.Desde <= periodo && (a.Hasta == null || a.Hasta >= periodo)).ToListAsync();
        var ausencias = await db.Ausencias.AsNoTracking().Where(a => a.Desde <= fin && a.Hasta >= ini && (a.Tipo == "Permiso sin goce" || a.Tipo == "Falta injustificada")).ToListAsync();
        var vacaciones = await db.Vacaciones.AsNoTracking().Where(v => v.Desde <= fin && v.Hasta >= ini).ToListAsync();
        var recargos = await db.Recargos.AsNoTracking().Where(r => r.Fecha >= ini && r.Fecha <= fin && !r.DescansoSustitutorio).ToListAsync();
        var hhee = (await db.AprobacionesHhee.AsNoTracking().Where(a => a.Fecha >= ini && a.Fecha <= fin && a.Enviado).ToListAsync())
            .Where(a => a.Estado is "Aprobado" or "Aprobado parcial").ToList();
        var anio = periodo / 100;
        var previas = await db.Boletas.AsNoTracking().Where(b => b.Definitiva && b.Periodo > anio * 100 && b.Periodo < periodo)
            .Select(b => new { b.ColaboradorId, b.Periodo, b.BaseQuinta, b.RetencionQuinta }).ToListAsync();

        var uit = P("uit");
        var rmv = P("rmv");
        var salida = new List<Boleta>();
        foreach (var c in cols)
        {
            var b = new Boleta
            {
                Periodo = periodo, ColaboradorId = c.Id, Nombre = $"{c.Apellidos}, {c.Nombres}", Documento = c.Documento, Cargo = c.Cargo,
            };
            void Linea(string codigo, decimal monto, string detalle = "", string? nombre = null, string? clase = null)
            {
                monto = R(monto);
                if (monto <= 0) return;
                porCodigo.TryGetValue(codigo, out var t);
                b.Lineas.Add(new LineaBoleta { Codigo = codigo, Nombre = nombre ?? t?.Nombre ?? codigo, Clase = clase ?? t?.Clase ?? "Haber", Detalle = detalle, Monto = monto });
            }
            bool Afecto(string codigo, Func<TipoItem, bool> flag) => porCodigo.TryGetValue(codigo, out var t) ? flag(t) : true;

            // Días del mes (convención de 30 días).
            var inicio = c.Ingreso > ini ? c.Ingreso : ini;
            var diasVinculo = c.Ingreso > ini ? Math.Max(0, 30 - (c.Ingreso.Day - 1)) : 30;
            var diasCalendario = fin.DayNumber - inicio.DayNumber + 1;
            int Contar(IEnumerable<(DateOnly d, DateOnly h)> rangos)
            {
                var n = rangos.Sum(r => Dias(r.d, r.h, inicio, fin));
                return n >= diasCalendario ? diasVinculo : Math.Min(n, diasVinculo);
            }
            var vac = Contar(vacaciones.Where(v => v.ColaboradorId == c.Id).Select(v => (v.Desde, v.Hasta)));
            var sinGoce = Math.Min(Contar(ausencias.Where(a => a.ColaboradorId == c.Id).Select(a => (a.Desde, a.Hasta))), diasVinculo - vac);
            var laborados = Math.Max(0, diasVinculo - vac - sinGoce);
            b.DiasLaborados = laborados;
            b.DiasVacaciones = vac;
            b.DiasNoTrabajados = sinGoce;

            var mensual = c.Jornal is { } j ? j * 30 : c.SueldoBase ?? c.SueldoNeto;
            var valorDia = c.Jornal ?? mensual / 30;
            var valorHora = R(R(valorDia) / 8);

            Linea("SUE-BAS", valorDia * laborados, $"{laborados} días" + (c.Jornal is { } jj ? $" × S/ {jj.ToString("N2", CultureInfo.GetCultureInfo("en-US"))}" : ""));
            Linea("VAC-REM", valorDia * vac, $"{vac} días");
            foreach (var a in asignaciones.Where(a => a.ColaboradorId == c.Id))
            {
                var t = tipos.FirstOrDefault(x => x.Id == a.TipoItemId);
                if (t is null || !t.Activo || CodigosCalculados.Contains(t.Codigo)) continue;
                Linea(t.Codigo, a.Monto, a.Observacion);
            }
            foreach (var pct in new[] { 25, 35 })
            {
                var min = hhee.Where(h => h.ColaboradorId == c.Id).SelectMany(h => h.Lineas).Where(l => l.Tipo == pct).Sum(l => l.Minutos);
                if (min > 0) Linea($"HEX-{pct}", min / 60m * valorHora * (1 + pct / 100m), $"{min / 60}:{min % 60:00} h");
            }
            var horasRecargo = recargos.Where(r => r.ColaboradorId == c.Id).Sum(r => r.Horas);
            if (horasRecargo > 0) Linea("REC-DOM", horasRecargo * valorHora * 2, $"{horasRecargo.ToString("0.##", CultureInfo.InvariantCulture)} h al 100 %");

            var haberes = b.Lineas.Where(l => l.Clase == "Haber").ToList();
            var basePension = haberes.Where(l => Afecto(l.Codigo, t => t.Pension)).Sum(l => l.Monto);
            var baseEssalud = haberes.Where(l => Afecto(l.Codigo, t => t.Essalud)).Sum(l => l.Monto);
            var baseQuinta = haberes.Where(l => Afecto(l.Codigo, t => t.Quinta)).Sum(l => l.Monto);

            // Pensión.
            var reg = LeerRegimen(c.Pension, c.Contrato);
            b.Regimen = reg.Texto;
            if (reg.Tipo == "ONP") Linea("APO-ONP", basePension * P("onpPct") / 100, $"{P("onpPct"):0.##} %", "Aporte ONP", "Descuento");
            else if (reg.Tipo == "AFP")
            {
                if (!afps.TryGetValue(reg.Afp!, out var afp)) b.Observaciones.Add($"La AFP “{reg.Afp}” no está en la tabla de comisiones: no se calculó la comisión.");
                Linea("AFP-APO", basePension * P("afpAportePct") / 100, $"{P("afpAportePct"):0.##} %", "Aporte obligatorio AFP", "Descuento");
                Linea("AFP-PRI", Math.Min(basePension, P("remMaxAsegurable")) * P("primaSeguroPct") / 100, $"{P("primaSeguroPct"):0.##} %", "Prima de seguro AFP", "Descuento");
                if (reg.ComisionFlujo && afp is not null)
                    Linea("AFP-COM", basePension * afp.ComisionFlujoPct / 100, $"{afp.ComisionFlujoPct:0.##} % sobre el flujo", "Comisión AFP", "Descuento");
            }
            else if (reg.Tipo == "Sin registrar") b.Observaciones.Add("No tiene régimen pensionario en su ficha: no se descontó ONP ni AFP.");

            // Renta de 5ta categoría.
            var propias = previas.Where(x => x.ColaboradorId == c.Id).ToList();
            // Remuneración fija mensual (para proyectar los meses que faltan): básica completa + ítems permanentes afectos.
            var fijos = valorDia * 30 + asignaciones
                .Where(a => a.ColaboradorId == c.Id && a.Hasta == null)
                .Where(a => tipos.FirstOrDefault(t => t.Id == a.TipoItemId) is { Clase: "Haber", Quinta: true, Activo: true } t && !CodigosCalculados.Contains(t.Codigo))
                .Sum(a => a.Monto);
            var gratificaciones = (mes <= 7 ? 2 : 1) * fijos * (1 + P("essaludPct") / 100);
            var rentaAnual = propias.Sum(x => x.BaseQuinta) + baseQuinta + fijos * (12 - mes) + gratificaciones;
            var rentaNeta = rentaAnual - P("deduccionUit") * uit;
            var impuesto = rentaNeta > 0 ? Impuesto(rentaNeta, uit, tramos) : 0;
            var retenido = propias.ToDictionary(x => x.Periodo % 100, x => x.RetencionQuinta);
            var retencion = impuesto > 0 ? RetencionMes(mes, impuesto, retenido) : 0;
            Linea("RET-5TA", retencion, $"Proyección anual S/ {R(rentaAnual).ToString("N2", CultureInfo.GetCultureInfo("en-US"))}");
            b.BaseQuinta = R(baseQuinta);
            b.RetencionQuinta = retencion;

            // EsSalud (empleador): base mínima la RMV proporcional a los días del vínculo.
            if (baseEssalud > 0) Linea("APO-ESS", Math.Max(baseEssalud, rmv * diasVinculo / 30) * P("essaludPct") / 100, $"{P("essaludPct"):0.##} %");

            b.Ingresos = b.Lineas.Where(l => l.Clase == "Haber").Sum(l => l.Monto);
            b.Descuentos = b.Lineas.Where(l => l.Clase == "Descuento").Sum(l => l.Monto);
            b.Aportes = b.Lineas.Where(l => l.Clase == "Aporte empleador").Sum(l => l.Monto);
            b.Neto = b.Ingresos - b.Descuentos;
            if (b.Neto < 0) b.Observaciones.Add("Los descuentos superan los ingresos del mes: revisa los adelantos y descuentos asignados.");
            salida.Add(b);
        }
        return salida;
    }

    /// <summary>Recalcula y reemplaza las boletas (borrador) del período abierto.</summary>
    public async Task<List<Boleta>> ProcesarAsync(int periodo, string usuario)
    {
        var p = await db.Periodos.FindAsync(periodo) ?? throw new InvalidOperationException("El período no existe.");
        if (p.Cerrado) throw new InvalidOperationException("El período está cerrado.");
        var nuevas = await CalcularAsync(periodo);
        db.Boletas.RemoveRange(db.Boletas.Where(b => b.Periodo == periodo));
        db.Boletas.AddRange(nuevas);
        p.ProcesadoEn = DateTime.UtcNow;
        p.ProcesadoPor = usuario;
        await db.SaveChangesAsync();
        return nuevas;
    }

    /// <summary>Recalcula, deja las boletas como definitivas, cierra el período y abre el siguiente.</summary>
    public async Task<List<Boleta>> CerrarAsync(int periodo, string usuario)
    {
        var boletas = await ProcesarAsync(periodo, usuario);
        foreach (var b in boletas) b.Definitiva = true;
        var p = (await db.Periodos.FindAsync(periodo))!;
        p.Cerrado = true;
        p.CerradoEn = DateTime.UtcNow;
        p.CerradoPor = usuario;
        var sig = Siguiente(periodo);
        if (await db.Periodos.FindAsync(sig) is null) db.Periodos.Add(new PeriodoPlanilla { Id = sig });
        await db.SaveChangesAsync();
        return boletas;
    }

    /// <summary>Período abierto (el más antiguo sin cerrar).</summary>
    public async Task<int?> AbiertoAsync() =>
        await db.Periodos.Where(p => !p.Cerrado).OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync();
}
