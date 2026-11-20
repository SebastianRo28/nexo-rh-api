using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Data;

/// <summary>Filas iniciales de cada catálogo genérico (las de Data/genericos.json), solo si el catálogo está vacío.</summary>
public static class SemillaCatalogos
{
    public static async Task CargarAsync(NexoDbContext db, Genericos genericos)
    {
        foreach (var cat in genericos.Catalogos.Values)
        {
            if (await db.FilasCatalogo.AnyAsync(f => f.Clave == cat.Clave)) continue;
            db.FilasCatalogo.AddRange(cat.Filas.Select(f => new FilaCatalogo { Clave = cat.Clave, Datos = f.ToJsonString(), ModificadoPor = "Sistema" }));
            if (cat.Clave == "feriados")
            {
                // La tabla de Asistencia ya tiene las fechas: se completa el alcance.
                var feriados = await db.Feriados.ToListAsync();
                foreach (var f in cat.Filas)
                    if (DateOnly.TryParseExact(f["fecha"]?.ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                        && feriados.FirstOrDefault(x => x.Fecha == d) is { } fer)
                        fer.Alcance = f["alcance"]?.ToString() ?? "Nacional";
            }
        }
        await db.SaveChangesAsync();
    }
}
