using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Data;

namespace NexoRh.Api.Catalogo;

/// <summary>Consultas de la consola que cruzan tenants a propósito (modo plataforma) o entran a uno en particular.</summary>
public class ConsultasPlataforma(CatalogoDbContext catalogo, IServiceScopeFactory scopes)
{
    /// <summary>Colaboradores (facturables) de cada tenant: una consulta en la BD compartida y una por cada BD dedicada.</summary>
    public async Task<Dictionary<Guid, int>> ColaboradoresAsync(IReadOnlyCollection<Tenant> tenants, CancellationToken ct = default)
    {
        var r = new Dictionary<Guid, int>();
        var compartidos = tenants.Where(t => t.CadenaConexion is null).Select(t => t.Id).ToList();
        if (compartidos.Count > 0)
            await EnPlataformaAsync(async db =>
            {
                foreach (var x in await db.Colaboradores.AsNoTracking().Where(c => compartidos.Contains(c.TenantId))
                             .GroupBy(c => c.TenantId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct))
                    r[x.Key] = x.N;
                return 0;
            });
        foreach (var t in tenants.Where(t => t.CadenaConexion is not null))
            r[t.Id] = await EnTenantAsync(t, sp => sp.GetRequiredService<NexoDbContext>().Colaboradores.CountAsync(ct));
        return r;
    }

    public async Task<T> EnTenantAsync<T>(Tenant t, Func<IServiceProvider, Task<T>> f)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(t);
        return await f(scope.ServiceProvider);
    }

    private async Task<T> EnPlataformaAsync<T>(Func<NexoDbContext, Task<T>> f)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ContextoTenant>().ActivarModoPlataforma();
        return await f(scope.ServiceProvider.GetRequiredService<NexoDbContext>());
    }

    /// <summary>Precio mensual por colaborador: el pactado o el del plan, más los módulos adicionales vigentes.</summary>
    public async Task<Dictionary<Guid, decimal>> PreciosAsync(IReadOnlyCollection<Tenant> tenants, CancellationToken ct = default)
    {
        var planes = await catalogo.Planes.AsNoTracking().ToDictionaryAsync(p => p.Codigo, ct);
        var modulos = await catalogo.ModulosComerciales.AsNoTracking().ToDictionaryAsync(m => m.Codigo, ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var ids = tenants.Select(t => t.Id).ToList();
        var contratados = await catalogo.TenantModulos.AsNoTracking()
            .Where(m => ids.Contains(m.TenantId) && m.ActivoDesde <= hoy && (m.ActivoHasta == null || m.ActivoHasta >= hoy)).ToListAsync(ct);
        return tenants.ToDictionary(t => t.Id, t =>
        {
            var plan = planes.GetValueOrDefault(t.PlanCodigo);
            var incluidos = Modulos.Lista(plan?.ModulosIncluidos);
            var adicionales = contratados.Where(m => m.TenantId == t.Id && !incluidos.Contains(m.Codigo))
                .Sum(m => m.PrecioPactado ?? modulos.GetValueOrDefault(m.Codigo)?.PrecioPorColaborador ?? 0);
            return (t.PrecioPactado ?? plan?.PrecioPorColaborador ?? 0) + adicionales;
        });
    }
}
