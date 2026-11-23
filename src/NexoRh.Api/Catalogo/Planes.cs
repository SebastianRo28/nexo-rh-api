using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace NexoRh.Api.Catalogo;

// Modelo comercial (sección 6): precio = tarifa por colaborador activo al mes × (plan + módulos adicionales).

/// <summary>Plan de la plataforma (Básico, Pro, Enterprise) con sus límites, módulos incluidos y funciones.</summary>
public class Plan
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal PrecioPorColaborador { get; set; }
    public int Orden { get; set; }
    public int LimiteEmpresas { get; set; }
    public int LimiteAlmacenamientoGb { get; set; }
    public int LimiteAdmins { get; set; }
    public int LimiteApiPorMinuto { get; set; }
    /// <summary>Códigos de módulo incluidos, separados por coma (p. ej. "PRO").</summary>
    public string ModulosIncluidos { get; set; } = "";
    /// <summary>Funciones finas incluidas, separadas por coma (SSO_SOCIAL, SAML, CUSTOM_DOMAIN, HIDE_POWERED_BY…).</summary>
    public string Funciones { get; set; } = "";
}

/// <summary>Módulo que se contrata aparte (o que incluye un plan). Los códigos son los de la especificación (6.2).</summary>
public class ModuloComercial
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    /// <summary>Beneficios para el modal “Este módulo no está incluido en tu plan”, separados por “|”.</summary>
    public string Beneficios { get; set; } = "";
    public decimal PrecioPorColaborador { get; set; }
    public int Orden { get; set; }
}

/// <summary>Módulo adicional contratado por un tenant (tenant_module), con vigencia y precio pactado.</summary>
public class TenantModulo
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string Codigo { get; set; } = "";
    public DateOnly ActivoDesde { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? ActivoHasta { get; set; }
    public decimal? PrecioPactado { get; set; }
}

/// <summary>Flag fino por tenant (tenant_feature_flag): activa o quita una función del plan (beta, excepciones).</summary>
public class TenantFlag
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string Flag { get; set; } = "";
    public bool Valor { get; set; } = true;
}

/// <summary>“Solicitar información” de un módulo no contratado: un lead para la consola del proveedor.</summary>
public class SolicitudInformacion
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string Modulo { get; set; } = "";
    public string Usuario { get; set; } = "";
    public string Correo { get; set; } = "";
    public string? Mensaje { get; set; }
    public string Estado { get; set; } = "Nueva";
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Factura mensual del tenant. La emisión electrónica (OSE/PSE ante SUNAT) se integra más adelante.</summary>
public class Factura
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string Numero { get; set; } = "";
    /// <summary>YYYY-MM.</summary>
    public string Periodo { get; set; } = "";
    public DateOnly Emitida { get; set; }
    public DateOnly Vence { get; set; }
    public int Colaboradores { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    /// <summary>Pagada, Pendiente o Vencida.</summary>
    public string Estado { get; set; } = "Pendiente";
    /// <summary>Estado ante SUNAT (Pendiente de integración, Aceptada…).</summary>
    public string SunatEstado { get; set; } = "Pendiente de integración";
}

public static class Modulos
{
    public const string Payroll = "PAYROLL", Attendance = "ATTENDANCE", Docs = "DOCS", Assets = "ASSETS", Whistleblower = "WHISTLEBLOWER",
        ServiceDesk = "SERVICE_DESK", Comp = "COMP", Headcount = "HEADCOUNT", Sst = "SST", Comms = "COMMS", Surveys = "SURVEYS", Pro = "PRO";
    public const string PlanPorDefecto = "PRO";
    public const decimal Igv = 0.18m;

    public static readonly Plan[] Planes =
    [
        new() { Codigo = "BASICO", Nombre = "Básico", PrecioPorColaborador = 12, Orden = 1, LimiteEmpresas = 1, LimiteAlmacenamientoGb = 5, LimiteAdmins = 3, LimiteApiPorMinuto = 0 },
        new() { Codigo = "PRO", Nombre = "Pro", PrecioPorColaborador = 18, Orden = 2, LimiteEmpresas = 3, LimiteAlmacenamientoGb = 20, LimiteAdmins = 10, LimiteApiPorMinuto = 60, ModulosIncluidos = Pro, Funciones = "SSO_SOCIAL" },
        new() { Codigo = "ENTERPRISE", Nombre = "Enterprise", PrecioPorColaborador = 25, Orden = 3, LimiteEmpresas = 50, LimiteAlmacenamientoGb = 200, LimiteAdmins = 100, LimiteApiPorMinuto = 600, ModulosIncluidos = Pro, Funciones = "SSO_SOCIAL,SAML,CUSTOM_DOMAIN,CUSTOM_EMAIL,HIDE_POWERED_BY,DEDICATED_DB" },
    ];

