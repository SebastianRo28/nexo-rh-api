using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Tests;

/// <summary>Cobro automático con Izipay (en modo simulado): tarjeta, Pagar ahora, rechazos, reintentos, morosidad, firma e IPN.</summary>
[Collection("api")]
public class PagosTests(ApiFixture api)
{
    private async Task<Tenant> NuevoTenant()
    {
        var id = Guid.NewGuid();
        return await api.Services.GetRequiredService<AprovisionadorTenants>().AsegurarAsync(
            new Tenant { Id = id, Slug = $"pg{id:N}"[..14], Nombre = "Pagos", RazonSocial = "Pagos de Prueba S.A.C.", Ruc = "20555555556" }, datosDemo: true);
    }

    private async Task<HttpClient> Admin(Tenant t)
    {
        var c = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{t.Slug}.localhost") });
        var r = await c.PostAsJsonAsync("/api/auth/login", new { correo = ApiFixture.Admin, password = api.PasswordSemilla });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<Sesion>())!.Token);
        return c;
    }

    private async Task<Factura> NuevaFactura(Tenant t, DateOnly emitida, DateOnly vence)
    {
        using var scope = api.Services.CreateScope();
        var cat = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var f = new Factura { TenantId = t.Id, Numero = $"P-{Guid.NewGuid():N}"[..12], Periodo = "2026-09", Emitida = emitida, Vence = vence, Colaboradores = 10, Subtotal = 180, Igv = 32.40m };
        cat.Facturas.Add(f);
        await cat.SaveChangesAsync();
        return f;
    }

    [Fact]
    public async Task Registra_la_tarjeta_paga_ahora_y_no_cobra_dos_veces()
    {
        var t = await NuevoTenant();
        var admin = await Admin(t);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var f = await NuevaFactura(t, hoy, hoy.AddDays(20));

        var vacia = await admin.GetFromJsonAsync<JsonElement>("/api/cuenta/pagos");
        Assert.Equal("Simulado", vacia.GetProperty("modo").GetString());
        Assert.Equal(JsonValueKind.Null, vacia.GetProperty("tarjeta").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/cuenta/facturas/{f.Numero}/pagar", null)).StatusCode);   // sin tarjeta

        // Sin autorizar el cargo automático no se registra.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/cuenta/pagos/tarjeta", new { autorizo = false, simulada = "aprobada" })).StatusCode);
        var reg = await admin.PostAsJsonAsync("/api/cuenta/pagos/tarjeta", new { autorizo = true, simulada = "aprobada" });
        Assert.True(reg.IsSuccessStatusCode, await reg.Content.ReadAsStringAsync());
        var tarjeta = await reg.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("411111******1111", tarjeta.GetProperty("tarjeta").GetString());
        Assert.False(tarjeta.TryGetProperty("token", out _));

        var pago = await admin.PostAsync($"/api/cuenta/facturas/{f.Numero}/pagar", null);
        Assert.True(pago.IsSuccessStatusCode, await pago.Content.ReadAsStringAsync());
        var cobro = await pago.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aprobado", cobro.GetProperty("estado").GetString());
        Assert.Equal(212.40m, cobro.GetProperty("monto").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/cuenta/facturas/{f.Numero}/pagar", null)).StatusCode);   // ya pagada

        var cuenta = await admin.GetFromJsonAsync<JsonElement>("/api/cuenta");
        var fila = cuenta.GetProperty("facturas").EnumerateArray().First(x => x.GetProperty("numero").GetString() == f.Numero);
        Assert.Equal("Pagada", fila.GetProperty("estado").GetString());
        Assert.Equal("Tarjeta VS •••• 1111", fila.GetProperty("medioPago").GetString());
        Assert.Contains("Recibimos tu pago", await UltimoAsunto(t));

        // El token queda cifrado en el catálogo.
        using var scope = api.Services.CreateScope();
        var m = await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().MediosPago.AsNoTracking().FirstAsync(x => x.TenantId == t.Id && x.Activo);
        Assert.DoesNotContain("sim-", m.TokenCifrado);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/cuenta/pagos/tarjeta")).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await admin.GetFromJsonAsync<JsonElement>("/api/cuenta/pagos")).GetProperty("tarjeta").ValueKind);
    }

    [Fact]
    public async Task Un_rechazo_programa_reintentos_y_la_cuenta_morosa_se_reactiva_al_pagar()
    {
        var t = await NuevoTenant();
        var admin = await Admin(t);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var scope = api.Services.CreateScope())
        {
            var cat = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            foreach (var x in await cat.Facturas.Where(x => x.TenantId == t.Id).ToListAsync()) x.Estado = "Pagada";
            await cat.SaveChangesAsync();
        }
        var f = await NuevaFactura(t, hoy.AddDays(-12), hoy.AddDays(-2));    // vencida: la cuenta pasa a morosa
        (await admin.PostAsJsonAsync("/api/cuenta/pagos/tarjeta", new { autorizo = true, simulada = "rechazada" })).EnsureSuccessStatusCode();

        // El ciclo de vida intenta el cobro automático (rechazado) y marca la cuenta morosa.
        using (var scope = api.Services.CreateScope())
        {
            var r = await scope.ServiceProvider.GetRequiredService<CicloVidaTenants>().EjecutarAsync(DateTime.UtcNow, t.Id);
            Assert.Equal(1, r.Morosos);
            var cobros = await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Cobros.AsNoTracking().Where(c => c.TenantId == t.Id).ToListAsync();
            Assert.Equal("Rechazado", Assert.Single(cobros).Estado);
            // En la misma corrida no se reintenta: el siguiente es a los 3 días.
            await scope.ServiceProvider.GetRequiredService<CicloVidaTenants>().EjecutarAsync(DateTime.UtcNow, t.Id);
            Assert.Single(await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Cobros.AsNoTracking().Where(c => c.TenantId == t.Id).ToListAsync());
        }
        var fila = (await admin.GetFromJsonAsync<JsonElement>("/api/cuenta")).GetProperty("facturas").EnumerateArray().First(x => x.GetProperty("numero").GetString() == f.Numero);
        Assert.Equal(hoy.AddDays(3).ToString("dd/MM/yyyy"), fila.GetProperty("proximoCobro").GetString());
        Assert.StartsWith("Rechazado", fila.GetProperty("ultimoCobro").GetString());
        Assert.Contains("No pudimos cobrar", await UltimoAsunto(t));

        // Registra otra tarjeta y paga: la factura queda pagada y la cuenta vuelve a estar activa.
        (await admin.PostAsJsonAsync("/api/cuenta/pagos/tarjeta", new { autorizo = true, simulada = "aprobada" })).EnsureSuccessStatusCode();
        var pago = await (await admin.PostAsync($"/api/cuenta/facturas/{f.Numero}/pagar", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aprobado", pago.GetProperty("estado").GetString());
        using (var scope = api.Services.CreateScope())
            Assert.Equal(EstadosTenant.Activo, (await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Tenants.AsNoTracking().FirstAsync(x => x.Id == t.Id)).Estado);
    }

    [Fact]
    public void Calendario_de_reintentos_el_dia_de_emision_y_a_los_3_y_7_dias()
    {
        var f = new Factura { Id = 1, Estado = "Pendiente", Emitida = new DateOnly(2026, 11, 1) };
        Cobro Auto(int dia, string estado = EstadosCobro.Rechazado) => new() { FacturaId = 1, Origen = OrigenesCobro.Automatico, Estado = estado, CreadoEn = new DateTime(2026, 11, dia, 10, 0, 0, DateTimeKind.Utc) };
        Assert.Equal(new DateOnly(2026, 11, 1), CobrosFacturas.ProximoIntento(f, []));
        Assert.Equal(new DateOnly(2026, 11, 5), CobrosFacturas.ProximoIntento(f, [Auto(2)]));
        Assert.Equal(new DateOnly(2026, 11, 9), CobrosFacturas.ProximoIntento(f, [Auto(2), Auto(5)]));
        Assert.Null(CobrosFacturas.ProximoIntento(f, [Auto(2), Auto(5), Auto(9)]));                       // sin más intentos automáticos
        Assert.Null(CobrosFacturas.ProximoIntento(f, [Auto(2, EstadosCobro.PorConfirmar)]));               // uno por confirmar bloquea
        // “Pagar ahora” fallido no consume los intentos automáticos.
        Assert.Equal(new DateOnly(2026, 11, 1), CobrosFacturas.ProximoIntento(f, [new Cobro { FacturaId = 1, Origen = OrigenesCobro.Cliente, Estado = EstadosCobro.Rechazado, CreadoEn = DateTime.UtcNow }]));
        Assert.Null(CobrosFacturas.ProximoIntento(new Factura { Estado = "Pagada" }, []));
    }

    [Fact]
    public void Valida_la_firma_HMAC_e_interpreta_la_respuesta_de_Izipay()
    {
        var pasarela = new PasarelaIzipay(new HttpClient(), Options.Create(new OpcionesIzipay { Modo = "Sandbox", ClaveHash = "clave-hash-de-prueba" }),
            new MemoryCache(new MemoryCacheOptions()), new EntornoPrueba(), NullLogger<PasarelaIzipay>.Instance);
        var payload = """{"code":"00","message":"Operación exitosa","response":{"order":[{"orderNumber":"NX0000000001","stateMessage":"Autorizado","codeAuth":"831000","referenceNumber":"6330602"}]}}""";
        var firma = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes("clave-hash-de-prueba"), Encoding.UTF8.GetBytes(payload)));
        Assert.True(pasarela.FirmaValida(payload, firma));
        Assert.False(pasarela.FirmaValida(payload.Replace("831000", "000000"), firma));
        Assert.False(pasarela.FirmaValida(payload, "no-es-base64"));

        var r = pasarela.Interpretar(new JsonObject { ["code"] = "00", ["payloadHttp"] = payload, ["signature"] = firma, ["response"] = JsonNode.Parse(payload)!["response"]!.DeepClone() });
        Assert.Equal(EstadosCobro.Aprobado, r.Estado);
        Assert.Equal("831000", r.CodigoAutorizacion);
        // Firma alterada: no se confía en la respuesta.
        Assert.Equal(EstadosCobro.PorConfirmar, pasarela.Interpretar(new JsonObject { ["code"] = "00", ["payloadHttp"] = payload, ["signature"] = Convert.ToBase64String(new byte[32]) }).Estado);
        Assert.Equal(EstadosCobro.Rechazado, pasarela.Interpretar(new JsonObject { ["code"] = "116", ["messageUser"] = "Fondos insuficientes" }).Estado);
    }

    [Fact]
    public async Task La_IPN_rechaza_las_notificaciones_sin_firma_valida()
    {
        var c = api.CreateClient();
        var r = await c.PostAsJsonAsync("/api/publico/pagos/izipay/ipn", new { code = "00", payloadHttp = "{\"code\":\"00\"}", signature = "AAAA" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    private async Task<string> UltimoAsunto(Tenant t)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Correos.AsNoTracking().Where(x => x.TenantId == t.Id).OrderByDescending(x => x.Id).Select(x => x.Asunto).FirstAsync();
    }

    private class EntornoPrueba : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Pruebas";
        public string ApplicationName { get; set; } = "NexoRh.Api";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
