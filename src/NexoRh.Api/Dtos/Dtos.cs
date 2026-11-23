namespace NexoRh.Api.Dtos;

// Las formas siguen los modelos del frontend (src/app/core/models.ts) para no cambiar las pantallas.

public record LoginPeticion(string Correo, string Password);
public record UsuarioSesion(int Id, string Nombre, string Correo, string Rol, string Perfil, int? ColaboradorId, string Iniciales, bool DebeCambiarPassword,
    bool DobleFactor = false, bool DebeActivarDobleFactor = false);
/// <summary>Sesión emitida, o (con MfaRequerido) el mfaToken para el segundo paso del login.</summary>
public record LoginRespuesta(string? Token, DateTime? Expira, UsuarioSesion? Usuario, bool MfaRequerido = false, string? MfaToken = null, SoporteInfo? Soporte = null);
/// <summary>Sesión de soporte de Nexo RH dentro de la app del tenant (banner rojo con cuenta regresiva).</summary>
public record SoporteInfo(Guid SesionId, string Agente, string Modo, string Motivo, string? Ticket, DateTime Expira);
public record DobleFactorPeticion(string MfaToken, string Codigo);
public record CodigoPeticion(string Codigo);
public record PasswordPeticion(string Password);
public record OlvidePeticion(string Correo);
public record NuevaPasswordPeticion(string Token, string Password, bool? AceptaPrivacidad = null);
public record TokenInfo(string Tipo, string Nombre, string Correo, int PasswordMinimo, string? PrivacidadUrl);
public record DobleFactorInicio(string Clave, string Uri);
public record CambioPassword(string Actual, string Nueva);

// Administración › Usuarios
public record UsuarioDto(int Id, string Nombre, string Correo, string Rol, string Perfil, int? PerfilId, int? ColaboradorId, string? Colaborador,
    bool Activo, string UltimoAcceso, bool Bloqueado, bool DebeCambiarPassword,
    bool PendienteActivacion = false, bool DobleFactor = false);
/// <summary>PerfilId define el rol (el del perfil); si no se envía, se usa el perfil predefinido del Rol.</summary>
public record UsuarioGuardar(string Nombre, string Correo, string? Rol, int? ColaboradorId, int? PerfilId = null, bool? Invitar = null);
public record PerfilDto(int Id, string Nombre, string Rol, bool Predefinido, string Descripcion, int Usuarios, string Creado, string Actualizado, string ModificadoPor);
public record PerfilGuardar(string? Nombre, string? Rol, string? Descripcion);
/// <summary>PasswordTemporal es null cuando se envió una invitación por correo.</summary>
public record UsuarioCreado(UsuarioDto Usuario, string? PasswordTemporal);
public record EstadoUsuario(bool Activo);

public class DetalleDto
{
    public string? TipoDocumento { get; set; }
    public string? EmailCorporativo { get; set; }
    public string? Telefono { get; set; }
    public string? Nacimiento { get; set; }
    public string? Direccion { get; set; }
    public string? Carrera { get; set; }
    public string? Institucion { get; set; }
    public string? PlanSalud { get; set; }
    public string? Pension { get; set; }
    public string? Cuspp { get; set; }
    public string? Banco { get; set; }
    public string? FormaPago { get; set; }
    public string? Supervisor { get; set; }
    public decimal? SueldoBase { get; set; }
    public string? Contrato { get; set; }
    public string? Jornada { get; set; }
    public string? Sindicalizado { get; set; }
    public string? Aviso { get; set; }
}

public class ColaboradorDto
{
    public int Id { get; set; }
    public string Apellidos { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Cargo { get; set; } = "";
    public string Division { get; set; } = "";
    public string Area { get; set; } = "";
    public string SubArea { get; set; } = "";
    public string Ingreso { get; set; } = "";
    /// <summary>Null cuando el usuario no puede ver datos salariales de esta persona.</summary>
    public decimal? SueldoNeto { get; set; }
    public int? JefeId { get; set; }
    /// <summary>True si se ocultaron datos personales o salariales por permisos.</summary>
    public bool Reservado { get; set; }
    public DetalleDto? Detalle { get; set; }
}

public class ColaboradorGuardar
{
    public string? Apellidos { get; set; }
    public string? Nombres { get; set; }
    public string? Documento { get; set; }
    public string? Cargo { get; set; }
    public string? Division { get; set; }
    public string? Area { get; set; }
    public string? SubArea { get; set; }
    public string? Ingreso { get; set; }
    public decimal? SueldoNeto { get; set; }
    public int? JefeId { get; set; }
    public DetalleDto? Detalle { get; set; }
}

public record CargoDto(int Id, string Nombre, string Codigo, int SubAreas, int Colaboradores, string Familia, bool Activo, string? Descripcion);
public record CargoGuardar(string? Nombre, string? Codigo, string? Familia, int? SubAreas, string? Descripcion, bool? Activo);
public record FamiliaDto(int Id, string Nombre, int Cargos, bool Activo);
public record CompetenciaDto(int Id, string Nombre, string Descripcion, bool Activo);
public record SubAreaDto(string Nombre, int Colaboradores);
public record AreaDto(string Nombre, int Colaboradores, List<SubAreaDto> SubAreas);
public record DivisionDto(string Nombre, int Colaboradores, List<AreaDto> Areas);

public record CambioDto(long Id, string Usuario, string Origen, string Colaborador, string Documento, string Entidad, string Accion, string Fecha,
    string Propiedad, string Anterior, string Actual);
public record CambioNuevo(string? Colaborador, string? Documento, string? Entidad, string? Accion, string? Propiedad, string? Anterior, string? Actual);
public record Pagina<T>(int Total, IReadOnlyList<T> Items);
