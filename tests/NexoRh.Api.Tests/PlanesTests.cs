using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Tests;

/// <summary>Planes y módulos (etapa 6.4): candados, límites, banners, Estado de cuenta, Seguridad y checklist.</summary>
[Collection("api")]
public class PlanesTests(ApiFixture api)
{
    private const string HostB = "andesur.localhost";
    private static readonly Guid TenantB = Guid.Parse("2b9d4c71-6a3e-4f8d-9c15-7e0a1d2f3b58");

    private async Task AsegurarB() => await api.Services.GetRequiredService<AprovisionadorTenants>().AsegurarAsync(
        new Tenant { Id = TenantB, Slug = "andesur", Nombre = "Andesur", RazonSocial = "Andesur Distribuciones S.A.C.", Ruc = "20512345678" }, datosDemo: true);

    private async Task<HttpClient> Sesion(string host, string correo)
    {
        var c = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}") });
        var r = await c.PostAsJsonAsync("/api/auth/login", new { correo, password = api.PasswordSemilla });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<Sesion>())!.Token);
        return c;
    }

    /// <summary>Cambia el catálogo del tenant B y limpia las cachés.</summary>
    private async Task Catalogo(Func<CatalogoDbContext, Task> cambio)
    {
        using var scope = api.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await cambio(sp.GetRequiredService<CatalogoDbContext>());
        await sp.GetRequiredService<CatalogoDbContext>().SaveChangesAsync();
        sp.GetRequiredService<Suscripciones>().Olvidar(TenantB);
        sp.GetRequiredService<AjustesTenant>().Olvidar(TenantB);
        sp.GetRequiredService<BuscadorTenants>().Olvidar(HostB);
    }

    [Fact]
    public async Task Contexto_con_plan_modulos_y_catalogo()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var c = await admin.GetFromJsonAsync<JsonElement>("/api/me/context");
        Assert.Equal("PRO", c.GetProperty("tenant").GetProperty("plan").GetString());
        var modulos = c.GetProperty("modulos").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("PAYROLL", modulos);
        Assert.Contains("PRO", modulos);
        Assert.Equal(12, c.GetProperty("catalogoModulos").GetArrayLength());
        Assert.Equal(4, c.GetProperty("limites").GetArrayLength());
        // Un colaborador no recibe límites ni banners de administración.
        var colab = await (await api.ClienteDe(ApiFixture.Colaborador)).GetFromJsonAsync<JsonElement>("/api/me/context");
        Assert.Equal(0, colab.GetProperty("limites").GetArrayLength());
        Assert.Equal(0, colab.GetProperty("banners").GetArrayLength());
    }

    [Fact]
    public async Task Modulo_no_contratado_responde_module_not_enabled()
    {
        await AsegurarB();
        await Catalogo(async c => c.TenantModulos.RemoveRange(await c.TenantModulos.Where(m => m.TenantId == TenantB && m.Codigo == Modulos.Payroll).ToListAsync()));
        try
        {
            var b = await Sesion(HostB, ApiFixture.Admin);
            var r = await b.GetAsync("/api/remuneraciones/parametros");
            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
            var cuerpo = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("module_not_enabled", cuerpo.GetProperty("codigo").GetString());
            Assert.Equal("PAYROLL", cuerpo.GetProperty("modulo").GetString());
            // Las boletas también son de Planillas.
            Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync("/api/remuneraciones/boletas")).StatusCode);
            // El otro tenant sigue con el módulo.
            Assert.Equal(HttpStatusCode.OK, (await (await api.ClienteDe(ApiFixture.Admin)).GetAsync("/api/remuneraciones/parametros")).StatusCode);

            var ctx = await b.GetFromJsonAsync<JsonElement>("/api/me/context");
            Assert.DoesNotContain("PAYROLL", ctx.GetProperty("modulos").EnumerateArray().Select(x => x.GetString()));
            Assert.False(ctx.GetProperty("catalogoModulos").EnumerateArray().First(m => m.GetProperty("codigo").GetString() == "PAYROLL").GetProperty("contratado").GetBoolean());

            // "Solicitar información": un lead por persona y módulo al día, visible en el Estado de cuenta.
            Assert.Equal(HttpStatusCode.Accepted, (await b.PostAsJsonAsync("/api/cuenta/solicitudes", new { modulo = "PAYROLL", mensaje = "Queremos planillas" })).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, (await b.PostAsJsonAsync("/api/cuenta/solicitudes", new { modulo = "PAYROLL" })).StatusCode);
            using (var scope = api.Services.CreateScope())
                Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Solicitudes.CountAsync(s => s.TenantId == TenantB && s.Modulo == "PAYROLL"));
            var cuenta = await b.GetFromJsonAsync<JsonElement>("/api/cuenta");
            var payroll = cuenta.GetProperty("modulos").EnumerateArray().First(m => m.GetProperty("codigo").GetString() == "PAYROLL");
            Assert.Equal("Disponible", payroll.GetProperty("estado").GetString());
            Assert.True(payroll.GetProperty("solicitudPendiente").GetBoolean());
        }
        finally
        {
            await Catalogo(async c =>
            {
                c.TenantModulos.Add(new TenantModulo { TenantId = TenantB, Codigo = Modulos.Payroll });
                c.Solicitudes.RemoveRange(await c.Solicitudes.Where(s => s.TenantId == TenantB).ToListAsync());
            });
        }
    }

    [Fact]
    public async Task Limites_del_plan_avisan_y_bloquean()
    {
        await AsegurarB();
        await Catalogo(async c => (await c.Tenants.FirstAsync(t => t.Id == TenantB)).PlanCodigo = "BASICO");
        try
        {
            var b = await Sesion(HostB, ApiFixture.Admin);
            var ctx = await b.GetFromJsonAsync<JsonElement>("/api/me/context");
            var empresas = ctx.GetProperty("limites").EnumerateArray().First(l => l.GetProperty("clave").GetString() == "empresas");
            Assert.Equal(1, empresas.GetProperty("maximo").GetDouble());
            Assert.DoesNotContain("PRO", ctx.GetProperty("modulos").EnumerateArray().Select(x => x.GetString()));
            // Sin Plataforma Pro, el historial de cambios no está disponible.
            Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync("/api/cambios")).StatusCode);

            // Empresas: los datos de ejemplo ya tienen al menos una; Básico permite 1.
            if (empresas.GetProperty("usado").GetDouble() >= 1)
            {
                var r = await b.PostAsJsonAsync("/api/catalogos/empresas", new { nombre = "Otra S.A.C." });
                Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
                Assert.Equal("limit_reached", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
                Assert.Contains(ctx.GetProperty("banners").EnumerateArray(), x => x.GetProperty("tipo").GetString() == "limite");
            }

            // Administradores: Básico permite 3.
            var admins = ctx.GetProperty("limites").EnumerateArray().First(l => l.GetProperty("clave").GetString() == "admins").GetProperty("usado").GetDouble();
            var perfiles = await b.GetFromJsonAsync<JsonElement>("/api/perfiles");
            var perfilAdmin = perfiles.EnumerateArray().First(p => p.GetProperty("predefinido").GetBoolean() && p.GetProperty("rol").GetString() == "Administrador").GetProperty("id").GetInt32();
            for (var i = (int)admins; i < 3; i++)
                (await b.PostAsJsonAsync("/api/usuarios", new { nombre = $"Admin Extra {i}", correo = $"admin.extra{i}@andesur.pe", perfilId = perfilAdmin, colaboradorId = (int?)null })).EnsureSuccessStatusCode();
            var excedido = await b.PostAsJsonAsync("/api/usuarios", new { nombre = "Uno Más", correo = "uno.mas@andesur.pe", perfilId = perfilAdmin, colaboradorId = (int?)null });
            Assert.Equal(HttpStatusCode.Forbidden, excedido.StatusCode);
            Assert.Equal("limit_reached", (await excedido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
        }
        finally { await Catalogo(async c => (await c.Tenants.FirstAsync(t => t.Id == TenantB)).PlanCodigo = "PRO"); }
    }

    [Fact]
    public async Task Banners_de_prueba_gratuita_y_morosidad()
    {
        await AsegurarB();
        try
        {
            await Catalogo(async c => { var t = await c.Tenants.FirstAsync(x => x.Id == TenantB); t.Estado = EstadosTenant.Trial; t.TrialHasta = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10); });
            var b = await Sesion(HostB, ApiFixture.Admin);
            var ctx = await b.GetFromJsonAsync<JsonElement>("/api/me/context");
            Assert.Equal(10, ctx.GetProperty("tenant").GetProperty("diasTrial").GetInt32());
            Assert.Contains(ctx.GetProperty("banners").EnumerateArray(), x => x.GetProperty("tipo").GetString() == "trial" && x.GetProperty("texto").GetString()!.Contains("10 días"));

            await Catalogo(async c => (await c.Tenants.FirstAsync(x => x.Id == TenantB)).Estado = EstadosTenant.Moroso);
            ctx = await b.GetFromJsonAsync<JsonElement>("/api/me/context");
            Assert.Contains(ctx.GetProperty("banners").EnumerateArray(), x => x.GetProperty("tipo").GetString() == "moroso");
            // El colaborador no lo ve.
            var colab = await (await Sesion(HostB, ApiFixture.Colaborador)).GetFromJsonAsync<JsonElement>("/api/me/context");
            Assert.Equal(0, colab.GetProperty("banners").GetArrayLength());
        }
        finally { await Catalogo(async c => { var t = await c.Tenants.FirstAsync(x => x.Id == TenantB); t.Estado = EstadosTenant.Activo; t.TrialHasta = null; }); }
    }

    [Fact]
    public async Task Estado_de_cuenta_y_datos_de_facturacion()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var cuenta = await admin.GetFromJsonAsync<JsonElement>("/api/cuenta");
        Assert.Equal("PRO", cuenta.GetProperty("plan").GetProperty("codigo").GetString());
        Assert.Equal(4, cuenta.GetProperty("facturas").GetArrayLength());
        Assert.Equal("Incluido", cuenta.GetProperty("modulos").EnumerateArray().First(m => m.GetProperty("codigo").GetString() == "PRO").GetProperty("estado").GetString());
        var est = cuenta.GetProperty("estimado");
        Assert.Equal(Math.Round(est.GetProperty("subtotal").GetDecimal() * 0.18m, 2), est.GetProperty("igv").GetDecimal());
        Assert.Equal(cuenta.GetProperty("colaboradores").GetInt32() * est.GetProperty("precioPorColaborador").GetDecimal(), est.GetProperty("subtotal").GetDecimal());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/cuenta/facturacion", new { razonSocial = "Comercial Andina S.A.C.", ruc = "123" })).StatusCode);
        var ok = await admin.PutAsJsonAsync("/api/cuenta/facturacion", new { razonSocial = "Comercial Andina S.A.C.", ruc = "20601234571", direccionFiscal = "Av. Javier Prado 123, Lima", correoFacturacion = "Facturas@ComercialAndina.pe" });
        ok.EnsureSuccessStatusCode();
        Assert.Equal("facturas@comercialandina.pe", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("correoFacturacion").GetString());
        // Solo administradores.
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.ClienteDe(ApiFixture.Colaborador)).GetAsync("/api/cuenta")).StatusCode);
    }

    [Fact]
    public async Task Seguridad_y_acceso_respeta_el_plan()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var antes = await admin.GetFromJsonAsync<JsonElement>("/api/seguridad");
        Assert.True(antes.GetProperty("permitidos").GetProperty("ssoSocial").GetBoolean());
        Assert.False(antes.GetProperty("permitidos").GetProperty("saml").GetBoolean());
        object Acceso(bool saml = false, int minimo = 12) => new { loginDocumento = true, passwordMinimo = minimo, maxIntentos = 5, bloqueoMinutos = 15, dobleFactor = "Opcional", sesionMinutos = 240, google = true, microsoft = false, saml };
        try
        {
            var r = await admin.PutAsJsonAsync("/api/seguridad", new { acceso = Acceso(saml: true, minimo: 4), privacidadUrl = "http://inseguro", permitirSoporte = true, diasSoporte = 90 });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            var errores = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            foreach (var campo in new[] { "acceso.saml", "acceso.passwordMinimo", "privacidadUrl", "diasSoporte" }) Assert.True(errores.TryGetProperty(campo, out _), campo);

            var ok = await (await admin.PutAsJsonAsync("/api/seguridad", new { acceso = Acceso(), privacidadUrl = "https://comercialandina.pe/privacidad", permitirSoporte = true, diasSoporte = 7 })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(12, ok.GetProperty("acceso").GetProperty("passwordMinimo").GetInt32());
            Assert.InRange(ok.GetProperty("soporteHasta").GetDateTime(), DateTime.UtcNow.AddDays(6.9), DateTime.UtcNow.AddDays(7.1));
            // El login lo refleja (documento habilitado).
            var entrada = await api.CreateClient().GetFromJsonAsync<JsonElement>("/api/publico/entrada");
            Assert.True(entrada.GetProperty("tenant").GetProperty("metodos").GetProperty("documento").GetBoolean());
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/seguridad", new { acceso = new PoliticaAcceso(), privacidadUrl = (string?)null, permitirSoporte = false })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Checklist_de_implementacion()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        try
        {
            var r = await (await admin.PutAsJsonAsync("/api/cuenta/implementacion", new { activo = true })).Content.ReadFromJsonAsync<JsonElement>();
            var pasos = r.GetProperty("pasos").EnumerateArray().ToList();
            Assert.Equal(8, pasos.Count);
            Assert.Equal(100, pasos.First(p => p.GetProperty("clave").GetString() == "colaboradores").GetProperty("avance").GetInt32());
            var ctx = await admin.GetFromJsonAsync<JsonElement>("/api/me/context");
            Assert.Equal(JsonValueKind.Object, ctx.GetProperty("implementacion").ValueKind);
        }
        finally { (await admin.PutAsJsonAsync("/api/cuenta/implementacion", new { activo = false })).EnsureSuccessStatusCode(); }
        Assert.Equal(JsonValueKind.Null, (await admin.GetFromJsonAsync<JsonElement>("/api/me/context")).GetProperty("implementacion").ValueKind);
    }
}
