namespace NexoRh.Api.Catalogo;

/// <summary>
/// Tenant del request actual (lo fija ResolucionTenant a partir del host). NexoDbContext lo usa para filtrar y para
/// SESSION_CONTEXT (Row-Level Security). Sin tenant, ninguna consulta de negocio devuelve filas.
/// </summary>
public class ContextoTenant(CadenasConexion? cadenas)
{
    /// <summary>Solo para herramientas de diseño (migraciones) y pruebas unitarias.</summary>
    public ContextoTenant() : this(null) { }

    public Guid? Id { get; private set; }
    public string Slug { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public string Estado { get; private set; } = "";
    public string? CadenaConexion { get; private set; }

    /// <summary>
    /// Solo para operaciones de la plataforma que deben cruzar tenants a propósito (consola del proveedor, búsqueda de
    /// enlaces por correo). Desactiva el filtro de EF y el de RLS: nunca lo actives en un request de un tenant.
    /// </summary>
    public bool ModoPlataforma { get; private set; }

    public bool Resuelto => Id is not null;

    public void Usar(Tenant t)
    {
        if (Id is { } actual && actual != t.Id) throw new InvalidOperationException("El tenant del request ya está fijado.");
        Id = t.Id;
        Slug = t.Slug;
        Nombre = t.Nombre;
        Estado = t.Estado;
        // La del catálogo viene cifrada; NexoDbContext la usa en claro.
        CadenaConexion = cadenas is null ? t.CadenaConexion : cadenas.Descifrar(t.CadenaConexion);
    }

    public void ActivarModoPlataforma() => ModoPlataforma = true;
}
