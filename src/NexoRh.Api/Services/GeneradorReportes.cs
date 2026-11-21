using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Services;

/// <summary>Plantilla de reporte. Generable = Nexo RH ya tiene los datos para armarla; RequierePeriodo = reporte de planilla.</summary>
public record PlantillaReporte(string Titulo, string Categoria, string Tipo, string Descripcion, bool Generable, bool RequierePeriodo);

public record TablaReporte(string[] Columnas, List<string[]> Filas);

/// <summary>
/// Arma los reportes de Exportadores con los datos actuales de la base (colaboradores, planilla, vacaciones,
/// compensaciones y headcount) y los entrega como CSV (UTF-8 con BOM y separador “;”, como los abre Excel en español).
/// </summary>
public class GeneradorReportes(NexoDbContext db, RelojAsistencia reloj)
{
    private const string Adm = "Administrativo", Rem = "Remuneraciones";
    /// <summary>Fecha del saldo inicial de vacaciones importado (igual que en Control de Asistencia).</summary>
    public static readonly DateOnly InicioSaldos = new(2025, 1, 1);
    public const int DiasVacacionesAnuales = 30;

    public static readonly PlantillaReporte[] Plantillas =
    [
        new("Reporte en blanco", "Todas", "blank", "Arma un reporte desde cero eligiendo las columnas.", false, false),
        new("Maestro de empleados", Adm, "pre", "Datos personales, contractuales y organizacionales de cada colaborador.", true, false),
        new("Nómina", Rem, "pre", "Haberes, descuentos y aportes por colaborador y proceso.", true, true),
        new("Boletas de Pago", Rem, "pre", "Resumen de boletas por colaborador y período.", true, true),
        new("Liquidaciones", Rem, "pre", "Montos de liquidación de beneficios sociales por término.", false, false),
        new("Nóminas de Pagos Parciales", Rem, "pre", "Detalle de pagos parciales y adelantos.", false, false),
        new("Registro de Vacaciones", Adm, "pre", "Vacaciones tomadas por colaborador con fechas y estado.", true, false),
        new("Saldos de Vacaciones", Adm, "pre", "Saldo de vacaciones a la fecha de corte.", true, false),
        new("Saldo de Vacaciones por Período", Adm, "pre", "Saldo desglosado por período de devengo.", false, false),
        new("CTS", Rem, "pre", "Cálculo de CTS semestral por colaborador.", false, false),
        new("Gratificación", Rem, "pre", "Gratificaciones de julio y diciembre con bonificación extraordinaria.", false, false),
        new("Resumen de Procesos", Rem, "pre", "Totales por proceso de pago.", true, false),
        new("Contabilidad", Rem, "pre", "Asientos contables de la planilla.", false, false),
        new("Compensaciones", Adm, "pre", "Sueldos, bandas y ubicación en banda.", true, false),
        new("Ítems", Rem, "pre", "Ítems asignados por colaborador y período.", true, true),
        new("Retención de 5ta", Rem, "pre", "Proyección y retención del impuesto de quinta categoría.", true, true),
        new("Reporte Compañeros de Banda", Adm, "pre", "Colaboradores que comparten banda salarial.", false, false),
        new("Competencias", "Desarrollo Organizacional", "pre", "Competencias asociadas a cada cargo.", false, false),
        new("Posiciones del plan de headcount", Adm, "pre", "Posiciones planificadas, ocupadas y vacantes.", true, false),
        new("Líneas Contables", Rem, "pre", "Detalle de líneas contables por cuenta y centro de costo.", false, false),
        new("Maestro de empleados · RR.HH.", Adm, "custom", "Copia personalizada con atributos propios de la empresa.", true, false),
    ];

    public static PlantillaReporte? Plantilla(string titulo) => Plantillas.FirstOrDefault(p => p.Titulo == titulo);

