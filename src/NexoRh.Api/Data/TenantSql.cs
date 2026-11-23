using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Data;

public partial class NexoDbContext
{
    /// <summary>
    /// Reglas multi-tenant para TODAS las entidades (se aplica al final de OnModelCreating):
    /// 1. Las claves que no son un int identity (códigos ACT-0042, períodos 202603, ids manuales…) pasan a (TenantId, …):
    ///    cada tenant numera lo suyo y los datos de ejemplo pueden repetirse en varios tenants.
    /// 2. Las relaciones hacia esas claves incluyen TenantId: una fila nunca apunta a otra de otro tenant.
    /// 3. Los índices únicos (correo, documento, nombre…) son únicos por tenant.
    /// 4. Filtro global TenantId = tenant del request y valor por defecto al insertar.
    /// </summary>
    private void AplicarTenant(ModelBuilder b)
    {
        var tipos = b.Model.GetEntityTypes().Where(t => !t.IsOwned()).ToList();
        var sinTenant = tipos.Where(t => !typeof(IEntidadTenant).IsAssignableFrom(t.ClrType)).Select(t => t.ClrType.Name).ToList();
        if (sinTenant.Count > 0)
            throw new InvalidOperationException($"Toda entidad de negocio debe implementar IEntidadTenant: {string.Join(", ", sinTenant)}.");

        static bool ClaveIdentidad(IMutableEntityType t) => t.FindPrimaryKey() is { Properties: [var p] } && (p.ClrType == typeof(int) || p.ClrType == typeof(long)) && p.ValueGenerated == ValueGenerated.OnAdd;
        var compuestas = tipos.Where(t => !ClaveIdentidad(t)).ToHashSet();
        foreach (var t in tipos)
            if (t.GetDeclaredReferencingForeignKeys().Any(fk => fk.DeclaringEntityType.IsOwned()) && compuestas.Contains(t))
                throw new InvalidOperationException($"{t.ClrType.Name} tiene tipos propios y una clave natural: no soportado.");

        // Relaciones hacia claves que cambian: se quitan y se vuelven a crear con TenantId.
        var relaciones = new List<(IMutableEntityType Dep, IReadOnlyList<IMutableProperty> Props, IMutableEntityType Principal, DeleteBehavior Borrado, bool Unica, bool Requerida)>();
        foreach (var t in tipos)
            foreach (var fk in t.GetForeignKeys().Where(f => compuestas.Contains(f.PrincipalEntityType)).ToList())
            {
                relaciones.Add((t, fk.Properties.ToList(), fk.PrincipalEntityType, fk.DeleteBehavior, fk.IsUnique, fk.IsRequired));
                t.RemoveForeignKey(fk);
            }

        foreach (var t in tipos)
        {
            var tid = t.FindProperty(nameof(IEntidadTenant.TenantId))!;
            tid.ValueGenerated = ValueGenerated.OnAdd;
            tid.SetValueGeneratorFactory((_, _) => new GeneradorTenant());

            if (compuestas.Contains(t))
            {
                var pk = t.FindPrimaryKey()!;
                var anteriores = pk.Properties.ToList();
                t.SetPrimaryKey([tid, .. anteriores]);
                // SetPrimaryKey deja la clave anterior como clave alternativa (única en toda la tabla): se quita.
                if (t.FindKey(anteriores) is { } vieja && !vieja.IsPrimaryKey()) t.RemoveKey(vieja);
            }

            // Filtro global: solo filas del tenant del request (o todas en modo plataforma, a propósito).
            var e = Expression.Parameter(t.ClrType, "e");
            var ctx = Expression.Constant(this);
            var filtro = Expression.OrElse(
                Expression.Property(ctx, nameof(ModoPlataforma)),
                Expression.Equal(Expression.Property(e, nameof(IEntidadTenant.TenantId)), Expression.Property(ctx, nameof(TenantActual))));
            t.SetQueryFilter(Expression.Lambda(filtro, e));
        }

        foreach (var (dep, props, principal, borrado, unica, requerida) in relaciones)
        {
            var fk = dep.AddForeignKey([dep.FindProperty(nameof(IEntidadTenant.TenantId))!, .. props], principal.FindPrimaryKey()!, principal);
            // SET NULL pondría también TenantId en NULL: se reemplaza por NO ACTION (el código limpia la referencia).
            fk.DeleteBehavior = borrado is DeleteBehavior.SetNull or DeleteBehavior.ClientSetNull ? DeleteBehavior.NoAction : borrado;
            fk.IsUnique = unica;
            fk.IsRequired = requerida;
        }

        foreach (var t in tipos)
        {
            var tid = t.FindProperty(nameof(IEntidadTenant.TenantId))!;
            foreach (var idx in t.GetIndexes().Where(i => i.IsUnique && !i.Properties.Contains(tid)).ToList())
            {
                var filtroSql = idx.GetFilter();
                IReadOnlyList<IMutableProperty> props = [tid, .. idx.Properties];
                t.RemoveIndex(idx);
                var nuevo = t.FindIndex(props) ?? t.AddIndex(props);
                nuevo.IsUnique = true;
                if (filtroSql is not null) nuevo.SetFilter(filtroSql);
            }
            // Las consultas de negocio siempre filtran por TenantId: que haya un índice que empiece por él.
            if (t.FindPrimaryKey()!.Properties[0] != tid && !t.GetIndexes().Any(i => i.Properties[0] == tid)) t.AddIndex(tid);
        }
    }

