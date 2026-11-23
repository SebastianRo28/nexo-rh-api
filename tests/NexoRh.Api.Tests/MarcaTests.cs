using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Tests;

/// <summary>Personaliza tu marca (etapa 6.3): versiones, contraste WCAG, imágenes seguras, marca pública y correos.</summary>
[Collection("api")]
public class MarcaTests(ApiFixture api)
{
    private const string HostA = "comercialandina.localhost";

    private HttpClient Cliente(string host = HostA) => api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}") });

    private static object Datos(string primario = "#0F766E", string? sidebar = null, string tipografia = "Inter", object? logos = null, string nivel1 = "Gerencia") => new
    {
        nombrePortal = "Mi Andino",
        logos = logos ?? new { },
        login = new { titulo = "Bienvenido a Comercial Andina", mensaje = "Ingresa con tu correo corporativo", ayuda = "¿Problemas? rrhh@comercialandina.pe", mostrarPoweredBy = false },
        colores = new { primario, sidebar, topbar = (string?)null, acento = "#F59E0B" },
        tipografia,
        radio = "redondeado",
        terminologia = new { nivel1, nivel2 = "Departamento", nivel3 = "Equipo", colaborador = "Trabajador", recinto = "Sede" },
        correos = new { remitente = "RR.HH. Comercial Andina", firma = "Equipo de Gestión de Personas" },
    };

    private async Task<HttpClient> Admin() => await api.ClienteDe(ApiFixture.Admin);

    /// <summary>Deja la marca por defecto para no afectar a otras pruebas.</summary>
    private async Task PorDefecto(HttpClient admin) =>
        (await admin.PostAsJsonAsync("/api/marca", new { datos = new MarcaDatos(), nota = "Valores por defecto" })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Publicar_versiones_y_restaurar()
    {
        var admin = await Admin();
        var antes = await Cliente().GetFromJsonAsync<JsonElement>("/api/publico/marca");
        try
        {
            var r = await admin.PostAsJsonAsync("/api/marca", new { datos = Datos(), nota = "Colores de la marca" });
            r.EnsureSuccessStatusCode();
            var edicion = await r.Content.ReadFromJsonAsync<JsonElement>();
            var version = edicion.GetProperty("version").GetInt32();
            Assert.Equal(antes.GetProperty("version").GetInt32() + 1, version);

            // La marca pública del host la refleja (y el login usa sus textos).
            var c = Cliente();
            var resp = await c.GetAsync("/api/publico/marca");
            var publica = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("#0F766E", publica.GetProperty("colores").GetProperty("primario").GetString());
            Assert.Equal("Inter", publica.GetProperty("tipografia").GetString());
            Assert.Equal("Mi Andino", publica.GetProperty("nombrePortal").GetString());
            Assert.Equal("Gerencia", publica.GetProperty("terminologia").GetProperty("nivel1").GetString());
            // Ocultar "Powered by Nexo RH" es de Enterprise: sigue visible.
            Assert.True(publica.GetProperty("login").GetProperty("mostrarPoweredBy").GetBoolean());
            Assert.Equal("Bienvenido a Comercial Andina", (await c.GetFromJsonAsync<JsonElement>("/api/publico/entrada")).GetProperty("tenant").GetProperty("titulo").GetString());

            // Cacheable con su versión.
            Assert.Contains("max-age", resp.Headers.CacheControl!.ToString());
            var condicional = new HttpRequestMessage(HttpMethod.Get, "/api/publico/marca");
            condicional.Headers.IfNoneMatch.Add(resp.Headers.ETag!);
            Assert.Equal(HttpStatusCode.NotModified, (await c.SendAsync(condicional)).StatusCode);

            // Otra versión y vuelta a la anterior (como versión nueva).
            (await admin.PostAsJsonAsync("/api/marca", new { datos = Datos("#7C2D12") })).EnsureSuccessStatusCode();
            var restaurada = await (await admin.PostAsync($"/api/marca/versiones/{version}/restaurar", null)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(version + 2, restaurada.GetProperty("version").GetInt32());
            Assert.Equal("#0F766E", restaurada.GetProperty("datos").GetProperty("colores").GetProperty("primario").GetString());
            Assert.Equal($"Restaurada desde la versión {version}", restaurada.GetProperty("versiones")[0].GetProperty("nota").GetString());

            // La otra empresa no cambió.
            var b = await Cliente("noexiste.localhost").GetFromJsonAsync<JsonElement>("/api/publico/marca");
            Assert.Equal(0, b.GetProperty("version").GetInt32());
        }
        finally { await PorDefecto(admin); }
    }

    [Fact]
    public async Task Colores_sin_contraste_se_rechazan_con_una_sugerencia()
    {
        var admin = await Admin();
        var r = await admin.PostAsJsonAsync("/api/marca", new { datos = Datos("#FFD84D", sidebar: "#9FD3FF", tipografia: "Comic Sans") });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errores = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        var mensaje = errores.GetProperty("colores.primario")[0].GetString()!;
        Assert.Contains("contraste", mensaje);
        var sugerido = System.Text.RegularExpressions.Regex.Match(mensaje, "#[0-9A-F]{6}").Value;
        Assert.True(ReglasMarca.Contraste(sugerido, "#FFFFFF") >= 4.5);
        Assert.True(errores.TryGetProperty("colores.sidebar", out _));
        Assert.True(errores.TryGetProperty("tipografia", out _));
        // Solo administradores.
        var colaborador = await api.ClienteDe(ApiFixture.Colaborador);
        Assert.Equal(HttpStatusCode.Forbidden, (await colaborador.GetAsync("/api/marca")).StatusCode);
    }

    [Fact]
    public async Task Imagenes_validadas_limpias_y_solo_de_su_empresa()
    {
        var admin = await Admin();
        async Task<HttpResponseMessage> Subir(byte[] datos, string nombre, string tipo = "horizontal")
        {
            var form = new MultipartFormDataContent { { new ByteArrayContent(datos), "archivo", nombre }, { new StringContent(tipo), "tipo" } };
            return await admin.PostAsync("/api/marca/archivos", form);
        }
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
        var r = await Subir(png, "logo.png");
        r.EnsureSuccessStatusCode();
        var subido = await r.Content.ReadFromJsonAsync<JsonElement>();
        var url = subido.GetProperty("url").GetString()!;
        var archivo = await Cliente().GetAsync(url);
        Assert.Equal("image/png", archivo.Content.Headers.ContentType!.MediaType);
        Assert.Contains("immutable", archivo.Headers.CacheControl!.ToString());
        // Una imagen de esta empresa no se sirve en el enlace de otra.
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente("app.localhost").GetAsync(url)).StatusCode);

        // SVG: sin scripts, sin manejadores on* ni enlaces externos.
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" onload="alert(1)"><script>alert(2)</script><a xlink:href="javascript:alert(3)"><rect width="10" height="10" fill="#0F766E"/></a><image href="https://malo.example/x.png"/></svg>""";
        var rs = await Subir(Encoding.UTF8.GetBytes(svg), "logo.svg");
        rs.EnsureSuccessStatusCode();
        var limpio = await (await Cliente().GetAsync((await rs.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString())).Content.ReadAsStringAsync();
        Assert.DoesNotContain("script", limpio);
        Assert.DoesNotContain("onload", limpio);
        Assert.DoesNotContain("javascript:", limpio);
        Assert.DoesNotContain("malo.example", limpio);
        Assert.Contains("<rect", limpio);

        // Otros archivos, no.
        Assert.Equal(HttpStatusCode.BadRequest, (await Subir(Encoding.UTF8.GetBytes("hola"), "logo.png")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Subir(Encoding.UTF8.GetBytes(svg), "fondo.svg", "login")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Subir(png, "x.png", "otro")).StatusCode);

        // No se publica una marca con imágenes que no son de la empresa.
        var ajena = Guid.NewGuid();
        var rp = await admin.PostAsJsonAsync("/api/marca", new { datos = Datos(logos: new { horizontal = ajena }) });
        Assert.Equal(HttpStatusCode.BadRequest, rp.StatusCode);
        try
        {
            (await admin.PostAsJsonAsync("/api/marca", new { datos = Datos(logos: new { horizontal = subido.GetProperty("id").GetGuid() }) })).EnsureSuccessStatusCode();
            var publica = await Cliente().GetFromJsonAsync<JsonElement>("/api/publico/marca");
            Assert.Equal(url, publica.GetProperty("logos").GetProperty("horizontal").GetString());
        }
        finally { await PorDefecto(admin); }
    }

    [Fact]
    public async Task Los_correos_llevan_la_marca()
    {
        var admin = await Admin();
        try
        {
            (await admin.PostAsJsonAsync("/api/marca", new { datos = Datos("#7C2D12") })).EnsureSuccessStatusCode();
            (await Cliente().PostAsJsonAsync("/api/auth/olvide", new { correo = ApiFixture.Supervisor })).EnsureSuccessStatusCode();
            using var scope = api.Services.CreateScope();
            var correo = await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Correos.AsNoTracking()
                .Where(c => c.Para == ApiFixture.Supervisor).OrderByDescending(c => c.Id).FirstAsync();
            Assert.Contains("background:#7C2D12", correo.Html);
            Assert.Contains(WebUtility.HtmlEncode("Equipo de Gestión de Personas"), correo.Html);
        }
        finally { await PorDefecto(admin); }
    }
}
