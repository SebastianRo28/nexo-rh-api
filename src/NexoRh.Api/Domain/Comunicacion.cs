namespace NexoRh.Api.Domain;

// Desarrollo Organizacional: encuestas, canal de denuncias y publicaciones del portal.

/// <summary>Encuesta de clima o libre. Se responde desde Mi Portal entre Inicio y Termino.</summary>
public class Encuesta : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Clima o Libre.</summary>
    public string Tipo { get; set; } = "Libre";
    public string Descripcion { get; set; } = "";
    /// <summary>Anónima: las respuestas no guardan quién respondió (solo que participó).</summary>
    public bool Anonima { get; set; }
    public DateOnly Inicio { get; set; }
    public DateOnly Termino { get; set; }
    /// <summary>Cerrada antes de tiempo por un administrador.</summary>
    public bool Cerrada { get; set; }
    /// <summary>Colaboradores a quienes va dirigida (vacía = todos).</summary>
    public List<int> Destinatarios { get; set; } = [];
    public List<PreguntaEncuesta> Preguntas { get; set; } = [];
    public DateOnly Creada { get; set; }
    public string CreadaPor { get; set; } = "";
}

public class PreguntaEncuesta
{
    /// <summary>Identificador de la pregunta dentro de la encuesta (c1, c2…); en la API se expone como "id".</summary>
    public string Codigo { get; set; } = "";
    public string Texto { get; set; } = "";
    /// <summary>escala (1 a 5), opcion o texto.</summary>
    public string Tipo { get; set; } = "escala";
    public List<string> Opciones { get; set; } = [];
    public bool Obligatoria { get; set; }
}

/// <summary>Quién ya respondió (para no responder dos veces). En las anónimas no se relaciona con las respuestas.</summary>
public class ParticipacionEncuesta : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int EncuestaId { get; set; }
    public int ColaboradorId { get; set; }
    public DateOnly Fecha { get; set; }
}

/// <summary>Respuestas de una persona. ColaboradorId es null en las encuestas anónimas.</summary>
public class RespuestaEncuesta : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int EncuestaId { get; set; }
    public int? ColaboradorId { get; set; }
    public Dictionary<string, string> Valores { get; set; } = [];
}

/// <summary>
/// Denuncia del canal ético. Si es anónima no se guarda quién la hizo: se sigue solo con el código de acceso, que se
/// muestra una vez al denunciante. La gestiona el comité (administradores).
/// </summary>
public class Denuncia : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    /// <summary>Código de seguimiento (secreto, 12 caracteres).</summary>
    public string Codigo { get; set; } = "";
    public string Tipo { get; set; } = "";
    /// <summary>Relación con la empresa: Colaborador, Ex colaborador, Proveedor, Cliente u Otro.</summary>
    public string Relacion { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool Anonima { get; set; } = true;
    public int? DenuncianteUsuarioId { get; set; }
    public string? DenuncianteNombre { get; set; }
    /// <summary>Nueva, En curso, Finalizada o Descartada.</summary>
    public string Estado { get; set; } = "Nueva";
    public string Resolucion { get; set; } = "";
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadaEn { get; set; } = DateTime.UtcNow;
    public List<MensajeDenuncia> Mensajes { get; set; } = [];
}

public class MensajeDenuncia : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public int DenunciaId { get; set; }
    /// <summary>Denunciante o Comité.</summary>
    public string Autor { get; set; } = "";
    public string Texto { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    /// <summary>El comité ya lo leyó (para "Nuevos mensajes").</summary>
    public bool Leido { get; set; }
}

/// <summary>Publicación del muro de Mi Portal. Programada si la fecha de publicación es futura.</summary>
public class Publicacion : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Texto { get; set; } = "";
    public string Autor { get; set; } = "Gestión de Personas";
    public bool Fijada { get; set; }
    /// <summary>null = borrador.</summary>
    public DateOnly? PublicarEl { get; set; }
    public DateOnly? Expira { get; set; }
    public int Vistas { get; set; }
    public DateTime ActualizadaEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Elementos del muro de Mi Portal que admiten reacciones y comentarios.</summary>
public static class ElementosMuro
{
    public const string Publicacion = "pub";
    public const string Reconocimiento = "rec";
    public static readonly string[] Todos = [Publicacion, Reconocimiento];
    /// <summary>Me gusta, Me encanta, Me divierte.</summary>
    public static readonly string[] Reacciones = ["like", "love", "smile"];
}

/// <summary>
/// Reacción de una persona a una publicación o un reconocimiento del muro: una por persona y elemento (se cambia o se quita).
/// Las de usuarios llevan UsuarioId; las de los datos de ejemplo, solo el colaborador.
/// </summary>
public class ReaccionMuro : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    /// <summary>ElementosMuro.Publicacion o ElementosMuro.Reconocimiento.</summary>
    public string Elemento { get; set; } = "";
    public int ElementoId { get; set; }
    public int? UsuarioId { get; set; }
    public int? ColaboradorId { get; set; }
    /// <summary>like, love o smile.</summary>
    public string Tipo { get; set; } = "like";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

/// <summary>Comentario en una publicación o un reconocimiento del muro. Lo borra su autor o un administrador.</summary>
public class ComentarioMuro : IEntidadTenant
{
    public Guid TenantId { get; set; }
    public int Id { get; set; }
    public string Elemento { get; set; } = "";
    public int ElementoId { get; set; }
    public int? UsuarioId { get; set; }
    public int? ColaboradorId { get; set; }
    /// <summary>Nombre mostrado ("Nombres Apellidos").</summary>
    public string Autor { get; set; } = "";
    public string Texto { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
