using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Catalogo;

/// <summary>
/// Mueve un tenant de la BD compartida a su BD dedicada (Enterprise) sin tocar el código: modo mantenimiento, crea y migra
/// la BD, copia sus filas (y los catálogos globales), compara los conteos, cambia la cadena en el catálogo y borra sus
/// filas de la compartida. Si algo falla antes del cambio, borra la BD nueva y el tenant sigue en la compartida.
/// </summary>
public class MudanzaBaseDedicada(IServiceScopeFactory scopes, AprovisionadorTenants aprovisionador, CadenasConexion cadenas, TrabajosAprovisionamiento trabajos,
    IConfiguration config, ILogger<MudanzaBaseDedicada> log)
{
    public static readonly (string Clave, string Titulo)[] Pasos =
        [("maintenance", "Modo mantenimiento"), ("database", "Crear y migrar la base dedicada"), ("copy", "Copiar los datos del tenant"),
         ("verify", "Comparar los conteos por tabla"), ("switch", "Cambiar la conexión en el catálogo"), ("cleanup", "Borrar sus filas de la base compartida")];

    public async Task MoverAsync(Guid trabajo, Guid tenantId, string actor, CancellationToken ct = default)
    {
        var paso = "maintenance";
        string? cadena = null;
        var cambiada = false;
        Tenant? t = null;
        try
        {
            trabajos.Paso(trabajo, paso, "running");
            t = await Catalogo(async (cat, buscador) =>
            {
                var x = await cat.Tenants.FirstAsync(y => y.Id == tenantId, ct);
                x.EnMantenimiento = true;
                await cat.SaveChangesAsync(ct);
                await Olvidar(cat, buscador, x);
                return x;
            });
            // Los requests en curso terminan antes de copiar (la caché de hosts ya se limpió).
            await Task.Delay(TimeSpan.FromSeconds(config.GetValue("BaseDatos:EsperaMantenimientoSegundos", 2)), ct);
            trabajos.Paso(trabajo, paso, "succeeded", tenantId: tenantId);

            paso = "database";
            trabajos.Paso(trabajo, paso, "running");
            cadena = aprovisionador.CadenaDedicada(t.Slug);
            var dedicado = Copia(t, cadenas.Proteger(cadena));
            await aprovisionador.MigrarDedicadaAsync(dedicado, ct);
            trabajos.Paso(trabajo, paso, "succeeded");

            paso = "copy";
            trabajos.Paso(trabajo, paso, "running");
            var compartida = config.GetConnectionString("Nexo")!;
            IModel modelo;
            using (var scope = scopes.CreateScope()) modelo = scope.ServiceProvider.GetRequiredService<NexoDbContext>().Model;
            var (globales, propias) = Tablas(modelo);
            await using var origen = await AbrirAsync(compartida, tenantId, ct);
            await using var destino = await AbrirAsync(cadena, tenantId, ct);
            // Misma instancia: INSERT … SELECT entre bases (sin memoria extra). Otra instancia: SqlBulkCopy por lotes.
            var (cOrigen, cDestino) = (new SqlConnectionStringBuilder(compartida), new SqlConnectionStringBuilder(cadena));
            string? baseOrigen = string.Equals(cOrigen.DataSource, cDestino.DataSource, StringComparison.OrdinalIgnoreCase) ? cOrigen.InitialCatalog : null;
            // Las FK se revisan todas al final (orden de inserción y autorreferencias sin importar).
            foreach (var tabla in globales.Concat(propias))
                await new SqlCommand($"ALTER TABLE {tabla} NOCHECK CONSTRAINT ALL", destino) { CommandTimeout = 0 }.ExecuteNonQueryAsync(ct);
            foreach (var tabla in globales)
                if (await ContarAsync(destino, tabla, null, ct) == 0) await CopiarAsync(origen, destino, baseOrigen, tabla, null, ct);
            foreach (var tabla in propias) await CopiarAsync(origen, destino, baseOrigen, tabla, tenantId, ct);
            foreach (var tabla in globales.Concat(propias))
                await new SqlCommand($"ALTER TABLE {tabla} WITH CHECK CHECK CONSTRAINT ALL", destino) { CommandTimeout = 0 }.ExecuteNonQueryAsync(ct);
            trabajos.Paso(trabajo, paso, "succeeded");

            paso = "verify";
            trabajos.Paso(trabajo, paso, "running");
            foreach (var tabla in propias)
            {
                var (a, b) = (await ContarAsync(origen, tabla, tenantId, ct), await ContarAsync(destino, tabla, tenantId, ct));
                if (a != b) throw new InvalidOperationException($"{tabla}: {a} filas en la compartida y {b} en la dedicada.");
            }
            trabajos.Paso(trabajo, paso, "succeeded");

            paso = "switch";
            trabajos.Paso(trabajo, paso, "running");
            await Catalogo(async (cat, buscador) =>
            {
                var x = await cat.Tenants.FirstAsync(y => y.Id == tenantId, ct);
                x.CadenaConexion = cadenas.Proteger(cadena);
                await cat.SaveChangesAsync(ct);
                await Olvidar(cat, buscador, x);
                return x;
            });
            cambiada = true;
            trabajos.Paso(trabajo, paso, "succeeded");

            paso = "cleanup";
            trabajos.Paso(trabajo, paso, "running");
            var borradas = await EnCompartidaAsync(t, db => CicloVidaTenants.BorrarNegocioAsync(db, tenantId, ct));
            await Catalogo(async (cat, buscador) =>
            {
                var x = await cat.Tenants.FirstAsync(y => y.Id == tenantId, ct);
                x.EnMantenimiento = false;
                cat.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = actor, Accion = "tenant.database.dedicated", TenantId = tenantId,
                    Detalle = $"{new SqlConnectionStringBuilder(cadena).InitialCatalog} · {borradas.Values.Sum()} filas movidas desde la compartida" });
                await cat.SaveChangesAsync(ct);
                await Olvidar(cat, buscador, x);
                return x;
            });
            trabajos.Paso(trabajo, paso, "succeeded");
        }
        catch (Exception e)
        {
            log.LogError(e, "Falló el paso {Paso} al mover el tenant {Tenant} a su BD dedicada.", paso, tenantId);
            trabajos.Paso(trabajo, paso, "failed", e.Message);
            if (!cambiada && cadena is not null && t is not null)
            {
                // El tenant sigue en la compartida: la BD a medio copiar se descarta.
                try { await BorrarBaseAsync(cadena, CancellationToken.None); }
                catch (Exception e2) { log.LogError(e2, "No se pudo borrar la BD dedicada incompleta de {Slug}.", t.Slug); }
            }
            try
            {
                await Catalogo(async (cat, buscador) =>
                {
                    var x = await cat.Tenants.FirstAsync(y => y.Id == tenantId, CancellationToken.None);
                    x.EnMantenimiento = false;
                    await cat.SaveChangesAsync(CancellationToken.None);
                    await Olvidar(cat, buscador, x);
                    return x;
                });
            }
            catch (Exception e3) { log.LogError(e3, "No se pudo quitar el modo mantenimiento de {Tenant}.", tenantId); }
        }
    }

    /// <summary>Borra una BD dedicada (eliminación definitiva del tenant o mudanza fallida).</summary>
    public static async Task BorrarBaseAsync(string cadena, CancellationToken ct)
    {
        var b = new SqlConnectionStringBuilder(cadena);
        var nombre = b.InitialCatalog;
        b.InitialCatalog = "master";
        await using var c = new SqlConnection(b.ConnectionString);
        await c.OpenAsync(ct);
        var q = $"IF DB_ID(@n) IS NOT NULL BEGIN ALTER DATABASE [{nombre.Replace("]", "]]")}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{nombre.Replace("]", "]]")}]; END";
        var cmd = new SqlCommand(q, c) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@n", nombre);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Tenant Copia(Tenant t, string? cadena) => new() { Id = t.Id, Slug = t.Slug, Nombre = t.Nombre, Estado = t.Estado, CadenaConexion = cadena };

    private async Task<T> EnCompartidaAsync<T>(Tenant t, Func<NexoDbContext, Task<T>> f)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(Copia(t, null));
        return await f(scope.ServiceProvider.GetRequiredService<NexoDbContext>());
    }

    private async Task<Tenant> Catalogo(Func<CatalogoDbContext, BuscadorTenants, Task<Tenant>> f)
    {
        using var scope = scopes.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<CatalogoDbContext>(), scope.ServiceProvider.GetRequiredService<BuscadorTenants>());
    }

    private static async Task Olvidar(CatalogoDbContext cat, BuscadorTenants buscador, Tenant t)
    {
        foreach (var h in await cat.Dominios.AsNoTracking().Where(d => d.TenantId == t.Id).Select(d => d.Host).ToListAsync()) buscador.Olvidar(h);
    }

    /// <summary>Tablas del modelo en orden de inserción (primero las referenciadas): globales (sin TenantId) y del tenant.</summary>
    private static (List<string> Globales, List<string> Propias) Tablas(IModel modelo)
    {
        var tipos = modelo.GetEntityTypes().Where(e => !e.IsOwned() && e.GetTableName() is not null && e.GetViewName() is null)
            .GroupBy(e => (e.GetSchema() ?? "dbo", e.GetTableName()!)).Select(g => g.First()).ToList();
        var pendientes = tipos.ToHashSet();
        var orden = new List<IEntityType>();
        while (pendientes.Count > 0)
        {
            // Se inserta una tabla cuando ya están todas las que referencia (las autorreferencias no cuentan).
            var libres = pendientes.Where(p => !p.GetForeignKeys().Any(fk => fk.PrincipalEntityType != p && pendientes.Contains(fk.PrincipalEntityType))).ToList();
            if (libres.Count == 0) libres = [pendientes.First()];
            foreach (var l in libres) { orden.Add(l); pendientes.Remove(l); }
        }
        static string N(IEntityType e) => $"[{e.GetSchema() ?? "dbo"}].[{e.GetTableName()}]";
        static bool Propia(IEntityType e) => e.FindProperty(nameof(IEntidadTenant.TenantId)) is not null;
        return (orden.Where(e => !Propia(e)).Select(N).ToList(), orden.Where(Propia).Select(N).ToList());
    }

    private static async Task<SqlConnection> AbrirAsync(string cadena, Guid tenantId, CancellationToken ct)
    {
        var c = new SqlConnection(cadena);
        await c.OpenAsync(ct);
        var cmd = new SqlCommand("EXEC sp_set_session_context @key = N'tid', @value = @t;", c);
        cmd.Parameters.AddWithValue("@t", tenantId);
        await cmd.ExecuteNonQueryAsync(ct);
        return c;
    }

    private static async Task<int> ContarAsync(SqlConnection c, string tabla, Guid? tenantId, CancellationToken ct)
    {
        var cmd = new SqlCommand($"SELECT COUNT(*) FROM {tabla}" + (tenantId is null ? "" : " WHERE [TenantId] = @t"), c) { CommandTimeout = 0 };
        if (tenantId is { } id) cmd.Parameters.AddWithValue("@t", id);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    /// <summary>Copia conservando los Id (sin columnas calculadas ni rowversion).</summary>
    private static async Task CopiarAsync(SqlConnection origen, SqlConnection destino, string? baseOrigen, string tabla, Guid? tenantId, CancellationToken ct)
    {
        var cols = new List<string>();
        var meta = new SqlCommand("SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@t) AND c.is_computed = 0 AND c.system_type_id <> 189 ORDER BY c.column_id", destino);
        meta.Parameters.AddWithValue("@t", tabla);
        await using (var r = await meta.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) cols.Add(r.GetString(0));
        var lista = string.Join(", ", cols.Select(x => $"[{x.Replace("]", "]]")}]"));
        if (baseOrigen is not null)
        {
            var identidad = new SqlCommand("SELECT OBJECTPROPERTY(OBJECT_ID(@t), 'TableHasIdentity')", destino);
            identidad.Parameters.AddWithValue("@t", tabla);
            var conIdentidad = (int?)await identidad.ExecuteScalarAsync(ct) == 1;
            var sql = (conIdentidad ? $"SET IDENTITY_INSERT {tabla} ON; " : "")
                + $"INSERT INTO {tabla} ({lista}) SELECT {lista} FROM [{baseOrigen.Replace("]", "]]")}].{tabla}" + (tenantId is null ? "" : " WHERE [TenantId] = @t") + ";"
                + (conIdentidad ? $" SET IDENTITY_INSERT {tabla} OFF;" : "");
            var cmd = new SqlCommand(sql, destino) { CommandTimeout = 0 };
            if (tenantId is { } tid) cmd.Parameters.AddWithValue("@t", tid);
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }
        var leer = new SqlCommand($"SELECT {lista} FROM {tabla}" + (tenantId is null ? "" : " WHERE [TenantId] = @t"), origen) { CommandTimeout = 0 };
        if (tenantId is { } id) leer.Parameters.AddWithValue("@t", id);
        await using var lector = await leer.ExecuteReaderAsync(ct);
        using var bulk = new SqlBulkCopy(destino, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls, null)
            { DestinationTableName = tabla, BulkCopyTimeout = 0, BatchSize = 2000, EnableStreaming = true };
        foreach (var x in cols) bulk.ColumnMappings.Add(x, x);
        await bulk.WriteToServerAsync(lector, ct);
    }
}
