using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexoRh.Api.Tests;

[Collection("api")]
public class CatalogosTests(ApiFixture api)
{
    [Fact]
    public async Task Solo_administradores_salvo_feriados()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.GetAsync("/api/catalogos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.GetAsync("/api/catalogos/empresas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await colab.GetAsync("/api/catalogos/feriados")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.PostAsJsonAsync("/api/catalogos/feriados", new { fecha = "24/12/2026", nombre = "x", alcance = "Nacional" })).StatusCode);

        var todos = await (await api.ClienteDe(ApiFixture.Admin)).GetFromJsonAsync<JsonElement>("/api/catalogos");
        Assert.Equal(12, todos.EnumerateObject().Count());
        Assert.Equal(16, todos.GetProperty("feriados").GetArrayLength());
        Assert.Equal("Comercial Andina S.A.C.", todos.GetProperty("empresas")[0].GetProperty("razon").GetString());
    }

    [Fact]
    public async Task Valida_cada_fila_con_la_definicion_del_catalogo()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var empresa = new { razon = "Andina Servicios S.A.C.", ruc = "20600000001", direccion = "Av. Arequipa 100", regimen = "General", activo = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/empresas", empresa with { ruc = "123" })).StatusCode);            // patrón
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/empresas", empresa with { regimen = "Otro" })).StatusCode);       // opción
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/empresas", empresa with { razon = "" })).StatusCode);              // obligatorio
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/empresas", empresa with { ruc = "20601234571" })).StatusCode);    // único (el RUC del tenant)
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/empresas", new { empresa.razon, empresa.ruc, empresa.direccion, empresa.regimen, inventado = 1 })).StatusCode);

        var r = await admin.PostAsJsonAsync("/api/catalogos/empresas", empresa);
        r.EnsureSuccessStatusCode();
        var fila = await r.Content.ReadFromJsonAsync<JsonElement>();
        var id = fila.GetProperty("id").GetInt32();
        var mod = await admin.PutAsJsonAsync($"/api/catalogos/empresas/{id}", empresa with { direccion = "Av. Arequipa 200" });
        Assert.Equal("Av. Arequipa 200", (await mod.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("direccion").GetString());
        var historial = await admin.GetStringAsync("/api/cambios?q=Andina%20Servicios");
        Assert.Contains("Av. Arequipa 200", historial);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/catalogos/empresas/{id}")).StatusCode);
    }

    [Fact]
    public async Task No_se_borra_lo_que_otro_catalogo_usa()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var cc = await (await admin.PostAsJsonAsync("/api/catalogos/centros-de-costo", new { codigo = "CC-901", nombre = "Proyecto QA", activo = true })).Content.ReadFromJsonAsync<JsonElement>();
        var ccId = cc.GetProperty("id").GetInt32();
        // Un puesto que apunta a un centro inexistente se rechaza; al existente, no.
        var puesto = new { codigo = "QA-901", cargo = "Analista Finanzas", area = "Finanzas", centro = "CC-999 · Nada", estado = "Vacante" };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/puestos", puesto)).StatusCode);
        var p = await admin.PostAsJsonAsync("/api/catalogos/puestos", puesto with { centro = "CC-901 · Proyecto QA" });
        p.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/catalogos/centros-de-costo/{ccId}")).StatusCode);
        var pid = (await p.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await admin.DeleteAsync($"/api/catalogos/puestos/{pid}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/catalogos/centros-de-costo/{ccId}")).StatusCode);
    }

    [Fact]
    public async Task Los_feriados_del_catalogo_son_los_de_Asistencia()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var r = await admin.PostAsJsonAsync("/api/catalogos/feriados", new { fecha = "24/12/2026", nombre = "Nochebuena (empresa)", alcance = "Día no laborable de la empresa" });
        r.EnsureSuccessStatusCode();
        var id = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var feriados = (await admin.GetFromJsonAsync<JsonElement>("/api/asistencia")).GetProperty("feriados").EnumerateArray().Select(f => f.GetProperty("fecha").GetString()).ToList();
        Assert.Contains("24/12/2026", feriados);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/feriados", new { fecha = "24/12/2026", nombre = "Otra", alcance = "Nacional" })).StatusCode);
        (await admin.DeleteAsync($"/api/catalogos/feriados/{id}")).EnsureSuccessStatusCode();
        feriados = (await admin.GetFromJsonAsync<JsonElement>("/api/asistencia")).GetProperty("feriados").EnumerateArray().Select(f => f.GetProperty("fecha").GetString()).ToList();
        Assert.DoesNotContain("24/12/2026", feriados);
        Assert.Equal(16, feriados.Count);
    }

    [Fact]
    public async Task Formularios_de_configuracion_de_todos_los_modulos()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.GetAsync("/api/configuracion/notificaciones")).StatusCode);
        var publicas = await colab.GetFromJsonAsync<JsonElement>("/api/configuracion");
        Assert.Equal(["ca-metodos", "ca-tolerancias"], publicas.EnumerateObject().Select(p => p.Name).Order());

        var admin = await api.ClienteDe(ApiFixture.Admin);
        Assert.Equal(11, (await admin.GetFromJsonAsync<JsonElement>("/api/configuracion")).EnumerateObject().Count());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/configuracion/notificaciones", new { respuesta = "no-es-correo" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/configuracion/compensaciones", new { referencia = "Inventada" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/configuracion/compensaciones", new { compaMin = 2 })).StatusCode);
        var r = await admin.PutAsJsonAsync("/api/configuracion/compensaciones", new { compaMin = 0.85 });
        r.EnsureSuccessStatusCode();
        Assert.Equal(0.85m, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("compaMin").GetDecimal());
    }
}

