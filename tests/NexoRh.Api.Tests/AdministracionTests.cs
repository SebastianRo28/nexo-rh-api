using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexoRh.Api.Tests;

[Collection("api")]
public class AdministracionTests(ApiFixture api)
{
    private static JsonElement Por(JsonElement datos, string lista, string campo, string valor) =>
        datos.GetProperty(lista).EnumerateArray().First(x => x.GetProperty(campo).GetString() == valor);

    [Fact]
    public async Task Solo_administradores()
    {
        var sup = await api.ClienteDe(ApiFixture.Supervisor);
        foreach (var url in new[] { "/api/activos", "/api/documentos", "/api/compensaciones", "/api/headcount" })
            Assert.Equal(HttpStatusCode.Forbidden, (await sup.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Activos_ordenes_e_historial()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/activos");
        Assert.Equal(16, datos.GetProperty("activos").GetArrayLength());
        // Pendiente con fecha de asignación pasada (01/03/2026 frente al 16/03/2026 del módulo).
        Assert.Equal("Atrasado", Por(datos, "ordenes", "para", "Ortiz Ramírez, Daniela").GetProperty("estado").GetString());
        Assert.Equal(4, (await admin.GetFromJsonAsync<JsonElement>("/api/activos/ACT-0042/historial")).GetArrayLength());

        // Configuración: categoría y producto nuevos.
        (await admin.PostAsJsonAsync("/api/activos/categorias", new { nombre = "Herramientas QA" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/activos/categorias", new { nombre = "Herramientas QA" })).StatusCode);
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/activos");
        var cat = Por(datos, "categorias", "nombre", "Herramientas QA").GetProperty("id").GetInt32();
        (await admin.PostAsJsonAsync("/api/activos/productos", new { nombre = "Multímetro QA", categoriaId = cat, marca = "Fluke" })).EnsureSuccessStatusCode();
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/activos");
        var prod = Por(datos, "productos", "nombre", "Multímetro QA").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/activos/categorias/{cat}")).StatusCode);

        // Una unidad en inventario y dos órdenes: la primera se atiende, la segunda no tiene stock.
        var codigos = await (await admin.PostAsJsonAsync("/api/activos", new { productoId = prod, cantidad = 1 })).Content.ReadFromJsonAsync<List<string>>();
        var codigo = Assert.Single(codigos!);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/activos/ordenes", new { productoId = prod, paraId = 13, asignarEl = "31-03-2026" })).StatusCode);
        var o1 = await (await admin.PostAsJsonAsync("/api/activos/ordenes", new { productoId = prod, paraId = 13, asignarEl = "31/03/2026" })).Content.ReadFromJsonAsync<int>();
        var o2 = await (await admin.PostAsJsonAsync("/api/activos/ordenes", new { productoId = prod, paraId = 14, asignarEl = "31/03/2026" })).Content.ReadFromJsonAsync<int>();
        var r = await admin.PostAsJsonAsync($"/api/activos/ordenes/{o1}/asignar", new { });
        r.EnsureSuccessStatusCode();
        Assert.Equal(codigo, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("activoCodigo").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/activos/ordenes/{o1}/asignar", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/activos/ordenes/{o2}/asignar", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/activos/ordenes/{o2}/rechazar", new { comentario = " " })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/activos/ordenes/{o2}/rechazar", new { comentario = "Sin stock" })).EnsureSuccessStatusCode();

        datos = await admin.GetFromJsonAsync<JsonElement>("/api/activos");
        var activo = Por(datos, "activos", "codigo", codigo);
        Assert.Equal("Asignado", activo.GetProperty("estado").GetString());
        Assert.Equal(13, activo.GetProperty("asignadoId").GetInt32());
        Assert.Equal("Rechazada", datos.GetProperty("ordenes").EnumerateArray().First(x => x.GetProperty("id").GetInt32() == o2).GetProperty("estado").GetString());

        // Cambio manual: "Asignado" exige colaborador; al pasar a mantenimiento se libera.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/activos/{codigo}", new { estado = "Asignado" })).StatusCode);
        (await admin.PutAsJsonAsync($"/api/activos/{codigo}", new { estado = "Mantenimiento", comentario = "Calibración" })).EnsureSuccessStatusCode();
        var hist = await admin.GetFromJsonAsync<JsonElement>($"/api/activos/{codigo}/historial");
        Assert.Equal(3, hist.GetArrayLength());
        Assert.Equal("Calibración", hist[0].GetProperty("comentario").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/activos/productos/{prod}")).StatusCode);
    }

    [Fact]
    public async Task Documentos_firma_y_papelera()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/documentos");
        Assert.Equal(25, datos.GetProperty("plantillas").GetArrayLength());
        Assert.Equal(2, datos.GetProperty("papelera").GetArrayLength());
        int Id(string parte) => datos.GetProperty("documentos").EnumerateArray().First(d => d.GetProperty("archivo").GetString()!.Contains(parte)).GetProperty("id").GetInt32();
        var practicante = Id("Contrato_de_practicante");   // revisión no aplica, firma de empresa pendiente
        var preaviso = Id("Carta_de_preaviso");            // revisión pendiente: aún no se puede firmar

        var lote = await (await admin.PostAsJsonAsync("/api/documentos/firmar", new { ids = new[] { practicante, preaviso } })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, lote.GetProperty("procesados").GetInt32());
        Assert.Equal(1, lote.GetProperty("omitidos").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/documentos/firmar", new { ids = Array.Empty<int>() })).StatusCode);

        var epp = Id("Acta_de_entrega_de_EPP");
        (await admin.PostAsJsonAsync("/api/documentos/ocultar", new { ids = new[] { epp }, oculto = true })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/documentos/eliminar", new { ids = new[] { epp } })).EnsureSuccessStatusCode();
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/documentos");
        Assert.Equal(3, datos.GetProperty("papelera").GetArrayLength());
        Assert.DoesNotContain(datos.GetProperty("documentos").EnumerateArray(), d => d.GetProperty("id").GetInt32() == epp);
        (await admin.PostAsync($"/api/documentos/papelera/{epp}/restaurar", null)).EnsureSuccessStatusCode();
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/documentos");
        var doc = datos.GetProperty("documentos").EnumerateArray().First(d => d.GetProperty("id").GetInt32() == epp);
        Assert.True(doc.GetProperty("oculto").GetBoolean());
        Assert.Equal("si", datos.GetProperty("documentos").EnumerateArray().First(d => d.GetProperty("id").GetInt32() == practicante).GetProperty("firmaEmpresa").GetString());

        var plantilla = datos.GetProperty("plantillas")[0];
        var pid = plantilla.GetProperty("id").GetInt32();
        (await admin.PostAsJsonAsync($"/api/documentos/plantillas/{pid}/estado", new { activa = !plantilla.GetProperty("activa").GetBoolean() })).EnsureSuccessStatusCode();
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/documentos");
        Assert.NotEqual(plantilla.GetProperty("activa").GetBoolean(), datos.GetProperty("plantillas")[0].GetProperty("activa").GetBoolean());
    }

    [Fact]
    public async Task Bandas_comp_teorica_y_estudio()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/compensaciones");
        var contadora = Por(datos, "bandas", "cargo", "Contadora").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/compensaciones/bandas", new { cargo = "Contadora", objetivo = 7000, pctMin = 85, pctMax = 120 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/compensaciones/bandas/{contadora}", new { cargo = "Contadora", objetivo = 7000, pctMin = 85, pctMax = 90 })).StatusCode);
        var b = await (await admin.PutAsJsonAsync($"/api/compensaciones/bandas/{contadora}", new { cargo = "Contadora", objetivo = 7250.5, pctMin = 80, pctMax = 125 })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7250.5m, b.GetProperty("objetivo").GetDecimal());
        var nueva = await (await admin.PostAsJsonAsync("/api/compensaciones/bandas", new { cargo = "Analista de Selección", objetivo = 4000, pctMin = 85, pctMax = 120 })).Content.ReadFromJsonAsync<JsonElement>();
        (await admin.DeleteAsync($"/api/compensaciones/bandas/{nueva.GetProperty("id").GetInt32()}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/compensaciones/remuneraciones/13/teorica", new { monto = 0 })).StatusCode);
        (await admin.PutAsJsonAsync("/api/compensaciones/remuneraciones/13/teorica", new { monto = 5100, comentario = "Ajuste por mercado" })).EnsureSuccessStatusCode();
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/compensaciones");
        var rem = datos.GetProperty("remuneraciones").EnumerateArray().First(x => x.GetProperty("colaboradorId").GetInt32() == 13);
        Assert.Equal(5100m, rem.GetProperty("compTeorica").GetDecimal());
        Assert.True(rem.GetProperty("sueldoBase").GetDecimal() > 0);

        // Cada cargo propio se empareja con un solo cargo de mercado.
        var ejecutivo = Por(datos, "estudio", "cargo", "Ejecutivo comercial").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/compensaciones/estudio/{ejecutivo}/interno", new { interno = "Analista Finanzas" })).StatusCode);
        (await admin.PutAsJsonAsync($"/api/compensaciones/estudio/{ejecutivo}/interno", new { interno = "Ejecutiva Comercial" })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/compensaciones/estudio/{ejecutivo}/interno", new { interno = (string?)null })).EnsureSuccessStatusCode();
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/compensaciones");
        Assert.Equal(JsonValueKind.Null, Por(datos, "estudio", "cargo", "Ejecutivo comercial").GetProperty("interno").ValueKind);
    }

    [Fact]
    public async Task Posiciones_ajustan_el_plan()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/headcount");
        var finanzas = datos.GetProperty("planArea").GetProperty("Finanzas").GetInt32();
        var cargo = datos.GetProperty("planCargo").GetProperty("Analista Finanzas").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/headcount/posiciones",
            new { cargo = "Analista Finanzas", division = "Departamento de Administración", area = "Finanzas", subArea = "Tesorería", ingreso = "01/06/2026", termino = "01/05/2026", cantidad = 1 })).StatusCode);
        var codigos = await (await admin.PostAsJsonAsync("/api/headcount/posiciones",
            new { cargo = "Analista Finanzas", division = "Departamento de Administración", area = "Finanzas", subArea = "Tesorería", ingreso = "01/06/2026", cantidad = 2 })).Content.ReadFromJsonAsync<List<string>>();
        Assert.Equal(["POS-0121", "POS-0122"], codigos);
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/headcount");
        Assert.Equal(finanzas + 2, datos.GetProperty("planArea").GetProperty("Finanzas").GetInt32());
        Assert.Equal(cargo + 2, datos.GetProperty("planCargo").GetProperty("Analista Finanzas").GetInt32());

        (await admin.DeleteAsync("/api/headcount/posiciones/POS-0122")).EnsureSuccessStatusCode();
        // Una posición no vigente no cambia el plan.
        (await admin.DeleteAsync("/api/headcount/posiciones/POS-0090")).EnsureSuccessStatusCode();
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/headcount");
        Assert.Equal(finanzas + 1, datos.GetProperty("planArea").GetProperty("Finanzas").GetInt32());
        Assert.Equal(cargo + 1, datos.GetProperty("planCargo").GetProperty("Analista Finanzas").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/headcount/posiciones/POS-0090")).StatusCode);
    }
}
