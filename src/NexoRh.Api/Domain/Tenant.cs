namespace NexoRh.Api.Domain;

/// <summary>
/// Toda entidad de negocio pertenece a un tenant (cuenta cliente). NexoDbContext filtra por el tenant del request,
/// asigna TenantId al insertar, impide cambiarlo, y SQL Server lo refuerza con Row-Level Security.
/// </summary>
public interface IEntidadTenant
{
    Guid TenantId { get; set; }
}
