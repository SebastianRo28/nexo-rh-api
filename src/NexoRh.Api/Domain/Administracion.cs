namespace NexoRh.Api.Domain;

// Administrativo: Gestión de Activos, Documentos, Compensaciones y Headcount.

public class CategoriaActivo : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

public class ProductoActivo : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int CategoriaId { get; set; }
    public string Marca { get; set; } = "";
}

/// <summary>Unidad del inventario (código ACT-0042).</summary>
public class Activo : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public string Codigo { get; set; } = "";
    public int ProductoId { get; set; }
    /// <summary>Disponible, Asignado, Mantenimiento o Inactivo.</summary>
    public string Estado { get; set; } = "Disponible";
    public int? AsignadoId { get; set; }
}

public class MovimientoActivo : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string ActivoCodigo { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Asignado { get; set; } = "—";
    public string Estado { get; set; } = "";
    public string Comentario { get; set; } = "";
    public string Admin { get; set; } = "";
}

/// <summary>Orden de asignación de un producto a una persona. "Atrasado" = pendiente con fecha pasada.</summary>
public class OrdenActivo : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    /// <summary>Workflow o Gestión de activos.</summary>
    public string Origen { get; set; } = "Gestión de activos";
    public int ProductoId { get; set; }
    public int? ParaId { get; set; }
    public string Para { get; set; } = "";
    public string Area { get; set; } = "";
    public string Cargo { get; set; } = "";
    public string Solicitante { get; set; } = "";
    public DateOnly Solicitada { get; set; }
    public DateOnly AsignarEl { get; set; }
    /// <summary>Pendiente, Asignada o Rechazada.</summary>
    public string Estado { get; set; } = "Pendiente";
    public string? ActivoCodigo { get; set; }
    public string Comentario { get; set; } = "";
}

public class PlantillaDocumento : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Carpeta { get; set; } = "";
    public bool Activa { get; set; } = true;
}

/// <summary>Documento de un colaborador con su circuito de revisión y firmas ("si", "no" o "na" = no aplica).</summary>
public class DocumentoColaborador : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public string Archivo { get; set; } = "";
    public DateOnly Fecha { get; set; }
    public string Revision { get; set; } = "na";
    public string FirmaColaborador { get; set; } = "na";
    public string FirmaEmpresa { get; set; } = "na";
    public bool Oculto { get; set; }
    /// <summary>En la papelera desde esta fecha (se conserva 60 días).</summary>
    public DateTime? EliminadoEn { get; set; }
    public string EliminadoPor { get; set; } = "";
}

/// <summary>Banda salarial de un cargo: mínimo y máximo como porcentaje del objetivo.</summary>
public class BandaSalarial : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Cargo { get; set; } = "";
    public decimal Objetivo { get; set; }
    public decimal PctMin { get; set; } = 85;
    public decimal PctMax { get; set; } = 120;
    public bool Activo { get; set; } = true;
}

/// <summary>Datos de compensación de un colaborador: última modificación del sueldo, evaluación y compensación teórica.</summary>
public class CompensacionColaborador : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int ColaboradorId { get; set; }
    public DateOnly? UltimaModificacion { get; set; }
    public string Evaluacion { get; set; } = "";
    public decimal? CompTeorica { get; set; }
    public string Comentario { get; set; } = "";
    public DateTime ActualizadaEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Cargo del estudio salarial de mercado; Interno = cargo propio emparejado.</summary>
public class CargoMercado : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Cargo { get; set; } = "";
    public string? Interno { get; set; }
    public string Descripcion { get; set; } = "";
    public string Industria { get; set; } = "";
    public string Tamano { get; set; } = "";
    public decimal P25 { get; set; }
    public decimal P50 { get; set; }
    public decimal P75 { get; set; }
    public decimal P90 { get; set; }
}

/// <summary>Posición planificada (vacante) de headcount.</summary>
public class Posicion : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public string Codigo { get; set; } = "";
    public string Cargo { get; set; } = "";
    public string Division { get; set; } = "";
    public string Area { get; set; } = "";
    public string SubArea { get; set; } = "";
    public DateOnly Ingreso { get; set; }
    public DateOnly? Termino { get; set; }
    public bool Vigente { get; set; } = true;
}

/// <summary>Dotación esperada por área o por cargo (Tipo = area o cargo).</summary>
public class PlanHeadcount : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public string Tipo { get; set; } = "area";
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
}
