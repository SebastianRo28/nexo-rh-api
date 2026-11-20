namespace NexoRh.Api.Domain;

/// <summary>Roles de acceso a la plataforma.</summary>
public enum Rol { Administrador, Supervisor, Colaborador }

/// <summary>
/// Colaborador (ficha de empleado). El Id lo asigna la aplicación y no es identidad: los módulos del frontend
/// que aún trabajan en memoria (asistencia, ítems, portal) referencian a los colaboradores por estos ids.
/// </summary>
public class Colaborador
{
    public int Id { get; set; }
    public string Apellidos { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string TipoDocumento { get; set; } = "DNI";
    public string Documento { get; set; } = "";
    public string Cargo { get; set; } = "";
    public string Division { get; set; } = "";
    public string Area { get; set; } = "";
    public string SubArea { get; set; } = "";
    public DateOnly Ingreso { get; set; }
    /// <summary>Monto en planilla.</summary>
    public decimal SueldoNeto { get; set; }
    /// <summary>Jefatura directa (organigrama). Define qué ve un supervisor.</summary>
    public int? JefeId { get; set; }

    // Datos de la ficha; opcionales.
    public string? EmailCorporativo { get; set; }
    public string? Telefono { get; set; }
    public DateOnly? Nacimiento { get; set; }
    public string? Direccion { get; set; }
    public string? Carrera { get; set; }
    public string? Institucion { get; set; }
    public string? PlanSalud { get; set; }
    public string? Pension { get; set; }
    public string? Cuspp { get; set; }
    public string? Banco { get; set; }
    public string? FormaPago { get; set; }
    public decimal? SueldoBase { get; set; }
    public string? Contrato { get; set; }
    public string? Jornada { get; set; }
    public string? Sindicalizado { get; set; }
    public string? Aviso { get; set; }

    // Asistencia.
    /// <summary>Recinto principal (lugar de trabajo).</summary>
    public int? RecintoId { get; set; }
    /// <summary>Puede marcar desde la app móvil.</summary>
    public bool UsaApp { get; set; }
    /// <summary>Jornal diario si se le paga por día; null = mensual.</summary>
    public decimal? Jornal { get; set; }
    /// <summary>Saldo de vacaciones importado al inicio del control (días).</summary>
    public decimal SaldoVacacionesInicial { get; set; }

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}

public class Cargo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Familia { get; set; } = "";
    /// <summary>Cantidad de sub-áreas donde existe el cargo.</summary>
    public int SubAreas { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

public class FamiliaCargo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
}

public class Competencia
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool Activo { get; set; } = true;
}

/// <summary>Usuario que inicia sesión. Puede estar vinculado a un colaborador.</summary>
public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Rol Rol { get; set; }
    /// <summary>Perfil mostrado en Administración › Usuarios (p. ej. "Prevencionista de Riesgos").</summary>
    public string Perfil { get; set; } = "";
    public int? ColaboradorId { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? UltimoAcceso { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    /// <summary>La contraseña es temporal (creada o restablecida por un administrador): debe cambiarse antes de usar la app.</summary>
    public bool DebeCambiarPassword { get; set; }
    /// <summary>Cambia al desactivar, restablecer o cambiar la contraseña o el rol: invalida los tokens emitidos antes.</summary>
    public string Sello { get; set; } = Guid.NewGuid().ToString("N");
}

/// <summary>Registro de auditoría ("Historial de cambios").</summary>
public class Cambio
{
    public long Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Usuario { get; set; } = "";
    public string Origen { get; set; } = "Web";
    public string Colaborador { get; set; } = "—";
    public string Documento { get; set; } = "—";
    public string Entidad { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Propiedad { get; set; } = "—";
    public string Anterior { get; set; } = "—";
    public string Actual { get; set; } = "—";
}
