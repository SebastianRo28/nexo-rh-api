using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NexoRh.Api.Catalogo;

// Personalización de marca por tenant (sección 5 de la especificación, pantalla P-MT.2). Se personaliza la identidad,
// no la estructura: logos, colores, tipografía, radio, textos del login, terminología y correos.

/// <summary>Versión publicada de la marca de un tenant (tenant_branding). Cada publicación crea una versión nueva.</summary>
public class TenantMarca
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public int Version { get; set; }
    /// <summary>MarcaDatos serializado (JSON).</summary>
    public string Json { get; set; } = "{}";
    public string PublicadoPor { get; set; } = "";
    public DateTime PublicadoEn { get; set; } = DateTime.UtcNow;
    /// <summary>Descripción corta de la versión (“Restaurada desde la versión 3”, “Valores por defecto”…).</summary>
    public string? Nota { get; set; }
}

/// <summary>Logo, isotipo o imagen del login subidos por el tenant (se sirven en /api/publico/marca/archivos/{id}).</summary>
public class ArchivoMarca
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Tipo { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Datos { get; set; } = [];
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class MarcaDatos
{
    /// <summary>Nombre del portal del colaborador, p. ej. “Mi Andino” (reemplaza “Mi Portal”).</summary>
    public string? NombrePortal { get; set; }
    public LogosMarca Logos { get; set; } = new();
    public LoginMarca Login { get; set; } = new();
    public ColoresMarca Colores { get; set; } = new();
    public string Tipografia { get; set; } = ReglasMarca.FuentePorDefecto;
    /// <summary>recto (2px) · suave (6px, por defecto) · redondeado (12px).</summary>
    public string Radio { get; set; } = "suave";
    public TerminologiaMarca Terminologia { get; set; } = new();
    public CorreosMarca Correos { get; set; } = new();
}

public class LogosMarca
{
    /// <summary>Logo horizontal para fondo oscuro (sidebar y topbar), 400×120.</summary>
    public Guid? Horizontal { get; set; }
    /// <summary>Logo horizontal para fondo claro (correos, documentos), 400×120.</summary>
    public Guid? HorizontalClaro { get; set; }
    /// <summary>Isotipo cuadrado 150×150 (login, rail colapsado, favicon).</summary>
    public Guid? Cuadrado { get; set; }
}

public class LoginMarca
{
    /// <summary>Imagen principal 1000×1000; sin imagen, el color primario con un patrón.</summary>
    public Guid? Imagen { get; set; }
    public string? Titulo { get; set; }
    public string? Mensaje { get; set; }
    /// <summary>“¿Problemas? Escribe a rrhh@cliente.com”.</summary>
    public string? Ayuda { get; set; }
    /// <summary>“Powered by Nexo RH”: solo Enterprise puede ocultarlo (marca blanca).</summary>
    public bool MostrarPoweredBy { get; set; } = true;
}

public class ColoresMarca
{
    /// <summary>Primario: botones, links, tabs activas y foco. #RRGGBB.</summary>
    public string Primario { get; set; } = ReglasMarca.PrimarioPorDefecto;
    /// <summary>null = automático (tono 800 del primario).</summary>
    public string? Sidebar { get; set; }
    /// <summary>null = automático (tono 900 del primario).</summary>
    public string? Topbar { get; set; }
    /// <summary>Opcional: badges destacados y gráficos.</summary>
    public string? Acento { get; set; }
}

/// <summary>Diccionario de etiquetas del tenant (en singular; el frontend arma los plurales).</summary>
public class TerminologiaMarca
{
    public string Nivel1 { get; set; } = "División";
    public string Nivel2 { get; set; } = "Área";
    public string Nivel3 { get; set; } = "Sub-área";
    public string Colaborador { get; set; } = "Colaborador";
    public string Recinto { get; set; } = "Recinto";
}

public class CorreosMarca
{
    /// <summary>Nombre del remitente, p. ej. “RR.HH. Grupo Andino”.</summary>
    public string? Remitente { get; set; }
    /// <summary>Firma al pie de los correos.</summary>
    public string? Firma { get; set; }
}

/// <summary>Reglas de la marca: valores por defecto, listas permitidas, contraste WCAG y serialización.</summary>
public static partial class ReglasMarca
{
    public const string PrimarioPorDefecto = "#2D4DA4";
    public const string FuentePorDefecto = "Nunito Sans";
    public static readonly string[] Fuentes = ["Nunito Sans", "Inter", "Source Sans 3", "Roboto", "Montserrat", "Open Sans", "Lato"];
    public static readonly string[] Radios = ["recto", "suave", "redondeado"];
    public static readonly string[] TiposArchivo = ["horizontal", "horizontalClaro", "cuadrado", "login"];
    public const double ContrasteMinimo = 4.5;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Serializar(MarcaDatos m) => JsonSerializer.Serialize(m, Json);
    public static MarcaDatos Leer(string json) => JsonSerializer.Deserialize<MarcaDatos>(json, Json) ?? new MarcaDatos();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex Hex();

    public static bool EsHex(string? c) => c is not null && Hex().IsMatch(c);

    /// <summary>Luminancia relativa WCAG 2.x de un color #RRGGBB.</summary>
    public static double Luminancia(string hex)
    {
        static double Canal(int v) { var c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        var r = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber);
        var g = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber);
        var b = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber);
        return 0.2126 * Canal(r) + 0.7152 * Canal(g) + 0.0722 * Canal(b);
    }

    public static double Contraste(string a, string b)
    {
        var (la, lb) = (Luminancia(a), Luminancia(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>
    /// Errores de la marca (campo → mensaje). Contraste ≥ 4.5:1 del primario sobre blanco (links, tabs, botones con
    /// texto blanco) y del texto blanco sobre el sidebar y el topbar personalizados.
    /// </summary>
    public static Dictionary<string, string> Validar(MarcaDatos m)
    {
        var e = new Dictionary<string, string>();
        void Color(string campo, string? valor, bool obligatorio, bool contraste)
        {
            if (valor is null) { if (obligatorio) e[campo] = "Elige un color."; return; }
            if (!EsHex(valor)) { e[campo] = "Usa un color en formato #RRGGBB."; return; }
            if (contraste && Contraste(valor, "#FFFFFF") < ContrasteMinimo)
                e[campo] = $"Este color no tiene suficiente contraste ({Contraste(valor, "#FFFFFF"):0.0}:1; mínimo 4.5:1). Prueba con {Sugerir(valor)}.";
        }
        Color("colores.primario", m.Colores.Primario, true, true);
        Color("colores.sidebar", m.Colores.Sidebar, false, true);
        Color("colores.topbar", m.Colores.Topbar, false, true);
        Color("colores.acento", m.Colores.Acento, false, false);
        if (!Fuentes.Contains(m.Tipografia)) e["tipografia"] = "Elige una de las fuentes de la lista.";
        if (!Radios.Contains(m.Radio)) e["radio"] = "Elige recto, suave o redondeado.";
        void Texto(string campo, string? valor, int max, bool obligatorio = false)
        {
            if (string.IsNullOrWhiteSpace(valor)) { if (obligatorio) e[campo] = "Este texto es obligatorio."; return; }
            if (valor.Length > max) e[campo] = $"Máximo {max} caracteres.";
        }
        Texto("nombrePortal", m.NombrePortal, 40);
        Texto("login.titulo", m.Login.Titulo, 80);
        Texto("login.mensaje", m.Login.Mensaje, 200);
        Texto("login.ayuda", m.Login.Ayuda, 200);
        Texto("terminologia.nivel1", m.Terminologia.Nivel1, 30, true);
        Texto("terminologia.nivel2", m.Terminologia.Nivel2, 30, true);
        Texto("terminologia.nivel3", m.Terminologia.Nivel3, 30, true);
        Texto("terminologia.colaborador", m.Terminologia.Colaborador, 30, true);
        Texto("terminologia.recinto", m.Terminologia.Recinto, 30, true);
        Texto("correos.remitente", m.Correos.Remitente, 60);
        Texto("correos.firma", m.Correos.Firma, 300);
        return e;
    }

    /// <summary>El tono más cercano (mismo matiz, más oscuro) que llega a 4.5:1 sobre blanco.</summary>
    public static string Sugerir(string hex)
    {
        var (h, s, l) = AHsl(hex);
        for (var x = l; x >= 0; x -= 0.005)
        {
            var c = DeHsl(h, s, x);
            if (Contraste(c, "#FFFFFF") >= ContrasteMinimo) return c;
        }
        return "#000000";
    }

    /// <summary>Limpia textos y aplica las reglas que el plan no permite cambiar.</summary>
    public static MarcaDatos Normalizar(MarcaDatos m, bool permiteOcultarPoweredBy = false)
    {
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        m.NombrePortal = T(m.NombrePortal);
        m.Login.Titulo = T(m.Login.Titulo);
        m.Login.Mensaje = T(m.Login.Mensaje);
        m.Login.Ayuda = T(m.Login.Ayuda);
        // Ocultar “Powered by Nexo RH” es de Enterprise (marca blanca completa).
        if (!permiteOcultarPoweredBy) m.Login.MostrarPoweredBy = true;
        m.Colores.Primario = m.Colores.Primario?.ToUpperInvariant() ?? PrimarioPorDefecto;
        m.Colores.Sidebar = T(m.Colores.Sidebar)?.ToUpperInvariant();
        m.Colores.Topbar = T(m.Colores.Topbar)?.ToUpperInvariant();
        m.Colores.Acento = T(m.Colores.Acento)?.ToUpperInvariant();
        var t = m.Terminologia;
        (t.Nivel1, t.Nivel2, t.Nivel3, t.Colaborador, t.Recinto) = (t.Nivel1?.Trim() ?? "", t.Nivel2?.Trim() ?? "", t.Nivel3?.Trim() ?? "", t.Colaborador?.Trim() ?? "", t.Recinto?.Trim() ?? "");
        m.Correos.Remitente = T(m.Correos.Remitente);
        m.Correos.Firma = T(m.Correos.Firma);
        return m;
    }

    private static (double H, double S, double L) AHsl(string hex)
    {
        var r = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, h = 0, s = 0;
        if (max != min)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            h /= 6;
        }
        return (h, s, l);
    }

    private static string DeHsl(double h, double s, double l)
    {
        static double F(double p, double q, double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            return t < 1.0 / 6 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6 : p;
        }
        double r, g, b;
        if (s == 0) r = g = b = l;
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            (r, g, b) = (F(p, q, h + 1.0 / 3), F(p, q, h), F(p, q, h - 1.0 / 3));
        }
        return $"#{(int)Math.Round(r * 255):X2}{(int)Math.Round(g * 255):X2}{(int)Math.Round(b * 255):X2}";
    }
}
