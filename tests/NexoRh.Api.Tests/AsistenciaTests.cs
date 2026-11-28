using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexoRh.Api.Tests;

[Collection("api")]
public class AsistenciaTests(ApiFixture api)
{
    private static async Task<JsonElement> Datos(HttpClient c) => await c.GetFromJsonAsync<JsonElement>("/api/asistencia");
    private static IEnumerable<int> Ids(JsonElement datos, string lista) =>
        datos.GetProperty(lista).EnumerateArray().Select(x => x.GetProperty("colaboradorId").GetInt32()).Distinct();

    [Fact]
    public async Task Cada_rol_ve_solo_la_asistencia_a_su_alcance()
    {
        var admin = await Datos(await api.ClienteDe(ApiFixture.Admin));
        Assert.Equal("16/03/2026", admin.GetProperty("corte").GetString());
        Assert.True(Ids(admin, "marcas").Count() > 10);

        var colab = await Datos(await api.ClienteDe(ApiFixture.Colaborador));
        Assert.Equal([0], Ids(colab, "marcas"));
        Assert.All(Ids(colab, "vacaciones").Concat(Ids(colab, "ausencias")).Concat(Ids(colab, "aprobaciones")), id => Assert.Equal(0, id));
        // Las observaciones del supervisor no las ve la propia persona.
        Assert.Empty(colab.GetProperty("notas").EnumerateArray());
        Assert.False(colab.GetProperty("puedeGestionar").GetBoolean());
        // Recintos y turnos sí (los necesita para marcar).
        Assert.NotEmpty(colab.GetProperty("recintos").EnumerateArray());

        var sup = await Datos(await api.ClienteDe(ApiFixture.Supervisor));
        Assert.Equal([0, 6, 11, 14], Ids(sup, "perfiles").Order());
    }

