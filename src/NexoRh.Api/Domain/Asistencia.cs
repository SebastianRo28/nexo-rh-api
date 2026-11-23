namespace NexoRh.Api.Domain;

// Módulos Asistencia (Administrativo) y Control de Asistencia. Las horas se guardan como texto "HH:mm" en hora de Lima,
// igual que las muestra el frontend; las fechas como DateOnly.

/// <summary>Lugar de trabajo. Cada colaborador tiene un recinto principal (Colaborador.RecintoId).</summary>
public class Recinto : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Empresa { get; set; } = "";
    public string Ruc { get; set; } = "";
    public string CodigoInterno { get; set; } = "";
    public string Pais { get; set; } = "Perú";
    public string Departamento { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string Distrito { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Clave { get; set; } = "";
    public string Info { get; set; } = "";
    public int? JefaturaId { get; set; }
    public int? AdministrativoId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    /// <summary>Metros; las marcas desde la app más lejos que esto se rechazan.</summary>
    public int Radio { get; set; }
    public List<string> Emails { get; set; } = [];
    public bool Activo { get; set; } = true;
    /// <summary>Colaboradores con acceso al recinto en Control de Asistencia.</summary>
    public List<int> Usuarios { get; set; } = [];
}

public class Turno : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Sigla { get; set; } = "";
    /// <summary>Permanente, Transitorio, Flexible o Rotativo.</summary>
    public string Tipo { get; set; } = "Permanente";
    /// <summary>Diurno, Nocturno o Mixto.</summary>
    public string Jornada { get; set; } = "Diurno";
    public string Entrada { get; set; } = "08:00";
    public string Salida { get; set; } = "17:00";
    public int Colacion { get; set; }
    /// <summary>Días de la semana que se trabaja (0 = domingo).</summary>
    public List<int> Dias { get; set; } = [];
    public int Tolerancia { get; set; }
    public string Color { get; set; } = "#D6E4FF";
    public int? CicloTrabajo { get; set; }
    public int? CicloDescanso { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Turno base de un colaborador desde una fecha (Hasta null = vigente).</summary>
public class AsignacionTurno : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public int TurnoId { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public DateOnly? CicloInicio { get; set; }
    public string CreadoPor { get; set; } = "";
}

/// <summary>Cambio de un solo día: otro turno (TurnoId) o un código de descanso (DES, LIB, COMP).</summary>
public class CambioTurno : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public DateOnly Fecha { get; set; }
    public int? TurnoId { get; set; }
    public string? Codigo { get; set; }
    public string CreadoPor { get; set; } = "";
}

public class Marca : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public int RecintoId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Hora { get; set; } = "";
    /// <summary>Entrada o Salida.</summary>
    public string Tipo { get; set; } = "";
    /// <summary>Web, App, QR, Foto o Manual.</summary>
    public string Metodo { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public bool DentroRadio { get; set; } = true;
    public bool Foto { get; set; }
    public string Observacion { get; set; } = "";
    public string? CreadoPor { get; set; }
    public string? Motivo { get; set; }
    public DateTime RegistradaEn { get; set; } = DateTime.UtcNow;
}

public class MarcaFallida : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public int RecintoId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Hora { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Metodo { get; set; } = "";
    public string Motivo { get; set; } = "";
    public int? Distancia { get; set; }
}

public class NotaSupervisor : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Texto { get; set; } = "";
    public string Autor { get; set; } = "";
}

public class Vacacion : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int Dias { get; set; }
    /// <summary>Solicitud, Registro manual o Importador.</summary>
    public string Origen { get; set; } = "";
    public string Observacion { get; set; } = "";
}

public class Ausencia : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public string Tipo { get; set; } = "";
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int Dias { get; set; }
    public string Observacion { get; set; } = "";
}

/// <summary>Certificado médico (CITT EsSalud o clínica privada).</summary>
public class LicenciaMedica : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Folio { get; set; } = "";
    public int ColaboradorId { get; set; }
    public string Emisor { get; set; } = "";
    public string Diagnostico { get; set; } = "";
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int Dias { get; set; }
    /// <summary>Recibida, Validada, Observada o Rechazada.</summary>
    public string Estado { get; set; } = "Recibida";
    public DateOnly Recibida { get; set; }
    /// <summary>Ausencia creada al validarla.</summary>
    public int? AusenciaId { get; set; }
}

/// <summary>Solicitud de vacaciones o permiso hecha por el colaborador.</summary>
public class SolicitudAsistencia : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    /// <summary>Vacaciones, Permiso con goce o Permiso sin goce.</summary>
    public string Tipo { get; set; } = "";
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int Dias { get; set; }
    public string Motivo { get; set; } = "";
    public DateOnly Creada { get; set; }
    /// <summary>Pendiente, Aprobada o Rechazada.</summary>
    public string Estado { get; set; } = "Pendiente";
    public string Aprobador { get; set; } = "";
    public string Comentario { get; set; } = "";
}

/// <summary>Día de descanso o feriado trabajado.</summary>
public class RecargoDomingo : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Horas { get; set; }
    public bool DescansoSustitutorio { get; set; }
    public string Observacion { get; set; } = "";
}

/// <summary>Decisión sobre las horas extras de un colaborador en un día (el exceso lo calcula el frontend).</summary>
public class AprobacionHhee : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public DateOnly Fecha { get; set; }
    /// <summary>Aprobado, Aprobado parcial o Rechazado ("En espera" = sin registro).</summary>
    public string Estado { get; set; } = "";
    public List<LineaHhee> Lineas { get; set; } = [];
    public string Observacion { get; set; } = "";
    public string AprobadoPor { get; set; } = "";
    /// <summary>Ya se envió a planilla.</summary>
    public bool Enviado { get; set; }
}

public class LineaHhee
{
    public int Minutos { get; set; }
    /// <summary>Sobretasa: 25, 35 o 100.</summary>
    public int Tipo { get; set; }
}

/// <summary>Feriado. Se mantiene igual al catálogo genérico "feriados" (Configuración › Feriados), que es donde se edita.</summary>
public class Feriado : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Nacional o Regional.</summary>
    public string Alcance { get; set; } = "Nacional";
}
