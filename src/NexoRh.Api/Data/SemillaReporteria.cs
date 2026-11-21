using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Data;

/// <summary>Reportes guardados de ejemplo (Exportadores). El historial de ejecuciones y de cargas empieza vacío.</summary>
public static class SemillaReporteria
{
    public static async Task CargarAsync(NexoDbContext db)
    {
        if (await db.Reportes.AnyAsync()) return;
        static DateOnly F(string s) => DateOnly.ParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        db.Reportes.AddRange(new (string n, string p, string c, string f, string por)[]
        {
            ("Maestro de empleados – Marzo", "Maestro de empleados", "Administrativo", "02/03/2026", "Rodrigo Paredes"),
            ("Nómina mensual consolidada", "Nómina", "Remuneraciones", "15/01/2026", "Lucía Cáceres"),
            ("Saldos de vacaciones por área", "Saldos de Vacaciones", "Administrativo", "10/02/2026", "Rodrigo Paredes"),
            ("Retención de 5ta 2026", "Retención de 5ta", "Remuneraciones", "08/01/2026", "Rodrigo Paredes"),
            ("Boletas de pago – resumen", "Boletas de Pago", "Remuneraciones", "28/02/2026", "Lucía Cáceres"),
            ("Bandas y compensaciones", "Compensaciones", "Administrativo", "20/02/2026", "Rodrigo Paredes"),
        }.Select(r => new ReporteGuardado { Nombre = r.n, Plantilla = r.p, Categoria = r.c, Creado = F(r.f), CreadoPor = r.por }));
        await db.SaveChangesAsync();
    }
}
