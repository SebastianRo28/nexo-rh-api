namespace NexoRh.Api.Domain;

// Seguridad y Salud en el Trabajo › Registro de evidencias.

/// <summary>Marco normativo de la empresa (p. ej. "Entrega de EPP", Ley 29783).</summary>
public class CumplimientoSst
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Normativa { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Requisito de un cumplimiento, que se asigna a colaboradores o a empresas.</summary>
public class RequisitoSst
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int CumplimientoId { get; set; }
    /// <summary>Colaboradores o Empresas.</summary>
    public string AplicaA { get; set; } = "Colaboradores";
    /// <summary>Meses de vigencia de cada evidencia (null = no vence).</summary>
    public int? VigenciaMeses { get; set; }
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Requisito asignado a un colaborador o a una empresa. Finalizada = ya no se exige (se conserva el historial).</summary>
public class AsignacionSst
{
    public int Id { get; set; }
    public int RequisitoId { get; set; }
    public int? ColaboradorId { get; set; }
    /// <summary>Razón social (catálogo de empresas) cuando el requisito aplica a empresas.</summary>
    public string? Empresa { get; set; }
    public bool Finalizada { get; set; }
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Evidencia de cumplimiento (fecha del documento y, si el requisito vence, su vencimiento).</summary>
public class EvidenciaSst
{
    public int Id { get; set; }
    public int AsignacionId { get; set; }
    public DateOnly Fecha { get; set; }
    public DateOnly? Vence { get; set; }
    public string Descripcion { get; set; } = "";
    public string RegistradaPor { get; set; } = "";
    public DateTime RegistradaEn { get; set; } = DateTime.UtcNow;
    public string? ArchivoNombre { get; set; }
    public string? ArchivoTipo { get; set; }
    public int? ArchivoTamano { get; set; }
}

/// <summary>Archivo de una evidencia (PDF o imagen), separado para no cargarlo al listar.</summary>
public class ArchivoEvidenciaSst
{
    public int EvidenciaId { get; set; }
    public byte[] Contenido { get; set; } = [];
}
