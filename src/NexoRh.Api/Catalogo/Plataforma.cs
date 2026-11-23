namespace NexoRh.Api.Catalogo;

// Consola del proveedor (admin.{dominio}, sección 9): solo el equipo de Nexo RH. Todo vive en el catálogo.

public static class RolesPlataforma
{
    /// <summary>platform_admin: escribe en toda la consola.</summary>
    public const string Admin = "Admin";
    /// <summary>Lee todo y ve Salud.</summary>
    public const string Operaciones = "Operaciones";
    /// <summary>Lee y abre sesiones de soporte.</summary>
    public const string Soporte = "Soporte";
    /// <summary>Lee tenants y planes.</summary>
    public const string Ventas = "Ventas";
    public static readonly string[] Todos = [Admin, Operaciones, Soporte, Ventas];
}

/// <summary>Usuario del equipo de Nexo RH (platform_user). Nunca es un usuario de un tenant. 2FA obligatorio.</summary>
public class UsuarioPlataforma
{
    public int Id { get; set; }
    public string Correo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Rol { get; set; } = RolesPlataforma.Operaciones;
    public string PasswordHash { get; set; } = "";
    /// <summary>Clave TOTP cifrada (Data Protection); con TotpActivo = false está pendiente de enrolar en el primer ingreso.</summary>
    public string? TotpSecreto { get; set; }
    public bool TotpActivo { get; set; }
    public bool Activo { get; set; } = true;
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime? UltimoAcceso { get; set; }
    public string Sello { get; set; } = Guid.NewGuid().ToString("N");
}

/// <summary>Auditoría de la consola (audit_platform): inicios de sesión, altas, suspensiones, soporte, planes, flags…</summary>
public class AuditoriaPlataforma
{
    public long Id { get; set; }
    public DateTime En { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "";
    public string Accion { get; set; } = "";
    public Guid? TenantId { get; set; }
    public string? Detalle { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>Token de un solo uso del login de la consola (segundo paso del 2FA, 5 minutos). Solo se guarda su hash.</summary>
public class TokenPlataforma
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Hash { get; set; } = "";
    public DateTime Expira { get; set; }
    public DateTime? Usado { get; set; }
}

public static class ModosSoporte
{
    public const string Lectura = "lectura", Escritura = "escritura";
}

/// <summary>Sesión de soporte de un usuario de plataforma dentro de la app de un tenant (60 minutos, auditada).</summary>
public class SesionSoporte
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UsuarioPlataformaId { get; set; }
    public string Agente { get; set; } = "";
    public Guid TenantId { get; set; }
    public string Motivo { get; set; } = "";
    public string? Ticket { get; set; }
    public string Modo { get; set; } = ModosSoporte.Lectura;
    public string? Justificacion { get; set; }
    /// <summary>El cliente lo había autorizado (Seguridad y acceso › Permitir acceso de soporte).</summary>
    public bool Autorizada { get; set; }
    /// <summary>Hash del token de entrada (un solo uso).</summary>
    public string TokenHash { get; set; } = "";
    public DateTime? Canjeada { get; set; }
    public DateTime Inicio { get; set; } = DateTime.UtcNow;
    public DateTime Expira { get; set; }
    public DateTime? Fin { get; set; }
    public bool Vigente => Fin is null && Expira > DateTime.UtcNow;
}

/// <summary>Nota interna del equipo sobre un tenant (no la ve el cliente).</summary>
public class NotaTenant
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string Autor { get; set; } = "";
    public string Texto { get; set; } = "";
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
}

public static class TiposAnuncio
{
    public const string Novedad = "Novedad", Mantenimiento = "Mantenimiento", AvisoLegal = "Aviso legal";
    public static readonly string[] Todos = [Novedad, Mantenimiento, AvisoLegal];
}

/// <summary>Anuncio para los administradores de los tenants (megáfono, banners de mantenimiento), segmentado por plan o módulo.</summary>
public class Anuncio
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Cuerpo { get; set; } = "";
    public string Tipo { get; set; } = TiposAnuncio.Novedad;
    /// <summary>Códigos de plan separados por coma (vacío = todos).</summary>
    public string Planes { get; set; } = "";
    /// <summary>Códigos de módulo separados por coma (vacío = todos).</summary>
    public string Modulos { get; set; } = "";
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    /// <summary>banner, modal o banner_correo.</summary>
    public string Canal { get; set; } = "banner";
    /// <summary>false = borrador; true = publicado (visible entre Desde y Hasta).</summary>
    public bool Publicado { get; set; }
    public string CreadoPor { get; set; } = "";
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public string Estado(DateTime ahora) => !Publicado ? "Borrador" : Desde > ahora ? "Programada" : Hasta is { } h && h <= ahora ? "Finalizada" : "Publicada";
}

/// <summary>Definición de un feature flag (lanzamiento gradual). La activación por tenant está en TenantFlag.</summary>
public class DefinicionFlag
{
    /// <summary>snake_case, p. ej. firma_masiva_v2.</summary>
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    /// <summary>Módulo dueño: el flag solo aplica si el tenant lo tiene contratado.</summary>
    public string? Modulo { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Catálogo global compartido por todos los tenants (ubigeo, AFP, feriados nacionales…).</summary>
public class CatalogoGlobal
{
    public string Clave { get; set; } = "";
    public string Nombre { get; set; } = "";
    /// <summary>Columnas esperadas del archivo, separadas por coma.</summary>
    public string Columnas { get; set; } = "";
}

/// <summary>Versión inmutable de un catálogo global; los tenants la adoptan desde su vigencia.</summary>
public class VersionCatalogo
{
    public int Id { get; set; }
    public string Clave { get; set; } = "";
    public string Version { get; set; } = "";
    public DateOnly VigenteDesde { get; set; }
    public int Registros { get; set; }
    public string? Notas { get; set; }
    /// <summary>Filas en CSV (con encabezado).</summary>
    public string Contenido { get; set; } = "";
    public string PublicadaPor { get; set; } = "";
    public DateTime PublicadaEn { get; set; } = DateTime.UtcNow;
}
