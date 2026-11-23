using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;

namespace NexoRh.Api.Tests;

/// <summary>Ciclo de vida y uso (etapa 6.6): trial, morosidad, suspensión, cancelación, eliminación, uso, facturas y exportación.</summary>
[Collection("api")]
public class CicloVidaTests(ApiFixture api)
{
    /// <summary>Tenant propio de cada prueba, con los datos de ejemplo (mismos usuarios y contraseña que Comercial Andina).</summary>
    private async Task<Tenant> NuevoTenant()
    {
        var id = Guid.NewGuid();
        return await api.Services.GetRequiredService<AprovisionadorTenants>().AsegurarAsync(
            new Tenant { Id = id, Slug = $"cv{id:N}"[..14], Nombre = "Ciclo de vida", RazonSocial = "Ciclo de Vida S.A.C.", Ruc = "20555555555" }, datosDemo: true);
    }

    private HttpClient Cliente(Tenant t) => api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{t.Slug}.localhost") });

    private async Task<HttpResponseMessage> Login(Tenant t, string correo) =>
        await Cliente(t).PostAsJsonAsync("/api/auth/login", new { correo, password = api.PasswordSemilla });

    private async Task<HttpClient> Sesion(Tenant t, string correo)
    {
        var r = await Login(t, correo);
        r.EnsureSuccessStatusCode();
        var c = Cliente(t);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<Sesion>())!.Token);
        return c;
    }

    /// <summary>Cambia el catálogo y limpia las cachés de ese tenant.</summary>
    private async Task Catalogo(Tenant t, Func<CatalogoDbContext, Tenant, Task> cambio)
    {
        using var scope = api.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var cat = sp.GetRequiredService<CatalogoDbContext>();
        await cambio(cat, await cat.Tenants.FirstAsync(x => x.Id == t.Id));
        await cat.SaveChangesAsync();
        sp.GetRequiredService<BuscadorTenants>().Olvidar($"{t.Slug}.localhost");
        sp.GetRequiredService<Suscripciones>().Olvidar(t.Id);
    }

    private async Task<(ResumenCicloVida Resumen, Tenant Tenant)> Ejecutar(Tenant t, DateTime? ahora = null)
    {
        using var scope = api.Services.CreateScope();
        var r = await scope.ServiceProvider.GetRequiredService<CicloVidaTenants>().EjecutarAsync(ahora ?? DateTime.UtcNow, t.Id);
        var actual = await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Tenants.AsNoTracking().FirstAsync(x => x.Id == t.Id);
        scope.ServiceProvider.GetRequiredService<BuscadorTenants>().Olvidar($"{t.Slug}.localhost");
        return (r, actual);
    }

    [Fact]
    public async Task Trial_vencido_morosidad_y_reactivacion_al_pagar()
    {
        var t = await NuevoTenant();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await Catalogo(t, async (_, x) => { x.Estado = EstadosTenant.Trial; x.TrialHasta = hoy.AddDays(-1); await Task.CompletedTask; });
        var (r1, e1) = await Ejecutar(t);
        Assert.Equal(1, r1.TrialsVencidos);
        Assert.Equal(EstadosTenant.Suspendido, e1.Estado);
        Assert.Equal("Prueba gratuita vencida", e1.MotivoSuspension);

        // Factura vencida hace 5 días → moroso (con banner para los administradores).
        await Catalogo(t, async (cat, x) =>
        {
            x.Estado = EstadosTenant.Activo; x.TrialHasta = null; x.MotivoSuspension = null;
            foreach (var f in await cat.Facturas.Where(f => f.TenantId == t.Id).ToListAsync()) f.Estado = "Pagada";
            cat.Facturas.Add(new Factura { TenantId = t.Id, Numero = $"T-{Guid.NewGuid():N}"[..12], Periodo = "2026-01", Emitida = hoy.AddDays(-35), Vence = hoy.AddDays(-5), Colaboradores = 10, Subtotal = 100, Igv = 18 });
        });
        var (_, e2) = await Ejecutar(t);
        Assert.Equal(EstadosTenant.Moroso, e2.Estado);
        var admin = await Sesion(t, ApiFixture.Admin);
        Assert.Contains((await admin.GetFromJsonAsync<JsonElement>("/api/me/context")).GetProperty("banners").EnumerateArray(), b => b.GetProperty("tipo").GetString() == "moroso");

        // Vencida hace más de 30 días → suspendido por morosidad; al pagar vuelve a Activo.
        await Catalogo(t, async (cat, _) => { foreach (var f in await cat.Facturas.Where(f => f.TenantId == t.Id && f.Estado == "Vencida").ToListAsync()) f.Vence = hoy.AddDays(-40); });
        var (_, e3) = await Ejecutar(t);
        Assert.Equal(EstadosTenant.Suspendido, e3.Estado);
        Assert.StartsWith("Morosidad", e3.MotivoSuspension);
        await Catalogo(t, async (cat, _) => { foreach (var f in await cat.Facturas.Where(f => f.TenantId == t.Id).ToListAsync()) f.Estado = "Pagada"; });
        var (r4, e4) = await Ejecutar(t);
        Assert.Equal(EstadosTenant.Activo, e4.Estado);
        Assert.Equal(1, r4.Reactivados);
    }

    [Fact]
    public async Task Uso_del_mes_y_factura_del_mes_anterior()
    {
        var t = await NuevoTenant();
        var proximoMes = DateTime.UtcNow.AddMonths(1);
        var (r, _) = await Ejecutar(t, proximoMes);
        Assert.Equal(1, r.Usos);
        Assert.Equal(1, r.Facturas);
        using var scope = api.Services.CreateScope();
        var cat = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var uso = await cat.Usos.AsNoTracking().FirstAsync(u => u.TenantId == t.Id && u.Periodo == CicloVidaTenants.Periodo(DateOnly.FromDateTime(proximoMes)));
        Assert.True(uso.Colaboradores > 0);
        var actual = CicloVidaTenants.Periodo(DateOnly.FromDateTime(DateTime.UtcNow));
        var factura = await cat.Facturas.AsNoTracking().FirstAsync(f => f.TenantId == t.Id && f.Periodo == actual);
        Assert.StartsWith("F001-", factura.Numero);
        Assert.Equal(Math.Round(factura.Subtotal * 0.18m, 2), factura.Igv);
        // Correr de nuevo no duplica la factura.
        var (r2, _) = await Ejecutar(t, proximoMes);
        Assert.Equal(0, r2.Facturas);
    }

    [Fact]
    public async Task Cuenta_suspendida_solo_administradores_y_exportacion()
    {
        var t = await NuevoTenant();
        await Catalogo(t, async (_, x) => { x.Estado = EstadosTenant.Suspendido; x.MotivoSuspension = "Prueba"; await Task.CompletedTask; });
        Assert.Equal(HttpStatusCode.Locked, (await Login(t, ApiFixture.Colaborador)).StatusCode);
        var admin = await Sesion(t, ApiFixture.Admin);
        Assert.Equal(HttpStatusCode.Locked, (await admin.GetAsync("/api/colaboradores")).StatusCode);
        Assert.Equal("Suspendido", (await admin.GetFromJsonAsync<JsonElement>("/api/me/context")).GetProperty("tenant").GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/cuenta")).StatusCode);

        var r = await admin.GetAsync("/api/exportacion");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/zip", r.Content.Headers.ContentType!.MediaType);
        using var zip = new ZipArchive(await r.Content.ReadAsStreamAsync());
        var nombres = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("LEEME.txt", nombres);
        Assert.Contains("Colaboradores.csv", nombres);
        Assert.DoesNotContain("TokensUsuario.csv", nombres);
        using var lector = new StreamReader(zip.GetEntry("Usuarios.csv")!.Open());
        var usuarios = await lector.ReadToEndAsync();
        Assert.DoesNotContain("PasswordHash", usuarios);
        Assert.DoesNotContain("TotpSecreto", usuarios);
        Assert.Contains(ApiFixture.Admin, usuarios);
        // Un colaborador no exporta.
        await Catalogo(t, async (_, x) => { x.Estado = EstadosTenant.Activo; await Task.CompletedTask; });
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Sesion(t, ApiFixture.Colaborador)).GetAsync("/api/exportacion")).StatusCode);
    }

    [Fact]
    public async Task Cancelada_solo_lectura_y_eliminacion_con_certificado()
    {
        var t = await NuevoTenant();
        await Catalogo(t, async (_, x) => { x.Estado = EstadosTenant.Cancelado; x.CanceladoEn = DateTime.UtcNow; await Task.CompletedTask; });
        var admin = await Sesion(t, ApiFixture.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/colaboradores")).StatusCode);
        var escribir = await admin.PostAsJsonAsync("/api/reportes", new { plantilla = "Maestro de empleados", nombre = "No debe guardarse" });
        Assert.Equal(HttpStatusCode.Locked, escribir.StatusCode);
        Assert.Equal(HttpStatusCode.Locked, (await Login(t, ApiFixture.Colaborador)).StatusCode);

        // Antes de los 30 días no se elimina; después, sí.
        var (r1, e1) = await Ejecutar(t, DateTime.UtcNow.AddDays(10));
        Assert.Equal(EstadosTenant.Cancelado, e1.Estado);
        var (r2, e2) = await Ejecutar(t, DateTime.UtcNow.AddDays(31));
        Assert.Equal(1, r2.Eliminados);
        Assert.Equal(EstadosTenant.Eliminado, e2.Estado);
        var cert = JsonDocument.Parse(e2.CertificadoEliminacion!).RootElement;
        Assert.True(cert.GetProperty("filasNegocio").GetInt32() > 0);
        Assert.True(cert.GetProperty("porTabla").GetProperty("Colaboradores").GetInt32() > 0);

        using var scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(e2);
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
        Assert.Equal(0, await db.Colaboradores.CountAsync());
        Assert.Equal(0, await db.Usuarios.CountAsync());
        var cat = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        Assert.True(await cat.Facturas.AnyAsync(f => f.TenantId == t.Id));          // se conservan (registro contable)
        Assert.False(await cat.TenantModulos.AnyAsync(m => m.TenantId == t.Id));
        Assert.Equal(HttpStatusCode.Locked, (await Login(t, ApiFixture.Admin)).StatusCode);
        // Los demás tenants no se tocan.
        Assert.True((await (await api.ClienteDe(ApiFixture.Admin)).GetFromJsonAsync<JsonElement>("/api/colaboradores")).GetArrayLength() > 0);
    }
}
