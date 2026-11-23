namespace NexoRh.Api.Catalogo;

// BD catálogo (NexoRhCatalogo): datos de la plataforma, fuera de cualquier tenant. Sin TenantId propio.

public static class EstadosTenant
{
    public const string Trial = "Trial", Activo = "Activo", Moroso = "Moroso", Suspendido = "Suspendido", Cancelado = "Cancelado", Eliminado = "Eliminado";
    public static readonly string[] Todos = [Trial, Activo, Moroso, Suspendido, Cancelado, Eliminado];
}

/// <summary>Cuenta cliente (quien contrata Nexo RH). Su app vive en {slug}.{dominio de la plataforma} o en un dominio propio.</summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>3 a 30 caracteres [a-z0-9-]; único y fijo tras la puesta en marcha.</summary>
    public string Slug { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string RazonSocial { get; set; } = "";
    public string Ruc { get; set; } = "";
    public string Pais { get; set; } = "PE";
    public string Estado { get; set; } = EstadosTenant.Activo;
    public DateOnly? TrialHasta { get; set; }
    /// <summary>Plan contratado (BASICO, PRO, ENTERPRISE).</summary>
    public string PlanCodigo { get; set; } = Modulos.PlanPorDefecto;
    /// <summary>Datos de facturación (además de RazonSocial y Ruc).</summary>
    public string? DireccionFiscal { get; set; }
    public string? CorreoFacturacion { get; set; }
    public string? Industria { get; set; }
    /// <summary>Ejecutivo de cuenta del equipo comercial de Nexo RH.</summary>
    public string? EjecutivoCuenta { get; set; }
    /// <summary>Precio por colaborador pactado (null = el del plan).</summary>
    public decimal? PrecioPactado { get; set; }
    /// <summary>Motivo de la última suspensión (se limpia al reactivar).</summary>
    public string? MotivoSuspension { get; set; }
    /// <summary>Cancelado: 30 días de solo lectura y exportación; luego se eliminan sus datos.</summary>
    public DateTime? CanceladoEn { get; set; }
    public DateTime? EliminadoEn { get; set; }
    /// <summary>Certificado de eliminación (JSON): fecha, responsable y filas borradas por tabla.</summary>
    public string? CertificadoEliminacion { get; set; }
    /// <summary>null = BD compartida; con valor, el tenant tiene BD dedicada (plan Enterprise). Cifrada con Data Protection.</summary>
    public string? CadenaConexion { get; set; }
    /// <summary>Mientras se mueve a su BD dedicada: la API responde 503 a la app del tenant.</summary>
    public bool EnMantenimiento { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Host con el que se entra a la app de un tenant: su subdominio o un dominio propio verificado.</summary>
public class TenantDominio
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>En minúsculas y sin puerto, p. ej. grupoandino.nexorh.pe o rrhh.cliente.com.</summary>
    public string Host { get; set; } = "";
    /// <summary>Subdominio o Personalizado.</summary>
    public string Tipo { get; set; } = "Subdominio";
    public bool Verificado { get; set; } = true;
    public bool Principal { get; set; } = true;
    /// <summary>Dominio propio: Pendiente (sin verificar), Emitiendo (verificado, se emite al primer acceso por HTTPS) o Activo.</summary>
    public string EstadoCertificado { get; set; } = EstadosCertificado.Activo;
    public DateTime? VerificadoEn { get; set; }
    public DateTime? CertificadoEn { get; set; }
    public DateTime? UltimaRevision { get; set; }
    /// <summary>Qué faltó en la última verificación (registro TXT, CNAME…).</summary>
    public string? ErrorVerificacion { get; set; }
}

public static class EstadosCertificado
{
    public const string Pendiente = "Pendiente", Emitiendo = "Emitiendo", Activo = "Activo";
}

/// <summary>
/// Correos desde el dominio del cliente (Enterprise): remitente {Remitente}@{Dominio}, firmado con DKIM. Se usa cuando SPF,
/// DKIM y DMARC están verificados en el DNS del cliente.
/// </summary>
public class TenantCorreoDominio
{
    public Guid TenantId { get; set; }
    public string Dominio { get; set; } = "";
    /// <summary>Parte local de la dirección, p. ej. rrhh → rrhh@cliente.com.pe.</summary>
    public string Remitente { get; set; } = "rrhh";
    public string Selector { get; set; } = "";
    /// <summary>SubjectPublicKeyInfo en base64 (el valor p= del registro DKIM).</summary>
    public string ClavePublica { get; set; } = "";
    /// <summary>Clave privada PKCS#8 cifrada con Data Protection.</summary>
    public string ClavePrivada { get; set; } = "";
    public bool SpfOk { get; set; }
    public bool DkimOk { get; set; }
    public bool DmarcOk { get; set; }
    public DateTime? VerificadoEn { get; set; }
    public DateTime? UltimaRevision { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public bool Verificado => SpfOk && DkimOk && DmarcOk;
}

/// <summary>Configuración de acceso y del login de un tenant (tenant_settings de la especificación).</summary>
public class TenantAjustes
{
    public Guid TenantId { get; set; }
    public PoliticaAcceso Acceso { get; set; } = new();
    /// <summary>Aviso legal o de privacidad propio del tenant (se acepta al activar la cuenta).</summary>
    public string? PrivacidadUrl { get; set; }
    /// <summary>Muestra el checklist de implementación a los administradores (Parámetros generales › Modo implementación).</summary>
    public bool ModoImplementacion { get; set; }
    /// <summary>“Permitir acceso de soporte” hasta esta fecha (null = no autorizado). Lo usa la consola del proveedor.</summary>
    public DateTime? SoporteHasta { get; set; }
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Métodos de login, política de contraseña, doble factor y sesión (sección 4.2). Se guarda como JSON.</summary>
public class PoliticaAcceso
{
    /// <summary>Permite ingresar con el N° de documento del colaborador en lugar del correo.</summary>
    public bool LoginDocumento { get; set; }
    public int PasswordMinimo { get; set; } = 10;
    public int MaxIntentos { get; set; } = 5;
    public int BloqueoMinutos { get; set; } = 15;
    /// <summary>Opcional, Administradores (obligatorio para ellos) u Obligatorio (todos).</summary>
    public string DobleFactor { get; set; } = ModosDobleFactor.Opcional;
    /// <summary>Duración de la sesión en minutos.</summary>
    public int SesionMinutos { get; set; } = 480;
    // Preparados para cuando se conecten los proveedores (todavía no funcionan).
    public bool Google { get; set; }
    public bool Microsoft { get; set; }
    public bool Saml { get; set; }
}

public static class ModosDobleFactor
{
    public const string Opcional = "Opcional", Administradores = "Administradores", Obligatorio = "Obligatorio";
}

/// <summary>Correo emitido por la plataforma. Con el proveedor “Bandeja” (desarrollo) queda solo aquí.</summary>
public class CorreoSaliente
{
    public long Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Para { get; set; } = "";
    public string Asunto { get; set; } = "";
    public string Html { get; set; } = "";
    /// <summary>Enlace principal del correo (para la bandeja de desarrollo y las pruebas).</summary>
    public string? Enlace { get; set; }
    public string Proveedor { get; set; } = "Bandeja";
    public string Estado { get; set; } = "Guardado";
    /// <summary>Dirección del remitente (la de la plataforma o la del dominio del cliente).</summary>
    public string? Remitente { get; set; }
    /// <summary>Dominio con el que se firmó DKIM (null = sin firma propia).</summary>
    public string? DkimDominio { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
