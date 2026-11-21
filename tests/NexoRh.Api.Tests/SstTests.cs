using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NexoRh.Api.Tests;

[Collection("api")]
public class SstTests(ApiFixture api)
{
    private static MultipartFormDataContent Evidencia(string fecha, string descripcion, (string nombre, byte[] bytes)? archivo = null)
    {
        var f = new MultipartFormDataContent { { new StringContent(fecha), "fecha" }, { new StringContent(descripcion), "descripcion" } };
        if (archivo is { } a)
        {
            var c = new ByteArrayContent(a.bytes);
            c.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            f.Add(c, "archivo", a.nombre);
        }
        return f;
    }

    private static JsonElement Por(JsonElement datos, string lista, string campo, string valor) =>
        datos.GetProperty(lista).EnumerateArray().First(x => x.GetProperty(campo).GetString() == valor);

    [Fact]
    public async Task Solo_administradores_y_estados_coherentes()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.ClienteDe(ApiFixture.Supervisor)).GetAsync("/api/sst")).StatusCode);
        var datos = await (await api.ClienteDe(ApiFixture.Admin)).GetFromJsonAsync<JsonElement>("/api/sst");
        Assert.True(datos.GetProperty("cumplimientos").GetArrayLength() >= 9);
        Assert.Equal(10, datos.GetProperty("biblioteca").GetArrayLength());
        var estados = datos.GetProperty("asignaciones").EnumerateArray().Select(a => a.GetProperty("estado").GetString()).ToHashSet();
        Assert.Contains("Vigente", estados);
        Assert.Contains("Vencida", estados);
        // Los totales del requisito cuadran con sus asignaciones activas.
        foreach (var r in datos.GetProperty("requisitos").EnumerateArray())
            Assert.Equal(r.GetProperty("asignaciones").GetInt32(),
                r.GetProperty("sinEvidencia").GetInt32() + r.GetProperty("vigentes").GetInt32() + r.GetProperty("porVencer").GetInt32() + r.GetProperty("vencidas").GetInt32());
    }

    [Fact]
    public async Task Evidencia_con_vigencia_y_archivo()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/sst");
        var cu = Por(datos, "cumplimientos", "nombre", "Capacitaciones obligatorias").GetProperty("id").GetInt32();
        (await admin.PostAsJsonAsync("/api/sst/requisitos", new { nombre = "Uso de extintores QA", cumplimientoId = cu, aplicaA = "Colaboradores", vigenciaMeses = 12, colaboradorIds = new[] { 0 } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/sst/requisitos", new { nombre = "Uso de extintores QA", cumplimientoId = cu, aplicaA = "Colaboradores" })).StatusCode);

        datos = await admin.GetFromJsonAsync<JsonElement>("/api/sst");
        var asig = Por(datos, "asignaciones", "requisito", "Uso de extintores QA");
        Assert.Equal("Sin evidencia", asig.GetProperty("estado").GetString());
        Assert.False(Por(datos, "cumplimientos", "nombre", "Capacitaciones obligatorias").GetProperty("cumple").GetBoolean());
        var id = asig.GetProperty("id").GetInt32();

        // Fecha futura o archivo que no es lo que dice: rechazados.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/sst/asignaciones/{id}/evidencias", Evidencia("17/03/2026", "Acta 1"))).StatusCode);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/sst/asignaciones/{id}/evidencias", Evidencia("01/03/2026", "Acta", ("acta.pdf", png)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/sst/asignaciones/{id}/evidencias", Evidencia("01/03/2026", "Acta", ("acta.exe", png)))).StatusCode);

        // Hace un año: vence hoy → por vencer.
        var r1 = await (await admin.PostAsync($"/api/sst/asignaciones/{id}/evidencias", Evidencia("16/03/2025", "Acta 2025"))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Por vencer", r1.GetProperty("asignacion").GetProperty("estado").GetString());
        Assert.Equal("16/03/2026", r1.GetProperty("asignacion").GetProperty("vence").GetString());

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% acta de capacitación\n%%EOF");
        var r2 = await admin.PostAsync($"/api/sst/asignaciones/{id}/evidencias", Evidencia("01/03/2026", "Acta 2026", ("acta.pdf", pdf)));
        r2.EnsureSuccessStatusCode();
        var det = await r2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Vigente", det.GetProperty("asignacion").GetProperty("estado").GetString());
        Assert.Equal(2, det.GetProperty("evidencias").GetArrayLength());
        var evId = det.GetProperty("evidencias")[0].GetProperty("id").GetInt32();
        var descarga = await admin.GetAsync($"/api/sst/evidencias/{evId}/archivo");
        Assert.Equal("application/pdf", descarga.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdf, await descarga.Content.ReadAsByteArrayAsync());

        // Con evidencias no se elimina el requisito; finalizar la asignación la saca de los totales.
        var reqId = asig.GetProperty("requisitoId").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/sst/requisitos/{reqId}")).StatusCode);
        (await admin.PostAsync($"/api/sst/asignaciones/{id}/finalizar", null)).EnsureSuccessStatusCode();
        var req = Por(await admin.GetFromJsonAsync<JsonElement>("/api/sst"), "requisitos", "nombre", "Uso de extintores QA");
        Assert.Equal(0, req.GetProperty("asignaciones").GetInt32());
    }

    [Fact]
    public async Task Biblioteca_de_cumplimientos()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var r = await admin.PostAsJsonAsync("/api/sst/cumplimientos/biblioteca", new { nombres = new[] { "Mapa de riesgos" } });
        r.EnsureSuccessStatusCode();
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/sst");
        Assert.True(Por(datos, "biblioteca", "nombre", "Mapa de riesgos").GetProperty("agregado").GetBoolean());
        var cu = Por(datos, "cumplimientos", "nombre", "Mapa de riesgos");
        Assert.False(cu.GetProperty("cumple").GetBoolean()); // sin asignaciones todavía
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/sst/cumplimientos/biblioteca", new { nombres = new[] { "Mapa de riesgos" } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/sst/cumplimientos/{cu.GetProperty("id").GetInt32()}")).StatusCode);
        var conRequisitos = Por(datos, "cumplimientos", "nombre", "Entrega de EPP").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/sst/cumplimientos/{conRequisitos}")).StatusCode);
    }
}
