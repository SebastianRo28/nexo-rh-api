using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NexoRh.Api.Services;

/// <summary>
/// Definiciones de los catálogos genéricos (empresas, centros de costo, feriados…) y de los formularios de
/// configuración, exportadas del frontend a Data/genericos.json (tools/exportar-genericos.js). Con ellas la API valida
/// cada fila o valor que recibe: campos conocidos, tipo, obligatorios, únicos, patrones, rangos y opciones.
/// </summary>
public class Genericos
{
    public record Patron(string Re, string Msg);
    public record RequeridoSi(string Campo, string Valor);
    public record Campo(string Key, string Label, string Tipo, JsonElement? Opciones, bool Obligatorio, RequeridoSi? RequeridoSi, bool Unico,
        Patron? Patron, decimal? Min, decimal? Max, string? DependeDe);
    public record Referencia(string Clave, string Campo);
    public record Catalogo(string Clave, string Singular, List<Campo> Campos, List<JsonObject> Filas, List<Referencia> ReferenciadoPor);
    public record Formulario(string Clave, List<Campo> Campos, JsonObject Valores);
    private record Archivo(List<Catalogo> Catalogos, List<Formulario> Formularios);

    public IReadOnlyDictionary<string, Catalogo> Catalogos { get; }
    public IReadOnlyDictionary<string, Formulario> Formularios { get; }

    public Genericos(IWebHostEnvironment env)
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Data", "genericos.json");
        if (!File.Exists(ruta)) ruta = Path.Combine(env.ContentRootPath, "Data", "genericos.json");
        var a = JsonSerializer.Deserialize<Archivo>(File.ReadAllText(ruta), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("No se pudo leer Data/genericos.json.");
        Catalogos = a.Catalogos.ToDictionary(c => c.Clave);
        Formularios = a.Formularios.ToDictionary(f => f.Clave);
    }

    /// <summary>Opciones fijas de un select, o null si vienen de otra fuente ("colaboradores", "catalogo:x"…).</summary>
    public static string[]? OpcionesFijas(Campo c) =>
        c.Opciones is { ValueKind: JsonValueKind.Array } o ? o.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : null;
    public static string? Fuente(Campo c) => c.Opciones is { ValueKind: JsonValueKind.String } o ? o.GetString() : null;

    private static readonly Regex Email = new(@"^[^\s@]+@[^\s@]+\.[A-Za-z]{2,}$");

    /// <summary>Texto con que se compara una fila para unicidad y referencias (primer campo).</summary>
    public static string Clave(JsonObject fila, string campo) => (fila[campo]?.ToString() ?? "").Split(" · ")[0].Trim();