    private static readonly CultureInfo Num = CultureInfo.GetCultureInfo("es-PE");
    private static string M(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string D(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string N(Colaborador c) => $"{c.Apellidos}, {c.Nombres}";

    public async Task<TablaReporte> Generar(PlantillaReporte p, int? periodo)
    {
        var cols = await db.Colaboradores.AsNoTracking().OrderBy(c => c.Apellidos).ThenBy(c => c.Nombres).ToListAsync();
        switch (p.Titulo)
        {
            case "Maestro de empleados":
                return new(["Apellidos", "Nombres", "Tipo de documento", "Número de documento", "Cargo", "División", "Área", "Sub-área", "Fecha de ingreso", "Jefatura", "Sueldo neto", "Sueldo base"],
                    cols.Select(c => new[] { c.Apellidos, c.Nombres, c.TipoDocumento, c.Documento, c.Cargo, c.Division, c.Area, c.SubArea, Fechas.Texto(c.Ingreso),
                        cols.FirstOrDefault(j => j.Id == c.JefeId) is { } j ? N(j) : "", M(c.SueldoNeto), c.SueldoBase is { } b ? M(b) : "" }).ToList());

            case "Maestro de empleados · RR.HH.":
                return new(["Apellidos", "Nombres", "Número de documento", "Cargo", "Área", "Fecha de ingreso", "Fecha de nacimiento", "Correo corporativo", "Teléfono",
                        "Contrato", "Jornada", "Previsión", "CUSPP", "Plan de salud", "Banco", "Forma de pago"],
                    cols.Select(c => new[] { c.Apellidos, c.Nombres, c.Documento, c.Cargo, c.Area, Fechas.Texto(c.Ingreso), Fechas.Texto(c.Nacimiento) ?? "", c.EmailCorporativo ?? "",
                        c.Telefono ?? "", c.Contrato ?? "", c.Jornada ?? "", c.Pension ?? "", c.Cuspp ?? "", c.PlanSalud ?? "", c.Banco ?? "", c.FormaPago ?? "" }).ToList());

            case "Boletas de Pago":
            {
                var bs = await db.Boletas.AsNoTracking().Where(b => b.Periodo == periodo).OrderBy(b => b.Nombre).ToListAsync();
                return new(["Período", "Colaborador", "Documento", "Cargo", "Régimen", "Días laborados", "Días de vacaciones", "Días no trabajados", "Ingresos", "Descuentos", "Aportes del empleador", "Neto a pagar"],
                    bs.Select(b => new[] { Planilla.Texto(b.Periodo), b.Nombre, b.Documento, b.Cargo, b.Regimen, b.DiasLaborados.ToString(), b.DiasVacaciones.ToString(),
                        b.DiasNoTrabajados.ToString(), M(b.Ingresos), M(b.Descuentos), M(b.Aportes), M(b.Neto) }).ToList());
            }

            case "Nómina":
            {
                var bs = await db.Boletas.AsNoTracking().Where(b => b.Periodo == periodo).OrderBy(b => b.Nombre).ToListAsync();
                return new(["Período", "Colaborador", "Documento", "Código", "Concepto", "Clase", "Detalle", "Monto"],
                    bs.SelectMany(b => b.Lineas.Select(l => new[] { Planilla.Texto(b.Periodo), b.Nombre, b.Documento, l.Codigo, l.Nombre, l.Clase, l.Detalle, M(l.Monto) })).ToList());
            }

            case "Retención de 5ta":
            {
                var bs = await db.Boletas.AsNoTracking().Where(b => b.Periodo == periodo).OrderBy(b => b.Nombre).ToListAsync();
                return new(["Período", "Colaborador", "Documento", "Base de quinta (proyectada)", "Retención del mes"],
                    bs.Select(b => new[] { Planilla.Texto(b.Periodo), b.Nombre, b.Documento, M(b.BaseQuinta), M(b.RetencionQuinta) }).ToList());
            }

            case "Ítems":
            {
                var tipos = await db.TiposItem.AsNoTracking().ToDictionaryAsync(t => t.Id);
                var p0 = periodo ?? 0;
                var asign = await db.AsignacionesItem.AsNoTracking().Where(a => a.Desde <= p0 && (a.Hasta == null || a.Hasta >= p0)).ToListAsync();
                return new(["Período", "Colaborador", "Documento", "Código", "Ítem", "Clase", "Monto", "Desde", "Hasta", "Observación"],
                    asign.Select(a => (a, c: cols.FirstOrDefault(c => c.Id == a.ColaboradorId), t: tipos.GetValueOrDefault(a.TipoItemId)))
                        .Where(x => x.c is not null).OrderBy(x => x.c!.Apellidos).ThenBy(x => x.t?.Nombre)
                        .Select(x => new[] { Planilla.Texto(p0), N(x.c!), x.c!.Documento, x.t?.Codigo ?? "", x.t?.Nombre ?? "", x.t?.Clase ?? "", M(x.a.Monto),
                            Planilla.Texto(x.a.Desde), x.a.Hasta is { } h ? Planilla.Texto(h) : "", x.a.Observacion }).ToList());
            }

            case "Resumen de Procesos":
            {
                var tot = await db.Boletas.GroupBy(b => b.Periodo).Select(g => new { g.Key, N = g.Count(), I = g.Sum(b => b.Ingresos), De = g.Sum(b => b.Descuentos), A = g.Sum(b => b.Aportes), Ne = g.Sum(b => b.Neto) })
                    .ToDictionaryAsync(x => x.Key);
                var ps = await db.Periodos.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync();
                return new(["Período", "Estado", "Boletas", "Ingresos", "Descuentos", "Aportes del empleador", "Neto a pagar", "Procesado por", "Procesado el"],
                    ps.Select(x =>
                    {
                        tot.TryGetValue(x.Id, out var t);
                        return new[] { Planilla.Texto(x.Id), x.Cerrado ? "Cerrado" : x.ProcesadoEn is null ? "Abierto" : "Procesado", (t?.N ?? 0).ToString(),
                            M(t?.I ?? 0), M(t?.De ?? 0), M(t?.A ?? 0), M(t?.Ne ?? 0), x.ProcesadoPor, x.ProcesadoEn is { } f ? Fechas.TextoHora(f) : "" };
                    }).ToList());
            }

            case "Registro de Vacaciones":
            {
                var vs = await db.Vacaciones.AsNoTracking().OrderByDescending(v => v.Desde).ToListAsync();
                return new(["Colaborador", "Documento", "Área", "Desde", "Hasta", "Días", "Estado", "Origen", "Observación"],
                    vs.Select(v => (v, c: cols.FirstOrDefault(c => c.Id == v.ColaboradorId))).Where(x => x.c is not null)
                        .Select(x => new[] { N(x.c!), x.c!.Documento, x.c.Area, Fechas.Texto(x.v.Desde), Fechas.Texto(x.v.Hasta), x.v.Dias.ToString(),
                            x.v.Desde <= reloj.Hoy ? "Tomadas" : "Programadas", x.v.Origen, x.v.Observacion }).ToList());
            }

            case "Saldos de Vacaciones":
            {
                var corte = reloj.Hoy;
                var vs = await db.Vacaciones.AsNoTracking().ToListAsync();
                return new(["Colaborador", "Documento", "Área", "Fecha de ingreso", "Fecha de corte", "Saldo inicial (01/01/2025)", "Devengado", "Tomados", "Programados", "Saldo"],
                    cols.Select(c =>
                    {
                        var dev = Devengado(c, corte);
                        var tom = vs.Where(v => v.ColaboradorId == c.Id && v.Desde <= corte).Sum(v => v.Dias);
                        var prog = vs.Where(v => v.ColaboradorId == c.Id && v.Desde > corte).Sum(v => v.Dias);
                        return new[] { N(c), c.Documento, c.Area, Fechas.Texto(c.Ingreso), Fechas.Texto(corte), D(c.SaldoVacacionesInicial), D(dev), tom.ToString(), prog.ToString(), D(Math.Round(dev - tom - prog, 2)) };
                    }).ToList());
            }

            case "Compensaciones":
            {
                var bandas = await db.Bandas.AsNoTracking().ToListAsync();
                var comp = await db.Compensaciones.AsNoTracking().ToDictionaryAsync(x => x.ColaboradorId);
                return new(["Colaborador", "Cargo", "Área", "Sueldo base", "Compensación teórica", "Mínimo de la banda", "Objetivo", "Máximo de la banda", "Ubicación", "Compa ratio"],
                    cols.Select(c =>
                    {
                        var sueldo = comp.GetValueOrDefault(c.Id)?.CompTeorica ?? c.SueldoBase ?? c.SueldoNeto;
                        var b = bandas.FirstOrDefault(x => x.Cargo == c.Cargo);
                        if (b is null) return new[] { N(c), c.Cargo, c.Area, M(c.SueldoBase ?? c.SueldoNeto), comp.GetValueOrDefault(c.Id)?.CompTeorica is { } t0 ? M(t0) : "", "", "", "", "Sin banda", "" };
                        decimal min = Math.Round(b.Objetivo * b.PctMin / 100, 2), max = Math.Round(b.Objetivo * b.PctMax / 100, 2);
                        var ub = sueldo < min ? "Bajo la banda" : sueldo > max ? "Sobre la banda" : "En la banda";
                        return new[] { N(c), c.Cargo, c.Area, M(c.SueldoBase ?? c.SueldoNeto), comp.GetValueOrDefault(c.Id)?.CompTeorica is { } t ? M(t) : "",
                            M(min), M(b.Objetivo), M(max), ub, (sueldo / b.Objetivo).ToString("0.00", CultureInfo.InvariantCulture) };
                    }).ToList());
            }

            case "Posiciones del plan de headcount":
            {
                var ps = await db.Posiciones.AsNoTracking().OrderBy(x => x.Codigo).ToListAsync();
                return new(["Código", "Cargo", "División", "Área", "Sub-área", "Ingreso proyectado", "Término", "Estado"],
                    ps.Select(x => new[] { x.Codigo, x.Cargo, x.Division, x.Area, x.SubArea, Fechas.Texto(x.Ingreso), Fechas.Texto(x.Termino) ?? "", x.Vigente ? "Vigente" : "No vigente" }).ToList());
            }
        }
        throw new InvalidOperationException($"La plantilla {p.Titulo} no se puede generar.");
    }

    /// <summary>Saldo inicial más 30 días por año, proporcional, desde el 01/01/2025 o el ingreso (igual que Control de Asistencia).</summary>
    public static decimal Devengado(Colaborador c, DateOnly corte)
    {
        if (c.Ingreso > corte) return 0;
        var desde = c.Ingreso > InicioSaldos ? c.Ingreso : InicioSaldos;
        var dias = corte.DayNumber - desde.DayNumber;
        return Math.Round(c.SaldoVacacionesInicial + dias / 365m * DiasVacacionesAnuales, 2);
    }

    public static byte[] Csv(TablaReporte t)
    {
        static string E(string s) => s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var sb = new StringBuilder();
        foreach (var fila in t.Filas.Prepend(t.Columnas)) sb.Append(string.Join(';', fila.Select(x => E(x ?? "")))).Append("\r\n");
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }
}
