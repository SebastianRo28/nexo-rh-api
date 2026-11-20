namespace NexoRh.Api.Domain;

/// <summary>Plantilla de workflow (Administrativo › Workflow › Plantillas).</summary>
public class PlantillaWorkflow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Alta, Flexible, Pre-Ingreso, Movimiento, Baja o Documento.</summary>
    public string Tipo { get; set; } = "Flexible";
    /// <summary>Activa, Inactiva, Incompleta o Error. Solo las activas se usan para crear solicitudes.</summary>
    public string Estado { get; set; } = "Activa";
    public int Operaciones { get; set; }
    public DateTime ActualizadaEn { get; set; } = DateTime.UtcNow;
    public string ModificadoPor { get; set; } = "";
}

/// <summary>
/// Solicitud de workflow: la crea RR.HH. o una jefatura desde una plantilla, o el colaborador desde Mi Portal › Procesos
/// Internos (Proceso = constancia, quinta, adelanto, datos o activo). La aprueba o rechaza RR.HH. o la jefatura del
/// colaborador afectado, nunca quien la pidió.
/// </summary>
public class SolicitudWorkflow
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int? PlantillaId { get; set; }
    public string? Proceso { get; set; }
    public int? SolicitanteUsuarioId { get; set; }
    public string Solicitante { get; set; } = "";
    public int? AfectadoId { get; set; }
    public string Afectado { get; set; } = "—";
    public string Documento { get; set; } = "—";
    public string Detalle { get; set; } = "";
    public decimal? Monto { get; set; }
    /// <summary>Pendiente, Aprobada o Rechazada.</summary>
    public string Estado { get; set; } = "Pendiente";
    public int Progreso { get; set; }
    public string Comentario { get; set; } = "";
    public string ResueltaPor { get; set; } = "";
    public DateOnly Creada { get; set; }
    public DateOnly Actualizada { get; set; }
}

/// <summary>Reconocimiento entre compañeros (Mi Portal). Se publica en el muro.</summary>
public class Reconocimiento
{
    public int Id { get; set; }
    /// <summary>Nombres de los reconocidos ("Apellidos, Nombres"; o un equipo).</summary>
    public string Para { get; set; } = "";
    public List<int> ParaIds { get; set; } = [];
    public string Autor { get; set; } = "";
    public int? AutorId { get; set; }
    public string Valor { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public DateOnly Fecha { get; set; }
}
