using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Data;

namespace NexoRh.Api.Services;

/// <summary>
/// Configuraciones de módulos guardadas en el servidor. Cada clave tiene una lista cerrada de campos (tipo, rango,
/// opciones y valor por defecto) definida en Data/genericos.json; lo guardado se combina con los valores por defecto.
/// </summary>
public class Configuraciones(NexoDbContext db, Genericos genericos)
{
    /// <summary>Las de Control de Asistencia las usan todos los usuarios (marcar, calcular la jornada); el resto, solo administradores.</summary>
    public static bool Publica(string clave) => clave.StartsWith("ca-");

    public bool Existe(string clave) => genericos.Formularios.ContainsKey(clave);
    public IEnumerable<string> Claves => genericos.Formularios.Keys;

    /// <summary>Valores vigentes: lo guardado sobre los valores por defecto (bool, decimal o string).</summary>
    public async Task<Dictionary<string, object>> LeerAsync(string clave)
    {
        var f = genericos.Formularios[clave];
        var valores = f.Campos.ToDictionary(c => c.Key, c => Genericos.Defecto(f.Valores[c.Key]));
        var fila = await db.Configuraciones.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave);
        if (fila is null) return valores;
        using var doc = JsonDocument.Parse(fila.Valores);
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            var c = f.Campos.FirstOrDefault(x => x.Key == p.Name);
            if (c is null) continue;
            if (c.Tipo == "switch" && p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) valores[p.Name] = p.Value.GetBoolean();
            else if (c.Tipo == "numero" && p.Value.TryGetDecimal(out var n)) valores[p.Name] = n;
            else if (c.Tipo is not ("switch" or "numero") && p.Value.ValueKind == JsonValueKind.String) valores[p.Name] = p.Value.GetString()!;
        }
        return valores;
    }

    public async Task<bool> InterruptorAsync(string clave, string campo) => (await LeerAsync(clave))[campo] is true;

    public Dictionary<string, string[]> Validar(string clave, JsonElement valores, out Dictionary<string, object> limpios) =>
        genericos.ValidarFormulario(genericos.Formularios[clave], valores, out limpios);
}