    [Fact]
    public async Task Marcar_usa_la_hora_del_servidor_y_respeta_el_orden()
    {
        // Ana Ríos (14) no tiene marcas el día de corte.
        var (correo, password) = await Usuarios.CrearConPassword(api, "Colaborador", 14);
        var c = await api.ClienteDe(correo, password);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Salida" })).StatusCode);
        var r = await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada", hora = "06:00", fecha = "01/01/2020", lat = Lat, lng = Lng });
        r.EnsureSuccessStatusCode();
        var m = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("16/03/2026", m.GetProperty("fecha").GetString());
        Assert.NotEqual("06:00", m.GetProperty("hora").GetString());
        Assert.Equal(1, m.GetProperty("recintoId").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada" })).StatusCode);
        (await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Salida", lat = Lat, lng = Lng })).EnsureSuccessStatusCode();
    }

    // Recinto 1 de los datos de ejemplo (radio 150 m).
    private const double Lat = -12.0912, Lng = -77.0225;

    /// <summary>Colaborador nuevo (sin marcas ni cuenta), para no depender de los datos de ejemplo.</summary>
    private async Task<int> NuevoColaborador()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var dni = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        var r = await admin.PostAsJsonAsync("/api/colaboradores", new
        {
            apellidos = "Marcación Prueba", nombres = "Test", documento = dni, cargo = "Analista de Planillas", division = "Administración",
            area = "Personas", subArea = "Compensaciones", ingreso = "01/03/2026", sueldoNeto = 3000, detalle = new { tipoDocumento = "DNI" },
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Marcar_desde_el_portal_valida_la_ubicacion_real_contra_el_radio()
    {
        var col = await NuevoColaborador();
        var (correo, password) = await Usuarios.CrearConPassword(api, "Colaborador", col);
        var c = await api.ClienteDe(correo, password);
        // Sin ubicación no se marca.
        var sin = await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada", recintoId = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, sin.StatusCode);
        // A ~1,1 km del recinto: rechazada y queda en Marcas Fallidas con la distancia.
        var lejos = await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada", recintoId = 1, lat = Lat + 0.01, lng = Lng, precision = 20 });
        Assert.Equal(HttpStatusCode.BadRequest, lejos.StatusCode);
        var datos = await c.GetFromJsonAsync<JsonElement>("/api/asistencia");
        var f = datos.GetProperty("fallidas").EnumerateArray().Single(x => x.GetProperty("motivo").GetString() == "Fuera del radio" && x.GetProperty("colaboradorId").GetInt32() == col);
        Assert.InRange(f.GetProperty("distancia").GetInt32(), 1000, 1200);
        // A ~100 m: se registra con las coordenadas del dispositivo.
        var cerca = await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada", recintoId = 1, lat = Lat + 0.0009, lng = Lng, precision = 12.4 });
        cerca.EnsureSuccessStatusCode();
        var m = await cerca.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(m.GetProperty("dentroRadio").GetBoolean());
        Assert.Equal(Lat + 0.0009, m.GetProperty("lat").GetDouble(), 6);
        Assert.InRange(m.GetProperty("distancia").GetInt32(), 90, 110);
        Assert.Equal(12, m.GetProperty("precision").GetInt32());
    }

    [Fact]
    public async Task Marcar_con_selfie_guarda_la_foto_y_solo_la_ven_quienes_corresponde()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var col = await NuevoColaborador();
        var (correo, password) = await Usuarios.CrearConPassword(api, "Colaborador", col);
        var c = await api.ClienteDe(correo, password);
        (await admin.PutAsJsonAsync("/api/configuracion/ca-metodos", new { fotoWeb = true })).EnsureSuccessStatusCode();
        try
        {
            var sinFoto = await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada", recintoId = 1, lat = Lat, lng = Lng });
            Assert.Equal(HttpStatusCode.BadRequest, sinFoto.StatusCode);
            var datos = await c.GetFromJsonAsync<JsonElement>("/api/asistencia");
            Assert.Contains(datos.GetProperty("fallidas").EnumerateArray(), x => x.GetProperty("motivo").GetString() == "Sin foto" && x.GetProperty("colaboradorId").GetInt32() == col);
            // Lo que no es un JPEG se rechaza.
            var png = "data:image/png;base64," + Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47]);
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada", recintoId = 1, lat = Lat, lng = Lng, foto = png })).StatusCode);

            byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 0xFF, 0xD9];
            var r = await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada", recintoId = 1, lat = Lat, lng = Lng, foto = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg) });
            r.EnsureSuccessStatusCode();
            var m = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(m.GetProperty("foto").GetBoolean());
            var id = m.GetProperty("id").GetInt32();

            Assert.Equal(jpeg, await c.GetByteArrayAsync($"/api/asistencia/marcas/{id}/foto"));
            var deAdmin = await admin.GetAsync($"/api/asistencia/marcas/{id}/foto");
            Assert.Equal("image/jpeg", deAdmin.Content.Headers.ContentType?.MediaType);
            // Otro colaborador no la ve.
            var (otro, clave) = await Usuarios.CrearConPassword(api, "Colaborador", 9);
            Assert.Equal(HttpStatusCode.NotFound, (await (await api.ClienteDe(otro, clave)).GetAsync($"/api/asistencia/marcas/{id}/foto")).StatusCode);
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/configuracion/ca-metodos", new { fotoWeb = false })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Supervisor_aprueba_horas_extras_solo_de_su_equipo()
    {
        var sup = await api.ClienteDe(ApiFixture.Supervisor);
        var propio = new { colaboradorId = 11, fecha = "10/03/2026", accion = "rechazar", observacion = "x", minutosExceso = 0 };
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsJsonAsync("/api/asistencia/hhee", propio)).StatusCode);
        var ajeno = propio with { colaboradorId = 7 };
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsJsonAsync("/api/asistencia/hhee", ajeno)).StatusCode);

        var ok = await sup.PostAsJsonAsync("/api/asistencia/hhee", new
        {
            colaboradorId = 6, fecha = "10/03/2026", accion = "aprobar", minutosExceso = 90, observacion = "",
            lineas = new[] { new { minutos = 60, tipo = 25 } },
        });
        ok.EnsureSuccessStatusCode();
        var a = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aprobado parcial", a.GetProperty("estado").GetString());
        Assert.Equal("Gonzalo Navarro Ríos", a.GetProperty("aprobadoPor").GetString());

        // No se puede aprobar más que el exceso, ni con sobretasas inventadas.
        var demas = await sup.PostAsJsonAsync("/api/asistencia/hhee", new
        {
            colaboradorId = 6, fecha = "10/03/2026", accion = "aprobar", minutosExceso = 30, lineas = new[] { new { minutos = 60, tipo = 50 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, demas.StatusCode);
        // Enviar a planilla es solo de administradores.
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsJsonAsync("/api/asistencia/hhee/enviar", new { items = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Solicitud_de_vacaciones_aprobada_por_la_jefatura_queda_en_el_calendario()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        var ajena = await colab.PostAsJsonAsync("/api/asistencia/solicitudes", new { colaboradorId = 6, tipo = "Vacaciones", desde = "06/04/2026", hasta = "08/04/2026", motivo = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);
        var pasada = await colab.PostAsJsonAsync("/api/asistencia/solicitudes", new { colaboradorId = 0, tipo = "Vacaciones", desde = "02/03/2026", hasta = "03/03/2026", motivo = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, pasada.StatusCode);

        var r = await colab.PostAsJsonAsync("/api/asistencia/solicitudes", new { colaboradorId = 0, tipo = "Vacaciones", desde = "06/04/2026", hasta = "08/04/2026", motivo = "Viaje" });
        r.EnsureSuccessStatusCode();
        var s = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pendiente", s.GetProperty("estado").GetString());
        Assert.Equal(3, s.GetProperty("dias").GetInt32());
        var id = s.GetProperty("id").GetInt32();

        // La propia persona no puede aprobarse; su jefatura sí.
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.PostAsJsonAsync($"/api/asistencia/solicitudes/{id}/resolver", new { aprobar = true })).StatusCode);
        var sup = await api.ClienteDe(ApiFixture.Supervisor);
        var res = await sup.PostAsJsonAsync($"/api/asistencia/solicitudes/{id}/resolver", new { aprobar = true, comentario = "OK" });
        res.EnsureSuccessStatusCode();
        var resultado = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aprobada", resultado.GetProperty("solicitud").GetProperty("estado").GetString());
        Assert.Equal("06/04/2026", resultado.GetProperty("vacacion").GetProperty("desde").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync($"/api/asistencia/solicitudes/{id}/resolver", new { aprobar = false, comentario = "x" })).StatusCode);

        // Ahora se cruza con las vacaciones aprobadas.
        var cruce = await colab.PostAsJsonAsync("/api/asistencia/solicitudes", new { colaboradorId = 0, tipo = "Permiso con goce", desde = "07/04/2026", hasta = "07/04/2026", motivo = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, cruce.StatusCode);
        var vacaciones = (await Datos(colab)).GetProperty("vacaciones").EnumerateArray().Select(v => v.GetProperty("desde").GetString());
        Assert.Contains("06/04/2026", vacaciones);
    }

    [Fact]
    public async Task Turnos_y_recintos_solo_los_administra_un_administrador()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        var turno = new { nombre = "Prueba", sigla = "PRB", tipo = "Permanente", jornada = "Diurno", entrada = "08:00", salida = "17:00", colacion = 60, dias = new[] { 1, 2 }, tolerancia = 5, activo = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.PostAsJsonAsync("/api/asistencia/turnos", turno)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.ClienteDe(ApiFixture.Supervisor)).PostAsJsonAsync("/api/asistencia/turnos", turno)).StatusCode);

        var admin = await api.ClienteDe(ApiFixture.Admin);
        var mala = await admin.PostAsJsonAsync("/api/asistencia/turnos", turno with { entrada = "25:00" });
        Assert.Equal(HttpStatusCode.BadRequest, mala.StatusCode);
        (await admin.PostAsJsonAsync("/api/asistencia/turnos", turno)).EnsureSuccessStatusCode();
        // El turno Administrativo (1) está vigente: no se puede desactivar.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/asistencia/turnos/1/estado", new { activo = false })).StatusCode);
    }

    [Fact]
    public async Task Asignar_turno_permanente_cierra_el_anterior()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var r = await admin.PostAsJsonAsync("/api/asistencia/asignaciones", new { colaboradorIds = new[] { 10 }, fecha = "01/04/2026", turnoId = 4, modo = "permanente" });
        r.EnsureSuccessStatusCode();
        var asig = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("asignaciones").EnumerateArray().ToList();
        Assert.Contains(asig, a => a.GetProperty("turnoId").GetInt32() == 1 && a.GetProperty("hasta").GetString() == "31/03/2026");
        Assert.Contains(asig, a => a.GetProperty("turnoId").GetInt32() == 4 && a.GetProperty("hasta").GetString() == "");
        // Un código de descanso no puede ser permanente.
        var des = await admin.PostAsJsonAsync("/api/asistencia/asignaciones", new { colaboradorIds = new[] { 10 }, fecha = "01/04/2026", codigo = "DES", modo = "permanente" });
        Assert.Equal(HttpStatusCode.BadRequest, des.StatusCode);
    }
}
