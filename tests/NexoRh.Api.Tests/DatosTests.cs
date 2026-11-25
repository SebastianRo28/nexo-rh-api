using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexoRh.Api.Tests;

/// <summary>Registros de módulos sin tabla propia (/api/datos): permisos, registros propios, canjes y claves de API.</summary>
[Collection("api")]
public class DatosTests(ApiFixture api)
{
    private static JsonElement Por(JsonElement lista, string campo, string valor) =>
        lista.EnumerateArray().First(x => x.GetProperty(campo).GetString() == valor);

    [Fact]
    public async Task La_empresa_demo_trae_sus_datos_y_cada_coleccion_respeta_quien_lee_y_escribe()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        Assert.Equal(4, (await admin.GetFromJsonAsync<JsonElement>("/api/datos/kpi-tipos")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.GetAsync("/api/datos/kpi-tipos")).StatusCode);              // solo administradores
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/datos/inventada")).StatusCode);
        Assert.Equal(7, (await colab.GetFromJsonAsync<JsonElement>("/api/datos/beneficios")).GetArrayLength());      // catálogo para todos
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.PostAsJsonAsync("/api/datos/beneficios", new { nombre = "x" })).StatusCode);
        // Varias colecciones a la vez: las que no puede leer se omiten.
        var varias = await colab.GetFromJsonAsync<JsonElement>("/api/datos?c=beneficios,cursos,kpi-tipos");
        Assert.True(varias.TryGetProperty("beneficios", out _));
        Assert.True(varias.TryGetProperty("cursos", out _));
        Assert.False(varias.TryGetProperty("kpi-tipos", out _));
        // Las referencias de la semilla apuntan a los ids nuevos.
        var tipos = await admin.GetFromJsonAsync<JsonElement>("/api/datos/kpi-tipos");
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/datos/kpi-datos");
        var ids = tipos.EnumerateArray().Select(t => t.GetProperty("id").GetInt32()).ToHashSet();
        Assert.All(datos.EnumerateArray(), d => Assert.Contains(d.GetProperty("kpiId").GetInt32(), ids));
    }

    [Fact]
    public async Task Un_colaborador_canjea_beneficios_con_sus_puntos_y_no_puede_cambiar_el_estado()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        var beneficios = await colab.GetFromJsonAsync<JsonElement>("/api/datos/beneficios");
        int Id(string nombre) => Por(beneficios, "nombre", nombre).GetProperty("id").GetInt32();
        var antes = (await colab.GetFromJsonAsync<JsonElement>("/api/datos/beneficio-canjes")).GetArrayLength();

        var sinStock = await colab.PostAsJsonAsync("/api/datos/beneficio-canjes", new { beneficioId = Id("Entradas al cine (2)") });
        Assert.Equal(HttpStatusCode.BadRequest, sinStock.StatusCode);
        var caro = await colab.PostAsJsonAsync("/api/datos/beneficio-canjes", new { beneficioId = Id("Curso online") });   // 1500 > 1200
        Assert.Equal(HttpStatusCode.BadRequest, caro.StatusCode);

        // The owner only sends the benefit: cost, date and state are set by the API (a "puntos" sent by the client is ignored).
        var r = await colab.PostAsJsonAsync("/api/datos/beneficio-canjes", new { beneficioId = Id("Vale de almuerzo"), puntos = 1, estado = "Entregado" });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var canje = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(250, canje.GetProperty("puntos").GetInt32());
        Assert.Equal("Solicitado", canje.GetProperty("estado").GetString());
        Assert.Equal(0, canje.GetProperty("colaboradorId").GetInt32());

        var id = canje.GetProperty("id").GetInt32();
        var put = await colab.PutAsJsonAsync($"/api/datos/beneficio-canjes/{id}", new { estado = "Entregado", puntos = 0 });
        Assert.Equal("Solicitado", (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.DeleteAsync($"/api/datos/beneficio-canjes/{id}")).StatusCode);
        Assert.Equal(antes + 1, (await colab.GetFromJsonAsync<JsonElement>("/api/datos/beneficio-canjes")).GetArrayLength());

        // Another collaborator does not see it.
        var supervisor = await api.ClienteDe(ApiFixture.Supervisor);
        var ajenos = await supervisor.GetFromJsonAsync<JsonElement>("/api/datos/beneficio-canjes");
        Assert.DoesNotContain(ajenos.EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
        await (await api.ClienteDe(ApiFixture.Admin)).DeleteAsync($"/api/datos/beneficio-canjes/{id}");
    }

    [Fact]
    public async Task Las_claves_de_API_guardan_solo_el_hash_y_el_token_se_muestra_una_vez()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var r = await admin.PostAsJsonAsync("/api/datos/api-claves", new { descripcion = "ERP de prueba", token = "elegido-por-el-cliente" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var k = await r.Content.ReadFromJsonAsync<JsonElement>();
        var token = k.GetProperty("tokenNuevo").GetString()!;
        Assert.StartsWith("nx_live_", token);
        Assert.NotEqual("elegido-por-el-cliente", token);
        var lista = await admin.GetFromJsonAsync<JsonElement>("/api/datos/api-claves");
        var guardada = lista.EnumerateArray().First(x => x.GetProperty("id").GetInt32() == k.GetProperty("id").GetInt32());
        Assert.Contains("…", guardada.GetProperty("token").GetString());
        Assert.False(guardada.TryGetProperty("tokenHash", out _));
        Assert.False(guardada.TryGetProperty("tokenNuevo", out _));
        await admin.DeleteAsync($"/api/datos/api-claves/{k.GetProperty("id").GetInt32()}");
    }

    [Fact]
    public async Task Los_valores_de_reconocimiento_son_los_de_la_empresa()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        var valores = await colab.GetFromJsonAsync<JsonElement>("/api/reconocimientos/valores");
        Assert.Equal(["Vamos por más!", "Nos mueve la excelencia", "Pasión por el servicio", "Al infinito y más allá"],
            valores.EnumerateArray().Select(v => v.GetProperty("nombre").GetString()!).ToArray());
        var malo = await colab.PostAsJsonAsync("/api/reconocimientos", new { paraIds = new[] { 1 }, valor = "Inventado", mensaje = "Hola" });
        Assert.Equal(HttpStatusCode.BadRequest, malo.StatusCode);
    }
}
