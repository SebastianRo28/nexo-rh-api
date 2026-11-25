using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Catalogo;

public record ResumenCicloVida(DateTime En, int TrialsVencidos, int Morosos, int Suspendidos, int Reactivados, int Eliminados, int Usos, int Facturas, List<string> Eventos);
public record CertificadoEliminacion(Guid TenantId, string Slug, string RazonSocial, string Ruc, DateTime EliminadoEn, string Responsable, int FilasNegocio, Dictionary<string, int> PorTabla, int ArchivosCatalogo, string Nota,
    string? BaseDedicadaEliminada = null);

/// <summary>
/// Ciclo de vida del tenant (sección 8): Trial → Activo → Moroso → Suspendido → Cancelado → Eliminado. Corre cada hora
/// (servicio en segundo plano) o desde la consola, para todos o para un tenant:
/// · trial vencido → Suspendido; factura vencida → Moroso; vencida hace más de 30 días → Suspendido; al día → Activo;
/// · cancelado hace más de 30 días → se eliminan sus datos (con certificado);
/// · uso del mes (colaboradores, usuarios, almacenamiento) y factura del mes anterior.
/// </summary>
public class CicloVidaTenants(CatalogoDbContext catalogo, ConsultasPlataforma consultas, CadenasConexion cadenas, CobrosFacturas cobros, ILogger<CicloVidaTenants> log)
{
    public const int DiasMorosidadSuspension = 30;
    public const int DiasCancelacion = 30;
    private static readonly string[] Operativos = [EstadosTenant.Trial, EstadosTenant.Activo, EstadosTenant.Moroso, EstadosTenant.Suspendido, EstadosTenant.Cancelado];

