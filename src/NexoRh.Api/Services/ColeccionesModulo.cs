using System.Globalization;
using System.Text.Json.Nodes;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Services;

public enum AccesoColeccion
{
    /// <summary>Solo administradores.</summary>
    Admin,
    /// <summary>Todo el personal (lectura de catálogos del portal: beneficios, cursos, portadas…).</summary>
    Todos,
    /// <summary>Cada colaborador ve y crea sus propios registros; los administradores ven y gestionan todos.</summary>
    Propios,
}

/// <summary>Contexto para completar en el servidor un registro creado por su dueño (p. ej. el costo de un canje).</summary>
public record CreacionPropia(JsonObject Datos, int ColaboradorId, Func<string, IEnumerable<JsonObject>> Registros, Func<string, IEnumerable<JsonObject>> Propios);

/// <summary>
/// Colección de RegistroModulo. <paramref name="Modulo"/> se exige para escribir siempre, y para leer salvo que la lectura sea
/// de todo el personal (el portal sigue mostrando, p. ej., las portadas aunque el módulo de Comunicaciones no esté contratado).
/// <paramref name="EditablesPorDueno"/>: campos que el dueño puede cambiar en un registro propio (los demás los pone el servidor
/// o un administrador); al crearlo puede enviar además los de <paramref name="AlCrear"/>. <paramref name="AlCrearPorDueno"/> completa o valida lo que crea un colaborador; devuelve un error o null.
/// </summary>
public record Coleccion(string Clave, string Nombre, AccesoColeccion Lectura, AccesoColeccion Escritura, string? Modulo,
    string[]? EditablesPorDueno = null, Func<CreacionPropia, string?>? AlCrearPorDueno = null, string[]? AlCrear = null)
{
    public bool Propios => Lectura == AccesoColeccion.Propios || Escritura == AccesoColeccion.Propios;
}

/// <summary>Colecciones de los módulos que guardan sus registros como JSON (RegistrosModulo).</summary>
public static class ColeccionesModulo
{
    private static string Hoy() => DateTime.UtcNow.AddHours(-5).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    /// <summary>Number of a JSON value, whether it came from the database (JsonElement) or was set in memory (int, decimal…).</summary>
    private static decimal Num(JsonNode? n) =>
        n is JsonValue && decimal.TryParse(n.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    /// <summary>Canje de un beneficio: el servidor fija costo, fecha y estado, y valida stock y saldo de puntos.</summary>
    private static string? Canjear(CreacionPropia c)
    {
        var id = (int)Num(c.Datos["beneficioId"]);
        var b = c.Registros("beneficios").FirstOrDefault(x => (int)Num(x["id"]) == id);
        if (b is null) return "El beneficio no existe.";
        var costo = Num(b["puntos"]);
        if (b["stock"] is JsonValue sv && sv.TryGetValue<decimal>(out var stock))
        {
            var usados = c.Registros("beneficio-canjes").Count(x => (int)Num(x["beneficioId"]) == id);
            if (usados >= stock) return "No quedan unidades de este beneficio.";
        }
        var saldo = c.Propios("beneficio-puntos").Sum(x => Num(x["puntos"])) - c.Propios("beneficio-canjes").Sum(x => Num(x["puntos"]));
        if (costo > saldo) return "No tienes puntos suficientes.";
        c.Datos["puntos"] = costo;
        c.Datos["fecha"] = Hoy();
        c.Datos["estado"] = "Solicitado";
        return null;
    }

    /// <summary>Ticket de la mesa de ayuda abierto por el colaborador: estado inicial y fecha.</summary>
    private static string? AbrirTicket(CreacionPropia c)
    {
        if (string.IsNullOrWhiteSpace(c.Datos["asunto"]?.ToString())) return "Indica el asunto.";
        c.Datos["estado"] = "Abierto";
        c.Datos["abierto"] = DateTime.UtcNow.AddHours(-5).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        return null;
    }

    public static readonly Coleccion[] Todas =
    [
        // Remuneraciones
        new("kpi-tipos", "Tipo de KPI", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Payroll),
        new("kpi-datos", "Dato de KPI", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Payroll),
        new("kpi-envios", "Envío de bonos KPI", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Payroll),
        new("archivos-pago", "Archivo de pago", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Payroll),
        // Compensaciones y beneficios
        new("bandas-niveles", "Nivel de banda", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Comp),
        new("beneficios", "Beneficio", AccesoColeccion.Todos, AccesoColeccion.Admin, Modulos.Comp),
        new("beneficio-puntos", "Puntos de beneficios", AccesoColeccion.Propios, AccesoColeccion.Admin, Modulos.Comp),
        new("beneficio-canjes", "Canje de beneficio", AccesoColeccion.Propios, AccesoColeccion.Propios, Modulos.Comp,
            EditablesPorDueno: [], AlCrearPorDueno: Canjear, AlCrear: ["beneficioId"]),
        // Comunicaciones y portal
        new("correos-automaticos", "Correo automático", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Comms),
        new("portal-portadas", "Portada del portal", AccesoColeccion.Todos, AccesoColeccion.Admin, Modulos.Comms),
        new("portal-accesos", "Acceso rápido del portal", AccesoColeccion.Todos, AccesoColeccion.Admin, Modulos.Comms),
        new("reconocimiento-valores", "Valor de reconocimiento", AccesoColeccion.Todos, AccesoColeccion.Admin, Modulos.Comms),
        // Talento
        new("cursos", "Curso", AccesoColeccion.Todos, AccesoColeccion.Admin, Modulos.Pro),
        new("curso-avance", "Avance de curso", AccesoColeccion.Propios, AccesoColeccion.Propios, Modulos.Pro,
            EditablesPorDueno: ["cursoId", "inscrito", "hechos", "completado"]),
        new("evaluaciones", "Evaluación", AccesoColeccion.Propios, AccesoColeccion.Propios, Modulos.Pro,
            EditablesPorDueno: ["auto", "estado"], AlCrearPorDueno: _ => "Las evaluaciones las crea Recursos Humanos."),
        // Mesa de ayuda
        new("tickets", "Ticket", AccesoColeccion.Propios, AccesoColeccion.Propios, Modulos.ServiceDesk,
            EditablesPorDueno: ["asunto", "categoria", "mensajes"], AlCrearPorDueno: AbrirTicket),
        // Reportes y API
        new("dashboards", "Dashboard", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Pro),
        new("widgets", "Widget", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Pro),
        new("api-claves", "Acceso API", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Pro),
        new("api-ips", "IP permitida", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Pro),
        new("api-webhooks", "Webhook", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Pro),
        new("integracion-contable", "Integración contable", AccesoColeccion.Admin, AccesoColeccion.Admin, Modulos.Pro),
    ];

    public static Coleccion? Buscar(string clave) => Todas.FirstOrDefault(c => c.Clave == clave);
}
