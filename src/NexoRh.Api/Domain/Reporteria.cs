namespace NexoRh.Api.Domain;

// Reportería: reportes guardados (Exportadores), sus ejecuciones y el historial de cargas de los importadores.

/// <summary>Reporte guardado a partir de una plantilla.</summary>
public class ReporteGuardado : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Plantilla { get; set; } = "";
    public string Categoria { get; set; } = "";
    public DateOnly Creado { get; set; }
    public string CreadoPor { get; set; } = "";
}

/// <summary>Cada generación de un reporte (archivo descargado).</summary>
public class EjecucionReporte : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Reporte { get; set; } = "";
    public string Plantilla { get; set; } = "";
    /// <summary>Período de planilla (AAAAMM) para los reportes de remuneraciones.</summary>
    public int? Periodo { get; set; }
    public string Usuario { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int Filas { get; set; }
}

/// <summary>Carga de un archivo con un importador. Exitosa, Con errores (no se cargó nada) o Fallida (archivo ilegible).</summary>
public class CargaImportacion : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Importador { get; set; } = "";
    public string Archivo { get; set; } = "";
    public string Usuario { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int Registros { get; set; }
    public string Estado { get; set; } = "";
    public List<ErrorCarga> Errores { get; set; } = [];
}

public class ErrorCarga
{
    public int Fila { get; set; }
    public string Campo { get; set; } = "";
    public string Mensaje { get; set; } = "";
}
