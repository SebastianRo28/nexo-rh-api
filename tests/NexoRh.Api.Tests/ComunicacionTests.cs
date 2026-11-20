using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Data;

namespace NexoRh.Api.Tests;

[Collection("api")]
public class ComunicacionTests(ApiFixture api)
{
    private T ConDb<T>(Func<NexoDbContext, T> f)
    {
        using var scope = api.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<NexoDbContext>());
    }

    [Fact]
    public async Task Encuesta_anonima_se_responde_una_vez_y_no_guarda_quien_respondio()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        var mias = (await colab.GetFromJsonAsync<JsonElement>("/api/encuestas/mias")).EnumerateArray().ToList();
        var nombres = mias.Select(e => e.GetProperty("nombre").GetString()).ToList();
        Assert.Contains("Clima organizacional 2026", nombres);
        Assert.Contains("Percepción de seguridad laboral", nombres);            // ya respondida
        Assert.DoesNotContain("Evaluación de la inducción", nombres);          // no es destinataria
        var clima = mias.First(e => e.GetProperty("nombre").GetString() == "Clima organizacional 2026");
        Assert.Equal(JsonValueKind.Null, clima.GetProperty("respondida").ValueKind);
        var id = clima.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await colab.PostAsJsonAsync($"/api/encuestas/{id}/responder", new { valores = new { c1 = "5" } })).StatusCode);          // faltan obligatorias
        Assert.Equal(HttpStatusCode.BadRequest, (await colab.PostAsJsonAsync($"/api/encuestas/{id}/responder", new { valores = new { c1 = "9", c2 = "4", c3 = "4", c4 = "Sí" } })).StatusCode);
        var ok = await colab.PostAsJsonAsync($"/api/encuestas/{id}/responder", new { valores = new { c1 = "5", c2 = "4", c3 = "4", c4 = "Sí", c5 = "Nada que agregar" } });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await colab.PostAsJsonAsync($"/api/encuestas/{id}/responder", new { valores = new { c1 = "5", c2 = "4", c3 = "4", c4 = "Sí" } })).StatusCode);

        // Se sabe que participó, pero ninguna respuesta de esa encuesta tiene su id.
        Assert.True(ConDb(db => db.ParticipacionesEncuesta.Any(p => p.EncuestaId == id && p.ColaboradorId == 0)));
        Assert.False(ConDb(db => db.RespuestasEncuesta.Any(r => r.EncuestaId == id && r.ColaboradorId != null)));
        var ahora = (await colab.GetFromJsonAsync<JsonElement>("/api/encuestas/mias")).EnumerateArray().First(e => e.GetProperty("id").GetInt32() == id);
        Assert.Equal("16/03/2026", ahora.GetProperty("respondida").GetString());
    }

    [Fact]
    public async Task Resultados_solo_para_administradores_y_ocultos_con_pocas_respuestas()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.ClienteDe(ApiFixture.Colaborador)).GetAsync("/api/encuestas")).StatusCode);
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var lista = (await admin.GetFromJsonAsync<JsonElement>("/api/encuestas?tipo=Clima")).EnumerateArray().ToList();
        var c2025 = lista.First(e => e.GetProperty("nombre").GetString() == "Clima laboral 2025");
        Assert.Equal("Finalizada", c2025.GetProperty("estado").GetString());
        var res = await admin.GetFromJsonAsync<JsonElement>($"/api/encuestas/{c2025.GetProperty("id").GetInt32()}/resultados");
        Assert.False(res.GetProperty("ocultos").GetBoolean());
        var p1 = res.GetProperty("preguntas")[0];
        Assert.InRange(p1.GetProperty("promedio").GetDecimal(), 1m, 5m);
        Assert.Equal(c2025.GetProperty("respondidas").GetInt32(), p1.GetProperty("conteo").EnumerateObject().Sum(x => x.Value.GetInt32()));

        var r = await admin.PostAsJsonAsync("/api/encuestas", new { tipo = "Libre", plantilla = "Teletrabajo", inicio = "16/03/2026", termino = "31/03/2026", anonima = true });
        r.EnsureSuccessStatusCode();
        var nueva = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("En curso", nueva.GetProperty("estado").GetString());
        var nid = nueva.GetProperty("id").GetInt32();
        Assert.True((await admin.GetFromJsonAsync<JsonElement>($"/api/encuestas/{nid}/resultados")).GetProperty("ocultos").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/encuestas/{c2025.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/encuestas/{nid}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/encuestas", new { tipo = "Clima", plantilla = "Teletrabajo", inicio = "16/03/2026", termino = "31/03/2026" })).StatusCode);
    }

    [Fact]
    public async Task Denuncia_anonima_se_sigue_con_el_codigo_y_no_deja_rastro_del_denunciante()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        Assert.Equal(HttpStatusCode.BadRequest, (await colab.PostAsJsonAsync("/api/denuncias", new { tipo = "Acoso laboral", relacion = "Colaborador", descripcion = "corto" })).StatusCode);
        var r = await colab.PostAsJsonAsync("/api/denuncias", new { tipo = "Acoso laboral", relacion = "Colaborador", descripcion = "Una jefatura grita al equipo en las reuniones diarias.", anonima = true });
        r.EnsureSuccessStatusCode();
        var codigo = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString()!;
        Assert.Equal(12, codigo.Length);
        Assert.True(ConDb(db => db.Denuncias.Any(d => d.Codigo == codigo && d.DenuncianteUsuarioId == null && d.DenuncianteNombre == null)));
        Assert.False(ConDb(db => db.Cambios.Any(c => c.Entidad.StartsWith("Denuncia") && (c.Usuario.Contains("Florencia") || c.Colaborador.Contains("Alamos")))));

        // Seguimiento con el código (desde cualquier sesión) y mensaje del denunciante.
        var otro = await api.ClienteDe(ApiFixture.Supervisor);
        Assert.Equal("Nueva", (await otro.GetFromJsonAsync<JsonElement>($"/api/denuncias/seguimiento/{codigo}")).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await colab.GetAsync("/api/denuncias/seguimiento/XXXXXXXXXXXX")).StatusCode);
        (await colab.PostAsJsonAsync($"/api/denuncias/seguimiento/{codigo}/mensajes", new { texto = "Hubo testigos." })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await colab.GetAsync("/api/denuncias")).StatusCode);
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var fila = (await admin.GetFromJsonAsync<JsonElement>("/api/denuncias")).EnumerateArray().First(d => d.GetProperty("codigo").GetString() == codigo);
        Assert.Equal(1, fila.GetProperty("nuevosMensajes").GetInt32());
        var id = fila.GetProperty("id").GetInt32();
        var det = await admin.GetFromJsonAsync<JsonElement>($"/api/denuncias/{id}");
        Assert.Equal(JsonValueKind.Null, det.GetProperty("denunciante").ValueKind);
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonElement>("/api/denuncias")).EnumerateArray().First(d => d.GetProperty("id").GetInt32() == id).GetProperty("nuevosMensajes").GetInt32());

        var resp = await (await admin.PostAsJsonAsync($"/api/denuncias/{id}/mensajes", new { texto = "Recibido, abrimos la investigación." })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("En curso", resp.GetProperty("resumen").GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/denuncias/{id}/estado", new { estado = "Finalizada" })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/denuncias/{id}/estado", new { estado = "Finalizada", resolucion = "Se aplicaron medidas disciplinarias." })).EnsureSuccessStatusCode();
        var seg = await colab.GetFromJsonAsync<JsonElement>($"/api/denuncias/seguimiento/{codigo}");
        Assert.Equal("Finalizada", seg.GetProperty("estado").GetString());
        Assert.Equal(2, seg.GetProperty("mensajes").GetArrayLength()); // el del denunciante y la respuesta del comité
        Assert.Equal(HttpStatusCode.BadRequest, (await colab.PostAsJsonAsync($"/api/denuncias/seguimiento/{codigo}/mensajes", new { texto = "¿Algo más?" })).StatusCode);
    }

    [Fact]
    public async Task Denuncia_con_identidad_la_ve_el_comite()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        var codigo = (await (await colab.PostAsJsonAsync("/api/denuncias", new { tipo = "Carga de trabajo", relacion = "Colaborador", descripcion = "Se nos asignan turnos dobles sin aviso previo.", anonima = false }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString();
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var id = (await admin.GetFromJsonAsync<JsonElement>("/api/denuncias")).EnumerateArray().First(d => d.GetProperty("codigo").GetString() == codigo).GetProperty("id").GetInt32();
        Assert.Equal("Florencia Alamos Ruiz", (await admin.GetFromJsonAsync<JsonElement>($"/api/denuncias/{id}")).GetProperty("denunciante").GetString());
    }

    [Fact]
    public async Task Muro_con_publicaciones_vigentes()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        var muro = (await colab.GetFromJsonAsync<JsonElement>("/api/publicaciones/muro")).EnumerateArray().Select(p => p.GetProperty("titulo").GetString()).ToList();
        Assert.Equal("Bienvenidos a Nexo RH", muro[0]);                          // fijada
        Assert.Contains("Nuevos beneficios de salud", muro);
        Assert.DoesNotContain("Recordatorio: cierre de la sede el 13/03", muro);  // expiró el 14/03
        Assert.DoesNotContain("Campaña de vacunación 2026", muro);                // programada para el 20/03
        Assert.DoesNotContain("Feriados de abril", muro);                         // borrador
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.PostAsJsonAsync("/api/publicaciones", new { titulo = "x", texto = "y" })).StatusCode);

        var admin = await api.ClienteDe(ApiFixture.Admin);
        var p = await (await admin.PostAsJsonAsync("/api/publicaciones", new { titulo = "Aviso QA", texto = "Contenido", fijada = false })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Borrador", p.GetProperty("estado").GetString());
        var id = p.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/publicaciones/{id}", new { titulo = "Aviso QA", texto = "Contenido", publicarEl = "10/03/2026", expira = "01/03/2026" })).StatusCode);
        var pub = await (await admin.PutAsJsonAsync($"/api/publicaciones/{id}", new { titulo = "Aviso QA", texto = "Contenido", publicarEl = "10/03/2026" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Publicado", pub.GetProperty("estado").GetString());
        Assert.Contains("Aviso QA", (await colab.GetFromJsonAsync<JsonElement>("/api/publicaciones/muro")).EnumerateArray().Select(x => x.GetProperty("titulo").GetString()));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/publicaciones/{id}")).StatusCode);
    }
}
