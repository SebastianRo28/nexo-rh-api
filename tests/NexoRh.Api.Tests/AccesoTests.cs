using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;

namespace NexoRh.Api.Tests;

/// <summary>Acceso por tenant (etapa 6.2): recuperación, invitación, doble factor, login por documento, selector de cuenta.</summary>
[Collection("api")]
public class AccesoTests(ApiFixture api)
{
    private const string HostA = "comercialandina.localhost";
    private static readonly Guid TenantB = Guid.Parse("2b9d4c71-6a3e-4f8d-9c15-7e0a1d2f3b58");

    private HttpClient Cliente(string host = HostA) => api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}") });

    private async Task<HttpClient> ComoAdmin(string host = HostA)
    {
        var c = Cliente(host);
        var r = await c.PostAsJsonAsync("/api/auth/login", new { correo = ApiFixture.Admin, password = api.PasswordSemilla });
        r.EnsureSuccessStatusCode();
        var s = (await r.Content.ReadFromJsonAsync<Sesion>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        return c;
    }

    /// <summary>Último correo de la bandeja para esa dirección (o null).</summary>
    private CorreoSaliente? UltimoCorreo(string para)
    {
        using var scope = api.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Correos.AsNoTracking()
            .Where(c => c.Para == para).OrderByDescending(c => c.Id).FirstOrDefault();
    }

    private static string Token(CorreoSaliente c) => HttpUtility.ParseQueryString(new Uri(c.Enlace!).Query)["token"]!;

    private async Task Politica(Guid tenant, Action<PoliticaAcceso> cambiar)
    {
        using var scope = api.Services.CreateScope();
        var catalogo = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var a = await catalogo.Ajustes.FirstOrDefaultAsync(x => x.TenantId == tenant);
        if (a is null) catalogo.Ajustes.Add(a = new TenantAjustes { TenantId = tenant });
        cambiar(a.Acceso);
        await catalogo.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<AjustesTenant>().Olvidar(tenant);
    }

    /// <summary>Crea una cuenta invitada por correo y la activa; devuelve su correo y su contraseña.</summary>
    private async Task<(string Correo, string Password)> CuentaActivada(string prefijo)
    {
        var correo = $"{prefijo}.{Guid.NewGuid():N}"[..20] + "@prueba.pe";
        var admin = await ComoAdmin();
        var r = await admin.PostAsJsonAsync("/api/usuarios", new { nombre = "Persona Invitada", correo, rol = "Administrador", invitar = true });
        r.EnsureSuccessStatusCode();
        var creado = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, creado.GetProperty("passwordTemporal").ValueKind);
        Assert.True(creado.GetProperty("usuario").GetProperty("pendienteActivacion").GetBoolean());

        var invitacion = UltimoCorreo(correo)!;
        Assert.StartsWith($"http://{HostA}/activar?token=", invitacion.Enlace);
        var password = "Clave" + Guid.NewGuid().ToString("N")[..8] + "9z";
        var anonimo = Cliente();
        Assert.Equal(HttpStatusCode.BadRequest, (await anonimo.PostAsJsonAsync("/api/auth/activar", new { token = Token(invitacion), password })).StatusCode);
        (await anonimo.PostAsJsonAsync("/api/auth/activar", new { token = Token(invitacion), password, aceptaPrivacidad = true })).EnsureSuccessStatusCode();
        return (correo, password);
    }

    [Fact]
    public async Task Invitacion_activa_la_cuenta_una_sola_vez()
    {
        var correo = $"inv.{Guid.NewGuid():N}"[..16] + "@prueba.pe";
        var admin = await ComoAdmin();
        (await admin.PostAsJsonAsync("/api/usuarios", new { nombre = "Ana Invitada", correo, rol = "Administrador", invitar = true })).EnsureSuccessStatusCode();
        var anonimo = Cliente();
        // Sin activar no entra con ninguna contraseña.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/auth/login", new { correo, password = "Cualquiera123" })).StatusCode);

        var token = Token(UltimoCorreo(correo)!);
        var info = await anonimo.GetFromJsonAsync<JsonElement>($"/api/auth/token?tipo=Activacion&token={Uri.EscapeDataString(token)}");
        Assert.Equal("Ana Invitada", info.GetProperty("nombre").GetString());
        Assert.DoesNotContain(correo.Split('@')[0], info.GetProperty("correo").GetString());

        var password = "Nueva" + Guid.NewGuid().ToString("N")[..8] + "7k";
        (await anonimo.PostAsJsonAsync("/api/auth/activar", new { token, password, aceptaPrivacidad = true })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.PostAsJsonAsync("/api/auth/activar", new { token, password, aceptaPrivacidad = true })).StatusCode);
        (await anonimo.PostAsJsonAsync("/api/auth/login", new { correo, password })).EnsureSuccessStatusCode();
        // El token de un tenant no existe en otro.
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente("andesur.localhost").GetAsync($"/api/auth/token?tipo=Activacion&token={Uri.EscapeDataString(token)}")).StatusCode);
    }

    [Fact]
    public async Task Olvide_mi_contrasena_no_revela_cuentas_y_restablece_con_el_enlace()
    {
        var (correo, password) = await CuentaActivada("olv");
        var anonimo = Cliente();

        var antes = UltimoCorreo("nadie@noexiste.pe");
        var r1 = await anonimo.PostAsJsonAsync("/api/auth/olvide", new { correo = "nadie@noexiste.pe" });
        var r2 = await anonimo.PostAsJsonAsync("/api/auth/olvide", new { correo });
        Assert.Equal(HttpStatusCode.Accepted, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, r2.StatusCode);
        Assert.Equal(await r1.Content.ReadAsStringAsync(), await r2.Content.ReadAsStringAsync());
        Assert.Null(antes ?? UltimoCorreo("nadie@noexiste.pe"));

        var correoRestablecer = UltimoCorreo(correo)!;
        Assert.StartsWith($"http://{HostA}/restablecer?token=", correoRestablecer.Enlace);
        var token = Token(correoRestablecer);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonimo.PostAsJsonAsync("/api/auth/restablecer", new { token, password = "corta1" })).StatusCode);
        var nueva = "Otra" + Guid.NewGuid().ToString("N")[..8] + "5q";
        Assert.Equal(HttpStatusCode.NoContent, (await anonimo.PostAsJsonAsync("/api/auth/restablecer", new { token, password = nueva })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.PostAsJsonAsync("/api/auth/restablecer", new { token, password = nueva + "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/auth/login", new { correo, password })).StatusCode);
        (await anonimo.PostAsJsonAsync("/api/auth/login", new { correo, password = nueva })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Doble_factor_pide_el_codigo_y_la_politica_lo_exige()
    {
        var (correo, password) = await CuentaActivada("mfa");
        var c = Cliente();
        var s = await (await c.PostAsJsonAsync("/api/auth/login", new { correo, password })).Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.GetProperty("token").GetString());

        var inicio = await (await c.PostAsync("/api/auth/2fa/iniciar", null)).Content.ReadFromJsonAsync<JsonElement>();
        var clave = inicio.GetProperty("clave").GetString()!;
        Assert.StartsWith("otpauth://totp/", inicio.GetProperty("uri").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/2fa/confirmar", new { codigo = "000000" })).StatusCode);
        string Codigo() => Totp.Codigo(Totp.DeBase32(clave), DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var confirmada = await c.PostAsJsonAsync("/api/auth/2fa/confirmar", new { codigo = Codigo() });
        confirmada.EnsureSuccessStatusCode();
        Assert.True((await confirmada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("usuario").GetProperty("dobleFactor").GetBoolean());
        // El token anterior dejó de servir (se cerraron las otras sesiones).
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);

        // Login: primero la contraseña, luego el código.
        var anonimo = Cliente();
        var paso1 = await (await anonimo.PostAsJsonAsync("/api/auth/login", new { correo, password })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(paso1.GetProperty("mfaRequerido").GetBoolean());
        Assert.Equal(JsonValueKind.Null, paso1.GetProperty("token").ValueKind);
        var mfaToken = paso1.GetProperty("mfaToken").GetString();
        var malo = await anonimo.PostAsJsonAsync("/api/auth/mfa", new { mfaToken, codigo = "123456" == Codigo() ? "654321" : "123456" });
        Assert.Equal(HttpStatusCode.Unauthorized, malo.StatusCode);
        Assert.Equal("mfa_invalido", (await malo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
        var bueno = await anonimo.PostAsJsonAsync("/api/auth/mfa", new { mfaToken, codigo = Codigo() });
        bueno.EnsureSuccessStatusCode();
        Assert.False(string.IsNullOrEmpty((await bueno.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()));
        // El mfaToken es de un solo uso.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/auth/mfa", new { mfaToken, codigo = Codigo() })).StatusCode);

        // Política "Obligatorio": una cuenta sin doble factor solo puede usar /api/auth hasta configurarlo.
        var (otro, passOtro) = await CuentaActivada("pol");
        await Politica(AprovisionadorTenants.ComercialAndina, p => p.DobleFactor = ModosDobleFactor.Obligatorio);
        try
        {
            var c2 = Cliente();
            var s2 = await (await c2.PostAsJsonAsync("/api/auth/login", new { correo = otro, password = passOtro })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(s2.GetProperty("usuario").GetProperty("debeActivarDobleFactor").GetBoolean());
            c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s2.GetProperty("token").GetString());
            var bloqueado = await c2.GetAsync("/api/publicaciones/muro");
            Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
            Assert.Equal("doble_factor_requerido", (await bloqueado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
            Assert.Equal(HttpStatusCode.OK, (await c2.GetAsync("/api/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await c2.PostAsJsonAsync("/api/auth/2fa/desactivar", new { password = passOtro })).StatusCode);
        }
        finally { await Politica(AprovisionadorTenants.ComercialAndina, p => p.DobleFactor = ModosDobleFactor.Opcional); }
    }

    [Fact]
    public async Task Login_con_numero_de_documento_solo_si_el_tenant_lo_permite()
    {
        string documento;
        using (var scope = api.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(AprovisionadorTenants.TenantDemo());
            var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
            var colaboradorId = await db.Usuarios.Where(u => u.ColaboradorId != null && u.Activo && !u.DebeCambiarPassword && !u.TotpActivo)
                .OrderBy(u => u.Id).Select(u => u.ColaboradorId).FirstAsync();
            documento = await db.Colaboradores.Where(c => c.Id == colaboradorId).Select(c => c.Documento).FirstAsync();
        }
        var anonimo = Cliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/auth/login", new { correo = documento, password = api.PasswordSemilla })).StatusCode);
        await Politica(AprovisionadorTenants.ComercialAndina, p => p.LoginDocumento = true);
        try
        {
            (await anonimo.PostAsJsonAsync("/api/auth/login", new { correo = documento, password = api.PasswordSemilla })).EnsureSuccessStatusCode();
            var entrada = await anonimo.GetFromJsonAsync<JsonElement>("/api/publico/entrada");
            Assert.True(entrada.GetProperty("tenant").GetProperty("metodos").GetProperty("documento").GetBoolean());
        }
        finally { await Politica(AprovisionadorTenants.ComercialAndina, p => p.LoginDocumento = false); }
    }

    [Fact]
    public async Task Entrada_segun_el_host()
    {
        var a = await Cliente().GetFromJsonAsync<JsonElement>("/api/publico/entrada");
        Assert.Equal("tenant", a.GetProperty("tipo").GetString());
        Assert.Equal("Comercial Andina", a.GetProperty("tenant").GetProperty("nombre").GetString());
        Assert.Equal("global", (await Cliente("app.localhost").GetFromJsonAsync<JsonElement>("/api/publico/entrada")).GetProperty("tipo").GetString());
        Assert.Equal("no_encontrado", (await Cliente("noexiste.localhost").GetFromJsonAsync<JsonElement>("/api/publico/entrada")).GetProperty("tipo").GetString());
        Assert.Equal("consola", (await Cliente("admin.localhost").GetFromJsonAsync<JsonElement>("/api/publico/entrada")).GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task No_conoces_tu_enlace_y_selector_de_cuenta()
    {
        await api.Services.GetRequiredService<AprovisionadorTenants>().AsegurarAsync(
            new Tenant { Id = TenantB, Slug = "andesur", Nombre = "Andesur", RazonSocial = "Andesur Distribuciones S.A.C.", Ruc = "20512345678" }, datosDemo: true);
        var global = Cliente("app.localhost");

        // Enlaces por correo: misma respuesta exista o no; el correo lista las dos empresas.
        var r = await global.PostAsJsonAsync("/api/publico/enlaces", new { correo = ApiFixture.Admin });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        var enviado = UltimoCorreo(ApiFixture.Admin)!;
        Assert.Contains("http://comercialandina.localhost", enviado.Html);
        Assert.Contains("http://andesur.localhost", enviado.Html);
        var r2 = await global.PostAsJsonAsync("/api/publico/enlaces", new { correo = "nadie@noexiste.pe" });
        Assert.Equal(await r.Content.ReadAsStringAsync(), await r2.Content.ReadAsStringAsync());
        Assert.Null(UltimoCorreo("nadie@noexiste.pe"));

        // Selector: con correo y contraseña válidos, las dos cuentas con su enlace de un solo uso.
        Assert.Equal(HttpStatusCode.Unauthorized, (await global.PostAsJsonAsync("/api/publico/cuentas", new { correo = ApiFixture.Admin, password = "Incorrecta123" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente().PostAsJsonAsync("/api/publico/cuentas", new { correo = ApiFixture.Admin, password = api.PasswordSemilla })).StatusCode);
        var cuentas = await (await global.PostAsJsonAsync("/api/publico/cuentas", new { correo = ApiFixture.Admin, password = api.PasswordSemilla })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["Andesur", "Comercial Andina"], cuentas.EnumerateArray().Select(x => x.GetProperty("nombre").GetString()).ToArray());
        var enlaceB = new Uri(cuentas.EnumerateArray().First().GetProperty("enlace").GetString()!);
        Assert.Equal("andesur.localhost", enlaceB.Host);
        var codigo = HttpUtility.ParseQueryString(enlaceB.Query)["codigo"];

        // El código solo sirve en su empresa y una vez.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente().PostAsJsonAsync("/api/auth/canjear", new { codigo })).StatusCode);
        var b = Cliente("andesur.localhost");
        var sesion = await (await b.PostAsJsonAsync("/api/auth/canjear", new { codigo })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(sesion.GetProperty("token").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsJsonAsync("/api/auth/canjear", new { codigo })).StatusCode);
    }
}
