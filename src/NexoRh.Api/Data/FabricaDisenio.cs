using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Data;

/// <summary>Solo para "dotnet ef" (migraciones): un contexto sin tenant.</summary>
public class FabricaNexoDbContext : IDesignTimeDbContextFactory<NexoDbContext>
{
    public NexoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NexoDbContext>().UseSqlServer("Server=.;Database=NexoRh;Trusted_Connection=True;TrustServerCertificate=True").Options, new ContextoTenant());
}

public class FabricaCatalogoDbContext : IDesignTimeDbContextFactory<CatalogoDbContext>
{
    public CatalogoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogoDbContext>().UseSqlServer("Server=.;Database=NexoRhCatalogo;Trusted_Connection=True;TrustServerCertificate=True").Options);
}
