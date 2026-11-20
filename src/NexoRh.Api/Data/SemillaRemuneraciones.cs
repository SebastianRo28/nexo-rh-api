using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Data;

/// <summary>
/// Datos de ejemplo de Remuneraciones: catálogo de ítems, parámetros legales de referencia, ítems asignados y régimen
/// pensionario. Enero y febrero de 2026 se calculan con el motor de planilla y quedan cerrados; marzo queda abierto.
/// Los valores legales son de referencia: verificarlos con la fuente oficial (MTPE, SUNAT, SBS) antes de usarlos.
/// </summary>
public static class SemillaRemuneraciones
{
    public static async Task CargarAsync(NexoDbContext db)
    {
        if (!await db.Colaboradores.AnyAsync()) return;
        if (!await db.TiposItem.AnyAsync())
        {
            db.TiposItem.AddRange(Tipos());
            db.ParametrosRem.AddRange(Parametros());
            db.ComisionesAfp.AddRange(
                new ComisionAfp { Afp = "Habitat", ComisionFlujoPct = 1.47m, ComisionSaldoPct = 1.25m },
                new ComisionAfp { Afp = "Integra", ComisionFlujoPct = 1.55m, ComisionSaldoPct = 0.78m },
                new ComisionAfp { Afp = "Prima", ComisionFlujoPct = 1.6m, ComisionSaldoPct = 1.25m },
                new ComisionAfp { Afp = "Profuturo", ComisionFlujoPct = 1.69m, ComisionSaldoPct = 1.2m });
            db.TramosRenta.AddRange(
                new TramoRenta { Desde = 0, Hasta = 5, TasaPct = 8 }, new TramoRenta { Desde = 5, Hasta = 20, TasaPct = 14 },
                new TramoRenta { Desde = 20, Hasta = 35, TasaPct = 17 }, new TramoRenta { Desde = 35, Hasta = 45, TasaPct = 20 },
                new TramoRenta { Desde = 45, Hasta = null, TasaPct = 30 });
            await db.SaveChangesAsync();

            AsignacionItem A(int c, int tipo, decimal monto, int desde, int? hasta = null, string obs = "") =>
                new() { ColaboradorId = c, TipoItemId = tipo, Monto = monto, Desde = desde, Hasta = hasta, Observacion = obs };
            var existentes = (await db.Colaboradores.Select(c => c.Id).ToListAsync()).ToHashSet();
            db.AsignacionesItem.AddRange(new[]
            {
                A(1, 2, 113, 202501), A(3, 2, 113, 202501), A(5, 2, 113, 202501), A(9, 2, 113, 202501), A(13, 2, 113, 202506),
                A(1, 9, 250, 202601, null, "Traslado a almacén Lurín"), A(9, 9, 200, 202601, 202606, "Supervisión de tiendas"),
                A(7, 10, 500, 202603, 202603, "Solicitado el 05/03/2026"), A(4, 10, 800, 202602, 202602),
            }.Where(a => existentes.Contains(a.ColaboradorId)));

            // Régimen pensionario de quienes no lo tienen en su ficha.
            var regimenes = new Dictionary<int, string>
            {
                [1] = "AFP Integra · comisión Saldo", [2] = "ONP", [3] = "AFP Prima · comisión Flujo", [4] = "AFP Profuturo · comisión Flujo",
                [5] = "AFP Habitat · comisión Saldo", [6] = "ONP", [7] = "ONP", [8] = "AFP Integra · comisión Flujo", [9] = "AFP Prima · comisión Saldo",
                [10] = "AFP Habitat · comisión Flujo", [11] = "AFP Integra · comisión Flujo", [13] = "ONP", [14] = "AFP Profuturo · comisión Saldo",
                [15] = "AFP Habitat · comisión Flujo",
            };
            foreach (var c in await db.Colaboradores.Where(c => c.Pension == null).ToListAsync())
                if (regimenes.TryGetValue(c.Id, out var r)) c.Pension = r;
            await db.SaveChangesAsync();
        }

        if (!await db.Periodos.AnyAsync())
        {
            var planilla = new Planilla(db);
            db.Periodos.Add(new PeriodoPlanilla { Id = 202601 });
            await db.SaveChangesAsync();
            await planilla.CerrarAsync(202601, "Lucía Cáceres");
            await planilla.CerrarAsync(202602, "Lucía Cáceres");
        }
    }