    public static readonly ModuloComercial[] Catalogo =
    [
        M(Pro, "Plataforma Pro", "Workflows, historial de cambios, analítica, permisos granulares y API.", 0, 1,
            "Flujos de aprobación configurables", "Historial de cambios con auditoría completa", "Accesos API e integraciones"),
        M(Payroll, "Planillas", "Cálculo de remuneraciones, boletas y archivos de pago.", 6, 2,
            "Cálculo mensual con AFP/ONP, 5ta categoría y CTS", "Boletas en el portal del colaborador", "Archivos para bancos y PLAME"),
        M(Attendance, "Control de Asistencia", "Turnos, marcaciones, ausencias y horas extras.", 4, 3,
            "Marcación web, móvil y biométrica", "Turnos y tolerancias por recinto", "Reportes de horas extras y tardanzas"),
        M(Docs, "Documentos y Firma", "Plantillas, generación masiva y firma de documentos.", 3, 4,
            "Contratos y constancias desde plantillas", "Circuito de revisión y firma", "Repositorio por colaborador"),
        M(Assets, "Gestión de Activos", "Inventario, asignaciones y actas de entrega.", 2, 5,
            "Inventario con códigos y estados", "Asignación y devolución con actas", "Alertas de mantenimiento"),
        M(Whistleblower, "Canal de Denuncias", "Denuncias anónimas con seguimiento (Ley 27942).", 2, 6,
            "Canal anónimo y confidencial", "Seguimiento con plazos legales", "Reportes para el comité"),
        M(ServiceDesk, "Servicio al colaborador", "Mesa de ayuda para consultas de RR.HH.", 2, 7,
            "Tickets por categoría", "Tiempos de respuesta medidos", "Base de respuestas frecuentes"),
        M(Comp, "Compensaciones", "Bandas salariales, estudio de mercado y revisiones.", 3, 8,
            "Bandas por cargo", "Comparación con el mercado", "Ciclos de revisión salarial"),
        M(Headcount, "Headcount", "Planificación de dotación y vacantes.", 2, 9,
            "Plan de dotación por área", "Control de vacantes y aprobaciones", "Proyección de costo"),
        M(Sst, "Seguridad y Salud en el Trabajo", "Requisitos legales, evidencias y cumplimiento (Ley 29783).", 3, 10,
            "Matriz de requisitos legales", "Evidencias con vencimientos", "Indicadores de cumplimiento"),
        M(Comms, "Comunicación", "Muro, publicaciones y reconocimientos.", 2, 11,
            "Publicaciones segmentadas", "Reconocimientos con los valores de la empresa", "Métricas de lectura"),
        M(Surveys, "Encuestas", "Clima laboral y encuestas libres.", 2, 12,
            "Encuestas anónimas de clima", "Resultados por área", "Encuestas libres y pulsos"),
    ];

    /// <summary>Módulos adicionales (los que no incluye ningún plan): los que se contratan aparte.</summary>
    public static IEnumerable<string> Adicionales => Catalogo.Select(m => m.Codigo).Where(c => c != Pro);

    private static ModuloComercial M(string codigo, string nombre, string descripcion, decimal precio, int orden, params string[] beneficios) =>
        new() { Codigo = codigo, Nombre = nombre, Descripcion = descripcion, PrecioPorColaborador = precio, Orden = orden, Beneficios = string.Join("|", beneficios) };

    public static HashSet<string> Lista(string? s) => (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
}

/// <summary>Plan, módulos y funciones vigentes de un tenant.</summary>
public record Suscripcion(Plan Plan, HashSet<string> Modulos, HashSet<string> Funciones)
{
    public bool Tiene(string modulo) => Modulos.Contains(modulo);
    public bool Funcion(string f) => Funciones.Contains(f);
}

/// <summary>Suscripción vigente de cada tenant (con caché de 1 minuto).</summary>
public class Suscripciones(CatalogoDbContext catalogo, IMemoryCache cache)
{
    public async Task<Suscripcion> DeAsync(Guid tenantId, CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync($"tenant-suscripcion:{tenantId}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            var codigo = await catalogo.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.PlanCodigo).FirstOrDefaultAsync(ct);
            var plan = await catalogo.Planes.AsNoTracking().FirstOrDefaultAsync(p => p.Codigo == codigo, ct)
                ?? Modulos.Planes.First(p => p.Codigo == Modulos.PlanPorDefecto);
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var modulos = Modulos.Lista(plan.ModulosIncluidos);
            modulos.UnionWith(await catalogo.TenantModulos.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.ActivoDesde <= hoy && (m.ActivoHasta == null || m.ActivoHasta >= hoy)).Select(m => m.Codigo).ToListAsync(ct));
            var funciones = Modulos.Lista(plan.Funciones);
            // Un flag solo tiene efecto si el tenant tiene contratado el módulo al que pertenece.
            var duenos = await catalogo.DefinicionesFlag.AsNoTracking().ToDictionaryAsync(d => d.Clave, d => d.Modulo, ct);
            foreach (var f in await catalogo.Flags.AsNoTracking().Where(f => f.TenantId == tenantId).ToListAsync(ct))
            {
                if (duenos.TryGetValue(f.Flag, out var modulo) && modulo is not null && !modulos.Contains(modulo)) continue;
                if (f.Valor) funciones.Add(f.Flag); else funciones.Remove(f.Flag);
            }
            return new Suscripcion(plan, modulos, funciones);
        }))!;

    public void Olvidar(Guid tenantId) => cache.Remove($"tenant-suscripcion:{tenantId}");
}

/// <summary>Uso mensual de un tenant (tenant_usage): colaboradores activos, almacenamiento, usuarios y llamadas a la API.</summary>
public class UsoMensual
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>YYYY-MM.</summary>
    public string Periodo { get; set; } = "";
    public int Colaboradores { get; set; }
    public int UsuariosActivos { get; set; }
    public decimal AlmacenamientoMb { get; set; }
    public int LlamadasApi { get; set; }
    public DateTime CalculadoEn { get; set; } = DateTime.UtcNow;
}