[Collection("api")]
public class UbigeoTests(ApiFixture api)
{
    [Fact]
    public async Task Las_empresas_leen_el_ubigeo_oficial_del_INEI_como_arbol()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/ubigeo")).StatusCode);
        var u = await (await api.ClienteDe(ApiFixture.Colaborador)).GetFromJsonAsync<JsonElement>("/api/ubigeo");
        Assert.Equal("v2026.1", u.GetProperty("version").GetString());
        var deps = u.GetProperty("departamentos").EnumerateArray().ToList();
        Assert.Equal(25, deps.Count);
        Assert.Equal(196, deps.Sum(d => d.GetProperty("provincias").GetArrayLength()));
        Assert.Equal(1892, deps.Sum(d => d.GetProperty("provincias").EnumerateArray().Sum(p => p.GetProperty("distritos").GetArrayLength())));
        var callao = deps.Single(d => d.GetProperty("nombre").GetString() == "Callao").GetProperty("provincias")[0].GetProperty("distritos").EnumerateArray().ToList();
        Assert.Contains(callao, d => d.GetProperty("nombre").GetString() == "Carmen de la Legua-Reynoso" && d.GetProperty("codigo").GetString() == "070103");
    }

    [Fact]
    public async Task Recintos_validan_y_normalizan_departamento_provincia_y_distrito()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        object Recinto(string nombre, string dep, string prov, string dist) => new
        {
            nombre, empresa = "Comercial Andina S.A.C.", ruc = "20512345678", codigoInterno = "CA-UB", pais = "Perú", departamento = dep, provincia = prov,
            distrito = dist, direccion = "Av. Prueba 100", clave = "UB-" + nombre[..2], info = "", lat = -12.0, lng = -77.0, radio = 150, activo = true,
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/asistencia/recintos", Recinto("Ubigeo malo 1", "Narnia", "Lima", "Lurin"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/asistencia/recintos", Recinto("Ubigeo malo 2", "Lima", "Lima", "Yanahuara"))).StatusCode); // es de Arequipa
        var r = await admin.PostAsJsonAsync("/api/asistencia/recintos", Recinto("Ubigeo con tildes", "lima", "LIMA", "Lurín"));
        r.EnsureSuccessStatusCode();
        var creado = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Lima", "Lima", "Lurin"), (creado.GetProperty("departamento").GetString(), creado.GetProperty("provincia").GetString(), creado.GetProperty("distrito").GetString()));
    }

    [Fact]
    public async Task Localidades_usan_los_departamentos_del_ubigeo()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var malo = await admin.PostAsJsonAsync("/api/catalogos/localidades", new { nombre = "Sede Narnia", tipo = "Oficina", direccion = "Calle 1", departamento = "Narnia", activo = true });
        Assert.Equal(HttpStatusCode.BadRequest, malo.StatusCode);
        var r = await admin.PostAsJsonAsync("/api/catalogos/localidades", new { nombre = "Sede Huancayo", tipo = "Oficina", direccion = "Jr. Real 100", departamento = "Junín", activo = true });
        r.EnsureSuccessStatusCode();
        Assert.Equal("Junin", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("departamento").GetString());
    }
}