    public async Task<ResumenCicloVida> EjecutarAsync(DateTime ahora, Guid? soloTenant = null, string actor = "Ciclo de vida", CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(ahora);
        var eventos = new List<string>();
        int trials = 0, morosos = 0, suspendidos = 0, reactivados = 0, eliminados = 0, usos = 0, facturas = 0;
        var tenants = await catalogo.Tenants.Where(t => Operativos.Contains(t.Estado) && (soloTenant == null || t.Id == soloTenant)).ToListAsync(ct);
        void Evento(Tenant t, string accion, string detalle)
        {
            eventos.Add($"{t.Slug}: {detalle}");
            catalogo.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = actor, Accion = accion, TenantId = t.Id, Detalle = detalle, En = ahora });
        }

        // Cobro automático de las facturas impagas (con tarjeta registrada), antes de evaluar la morosidad.
        if (await cobros.EjecutarAsync(ahora, soloTenant, ct) is > 0 and var cobradas) eventos.Add($"{cobradas} cobro(s) automático(s) con tarjeta");

        // Facturas vencidas.
        var ids = tenants.Select(t => t.Id).ToList();
        foreach (var f in await catalogo.Facturas.Where(f => ids.Contains(f.TenantId) && f.Estado == "Pendiente" && f.Vence < hoy).ToListAsync(ct)) f.Estado = "Vencida";
        await catalogo.SaveChangesAsync(ct);
        var vencidas = await catalogo.Facturas.AsNoTracking().Where(f => ids.Contains(f.TenantId) && f.Estado == "Vencida").ToListAsync(ct);

        foreach (var t in tenants)
        {
            var suyas = vencidas.Where(f => f.TenantId == t.Id).ToList();
            var masAntigua = suyas.Count == 0 ? (int?)null : hoy.DayNumber - suyas.Min(f => f.Vence).DayNumber;
            switch (t.Estado)
            {
                case EstadosTenant.Trial when t.TrialHasta is { } th && th < hoy:
                    t.Estado = EstadosTenant.Suspendido;
                    t.MotivoSuspension = "Prueba gratuita vencida";
                    trials++;
                    Evento(t, "tenant.trial_expired", $"Prueba gratuita vencida el {th:dd/MM/yyyy}: suspendido hasta contratar");
                    break;
                case EstadosTenant.Activo when masAntigua is int d1:
                    t.Estado = d1 > DiasMorosidadSuspension ? EstadosTenant.Suspendido : EstadosTenant.Moroso;
                    if (t.Estado == EstadosTenant.Suspendido) { t.MotivoSuspension = "Morosidad (factura vencida > 30 días)"; suspendidos++; } else morosos++;
                    Evento(t, t.Estado == EstadosTenant.Moroso ? "tenant.overdue" : "tenant.suspended", $"{suyas.Count} factura(s) vencida(s), la más antigua hace {d1} días");
                    break;
                case EstadosTenant.Moroso when masAntigua is int d2 && d2 > DiasMorosidadSuspension:
                    t.Estado = EstadosTenant.Suspendido;
                    t.MotivoSuspension = "Morosidad (factura vencida > 30 días)";
                    suspendidos++;
                    Evento(t, "tenant.suspended", $"Factura vencida hace {d2} días");
                    break;
                case EstadosTenant.Moroso when masAntigua is null:
                case EstadosTenant.Suspendido when masAntigua is null && t.MotivoSuspension?.StartsWith("Morosidad") == true:
                    t.Estado = EstadosTenant.Activo;
                    t.MotivoSuspension = null;
                    reactivados++;
                    Evento(t, "tenant.reactivated", "Facturas al día");
                    break;
                case EstadosTenant.Cancelado when t.CanceladoEn is { } c && c.AddDays(DiasCancelacion) <= ahora:
                    await catalogo.SaveChangesAsync(ct);
                    await EliminarAsync(t, actor, ahora, ct);
                    eliminados++;
                    eventos.Add($"{t.Slug}: datos eliminados ({DiasCancelacion} días después de la cancelación)");
                    break;
            }
        }
        await catalogo.SaveChangesAsync(ct);

        // Uso del mes y factura del mes anterior (no para trials, suspendidos por trial ni cancelados).
        var periodo = Periodo(hoy);
        var anterior = Periodo(hoy.AddMonths(-1));
        var vivos = tenants.Where(t => t.Estado is not (EstadosTenant.Eliminado or EstadosTenant.Cancelado)).ToList();
        var precios = await consultas.PreciosAsync(vivos, ct);
        var numero = await SiguienteNumero(ct);
        foreach (var t in vivos)
        {
            var uso = await MedirAsync(t, periodo, ahora, ct);
            usos++;
            var facturable = t.Estado is EstadosTenant.Activo or EstadosTenant.Moroso || (t.Estado == EstadosTenant.Suspendido && t.MotivoSuspension?.StartsWith("Morosidad") == true);
            if (!facturable || t.CreadoEn >= new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc)) continue;
            if (await catalogo.Facturas.AnyAsync(f => f.TenantId == t.Id && f.Periodo == anterior, ct)) continue;
            var usoAnterior = await catalogo.Usos.AsNoTracking().FirstOrDefaultAsync(u => u.TenantId == t.Id && u.Periodo == anterior, ct);
            var colaboradores = usoAnterior?.Colaboradores ?? uso.Colaboradores;
            var subtotal = Math.Round(colaboradores * precios[t.Id], 2);
            var emitida = new DateOnly(hoy.Year, hoy.Month, 1);
            catalogo.Facturas.Add(new Factura { TenantId = t.Id, Numero = $"F001-{numero++:D6}", Periodo = anterior, Emitida = emitida, Vence = emitida.AddMonths(1).AddDays(-1),
                Colaboradores = colaboradores, Subtotal = subtotal, Igv = Math.Round(subtotal * Modulos.Igv, 2) });
            facturas++;
            Evento(t, "invoice.issued", $"Factura {anterior}: {colaboradores} colaboradores · S/ {subtotal:0.00} + IGV");
        }
        await catalogo.SaveChangesAsync(ct);
        // Primer cobro de las facturas recién emitidas.
        if (facturas > 0 && await cobros.EjecutarAsync(ahora, soloTenant, ct) is > 0 and var nuevas) eventos.Add($"{nuevas} cobro(s) de facturas nuevas");
        if (eventos.Count > 0) log.LogInformation("Ciclo de vida: {Eventos}", string.Join(" | ", eventos));
        return new ResumenCicloVida(ahora, trials, morosos, suspendidos, reactivados, eliminados, usos, facturas, eventos);
    }

    public static string Periodo(DateOnly d) => $"{d.Year:D4}-{d.Month:D2}";

    private async Task<int> SiguienteNumero(CancellationToken ct)
    {
        // Correlativo de la serie F001 (las facturas de ejemplo usan otra serie).
        var numeros = await catalogo.Facturas.AsNoTracking().Where(f => f.Numero.StartsWith("F001-")).Select(f => f.Numero).ToListAsync(ct);
        return numeros.Select(n => int.TryParse(n.Split('-').Last(), out var x) ? x : 0).DefaultIfEmpty(0).Max() + 1;
    }

    /// <summary>Uso del período (se actualiza en cada corrida del mes).</summary>
    public async Task<UsoMensual> MedirAsync(Tenant t, string periodo, DateTime ahora, CancellationToken ct = default)
    {
        var inicio = new DateTime(ahora.Year, ahora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var (colab, usuarios, mb) = await consultas.EnTenantAsync(t, async sp =>
        {
            var db = sp.GetRequiredService<NexoDbContext>();
            var gb = (await sp.GetRequiredService<LimitesPlan>().UsoAsync()).First(l => l.Clave == LimitesPlan.Almacenamiento).Usado;
            return (await db.Colaboradores.CountAsync(ct), await db.Usuarios.CountAsync(u => u.Activo && u.UltimoAcceso >= inicio, ct), (decimal)Math.Round(gb * 1024, 2));
        });
        var u = await catalogo.Usos.FirstOrDefaultAsync(x => x.TenantId == t.Id && x.Periodo == periodo, ct);
        if (u is null) catalogo.Usos.Add(u = new UsoMensual { TenantId = t.Id, Periodo = periodo });
        (u.Colaboradores, u.UsuariosActivos, u.AlmacenamientoMb, u.CalculadoEn) = (colab, usuarios, mb, ahora);
        await catalogo.SaveChangesAsync(ct);
        return u;
    }

    /// <summary>
    /// Eliminación definitiva: borra todas las filas del tenant en su BD de negocio (en orden de dependencias) y sus datos
    /// del catálogo (marca, módulos, notas, correos…). Conserva la fila del tenant como Eliminado, sus facturas (registro
    /// contable) y la auditoría de la plataforma, y emite un certificado de eliminación.
    /// </summary>
    public async Task<CertificadoEliminacion> EliminarAsync(Tenant t, string responsable, DateTime ahora, CancellationToken ct = default)
    {
        var porTabla = await consultas.EnTenantAsync(t, sp => BorrarNegocioAsync(sp.GetRequiredService<NexoDbContext>(), t.Id, ct));
        // BD dedicada: además de vaciarla, se borra la base (sus copias de seguridad expiran con la retención).
        string? baseDedicada = null;
        if (cadenas.Descifrar(t.CadenaConexion) is { } dedicada)
        {
            await MudanzaBaseDedicada.BorrarBaseAsync(dedicada, ct);
            baseDedicada = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(dedicada).InitialCatalog;
            t.CadenaConexion = null;
        }
        catalogo.CorreosDominio.RemoveRange(catalogo.CorreosDominio.Where(a => a.TenantId == t.Id));
        var archivos = await catalogo.ArchivosMarca.CountAsync(a => a.TenantId == t.Id, ct);
        catalogo.ArchivosMarca.RemoveRange(catalogo.ArchivosMarca.Where(a => a.TenantId == t.Id));
        catalogo.Marcas.RemoveRange(catalogo.Marcas.Where(a => a.TenantId == t.Id));
        catalogo.Ajustes.RemoveRange(catalogo.Ajustes.Where(a => a.TenantId == t.Id));
        catalogo.TenantModulos.RemoveRange(catalogo.TenantModulos.Where(a => a.TenantId == t.Id));
        catalogo.Flags.RemoveRange(catalogo.Flags.Where(a => a.TenantId == t.Id));
        catalogo.Notas.RemoveRange(catalogo.Notas.Where(a => a.TenantId == t.Id));
        catalogo.Solicitudes.RemoveRange(catalogo.Solicitudes.Where(a => a.TenantId == t.Id));
        catalogo.SesionesSoporte.RemoveRange(catalogo.SesionesSoporte.Where(a => a.TenantId == t.Id));
        catalogo.Usos.RemoveRange(catalogo.Usos.Where(a => a.TenantId == t.Id));
        // Tarjetas registradas (los cobros se conservan con las facturas).
        catalogo.MediosPago.RemoveRange(catalogo.MediosPago.Where(a => a.TenantId == t.Id));
        catalogo.Correos.RemoveRange(catalogo.Correos.Where(a => a.TenantId == t.Id));
        catalogo.Dominios.RemoveRange(catalogo.Dominios.Where(d => d.TenantId == t.Id && d.Tipo == "Personalizado"));
        var cert = new CertificadoEliminacion(t.Id, t.Slug, t.RazonSocial, t.Ruc, ahora, responsable, porTabla.Values.Sum(), porTabla, archivos,
            (baseDedicada is null ? "Se eliminaron definitivamente los datos del cliente en la base de negocio y en el catálogo." : $"Se eliminaron definitivamente los datos del cliente, su base de datos dedicada ({baseDedicada}) y sus datos del catálogo.")
            + " Se conservan las facturas (obligación tributaria) y la auditoría de la plataforma. Las copias de seguridad expiran según la política de retención (30 días).", baseDedicada);
        t.Estado = EstadosTenant.Eliminado;
        t.EliminadoEn = ahora;
        t.CertificadoEliminacion = JsonSerializer.Serialize(cert, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        catalogo.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = responsable, Accion = "tenant.deleted", TenantId = t.Id, En = ahora,
            Detalle = $"{cert.FilasNegocio} filas de negocio y {archivos} archivos de marca eliminados" });
        await catalogo.SaveChangesAsync(ct);
        return cert;
    }

    /// <summary>DELETE por tabla en orden de dependencias (primero las que referencian a otras); las autorreferencias se anulan antes.</summary>
    public static async Task<Dictionary<string, int>> BorrarNegocioAsync(NexoDbContext db, Guid tenantId, CancellationToken ct)
    {
        var tipos = db.Model.GetEntityTypes().Where(e => !e.IsOwned() && e.GetTableName() is not null && e.FindProperty(nameof(IEntidadTenant.TenantId)) is not null).ToList();
        var pendientes = tipos.ToHashSet();
        var orden = new List<IEntityType>();
        while (pendientes.Count > 0)
        {
            // Una tabla se puede borrar cuando ninguna otra pendiente la referencia.
            var libres = pendientes.Where(p => !pendientes.Any(o => o != p && o.GetForeignKeys().Any(fk => fk.PrincipalEntityType == p))).ToList();
            if (libres.Count == 0) libres = [pendientes.First()];
            foreach (var l in libres) { orden.Add(l); pendientes.Remove(l); }
        }
        var r = new Dictionary<string, int>();
        foreach (var e in orden)
        {
            var tabla = $"[{e.GetSchema() ?? "dbo"}].[{e.GetTableName()}]";
            foreach (var fk in e.GetForeignKeys().Where(fk => fk.PrincipalEntityType == e))
            {
                var cols = fk.Properties.Where(p => p.IsNullable).Select(p => $"[{p.GetColumnName()}] = NULL").ToList();
                if (cols.Count > 0)
#pragma warning disable EF1002 // nombres de tabla y columna salen del modelo, no del usuario
                    await db.Database.ExecuteSqlRawAsync($"UPDATE {tabla} SET {string.Join(", ", cols)} WHERE [TenantId] = @t", [new Microsoft.Data.SqlClient.SqlParameter("@t", tenantId)], ct);
            }
            var n = await db.Database.ExecuteSqlRawAsync($"DELETE FROM {tabla} WHERE [TenantId] = @t", [new Microsoft.Data.SqlClient.SqlParameter("@t", tenantId)], ct);
#pragma warning restore EF1002
            if (n > 0) r[e.GetTableName()!] = r.GetValueOrDefault(e.GetTableName()!) + n;
        }
        return r;
    }
}

/// <summary>Corre el ciclo de vida cada hora (CicloVida:Habilitado; apagado por defecto en desarrollo y pruebas).</summary>
public class CicloVidaEnSegundoPlano(IServiceScopeFactory scopes, IConfiguration config, IHostEnvironment entorno, ILogger<CicloVidaEnSegundoPlano> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var habilitado = config.GetValue("CicloVida:Habilitado", !entorno.IsDevelopment() && !entorno.IsEnvironment("Pruebas"));
        if (!habilitado) { log.LogInformation("Ciclo de vida automático apagado (CicloVida:Habilitado)."); return; }
        var intervalo = TimeSpan.FromMinutes(Math.Max(5, config.GetValue("CicloVida:Minutos", 60)));
        using var reloj = new PeriodicTimer(intervalo);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CicloVidaTenants>().EjecutarAsync(DateTime.UtcNow, ct: ct);
            }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "Falló el ciclo de vida de los tenants."); }
        } while (await reloj.WaitForNextTickAsync(ct));
    }
}
