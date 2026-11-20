using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Data;

namespace NexoRh.Api.Services;

/// <summary>
/// Configuraciones de módulos guardadas en el servidor. Cada clave tiene una lista cerrada de campos con su tipo, rango y
/// valor por defecto (los mismos del formulario del frontend); lo guardado se combina con los valores por defecto.
/// </summary>
public class Configuraciones(NexoDbContext db)
{
    private abstract record Campo(object Defecto);
    private sealed record Interruptor(bool Valor) : Campo(Valor);
    private sealed record Numero(int Valor, int Min, int Max) : Campo(Valor);

    private static readonly Dictionary<string, Dictionary<string, Campo>> Esquemas = new()
    {
        // Control de Asistencia › Configuraciones › Tolerancias y alertas.
        ["ca-tolerancias"] = new()
        {
            ["toleranciaSalida"] = new Numero(5, 0, 60), ["minExceso"] = new Numero(30, 1, 120), ["contarAntes"] = new Interruptor(true),
            ["alDisminucion"] = new Interruptor(true), ["umbralDisminucion"] = new Numero(15, 5, 50), ["alSinFoto"] = new Interruptor(true),
            ["alIncompletos"] = new Interruptor(true), ["alHhee"] = new Interruptor(true), ["alLunes"] = new Interruptor(true), ["lunes"] = new Numero(2, 1, 4),
            ["alInasistencias"] = new Interruptor(true), ["inasistencias"] = new Numero(3, 1, 10), ["alDomingos"] = new Interruptor(true), ["domingos"] = new Numero(2, 1, 5),
        },
        // Control de Asistencia › Configuraciones › Métodos de marcación.
        ["ca-metodos"] = new()
        {
            ["web"] = new Interruptor(true), ["app"] = new Interruptor(true), ["qr"] = new Interruptor(true), ["foto"] = new Interruptor(false),
            ["exigirFoto"] = new Interruptor(true), ["exigirRadio"] = new Interruptor(true), ["radioDefecto"] = new Numero(150, 20, 2000),
        },
    };

    public static IEnumerable<string> Claves => Esquemas.Keys;
    public static bool Existe(string clave) => Esquemas.ContainsKey(clave);

    /// <summary>Valores vigentes: lo guardado sobre los valores por defecto.</summary>
    public async Task<Dictionary<string, object>> LeerAsync(string clave)
    {
        var esquema = Esquemas[clave];
        var valores = esquema.ToDictionary(kv => kv.Key, kv => kv.Value.Defecto);
        var fila = await db.Configuraciones.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave);
        if (fila is null) return valores;
        using var doc = JsonDocument.Parse(fila.Valores);
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            if (!esquema.TryGetValue(p.Name, out var campo)) continue;
            if (campo is Interruptor && p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) valores[p.Name] = p.Value.GetBoolean();
            else if (campo is Numero && p.Value.TryGetInt32(out var n)) valores[p.Name] = n;
        }
        return valores;
    }

    public async Task<bool> InterruptorAsync(string clave, string campo) => (await LeerAsync(clave))[campo] is true;

    /// <summary>Valida los valores enviados (solo campos conocidos, con su tipo y rango). Devuelve errores por campo.</summary>
    public static Dictionary<string, string[]> Validar(string clave, JsonElement valores, out Dictionary<string, object> limpios)
    {
        var errores = new Dictionary<string, string[]>();
        limpios = [];
        if (valores.ValueKind != JsonValueKind.Object) { errores["valores"] = ["Los valores deben ser un objeto."]; return errores; }
        var esquema = Esquemas[clave];
        foreach (var p in valores.EnumerateObject())
        {
            if (!esquema.TryGetValue(p.Name, out var campo)) { errores[p.Name] = ["Campo desconocido."]; continue; }
            switch (campo)
            {
                case Interruptor when p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    limpios[p.Name] = p.Value.GetBoolean();
                    break;
                case Numero n when p.Value.TryGetInt32(out var v) && v >= n.Min && v <= n.Max:
                    limpios[p.Name] = v;
                    break;
                case Numero n:
                    errores[p.Name] = [$"Debe ser un número entero entre {n.Min} y {n.Max}."];
                    break;
                default:
                    errores[p.Name] = ["Debe ser verdadero o falso."];
                    break;
            }
        }
        return errores;
    }
}
