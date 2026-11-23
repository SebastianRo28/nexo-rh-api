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

namespace NexoRh.Api.Tests;

/// <summary>Consola del proveedor (etapa 6.5): login con 2FA, alta de tenants, suspensión, módulos, soporte y configuración.</summary>
[Collection("api")]
public class ConsolaTests(ApiFixture api)
{
    private const string Consola = "admin.localhost";
    private const string CorreoPlataforma = "plataforma@nexorh.pe";
    private static string? clave;
    private static readonly SemaphoreSlim Candado = new(1, 1);

    private HttpClient Cliente(string host) => api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}") });
    private static string Codigo(string k) => Totp.Codigo(Totp.DeBase32(k), DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);

    /// <summary>Sesión de la consola. El primer ingreso enrola el doble factor; los siguientes lo piden.</summary>
    private async Task<HttpClient> Plataforma()
    {
        await Candado.WaitAsync();
        try
        {
            var c = Cliente(Consola);
            var paso1 = await (await c.PostAsJsonAsync("/api/platform/auth/login", new { correo = CorreoPlataforma, password = api.PasswordSemilla })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(paso1.GetProperty("mfaRequerido").GetBoolean());
            if (paso1.GetProperty("enrolar").GetBoolean()) clave = paso1.GetProperty("clave").GetString();
            var r = await c.PostAsJsonAsync("/api/platform/auth/mfa/verify", new { mfaToken = paso1.GetProperty("mfaToken").GetString(), codigo = Codigo(clave!) });
            r.EnsureSuccessStatusCode();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
            return c;
        }
        finally { Candado.Release(); }
    }

    private CorreoSaliente? UltimoCorreo(string para)
    {
        using var scope = api.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Correos.AsNoTracking().Where(c => c.Para == para).OrderByDescending(c => c.Id).FirstOrDefault();
    }

    /// <summary>Crea un tenant desde el wizard y espera el aprovisionamiento.</summary>
    private async Task<(Guid Id, string Slug, string AdminCorreo)> Alta(HttpClient p, string prefijo, List<string>? modulos = null, string plan = "PRO", bool bdDedicada = false)
    {
        var slug = $"{prefijo}{Guid.NewGuid():N}"[..14];
        var correo = $"admin@{slug}.pe";
        var r = await p.PostAsJsonAsync("/api/platform/tenants", new
        {
            razonSocial = $"Empresa {slug} S.A.C.", ruc = "20" + Random.Shared.NextInt64(100_000_000, 999_999_999), nombre = $"Empresa {slug}", industria = "Retail",
            ejecutivo = "Mariana Llosa", correoFacturacion = $"finanzas@{slug}.pe", slug, plan, modulos = modulos ?? ["PAYROLL", "ATTENDANCE"],
            precioPorColaborador = 15.9m, trialHasta = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
            admin = new { nombres = "Lucía", apellidos = "Ramos", correo, cargo = "Jefa de RR.HH." }, opciones = new { datosEjemplo = false, bdDedicada },
        });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        var job = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("provisioningJobId").GetString();
        JsonElement estado = default;
        for (var i = 0; i < 120; i++)
        {
            estado = await p.GetFromJsonAsync<JsonElement>($"/api/platform/provisioning/{job}");
            if (estado.GetProperty("estado").GetString() != "running") break;
            await Task.Delay(500);
        }
        Assert.Equal("succeeded", estado.GetProperty("estado").GetString());
        Assert.Equal(100, estado.GetProperty("progreso").GetInt32());
        return (estado.GetProperty("tenantId").GetGuid(), slug, correo);
    }

    [Fact]
    public async Task Login_de_consola_con_doble_factor_y_aislado_de_los_tenants()
    {
        var p = await Plataforma();
        Assert.Equal(HttpStatusCode.OK, (await p.GetAsync("/api/platform/dashboard")).StatusCode);
        // Contraseña errada: aviso con intentos restantes.
        var malo = await Cliente(Consola).PostAsJsonAsync("/api/platform/auth/login", new { correo = CorreoPlataforma, password = "Incorrecta123" });
        Assert.Equal(HttpStatusCode.Unauthorized, malo.StatusCode);
        Assert.Contains("Te quedan", await malo.Content.ReadAsStringAsync());
        // La consola no existe en el host de un tenant.
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente("comercialandina.localhost").PostAsJsonAsync("/api/platform/auth/login", new { correo = CorreoPlataforma, password = api.PasswordSemilla })).StatusCode);
        // Un token de tenant no sirve en la consola y uno de consola no sirve en un tenant.
        var tenant = await api.ClienteDe(ApiFixture.Admin);
        var conTokenTenant = Cliente(Consola);
        conTokenTenant.DefaultRequestHeaders.Authorization = tenant.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await conTokenTenant.GetAsync("/api/platform/dashboard")).StatusCode);
        var conTokenConsola = Cliente("comercialandina.localhost");
        conTokenConsola.DefaultRequestHeaders.Authorization = p.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await conTokenConsola.GetAsync("/api/colaboradores")).StatusCode);
        // Código errado en el segundo paso.
        var paso1 = await (await Cliente(Consola).PostAsJsonAsync("/api/platform/auth/login", new { correo = CorreoPlataforma, password = api.PasswordSemilla })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(paso1.GetProperty("enrolar").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await Cliente(Consola).PostAsJsonAsync("/api/platform/auth/mfa/verify", new { mfaToken = paso1.GetProperty("mfaToken").GetString(), codigo = "000000" == Codigo(clave!) ? "111111" : "000000" })).StatusCode);
    }

    [Fact]
    public async Task Alta_de_tenant_con_aprovisionamiento_e_invitacion()
    {
        var p = await Plataforma();
        async Task<string> Slug(string s) => (await p.GetFromJsonAsync<JsonElement>($"/api/platform/slug-availability?slug={s}")).GetProperty("status").GetString()!;
        Assert.Equal("taken", await Slug("comercialandina"));
        Assert.Equal("reserved", await Slug("admin"));
        Assert.Equal("invalid", await Slug("A_b"));
        Assert.Equal("available", await Slug("empresa-nueva-xyz"));

        var (id, slug, correo) = await Alta(p, "alta");
        Assert.Equal("taken", await Slug(slug));
        var detalle = await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{id}");
        Assert.Equal("Trial", detalle.GetProperty("estado").GetString());
        Assert.Equal(15.9m + 6m + 4m, detalle.GetProperty("precioPorColaborador").GetDecimal());
        Assert.True(detalle.GetProperty("modulos").EnumerateArray().First(m => m.GetProperty("codigo").GetString() == "PAYROLL").GetProperty("activo").GetBoolean());
        Assert.False(detalle.GetProperty("modulos").EnumerateArray().First(m => m.GetProperty("codigo").GetString() == "SST").GetProperty("activo").GetBoolean());

        // El administrador inicial recibe la invitación y activa su cuenta en el subdominio nuevo.
        var invitacion = UltimoCorreo(correo)!;
        Assert.StartsWith($"http://{slug}.localhost/activar?token=", invitacion.Enlace);
        var token = HttpUtility.ParseQueryString(new Uri(invitacion.Enlace!).Query)["token"];
        var app = Cliente($"{slug}.localhost");
        var password = "Inicial" + Guid.NewGuid().ToString("N")[..8] + "4x";
        (await app.PostAsJsonAsync("/api/auth/activar", new { token, password, aceptaPrivacidad = true })).EnsureSuccessStatusCode();
        var sesion = await (await app.PostAsJsonAsync("/api/auth/login", new { correo, password })).Content.ReadFromJsonAsync<JsonElement>();
        app.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.GetProperty("token").GetString());
        var ctx = await app.GetFromJsonAsync<JsonElement>("/api/me/context");
        var modulos = ctx.GetProperty("modulos").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("PAYROLL", modulos);
        Assert.DoesNotContain("SST", modulos);
        Assert.Equal(0, (await app.GetFromJsonAsync<JsonElement>("/api/colaboradores")).GetArrayLength());

        // Validaciones del alta.
        var dup = await p.PostAsJsonAsync("/api/platform/tenants", new { razonSocial = "Otra S.A.C.", ruc = "20123456789", slug, plan = "PRO", modulos = new[] { "PAYROLL" }, precioPorColaborador = 10, admin = new { nombres = "A", apellidos = "B", correo = "a@b.pe" } });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var ded = await p.PostAsJsonAsync("/api/platform/tenants", new { razonSocial = "Otra S.A.C.", ruc = "20123456789", slug = "otra-ded-" + Random.Shared.Next(1000, 9999), plan = "PRO", modulos = new[] { "PAYROLL" }, precioPorColaborador = 10, admin = new { nombres = "A", apellidos = "B", correo = "a@b.pe" }, opciones = new { bdDedicada = true } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ded.StatusCode);
        Assert.Contains((await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{id}/audit")).EnumerateArray(), a => a.GetProperty("accion").GetString() == "tenant.created");
    }

    [Fact]
    public async Task Suspender_reactivar_y_modulos_con_vigencia()
    {
        var p = await Plataforma();
        var (id, slug, correo) = await Alta(p, "susp");
        Assert.Equal(HttpStatusCode.BadRequest, (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/suspend", new { motivo = "Morosidad", confirmacion = "otro" })).StatusCode);
        (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/suspend", new { motivo = "Morosidad (factura vencida > 30 días)", nota = "Prueba", confirmacion = slug })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Locked, (await Cliente($"{slug}.localhost").GetAsync("/api/colaboradores")).StatusCode);
        Assert.Equal("Suspendido", (await Cliente($"{slug}.localhost").GetFromJsonAsync<JsonElement>("/api/publico/entrada")).GetProperty("tenant").GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/suspend", new { motivo = "x", confirmacion = slug })).StatusCode);
        (await p.PostAsync($"/api/platform/tenants/{id}/reactivate", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente($"{slug}.localhost").PostAsJsonAsync("/api/auth/login", new { correo, password = "loquesea123" })).StatusCode);

        // Contratar SST desde hoy y quitar Planillas desde hoy.
        var r = await p.PutAsJsonAsync($"/api/platform/tenants/{id}/modules", new { modulos = new[] { new { codigo = "SST", activo = true, desde = DateOnly.FromDateTime(DateTime.UtcNow) }, new { codigo = "PAYROLL", activo = false, desde = DateOnly.FromDateTime(DateTime.UtcNow) } } });
        r.EnsureSuccessStatusCode();
        var mods = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(mods.EnumerateArray().First(m => m.GetProperty("codigo").GetString() == "SST").GetProperty("activo").GetBoolean());
        Assert.False(mods.EnumerateArray().First(m => m.GetProperty("codigo").GetString() == "PAYROLL").GetProperty("activo").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await p.PutAsJsonAsync($"/api/platform/tenants/{id}/modules", new { modulos = new[] { new { codigo = "COMP", activo = true, desde = new DateOnly(2020, 1, 1) } } })).StatusCode);
        // Notas internas y lista con filtros.
        (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/notes", new { texto = "Cliente pidió demo de SST." })).EnsureSuccessStatusCode();
        Assert.Single((await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{id}/notes")).EnumerateArray());
        var lista = await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants?q={slug}");
        Assert.Equal(1, lista.GetProperty("total").GetInt32());
        Assert.Equal("Trial", lista.GetProperty("items")[0].GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Cancelar_y_eliminar_desde_la_consola_con_certificado()
    {
        var p = await Plataforma();
        var (id, slug, _) = await Alta(p, "canc");
        Assert.Equal(HttpStatusCode.Conflict, (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/delete", new { confirmacion = slug })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/cancel", new { motivo = "Solicitud del cliente", confirmacion = "otro" })).StatusCode);
        var c = await (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/cancel", new { motivo = "Solicitud del cliente", confirmacion = slug })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelado", c.GetProperty("estado").GetString());
        var det = await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{id}");
        Assert.Equal(JsonValueKind.String, det.GetProperty("eliminacionProgramada").ValueKind);
        var cert = await (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/delete", new { confirmacion = slug })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(slug, cert.GetProperty("slug").GetString());
        Assert.Equal("Eliminado", (await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{id}")).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.OK, (await p.GetAsync($"/api/platform/tenants/{id}/certificado")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await p.PostAsync($"/api/platform/tenants/{id}/reactivate", null)).StatusCode);
        Assert.Contains((await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{id}/audit")).EnumerateArray(), a => a.GetProperty("accion").GetString() == "tenant.deleted");
    }

    [Fact]
    public async Task Sesion_de_soporte_auditada_y_de_solo_lectura()
    {
        var p = await Plataforma();
        var demo = AprovisionadorTenants.ComercialAndina;
        var sinTicket = await p.PostAsJsonAsync("/api/platform/support-sessions", new { tenantId = demo, motivo = "Revisar el cálculo de la planilla de marzo", modo = "lectura" });
        Assert.Equal(HttpStatusCode.BadRequest, sinTicket.StatusCode);
        Assert.True((await sinTicket.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("ticket", out _));
        var r = await p.PostAsJsonAsync("/api/platform/support-sessions", new { tenantId = demo, motivo = "Revisar el cálculo de la planilla de marzo", ticket = "SOP-48213", modo = "lectura" });
        r.EnsureSuccessStatusCode();
        var creada = await r.Content.ReadFromJsonAsync<JsonElement>();
        var url = new Uri(creada.GetProperty("url").GetString()!);
        Assert.Equal("comercialandina.localhost", url.Host);
        Assert.Equal(HttpStatusCode.Conflict, (await p.PostAsJsonAsync("/api/platform/support-sessions", new { tenantId = demo, motivo = "Revisar el cálculo de la planilla de marzo", ticket = "SOP-1", modo = "lectura" })).StatusCode);

        var token = HttpUtility.ParseQueryString(url.Query)["token"];
        var app = Cliente("comercialandina.localhost");
        var entrada = await (await app.PostAsJsonAsync("/api/auth/soporte", new { codigo = token })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("lectura", entrada.GetProperty("soporte").GetProperty("modo").GetString());
        // Vence a los 60 minutos, en UTC (la sesión y el token).
        Assert.InRange(entrada.GetProperty("soporte").GetProperty("expira").GetDateTimeOffset(), DateTimeOffset.UtcNow.AddMinutes(58), DateTimeOffset.UtcNow.AddMinutes(61));
        Assert.InRange(DateTimeOffset.FromUnixTimeSeconds(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(entrada.GetProperty("token").GetString()).Payload.Expiration!.Value), DateTimeOffset.UtcNow.AddMinutes(58), DateTimeOffset.UtcNow.AddMinutes(61));
        app.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", entrada.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/api/colaboradores")).StatusCode);
        Assert.Equal("Administrador", (await app.GetFromJsonAsync<JsonElement>("/api/me/context")).GetProperty("usuario").GetProperty("rol").GetString());
        var escribir = await app.PostAsJsonAsync("/api/reportes", new { plantilla = "Maestro de empleados", nombre = "Desde soporte" });
        Assert.Equal(HttpStatusCode.Forbidden, escribir.StatusCode);
        Assert.Equal("SUPPORT_READ_ONLY", (await escribir.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
        // El enlace es de un solo uso.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente("comercialandina.localhost").PostAsJsonAsync("/api/auth/soporte", new { codigo = token })).StatusCode);
        // Queda en el historial del tenant y se avisa a sus administradores.
        var admin = await api.ClienteDe(ApiFixture.Admin);
        Assert.Contains("Acceso de soporte", (await admin.GetFromJsonAsync<JsonElement>("/api/cambios")).GetRawText());
        Assert.Contains("soporte", UltimoCorreo(ApiFixture.Admin)!.Asunto, StringComparison.OrdinalIgnoreCase);
        // Terminar desde la consola: el token deja de servir.
        (await p.DeleteAsync($"/api/platform/support-sessions/{creada.GetProperty("id").GetString()}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/api/colaboradores")).StatusCode);
    }

    [Fact]
    public async Task Planes_flags_anuncios_catalogos_y_salud()
    {
        var p = await Plataforma();
        // Planes: crear, no duplicar, no borrar con tenants.
        var nuevo = await p.PostAsJsonAsync("/api/platform/plans", new { codigo = "PYME", nombre = "Pyme", precioPorColaborador = 8.5m, limiteEmpresas = 1, limiteAlmacenamientoGb = 5, limiteAdmins = 2, limiteApiPorMinuto = 0, modulosIncluidos = new[] { "COMMS" } });
        Assert.Equal(HttpStatusCode.Created, nuevo.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await p.PostAsJsonAsync("/api/platform/plans", new { codigo = "PYME", nombre = "Pyme", precioPorColaborador = 8.5m })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await p.DeleteAsync("/api/platform/plans/PRO")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await p.DeleteAsync("/api/platform/plans/PYME")).StatusCode);

        // Flag: solo con el módulo dueño contratado; el tenant lo ve en su contexto.
        (await p.PutAsJsonAsync($"/api/platform/feature-flags/firma_masiva_v2/tenants/{AprovisionadorTenants.ComercialAndina}", new { activo = true })).EnsureSuccessStatusCode();
        var admin = await api.ClienteDe(ApiFixture.Admin);
        try
        {
            Assert.Contains("firma_masiva_v2", (await admin.GetFromJsonAsync<JsonElement>("/api/me/context")).GetProperty("funciones").EnumerateArray().Select(x => x.GetString()));
            Assert.True((await p.GetFromJsonAsync<JsonElement>("/api/platform/feature-flags")).EnumerateArray().First(f => f.GetProperty("clave").GetString() == "firma_masiva_v2").GetProperty("activos").GetInt32() >= 1);
        }
        finally { (await p.PutAsJsonAsync($"/api/platform/feature-flags/firma_masiva_v2/tenants/{AprovisionadorTenants.ComercialAndina}", new { activo = false })).EnsureSuccessStatusCode(); }

        // Anuncio publicado para Pro: los administradores del tenant lo reciben; los colaboradores no.
        var an = await p.PostAsJsonAsync("/api/platform/announcements", new { titulo = "Mantenimiento programado", cuerpo = "El sábado de 02:00 a 04:00.", tipo = "Mantenimiento", planes = new[] { "PRO" }, canal = "banner", publicar = true });
        an.EnsureSuccessStatusCode();
        var idAnuncio = (await an.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        try
        {
            Assert.Contains((await admin.GetFromJsonAsync<JsonElement>("/api/me/context")).GetProperty("anuncios").EnumerateArray(), a => a.GetProperty("titulo").GetString() == "Mantenimiento programado");
            Assert.Equal(0, (await (await api.ClienteDe(ApiFixture.Colaborador)).GetFromJsonAsync<JsonElement>("/api/me/context")).GetProperty("anuncios").GetArrayLength());
        }
        finally { await p.DeleteAsync($"/api/platform/announcements/{idAnuncio}"); }

        // Catálogo global: versión con diferencias; la misma versión no se repite.
        async Task<HttpResponseMessage> Subir(string version, string csv)
        {
            var form = new MultipartFormDataContent { { new StringContent(version), "version" }, { new StringContent("2027-01-01"), "vigenteDesde" }, { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "archivo", "monedas.csv" } };
            return await p.PostAsync("/api/platform/catalogs/monedas/versions", form);
        }
        var v1 = await Subir("v2027.1", "codigo,nombre,simbolo\nPEN,Sol,S/\nUSD,Dólar,$");
        Assert.Equal(HttpStatusCode.Created, v1.StatusCode);
        var v2 = await (await Subir("v2027.2", "codigo,nombre,simbolo\nPEN,Sol,S/\nUSD,Dólar estadounidense,US$\nEUR,Euro,€")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, v2.GetProperty("agregados").GetInt32());
        Assert.Equal(1, v2.GetProperty("modificados").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Subir("v2027.1", "codigo,nombre,simbolo\nPEN,Sol,S/")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Subir("v2027.9", "otra,cosa\n1,2")).StatusCode);

        // Salud: bases con su versión de esquema y métricas por tenant.
        var salud = await p.GetFromJsonAsync<JsonElement>("/api/platform/health");
        Assert.True(salud.GetProperty("basesDatos").GetArrayLength() >= 2);
        Assert.All(salud.GetProperty("basesDatos").EnumerateArray(), b => Assert.Equal("Al día", b.GetProperty("estado").GetString()));
    }

    [Fact]
    public async Task Alta_Enterprise_con_BD_dedicada_dominio_propio_y_mudanza_desde_la_consola()
    {
        var p = await Plataforma();

        // Pro: ni BD dedicada en el alta ni dominio propio.
        var r = await p.PostAsJsonAsync("/api/platform/tenants", new
        {
            razonSocial = "Pro S.A.C.", ruc = "20123456789", slug = $"pr{Guid.NewGuid():N}"[..12], plan = "PRO", modulos = new[] { "PAYROLL" }, precioPorColaborador = 18m,
            admin = new { nombres = "Ana", apellidos = "Pro", correo = "ana@pro.pe" }, opciones = new { datosEjemplo = false, bdDedicada = true },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        var (pro, proSlug, _) = await Alta(p, "pro");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await p.PostAsJsonAsync($"/api/platform/tenants/{pro}/domains", new { host = "rrhh.pro-test.pe" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await p.PostAsJsonAsync($"/api/platform/tenants/{pro}/database/dedicate", new { confirmacion = proSlug })).StatusCode);

        // Enterprise con BD dedicada desde el wizard.
        var (id, slug, correo) = await Alta(p, "ent", plan: "ENTERPRISE", bdDedicada: true);
        var detalle = await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{id}");
        Assert.Equal("Dedicada", detalle.GetProperty("baseDatos").GetString());
        Assert.Contains("DEDICATED_DB", detalle.GetProperty("funciones").EnumerateArray().Select(x => x.GetString()));
        Assert.NotNull(UltimoCorreo(correo));
        Assert.Equal(HttpStatusCode.Conflict, (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/database/dedicate", new { confirmacion = slug })).StatusCode);
        var salud = await p.GetFromJsonAsync<JsonElement>("/api/platform/health");
        Assert.Contains(salud.GetProperty("basesDatos").EnumerateArray(), b => b.GetProperty("nombre").GetString()!.StartsWith($"Dedicada · {slug} · ") && b.GetProperty("estado").GetString() == "Al día");

        // Dominio propio desde la consola: agregar, verificar con el DNS, principal y quitar.
        var d = await (await p.PostAsJsonAsync($"/api/platform/tenants/{id}/domains", new { host = $"rrhh.{slug}.pe" })).Content.ReadFromJsonAsync<JsonElement>();
        var dominioId = d.GetProperty("id").GetInt32();
        var dns = api.Services.GetRequiredService<ResolutorDnsSimulado>();
        foreach (var x in d.GetProperty("registros").EnumerateArray()) dns.Publicar(x.GetProperty("tipo").GetString()!, x.GetProperty("nombre").GetString()!, x.GetProperty("valor").GetString()!);
        var v = await (await p.PostAsync($"/api/platform/tenants/{id}/domains/{dominioId}/verify", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("verified", v.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await p.PostAsync($"/api/platform/tenants/{id}/domains/{dominioId}/primary", null)).StatusCode);
        Assert.Equal($"rrhh.{slug}.pe", (await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{id}")).GetProperty("dominioPrincipal").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await p.DeleteAsync($"/api/platform/tenants/{id}/domains/{dominioId}")).StatusCode);

        // Mudanza de un tenant existente (se activa con el flag del plan o pasando a Enterprise).
        Assert.Equal(HttpStatusCode.OK, (await p.PatchAsJsonAsync($"/api/platform/tenants/{pro}", new { plan = "ENTERPRISE" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await p.PostAsJsonAsync($"/api/platform/tenants/{pro}/database/dedicate", new { confirmacion = "otro" })).StatusCode);
        r = await p.PostAsJsonAsync($"/api/platform/tenants/{pro}/database/dedicate", new { confirmacion = proSlug });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        var job = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("provisioningJobId").GetString();
        JsonElement estado = default;
        for (var i = 0; i < 240; i++)
        {
            estado = await p.GetFromJsonAsync<JsonElement>($"/api/platform/provisioning/{job}");
            if (estado.GetProperty("estado").GetString() != "running") break;
            await Task.Delay(500);
        }
        Assert.True(estado.GetProperty("estado").GetString() == "succeeded", estado.GetRawText());
        Assert.Equal(6, estado.GetProperty("pasos").GetArrayLength());
        Assert.Equal("Dedicada", (await p.GetFromJsonAsync<JsonElement>($"/api/platform/tenants/{pro}")).GetProperty("baseDatos").GetString());
    }
}
