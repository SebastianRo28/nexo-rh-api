namespace NexoRh.Api.Domain;

// Remuneraciones: ítems, parámetros legales, períodos de planilla y boletas. Los períodos se identifican como yyyymm
// (202603 = marzo de 2026); el frontend los muestra como "MM/YYYY".

/// <summary>Concepto de planilla. Plame = código SUNAT (Tabla 22).</summary>
public class TipoItem : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    /// <summary>Haber, Descuento o Aporte empleador.</summary>
    public string Clase { get; set; } = "Haber";
    public string Plame { get; set; } = "";
    /// <summary>Afecto a EsSalud, al aporte pensionario (AFP/ONP) y a la renta de 5ta categoría.</summary>
    public bool Essalud { get; set; }
    public bool Pension { get; set; }
    public bool Quinta { get; set; }
    /// <summary>Lo calcula Nexo RH (no se asigna a mano), salvo los que vienen de otros módulos (p. ej. BON-KPI).</summary>
    public bool Automatico { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Ítem asignado a un colaborador para un rango de períodos (Hasta null = sin fin).</summary>
public class AsignacionItem : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public int TipoItemId { get; set; }
    public decimal Monto { get; set; }
    public int Desde { get; set; }
    public int? Hasta { get; set; }
    public string Observacion { get; set; } = "";
}

/// <summary>Parámetro legal (RMV, UIT, tasas). Clave fija; el valor lo actualiza un administrador.</summary>
public class ParametroRem : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public string Clave { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal Valor { get; set; }
    /// <summary>S/, % o UIT.</summary>
    public string Unidad { get; set; } = "";
    public string VigenteDesde { get; set; } = "";
    public string Fuente { get; set; } = "";
}

public class ComisionAfp : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public string Afp { get; set; } = "";
    public decimal ComisionFlujoPct { get; set; }
    public decimal ComisionSaldoPct { get; set; }
}

/// <summary>Tramo anual de renta de 5ta categoría, en UIT (Hasta null = sin límite).</summary>
public class TramoRenta : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public decimal Desde { get; set; }
    public decimal? Hasta { get; set; }
    public decimal TasaPct { get; set; }
}

/// <summary>Período mensual de planilla. Uno solo está abierto; al cerrarlo sus boletas quedan definitivas.</summary>
public class PeriodoPlanilla : IEntidadTenant
{
    public Guid TenantId { get; set; }
    /// <summary>yyyymm.</summary>
    public int Id { get; set; }
    public bool Cerrado { get; set; }
    public DateTime? ProcesadoEn { get; set; }
    public string ProcesadoPor { get; set; } = "";
    public DateTime? CerradoEn { get; set; }
    public string CerradoPor { get; set; } = "";
}

/// <summary>Boleta de pago de un colaborador en un período (borrador mientras el período esté abierto).</summary>
public class Boleta : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int Periodo { get; set; }
    public int ColaboradorId { get; set; }
    // Datos del colaborador al momento del cálculo (la boleta no cambia si luego cambia la ficha).
    public string Nombre { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Cargo { get; set; } = "";
    public string Regimen { get; set; } = "";
    public int DiasLaborados { get; set; }
    public int DiasVacaciones { get; set; }
    public int DiasNoTrabajados { get; set; }
    public decimal Ingresos { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Aportes { get; set; }
    public decimal Neto { get; set; }
    /// <summary>Base afecta a renta de 5ta del mes (para proyectar la retención de los meses siguientes).</summary>
    public decimal BaseQuinta { get; set; }
    public decimal RetencionQuinta { get; set; }
    public List<LineaBoleta> Lineas { get; set; } = [];
    public List<string> Observaciones { get; set; } = [];
    public bool Definitiva { get; set; }
    public DateTime CalculadaEn { get; set; } = DateTime.UtcNow;
}

public class LineaBoleta
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    /// <summary>Haber, Descuento o Aporte empleador.</summary>
    public string Clase { get; set; } = "";
    public string Detalle { get; set; } = "";
    public decimal Monto { get; set; }
}
