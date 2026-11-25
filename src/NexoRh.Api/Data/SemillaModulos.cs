using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Data;

/// <summary>
/// Datos de ejemplo de los módulos que guardan sus registros como JSON (KPI, beneficios, cursos, tickets, dashboards…),
/// solo para la empresa de ejemplo. Data/demo-modulos.json se generó desde los datos del diseño. Cada colección se carga si
/// el tenant todavía no tiene registros en ella; los ids de referencia ("kpiId", "beneficioId", "cursoId") se reasignan a
/// los ids nuevos. También completa el anexo de los colaboradores de ejemplo que no lo tengan.
/// </summary>
public static class SemillaModulos
{
    public static async Task CargarAsync(NexoDbContext db)
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Data", "demo-modulos.json");
        if (!File.Exists(ruta)) return;
        var raiz = JsonNode.Parse(await File.ReadAllTextAsync(ruta))!.AsObject();
        var referencias = raiz["$referencias"]?.AsObject() ?? [];
        var existentes = (await db.RegistrosModulo.AsNoTracking().Select(r => r.Coleccion).Distinct().ToListAsync()).ToHashSet();
        var ids = new Dictionary<string, Dictionary<int, int>>();

        foreach (var (clave, nodo) in raiz)
        {
            if (clave.StartsWith('$') || nodo is not JsonArray filas || ColeccionesModulo.Buscar(clave) is not { } col) continue;
            if (existentes.Contains(clave))
            {
                // Already loaded: keep the old→new map from the "ref" stored with each row so later collections can use it.
                ids[clave] = (await db.RegistrosModulo.AsNoTracking().Where(r => r.Coleccion == clave).ToListAsync())
                    .Select(r => (r.Id, Ref: JsonNode.Parse(r.Datos)?["ref"]?.GetValue<int>()))
                    .Where(x => x.Ref is not null).ToDictionary(x => x.Ref!.Value, x => x.Id);
                continue;
            }
            var nuevos = new List<(int? Ref, RegistroModulo Reg)>();
            foreach (var f in filas.OfType<JsonObject>())
            {
                var datos = f.DeepClone().AsObject();
                var refId = datos["ref"]?.GetValue<int>();
                datos.Remove("id");
                if (referencias[clave] is JsonObject refs)
                    foreach (var (campo, destino) in refs)
                        if (datos[campo] is JsonValue v && v.TryGetValue<int>(out var viejo) && ids.TryGetValue(destino!.GetValue<string>(), out var mapa) && mapa.TryGetValue(viejo, out var nuevo))
                            datos[campo] = nuevo;
                int? dueno = col.Propios && datos["colaboradorId"] is JsonValue c && c.TryGetValue<int>(out var x) ? x : null;
                var reg = new RegistroModulo { Coleccion = clave, ColaboradorId = dueno, Datos = datos.ToJsonString(), ModificadoPor = "Semilla" };
                db.RegistrosModulo.Add(reg);
                nuevos.Add((refId, reg));
            }
            await db.SaveChangesAsync();
            ids[clave] = nuevos.Where(n => n.Ref is not null).ToDictionary(n => n.Ref!.Value, n => n.Reg.Id);
        }

        if (raiz["$anexos"] is JsonObject anexos)
        {
            var colaboradores = await db.Colaboradores.Where(c => c.Anexo == null).ToListAsync();
            foreach (var c in colaboradores)
                if (anexos[c.Id.ToString()]?.GetValue<string>() is { } anexo) c.Anexo = anexo;
            await db.SaveChangesAsync();
        }
    }
}
