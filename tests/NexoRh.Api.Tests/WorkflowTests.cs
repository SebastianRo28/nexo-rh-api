using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexoRh.Api.Tests;

[Collection("api")]
public class WorkflowTests(ApiFixture api)
{
    private const string Diego = "d.benavides@comercialandina.pe"; // supervisor de 1, 7 y 13

    private static List<JsonElement> Lista(JsonElement e) => e.EnumerateArray().ToList();

    [Fact]
    public async Task Cada_rol_ve_sus_solicitudes_y_nadie_resuelve_las_propias()
    {
        var admin = Lista(await (await api.ClienteDe(ApiFixture.Admin)).GetFromJsonAsync<JsonElement>("/api/workflow/solicitudes"));
        Assert.True(admin.Count >= 9);

        // Florencia (0) ve las que la afectan y no puede resolver ninguna.
        var colab = Lista(await (await api.ClienteDe(ApiFixture.Colaborador)).GetFromJsonAsync<JsonElement>("/api/workflow/solicitudes"));
        Assert.All(colab, s => Assert.False(s.GetProperty("puedoResolver").GetBoolean()));
        Assert.Contains(colab, s => s.GetProperty("titulo").GetString() == "Aporte Patronal");
        Assert.DoesNotContain(colab, s => s.GetProperty("titulo").GetString()!.StartsWith("Constancia de trabajo"));

        // Gonzalo creó "Aporte Patronal": la ve como suya, pero no puede resolverla.
        var sup = await api.ClienteDe(ApiFixture.Supervisor);
        var propia = Lista(await sup.GetFromJsonAsync<JsonElement>("/api/workflow/solicitudes")).First(s => s.GetProperty("titulo").GetString() == "Aporte Patronal");
        Assert.True(propia.GetProperty("esMia").GetBoolean());
        Assert.False(propia.GetProperty("puedoResolver").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsJsonAsync($"/api/workflow/solicitudes/{propia.GetProperty("id").GetInt32()}/resolver", new { aprobar = true })).StatusCode);
        // La constancia de Sofía (fuera de su equipo) ni siquiera la ve.
        var ajena = admin.First(s => s.GetProperty("titulo").GetString()!.StartsWith("Constancia de trabajo")).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await sup.PostAsJsonAsync($"/api/workflow/solicitudes/{ajena}/resolver", new { aprobar = true })).StatusCode);
    }

    [Fact]
    public async Task Adelanto_aprobado_por_la_jefatura_se_descuenta_en_la_planilla()
    {
        // Luis Pérez (13): sueldo S/ 6,050.30 → adelanto máximo S/ 3,025.15.
        var (correo, password) = await Usuarios.CrearConPassword(api, "Colaborador", 13);
        var luis = await api.ClienteDe(correo, password);
        Assert.Equal(HttpStatusCode.BadRequest, (await luis.PostAsJsonAsync("/api/workflow/solicitudes/portal", new { proceso = "adelanto", valor = "3100" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await luis.PostAsJsonAsync("/api/workflow/solicitudes/portal", new { proceso = "constancia", valor = "Otra entidad" })).StatusCode);
        var r = await luis.PostAsJsonAsync("/api/workflow/solicitudes/portal", new { proceso = "adelanto", valor = "1200.50" });
        r.EnsureSuccessStatusCode();
        var s = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pendiente", s.GetProperty("estado").GetString());
        Assert.Equal(1200.50m, s.GetProperty("monto").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await luis.PostAsJsonAsync("/api/workflow/solicitudes/portal", new { proceso = "adelanto", valor = "100" })).StatusCode); // ya hay uno pendiente
        var id = s.GetProperty("id").GetInt32();
        Assert.True(id > 18_700);

        var diego = await api.ClienteDe(Diego);
        Assert.Equal(HttpStatusCode.BadRequest, (await diego.PostAsJsonAsync($"/api/workflow/solicitudes/{id}/resolver", new { aprobar = false })).StatusCode); // rechazo sin motivo
        var res = await diego.PostAsJsonAsync($"/api/workflow/solicitudes/{id}/resolver", new { aprobar = true, comentario = "OK" });
        res.EnsureSuccessStatusCode();
        var resolucion = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aprobada", resolucion.GetProperty("solicitud").GetProperty("estado").GetString());
        Assert.True(resolucion.GetProperty("itemId").GetInt32() > 0);

        var admin = await api.ClienteDe(ApiFixture.Admin);
        (await admin.PostAsync("/api/remuneraciones/periodos/03-2026/procesar", null)).EnsureSuccessStatusCode();
        var boletaId = (await admin.GetFromJsonAsync<JsonElement>("/api/remuneraciones/boletas?periodo=03/2026&colaboradorId=13"))[0].GetProperty("id").GetInt32();
        var boleta = await admin.GetFromJsonAsync<JsonElement>($"/api/remuneraciones/boletas/{boletaId}");
        Assert.Contains(boleta.GetProperty("lineas").EnumerateArray(), l => l.GetProperty("codigo").GetString() == "ADE-SUE" && l.GetProperty("monto").GetDecimal() == 1200.50m);
    }

    [Fact]
    public async Task Plantillas_solo_activas_y_dentro_del_equipo()
    {
        var sup = await api.ClienteDe(ApiFixture.Supervisor);
        var plantillas = Lista(await sup.GetFromJsonAsync<JsonElement>("/api/workflow/plantillas"));
        int Id(string nombre) => plantillas.First(p => p.GetProperty("nombre").GetString() == nombre).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await sup.PostAsJsonAsync("/api/workflow/solicitudes", new { plantillaId = Id("Cambio de cargo"), afectadoId = 6, detalle = "Contadora senior" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsJsonAsync("/api/workflow/solicitudes", new { plantillaId = Id("Cambio de cargo"), afectadoId = 7 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync("/api/workflow/solicitudes", new { plantillaId = Id("Baja por término de contrato"), afectadoId = 6 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.ClienteDe(ApiFixture.Colaborador)).GetAsync("/api/workflow/plantillas")).StatusCode);

        var admin = await api.ClienteDe(ApiFixture.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsJsonAsync($"/api/workflow/plantillas/{Id("Alta simple")}/estado", new { activa = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/workflow/plantillas/{Id("Cambio de sueldo")}/estado", new { activa = true })).StatusCode); // incompleta
        var copia = await (await admin.PostAsync($"/api/workflow/plantillas/{Id("Alta simple")}/duplicar", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Alta simple (copia)", copia.GetProperty("nombre").GetString());
        Assert.Equal("Inactiva", copia.GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/workflow/plantillas/{copia.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/workflow/plantillas/{Id("Solicitud bono")}")).StatusCode); // tiene pendientes
    }

    [Fact]
    public async Task Reconocimientos_entre_companeros()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        Assert.Equal(HttpStatusCode.BadRequest, (await colab.PostAsJsonAsync("/api/reconocimientos", new { paraIds = new[] { 0 }, valor = "Vamos por más!", mensaje = "Yo" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await colab.PostAsJsonAsync("/api/reconocimientos", new { paraIds = new[] { 3 }, valor = "Inventado", mensaje = "x" })).StatusCode);
        var r = await colab.PostAsJsonAsync("/api/reconocimientos", new { paraIds = new[] { 3, 8 }, valor = "Al infinito y más allá", mensaje = "Gracias por el soporte en el cierre." });
        r.EnsureSuccessStatusCode();
        var nuevo = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Chávez Quispe, Renato; Herrera Castillo, Valeria", nuevo.GetProperty("para").GetString());
        Assert.Equal("Alamos Ruiz, Florencia", nuevo.GetProperty("autor").GetString());
        var lista = Lista(await (await api.ClienteDe(ApiFixture.Supervisor)).GetFromJsonAsync<JsonElement>("/api/reconocimientos"));
        Assert.Equal(nuevo.GetProperty("id").GetInt32(), lista[0].GetProperty("id").GetInt32());
        // Un administrador sin colaborador vinculado no puede reconocer.
        Assert.Equal(HttpStatusCode.BadRequest, (await (await api.ClienteDe(ApiFixture.Admin)).PostAsJsonAsync("/api/reconocimientos", new { paraIds = new[] { 3 }, valor = "Vamos por más!", mensaje = "x" })).StatusCode);
    }
}
