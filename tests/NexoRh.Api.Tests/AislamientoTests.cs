using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Tests;

/// <summary>
/// Aislamiento entre tenants (regla de oro de la especificación multiempresa). Un segundo tenant recibe los mismos
/// datos de ejemplo que Comercial Andina: mismos ids, mismos correos y mismos documentos. Nada debe cruzar.
/// </summary>
[Collection("api")]
public class AislamientoTests(ApiFixture api)
{
    private const string SlugB = "andesur";
    private static readonly Guid TenantB = Guid.Parse("2b9d4c71-6a3e-4f8d-9c15-7e0a1d2f3b58");
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static bool creado;

    private async Task AsegurarTenantB()
    {
        await Candado.WaitAsync();
        try
        {
            if (creado) return;
            await api.Services.GetRequiredService<AprovisionadorTenants>().AsegurarAsync(
                new Tenant { Id = TenantB, Slug = SlugB, Nombre = "Andesur", RazonSocial = "Andesur Distribuciones S.A.C.", Ruc = "20512345678" }, datosDemo: true);
            creado = true;
        }
        finally { Candado.Release(); }
    }

    private HttpClient Cliente(string host) => api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}") });

    private async Task<(HttpClient Cliente, Sesion Sesion)> Login(string host, string correo)
    {
        var c = Cliente(host);
        var r = await c.PostAsJsonAsync("/api/auth/login", new { correo, password = api.PasswordSemilla });
        r.EnsureSuccessStatusCode();
        var s = (await r.Content.ReadFromJsonAsync<Sesion>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        return (c, s);
    }

    private T ConTenant<T>(Guid tenant, Func<NexoDbContext, T> f)
    {
        using var scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(new Tenant { Id = tenant, Slug = "x" });
        return f(scope.ServiceProvider.GetRequiredService<NexoDbContext>());
    }

    [Fact]
    public async Task El_mismo_usuario_en_dos_tenants_es_otra_cuenta()
    {
        await AsegurarTenantB();
        var (_, a) = await Login("localhost", ApiFixture.Admin);
        var (_, b) = await Login($"{SlugB}.localhost", ApiFixture.Admin);
        Assert.NotEqual(a.Usuario.Id, b.Usuario.Id);
        string Tid(Sesion s) => new JwtSecurityTokenHandler().ReadJwtToken(s.Token).Claims.First(c => c.Type == "tid").Value;
        Assert.Equal(AprovisionadorTenants.ComercialAndina.ToString(), Tid(a));
        Assert.Equal(TenantB.ToString(), Tid(b));
    }

    [Fact]
    public async Task Un_token_no_sirve_en_el_enlace_de_otra_empresa()
    {
        await AsegurarTenantB();
        var (_, a) = await Login("localhost", ApiFixture.Admin);
        var otro = Cliente($"{SlugB}.localhost");
        otro.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otro.GetAsync("/api/colaboradores")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otro.GetAsync("/api/reportes")).StatusCode);
    }

    [Fact]
    public async Task Lo_que_cambia_un_tenant_no_lo_ve_el_otro()
    {
        await AsegurarTenantB();
        var (ca, _) = await Login("localhost", ApiFixture.Admin);
        var (cb, _) = await Login($"{SlugB}.localhost", ApiFixture.Admin);

        // Mismos datos de ejemplo en los dos (los ids se repiten sin chocar)…
        var colA = await ca.GetFromJsonAsync<JsonElement>("/api/colaboradores");
        var colB = await cb.GetFromJsonAsync<JsonElement>("/api/colaboradores");
        Assert.Equal(colA.GetArrayLength(), colB.GetArrayLength());

        // …pero cada escritura queda en su tenant.
        (await cb.PostAsJsonAsync("/api/reportes", new { plantilla = "Maestro de empleados", nombre = "Solo de Andesur" })).EnsureSuccessStatusCode();
        Assert.Contains((await cb.GetFromJsonAsync<JsonElement>("/api/reportes")).GetProperty("reportes").EnumerateArray(), r => r.GetProperty("nombre").GetString() == "Solo de Andesur");
        Assert.DoesNotContain((await ca.GetFromJsonAsync<JsonElement>("/api/reportes")).GetProperty("reportes").EnumerateArray(), r => r.GetProperty("nombre").GetString() == "Solo de Andesur");

        var bandasB = await cb.GetFromJsonAsync<JsonElement>("/api/compensaciones");
        var contadora = bandasB.GetProperty("bandas").EnumerateArray().First(x => x.GetProperty("cargo").GetString() == "Contadora");
        (await cb.PutAsJsonAsync($"/api/compensaciones/bandas/{contadora.GetProperty("id").GetInt32()}", new { cargo = "Contadora", objetivo = 9999, pctMin = 85, pctMax = 120 })).EnsureSuccessStatusCode();
        var bandaA = (await ca.GetFromJsonAsync<JsonElement>("/api/compensaciones")).GetProperty("bandas").EnumerateArray().First(x => x.GetProperty("cargo").GetString() == "Contadora");
        Assert.NotEqual(9999m, bandaA.GetProperty("objetivo").GetDecimal());

        // Un id de otro tenant no existe para este.
        var idBandaB = contadora.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await ca.DeleteAsync($"/api/compensaciones/bandas/{idBandaB}")).StatusCode);

        // El historial de cambios tampoco se mezcla.
        var cambiosA = await ca.GetFromJsonAsync<JsonElement>("/api/cambios");
        Assert.DoesNotContain("Solo de Andesur", cambiosA.GetRawText());
        Assert.Contains("Solo de Andesur", (await cb.GetFromJsonAsync<JsonElement>("/api/cambios")).GetRawText());
    }

    [Fact]
    public async Task Hosts_sin_tenant_y_tenant_suspendido()
    {
        await AsegurarTenantB();
        foreach (var host in new[] { "noexiste.localhost", "admin.localhost", "app.localhost" })
        {
            var r = await Cliente(host).PostAsJsonAsync("/api/auth/login", new { correo = ApiFixture.Admin, password = api.PasswordSemilla });
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
            Assert.Equal("tenant_not_found", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
        }
        Assert.Equal(HttpStatusCode.OK, (await Cliente("noexiste.localhost").GetAsync("/api/salud")).StatusCode);

        using (var scope = api.Services.CreateScope())
        {
            var catalogo = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            (await catalogo.Tenants.FirstAsync(t => t.Id == TenantB)).Estado = EstadosTenant.Suspendido;
            await catalogo.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<BuscadorTenants>().Olvidar($"{SlugB}.localhost");
        }
        try
        {
            // Suspendido: el colaborador no entra; el administrador sí, pero solo al estado de cuenta y la exportación.
            var r = await Cliente($"{SlugB}.localhost").PostAsJsonAsync("/api/auth/login", new { correo = ApiFixture.Colaborador, password = api.PasswordSemilla });
            Assert.Equal(HttpStatusCode.Locked, r.StatusCode);
            Assert.Equal("tenant_suspended", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
            var (admin, _) = await Login($"{SlugB}.localhost", ApiFixture.Admin);
            Assert.Equal(HttpStatusCode.Locked, (await admin.GetAsync("/api/colaboradores")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/cuenta")).StatusCode);
        }
        finally
        {
            using var scope = api.Services.CreateScope();
            var catalogo = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            (await catalogo.Tenants.FirstAsync(t => t.Id == TenantB)).Estado = EstadosTenant.Activo;
            await catalogo.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<BuscadorTenants>().Olvidar($"{SlugB}.localhost");
        }
    }

    [Fact]
    public async Task La_aplicacion_no_escribe_en_otro_tenant()
    {
        await AsegurarTenantB();
        // Sin tenant, nada.
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var scope = api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
            db.Reportes.Add(new ReporteGuardado { Nombre = "Sin tenant", Plantilla = "Maestro de empleados" });
            db.SaveChanges();
        });
        // Con tenant A, una fila marcada para B se rechaza.
        Assert.Throws<InvalidOperationException>(() => ConTenant(AprovisionadorTenants.ComercialAndina, db =>
        {
            db.Reportes.Add(new ReporteGuardado { TenantId = TenantB, Nombre = "Cruzado", Plantilla = "Maestro de empleados" });
            return db.SaveChanges();
        }));
        // Cambiar el TenantId de una fila existente tampoco.
        Assert.Throws<InvalidOperationException>(() => ConTenant(AprovisionadorTenants.ComercialAndina, db =>
        {
            var r = db.Reportes.First();
            r.TenantId = TenantB;
            return db.SaveChanges();
        }));
    }

    [Fact]
    public async Task Row_Level_Security_filtra_aunque_se_escriba_SQL_a_mano()
    {
        await AsegurarTenantB();
        // Con el contexto de A, SQL crudo solo ve las filas de A (la política de SQL Server, no el filtro de EF).
        var enA = ConTenant(AprovisionadorTenants.ComercialAndina, db => db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Colaboradores").AsEnumerable().First());
        var enB = ConTenant(TenantB, db => db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Colaboradores").AsEnumerable().First());
        var todosA = ConTenant(AprovisionadorTenants.ComercialAndina, db => db.Colaboradores.IgnoreQueryFilters().Count());
        Assert.True(enA > 0);
        Assert.Equal(enA, todosA);   // IgnoreQueryFilters tampoco salta la política
        Assert.Equal(enA, enB);

        // Una conexión sin SESSION_CONTEXT no ve nada, y no puede insertar filas de otro tenant.
        string cadena;
        using (var scope = api.Services.CreateScope()) cadena = scope.ServiceProvider.GetRequiredService<NexoDbContext>().Database.GetConnectionString()!;
        await using var cn = new SqlConnection(cadena);
        await cn.OpenAsync();
        await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Colaboradores", cn)) Assert.Equal(0, (int)(await cmd.ExecuteScalarAsync())!);
        await using (var cmd = new SqlCommand("EXEC sp_set_session_context @key = N'tid', @value = @t", cn))
        {
            cmd.Parameters.AddWithValue("@t", AprovisionadorTenants.ComercialAndina);
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = new SqlCommand("INSERT INTO Reportes (TenantId, Nombre, Plantilla, Categoria, Creado, CreadoPor) VALUES (@b, N'Cruzado', N'x', N'x', '2026-03-16', N'x')", cn))
        {
            cmd.Parameters.AddWithValue("@b", TenantB);
            var e = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("block predicate", e.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Toda tabla con TenantId está en la política (filtro y bloqueo).
        await using (var cmd = new SqlCommand(@"SELECT
            (SELECT COUNT(*) FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'TenantId'),
            (SELECT COUNT(DISTINCT target_object_id) FROM sys.security_predicates WHERE predicate_type = 0),
            (SELECT COUNT(DISTINCT target_object_id) FROM sys.security_predicates WHERE predicate_type = 1)", cn))
        await using (var rd = await cmd.ExecuteReaderAsync())
        {
            await rd.ReadAsync();
            Assert.True(rd.GetInt32(0) >= 59);
            Assert.Equal(rd.GetInt32(0), rd.GetInt32(1));
            Assert.Equal(rd.GetInt32(0), rd.GetInt32(2));
        }
    }
}