    public override int SaveChanges(bool aceptarTodos)
    {
        ValidarTenant();
        return base.SaveChanges(aceptarTodos);
    }

    public override Task<int> SaveChangesAsync(bool aceptarTodos, CancellationToken ct = default)
    {
        ValidarTenant();
        return base.SaveChangesAsync(aceptarTodos, ct);
    }

    /// <summary>Ninguna escritura sin tenant, en otro tenant o cambiando el TenantId de una fila.</summary>
    private void ValidarTenant()
    {
        foreach (var e in ChangeTracker.Entries<IEntidadTenant>().ToList())
        {
            if (e.State == EntityState.Added)
            {
                // EF no genera valores para columnas que también son parte de una relación (TenantId en una clave
                // compuesta con FK): se asigna aquí, antes de guardar.
                if (e.Entity.TenantId == Guid.Empty && TenantActual != Guid.Empty) e.Property(x => x.TenantId).CurrentValue = TenantActual;
                if (e.Entity.TenantId == Guid.Empty) throw new InvalidOperationException($"No hay tenant para la fila nueva ({e.Entity.GetType().Name}).");
                if (!ModoPlataforma && e.Entity.TenantId != TenantActual) throw new InvalidOperationException("La fila pertenece a otro tenant.");
            }
            else if (e.State is EntityState.Modified or EntityState.Deleted)
            {
                if (e.Property(x => x.TenantId).IsModified) throw new InvalidOperationException("El TenantId de una fila no se puede cambiar.");
                if (!ModoPlataforma && e.Entity.TenantId != TenantActual) throw new InvalidOperationException("La fila pertenece a otro tenant.");
            }
        }
    }
}

/// <summary>Asigna el tenant del request a las filas nuevas (también cuando TenantId es parte de la clave).</summary>
public class GeneradorTenant : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;

    public override Guid Next(EntityEntry entry)
    {
        var id = ((NexoDbContext)entry.Context).TenantActual;
        return id != Guid.Empty ? id : throw new InvalidOperationException("No hay tenant para la fila nueva.");
    }
}

/// <summary>
/// Segunda barrera (Row-Level Security): al abrir cada conexión fija SESSION_CONTEXT 'tid' (y 'plataforma' solo en
/// modo plataforma). La política rls.PoliticaTenant de SQL Server filtra y bloquea filas de otros tenants aunque
/// alguien escriba SQL a mano.
/// </summary>
public class SesionTenantInterceptor : DbConnectionInterceptor
{
    public static readonly SesionTenantInterceptor Instancia = new();

    public override void ConnectionOpened(DbConnection conexion, ConnectionEndEventData datos)
    {
        using var cmd = Comando(conexion, datos.Context);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection conexion, ConnectionEndEventData datos, CancellationToken ct = default)
    {
        await using var cmd = Comando(conexion, datos.Context);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static DbCommand Comando(DbConnection conexion, DbContext? contexto)
    {
        var tenant = (contexto as NexoDbContext)?.Tenant;
        var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key = N'tid', @value = @tid; EXEC sp_set_session_context @key = N'plataforma', @value = @plataforma;";
        var tid = cmd.CreateParameter();
        tid.ParameterName = "@tid";
        tid.Value = tenant?.Id is { } id ? id : DBNull.Value;
        var plataforma = cmd.CreateParameter();
        plataforma.ParameterName = "@plataforma";
        plataforma.Value = tenant?.ModoPlataforma == true ? 1 : DBNull.Value;
        cmd.Parameters.Add(tid);
        cmd.Parameters.Add(plataforma);
        return cmd;
    }
}

public static class ExtensionesTenant
{
    /// <summary>
    /// Busca por clave sin el TenantId (que se completa con el del request) y respetando el filtro del tenant.
    /// Reemplaza a FindAsync, que con claves compuestas pediría también el TenantId.
    /// </summary>
    public static async ValueTask<T?> Buscar<T>(this DbSet<T> set, params object?[] clave) where T : class
    {
        var ctx = (NexoDbContext)set.GetService<ICurrentDbContext>().Context;
        var pk = ctx.Model.FindEntityType(typeof(T))!.FindPrimaryKey()!;
        var props = pk.Properties.Where(p => p.Name != nameof(IEntidadTenant.TenantId)).ToList();
        if (props.Count != clave.Length) throw new ArgumentException($"{typeof(T).Name} se busca con {props.Count} valor(es) de clave.");
        if (clave.Any(c => c is null)) return null;
        var completa = pk.Properties.Count == props.Count ? clave : [ctx.TenantActual, .. clave];
        var encontrado = await set.FindAsync(completa);
        // FindAsync puede devolver una fila ya cargada sin pasar por el filtro global: se verifica el tenant.
        return encontrado is IEntidadTenant e && e.TenantId != ctx.TenantActual && !ctx.ModoPlataforma ? null : encontrado;
    }
}