    /// <summary>
    /// Valida una fila completa de un catálogo. <paramref name="otras"/> son las demás filas del catálogo (para los únicos)
    /// y <paramref name="valoresDe"/> devuelve los valores de referencia de otro catálogo (primer campo).
    /// </summary>
    public Dictionary<string, string[]> ValidarFila(Catalogo cat, JsonElement entrada, IEnumerable<JsonObject> otras,
        Func<string, IEnumerable<string>> valoresDe, out JsonObject limpia, IReadOnlyList<string>? departamentos = null)
    {
        var e = new Dictionary<string, string[]>();
        limpia = [];
        if (entrada.ValueKind != JsonValueKind.Object) { e["fila"] = ["La fila debe ser un objeto."]; return e; }
        var conocidos = cat.Campos.Select(c => c.Key).ToHashSet();
        foreach (var p in entrada.EnumerateObject())
            if (p.Name != "id" && !conocidos.Contains(p.Name)) e[p.Name] = ["Campo desconocido."];
        var lista = otras.ToList();
        foreach (var c in cat.Campos)
        {
            entrada.TryGetProperty(c.Key, out var v);
            var vacio = v.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || (v.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(v.GetString()));
            var requerido = c.Obligatorio || (c.RequeridoSi is { } rs && entrada.TryGetProperty(rs.Campo, out var otro) && otro.ToString() == rs.Valor);
            if (vacio)
            {
                if (requerido && c.Tipo != "switch") { e[c.Key] = [$"{c.Label} es obligatorio."]; continue; }
                limpia[c.Key] = c.Tipo switch { "switch" => JsonValue.Create(false), "numero" => null, _ => JsonValue.Create("") };
                continue;
            }
            switch (c.Tipo)
            {
                case "switch":
                    if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) { e[c.Key] = ["Debe ser verdadero o falso."]; continue; }
                    limpia[c.Key] = v.GetBoolean();
                    break;
                case "numero":
                    if (!v.TryGetDecimal(out var n)) { e[c.Key] = [$"{c.Label} debe ser un número."]; continue; }
                    if ((c.Min is { } mn && n < mn) || (c.Max is { } mx && n > mx)) { e[c.Key] = [$"{c.Label} debe estar entre {c.Min} y {c.Max}."]; continue; }
                    limpia[c.Key] = n;
                    break;
                case "fecha":
                    var f = v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : "";
                    if (!DateOnly.TryParseExact(f, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) { e[c.Key] = [$"{c.Label} debe tener el formato DD/MM/AAAA."]; continue; }
                    limpia[c.Key] = f;
                    break;
                default: // texto, textarea, select
                    if (v.ValueKind != JsonValueKind.String) { e[c.Key] = [$"{c.Label} debe ser texto."]; continue; }
                    var s = v.GetString()!.Trim();
                    var max = c.Tipo == "textarea" ? 2000 : 200;
                    if (s.Length > max) { e[c.Key] = [$"{c.Label} admite hasta {max} caracteres."]; continue; }
                    if (c.Patron is { } pt && !Regex.IsMatch(s, pt.Re)) { e[c.Key] = [pt.Msg]; continue; }
                    if (c.Tipo == "select")
                    {
                        var fijas = OpcionesFijas(c);
                        var fuente = Fuente(c);
                        if (fijas is not null && !fijas.Contains(s)) { e[c.Key] = [$"{c.Label}: opción no válida."]; continue; }
                        if (fuente?.StartsWith("catalogo:") == true && !valoresDe(fuente[9..]).Contains(s.Split(" · ")[0].Trim(), StringComparer.OrdinalIgnoreCase))
                        { e[c.Key] = [$"{c.Label}: “{s}” no existe."]; continue; }
                        // "departamentos": ubigeo vigente (sin distinguir tildes; se guarda el nombre oficial). Sin ubigeo publicado, se acepta.
                        if (fuente == "departamentos" && departamentos is { Count: > 0 } && s.Length > 0)
                        {
                            var oficial = departamentos.FirstOrDefault(d => Controllers.Ubigeo.Igual(d, s));
                            if (oficial is null) { e[c.Key] = [$"{c.Label}: elige un departamento de la lista."]; continue; }
                            s = oficial;
                        }
                    }
                    limpia[c.Key] = s;
                    break;
            }
            if (c.Unico && limpia[c.Key] is { } u && lista.Any(o => string.Equals(o[c.Key]?.ToString()?.Trim(), u.ToString(), StringComparison.OrdinalIgnoreCase)))
                e[c.Key] = [$"Ya existe otro registro con ese {c.Label.ToLowerInvariant()}."];
        }
        return e;
    }

    /// <summary>Valida los campos enviados de un formulario (los demás quedan igual). Devuelve errores por campo.</summary>
    public Dictionary<string, string[]> ValidarFormulario(Formulario f, JsonElement entrada, out Dictionary<string, object> limpios)
    {
        var e = new Dictionary<string, string[]>();
        limpios = [];
        if (entrada.ValueKind != JsonValueKind.Object) { e["valores"] = ["Los valores deben ser un objeto."]; return e; }
        var campos = f.Campos.ToDictionary(c => c.Key);
        foreach (var p in entrada.EnumerateObject())
        {
            if (!campos.TryGetValue(p.Name, out var c)) { e[p.Name] = ["Campo desconocido."]; continue; }
            var v = p.Value;
            switch (c.Tipo)
            {
                case "switch":
                    if (v.ValueKind is JsonValueKind.True or JsonValueKind.False) limpios[c.Key] = v.GetBoolean();
                    else e[c.Key] = ["Debe ser verdadero o falso."];
                    break;
                case "numero":
                    if (v.TryGetDecimal(out var n) && (c.Min is not { } mn || n >= mn) && (c.Max is not { } mx || n <= mx)) limpios[c.Key] = n;
                    else e[c.Key] = [c.Min is not null ? $"Debe ser un número entre {c.Min} y {c.Max}." : "Debe ser un número."];
                    break;
                default:
                    var s = v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : null;
                    if (s is null) { e[c.Key] = ["Debe ser texto."]; break; }
                    if (s.Length == 0 && c.Obligatorio) { e[c.Key] = [$"{c.Label} es obligatorio."]; break; }
                    if (s.Length > 200) { e[c.Key] = ["Admite hasta 200 caracteres."]; break; }
                    if (c.Tipo == "email" && s.Length > 0 && !Email.IsMatch(s)) { e[c.Key] = ["Ingresa un correo válido."]; break; }
                    if (c.Tipo == "select" && OpcionesFijas(c) is { } op && !op.Contains(s)) { e[c.Key] = ["Opción no válida."]; break; }
                    limpios[c.Key] = s;
                    break;
            }
        }
        return e;
    }

    /// <summary>Valor por defecto de un campo de formulario como objeto .NET (bool, decimal o string).</summary>
    public static object Defecto(JsonNode? n) => n switch
    {
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<decimal>(out var d) => d,
        _ => n?.ToString() ?? "",
    };
}