    private static IEnumerable<TipoItem> Tipos()
    {
        TipoItem T(int id, string codigo, string nombre, string clase, string plame, bool afecto, bool automatico = false, bool? quinta = null) =>
            new() { Id = id, Codigo = codigo, Nombre = nombre, Clase = clase, Plame = plame, Essalud = afecto, Pension = afecto, Quinta = quinta ?? afecto, Automatico = automatico };
        return
        [
            T(1, "SUE-BAS", "Remuneración básica", "Haber", "0121", true, true),
            T(2, "ASI-FAM", "Asignación familiar", "Haber", "0201", true),
            T(3, "HEX-25", "Horas extras 25 %", "Haber", "0105", true, true),
            T(4, "HEX-35", "Horas extras 35 %", "Haber", "0106", true, true),
            T(5, "REC-DOM", "Trabajo en día de descanso o feriado", "Haber", "0107", true, true),
            T(6, "VAC-REM", "Remuneración vacacional", "Haber", "0118", true, true),
            T(7, "GRA-FPN", "Gratificación Fiestas Patrias y Navidad", "Haber", "0406", false, true, true),
            T(8, "BON-EXT", "Bonificación extraordinaria Ley 29351", "Haber", "0312", false, true, true),
            T(9, "MOV-ASI", "Movilidad supeditada a asistencia", "Haber", "0909", false),
            T(10, "ADE-SUE", "Adelanto de sueldo", "Descuento", "0701", false),
            T(11, "DSC-INA", "Inasistencias", "Descuento", "0705", false, true),
            T(12, "RET-5TA", "Retención renta de quinta categoría", "Descuento", "0605", false, true),
            T(13, "APO-ESS", "EsSalud", "Aporte empleador", "0804", false, true),
            T(14, "BON-KPI", "Bonificación por cumplimiento de KPI", "Haber", "0303", true, true),
            T(15, "APO-ONP", "Aporte ONP", "Descuento", "0607", false, true),
            T(16, "AFP-APO", "Aporte obligatorio AFP", "Descuento", "0608", false, true),
            T(17, "AFP-COM", "Comisión AFP", "Descuento", "0601", false, true),
            T(18, "AFP-PRI", "Prima de seguro AFP", "Descuento", "0606", false, true),
        ];
    }

    private static IEnumerable<ParametroRem> Parametros() =>
    [
        new() { Clave = "rmv", Nombre = "Remuneración Mínima Vital (RMV)", Valor = 1130, Unidad = "S/", VigenteDesde = "01/01/2025", Fuente = "D.S. 006-2024-TR" },
        new() { Clave = "uit", Nombre = "Unidad Impositiva Tributaria (UIT)", Valor = 5500, Unidad = "S/", VigenteDesde = "01/01/2026", Fuente = "SUNAT" },
        new() { Clave = "asigFamiliarPct", Nombre = "Asignación familiar", Valor = 10, Unidad = "%", VigenteDesde = "01/01/2025", Fuente = "Ley 25129 (% de la RMV)" },
        new() { Clave = "essaludPct", Nombre = "Aporte EsSalud (empleador)", Valor = 9, Unidad = "%", VigenteDesde = "01/01/2025", Fuente = "Ley 26790" },
        new() { Clave = "onpPct", Nombre = "Aporte ONP", Valor = 13, Unidad = "%", VigenteDesde = "01/01/2025", Fuente = "D.L. 19990" },
        new() { Clave = "afpAportePct", Nombre = "Aporte obligatorio AFP", Valor = 10, Unidad = "%", VigenteDesde = "01/01/2025", Fuente = "SBS" },
        new() { Clave = "primaSeguroPct", Nombre = "Prima de seguro AFP", Valor = 1.37m, Unidad = "%", VigenteDesde = "01/01/2025", Fuente = "SBS" },
        new() { Clave = "remMaxAsegurable", Nombre = "Remuneración máxima asegurable (prima AFP)", Valor = 12027.80m, Unidad = "S/", VigenteDesde = "01/01/2026", Fuente = "SBS · verificar el valor del trimestre" },
        new() { Clave = "deduccionUit", Nombre = "Deducción anual renta de 5ta categoría", Valor = 7, Unidad = "UIT", VigenteDesde = "01/01/2025", Fuente = "Ley del Impuesto a la Renta, art. 46" },
    ];
}
