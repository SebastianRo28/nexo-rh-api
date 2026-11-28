using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

namespace NexoRh.Api.Catalogo;

/// <summary>
/// Alta de tenants: fila en el catálogo, su subdominio y los datos iniciales en la BD de negocio (datos base y,
/// opcionalmente, la empresa de ejemplo). Cada tenant se siembra en su propio scope, con su ContextoTenant.
/// </summary>
public class AprovisionadorTenants(IServiceScopeFactory scopes, IConfiguration config, IOptions<OpcionesPlataforma> plataforma, CadenasConexion cadenas,
    ILogger<AprovisionadorTenants> log)
{
    /// <summary>
    /// Cadena de la BD dedicada de un tenant: misma instancia que la compartida (o BaseDatos:ServidorDedicadas) con la base
    /// {BD compartida}_{slug}.
    /// </summary>
    public string CadenaDedicada(string slug)
    {
        var compartida = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(config.GetConnectionString("Nexo"));
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(config["BaseDatos:ServidorDedicadas"] is { Length: > 0 } s ? s : compartida.ConnectionString)
        {
            InitialCatalog = $"{compartida.InitialCatalog}_{slug.Replace('-', '_')}",
        };
        return b.ConnectionString;
    }

    /// <summary>Crea (si no existe) y migra la BD dedicada del tenant, con su política de Row-Level Security.</summary>
    public async Task MigrarDedicadaAsync(Tenant t, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(t);
        await MigrarAsync(scope.ServiceProvider.GetRequiredService<NexoDbContext>(), ct);
    }

    /// <summary>Tenant de la empresa de ejemplo. Los datos de antes de la capa multiempresa quedaron en él.</summary>
    public static readonly Guid ComercialAndina = Guid.Parse("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44");

    public static Tenant TenantDemo() => new()
    {
        Id = ComercialAndina, Slug = "comercialandina", Nombre = "Comercial Andina", RazonSocial = "Comercial Andina S.A.C.", Ruc = "20601234571",
    };

    /// <summary>
    /// Crea el tenant si no existe (por id o slug) y carga sus datos iniciales; las semillas no duplican nada. Al crearlo le
    /// asigna los módulos adicionales indicados (por defecto, todos) y, con datos de ejemplo, facturas de muestra.
    /// </summary>
    public async Task<Tenant> AsegurarAsync(Tenant datos, bool datosDemo, CancellationToken ct = default, IEnumerable<string>? modulos = null, bool adminDesdeConfig = true,
        bool bdDedicada = false)
    {
        var nuevaDedicada = false;
        Tenant t;
        using (var scope = scopes.CreateScope())
        {
            var catalogo = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            t = await catalogo.Tenants.FirstOrDefaultAsync(x => x.Id == datos.Id || x.Slug == datos.Slug, ct) ?? datos;
            if (catalogo.Entry(t).State == EntityState.Detached)
            {
                if (bdDedicada) { t.CadenaConexion = cadenas.Proteger(CadenaDedicada(t.Slug)); nuevaDedicada = true; }
                catalogo.Tenants.Add(t);
                catalogo.Dominios.Add(new TenantDominio { TenantId = t.Id, Host = $"{t.Slug}.{plataforma.Value.Dominio}".ToLowerInvariant() });
                foreach (var m in (modulos ?? Modulos.Adicionales).Distinct())
                    catalogo.TenantModulos.Add(new TenantModulo { TenantId = t.Id, Codigo = m });
                if (datosDemo) catalogo.Facturas.AddRange(FacturasDemo(t.Id));
                await catalogo.SaveChangesAsync(ct);
                scope.ServiceProvider.GetRequiredService<BuscadorTenants>().Olvidar($"{t.Slug}.{plataforma.Value.Dominio}");
                log.LogInformation("Tenant creado: {Slug} ({Id}).", t.Slug, t.Id);
            }
        }

        if (nuevaDedicada) await MigrarDedicadaAsync(t, ct);
        using (var scope = scopes.CreateScope())
        {
            var sp = scope.ServiceProvider;
            sp.GetRequiredService<ContextoTenant>().Usar(t);
            var db = sp.GetRequiredService<NexoDbContext>();
            await SemillaBase.CargarAsync(db, sp.GetRequiredService<Genericos>(), t);
            if (datosDemo)
            {
                await Semilla.CargarAsync(db, config, log);
                await SemillaWorkflow.CargarAsync(db);
                await SemillaComunicacion.CargarAsync(db);
                await SemillaSst.CargarAsync(db);
                await SemillaAdministracion.CargarAsync(db);
                await SemillaReporteria.CargarAsync(db);
                await SemillaModulos.CargarAsync(db);
                await SemillaAsistencia.CompletarDemoAsync(db);
            }
            else await SemillaBase.CargarProduccionAsync(db, config, sp.GetRequiredService<RelojAsistencia>(), log, adminDesdeConfig);
        }
        return t;
    }

    /// <summary>
    /// Facturas de muestra de la empresa de ejemplo (plan Pro, S/ 18 por colaborador): los tres meses anteriores pagados y la
    /// del mes pasado pendiente, que vence a fin de este mes (relativas a la fecha del alta: nunca nacen vencidas).
    /// </summary>
    public static IEnumerable<Factura> FacturasDemo(Guid tenantId)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesActual = new DateOnly(hoy.Year, hoy.Month, 1);
        return Enumerable.Range(1, 4).Reverse().Select(atras =>
        {
            var periodo = mesActual.AddMonths(-atras);
            var emitida = periodo.AddMonths(1);
            var colaboradores = atras == 1 ? 16 : 15;
            var subtotal = colaboradores * 18m;
            return new Factura { TenantId = tenantId, Numero = $"D{tenantId.ToString("N")[..4].ToUpperInvariant()}-{emitida:yyyyMM}", Periodo = $"{periodo.Year:D4}-{periodo.Month:D2}",
                Emitida = emitida, Vence = emitida.AddMonths(1).AddDays(-1), Colaboradores = colaboradores, Subtotal = subtotal,
                Igv = Math.Round(subtotal * Modulos.Igv, 2), Estado = atras == 1 ? "Pendiente" : "Pagada" };
        }).ToList();
    }

    /// <summary>
    /// Migra la BD de negocio: apaga la política de Row-Level Security mientras corren las migraciones y la vuelve a
    /// crear al final con todas las tablas que tienen TenantId (también las que agregue una migración nueva).
    /// </summary>
    public async Task MigrarNegocioAsync(CancellationToken ct = default)
    {
        using (var scope = scopes.CreateScope())
            await MigrarAsync(scope.ServiceProvider.GetRequiredService<NexoDbContext>(), ct);
        // Luego cada BD dedicada del catálogo; se detiene ante el primer error (sección 12) y Salud la muestra pendiente.
        List<Tenant> dedicadas;
        using (var scope = scopes.CreateScope())
            dedicadas = await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Tenants.AsNoTracking()
                .Where(t => t.CadenaConexion != null && t.Estado != EstadosTenant.Eliminado).ToListAsync(ct);
        foreach (var t in dedicadas)
        {
            try { await MigrarDedicadaAsync(t, ct); }
            catch (Exception e)
            {
                log.LogError(e, "No se pudo migrar la BD dedicada de {Slug}; se detienen las migraciones de las BD dedicadas.", t.Slug);
                break;
            }
        }
    }

    private static async Task MigrarAsync(NexoDbContext db, CancellationToken ct)
    {
        if (await db.Database.CanConnectAsync(ct))
            await db.Database.ExecuteSqlRawAsync("IF OBJECT_ID(N'rls.PoliticaTenant', N'SP') IS NOT NULL DROP SECURITY POLICY rls.PoliticaTenant;", ct);
        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync("IF OBJECT_ID(N'rls.ActualizarPolitica', N'P') IS NOT NULL EXEC rls.ActualizarPolitica;", ct);
    }
}
