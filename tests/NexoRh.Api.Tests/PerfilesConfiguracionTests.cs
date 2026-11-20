using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexoRh.Api.Tests;

[Collection("api")]
public class ConfiguracionTests(ApiFixture api)
{
    [Fact]
    public async Task Solo_un_administrador_cambia_la_configuracion_y_se_valida()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        Assert.Equal(HttpStatusCode.OK, (await colab.GetAsync("/api/configuracion/ca-metodos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.PutAsJsonAsync("/api/configuracion/ca-metodos", new { web = false })).StatusCode);

        var admin = await api.ClienteDe(ApiFixture.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/configuracion/ca-tolerancias", new { minExceso = 500 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/configuracion/ca-tolerancias", new { inventado = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/configuracion/otra")).StatusCode);

        var r = await admin.PutAsJsonAsync("/api/configuracion/ca-tolerancias", new { minExceso = 45 });
        r.EnsureSuccessStatusCode();
        var v = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45, v.GetProperty("minExceso").GetInt32());
        Assert.Equal(5, v.GetProperty("toleranciaSalida").GetInt32()); // los demás quedan igual
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/asistencia");
        Assert.Equal(45, datos.GetProperty("configuracion").GetProperty("ca-tolerancias").GetProperty("minExceso").GetInt32());
        (await admin.PutAsJsonAsync("/api/configuracion/ca-tolerancias", new { minExceso = 30 })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Marcar_por_un_metodo_desactivado_queda_como_marca_fallida()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        // Carolina Flores (6) no tiene cuenta en los datos de ejemplo.
        var (correo, password) = await Usuarios.CrearConPassword(api, "Colaborador", 6);
        var c = await api.ClienteDe(correo, password);
        (await admin.PutAsJsonAsync("/api/configuracion/ca-metodos", new { web = false })).EnsureSuccessStatusCode();
        try
        {
            var r = await c.PostAsJsonAsync("/api/asistencia/marcar", new { tipo = "Entrada", metodo = "Web" });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            var datos = await c.GetFromJsonAsync<JsonElement>("/api/asistencia");
            Assert.Contains(datos.GetProperty("fallidas").EnumerateArray(), f => f.GetProperty("motivo").GetString() == "Método no permitido" && f.GetProperty("fecha").GetString() == "16/03/2026");
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/configuracion/ca-metodos", new { web = true })).EnsureSuccessStatusCode();
        }
    }
}

[Collection("api")]
public class PerfilesTests(ApiFixture api)
{
    [Fact]
    public async Task El_rol_del_perfil_define_el_de_sus_usuarios()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.ClienteDe(ApiFixture.Supervisor)).GetAsync("/api/perfiles")).StatusCode);
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var r = await admin.PostAsJsonAsync("/api/perfiles", new { nombre = "Analista QA", rol = "Supervisor", descripcion = "Prueba" });
        r.EnsureSuccessStatusCode();
        var perfil = await r.Content.ReadFromJsonAsync<JsonElement>();
        var perfilId = perfil.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/perfiles", new { nombre = "Analista QA", rol = "Supervisor" })).StatusCode);

        // Usuario con ese perfil: toma el rol Supervisor.
        var correo = $"perfil.{Guid.NewGuid():N}"[..20] + "@comercialandina.pe";
        var cr = await admin.PostAsJsonAsync("/api/usuarios", new { nombre = "Con Perfil", correo, perfilId, colaboradorId = 8 });
        Assert.Equal(HttpStatusCode.Created, cr.StatusCode);
        var creado = (await cr.Content.ReadFromJsonAsync<UsuarioCreadoVista>())!;
        Assert.Equal("Supervisor", creado.Usuario.Rol);
        var (c, _) = await api.Login(correo, creado.PasswordTemporal);
        (await c.PostAsJsonAsync("/api/auth/password", new { actual = creado.PasswordTemporal, nueva = "PerfilClave2026" })).EnsureSuccessStatusCode();
        var (c2, s) = await api.Login(correo, "PerfilClave2026");
        Assert.Equal("Supervisor", s.Usuario.Rol);

        // Cambiar el rol del perfil cambia el del usuario y cierra su sesión.
        (await admin.PutAsJsonAsync($"/api/perfiles/{perfilId}", new { nombre = "Analista QA", rol = "Colaborador" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c2.GetAsync("/api/auth/me")).StatusCode);
        var (_, s2) = await api.Login(correo, "PerfilClave2026");
        Assert.Equal("Colaborador", s2.Usuario.Rol);

        // Con usuarios no se puede borrar; los predefinidos nunca.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/perfiles/{perfilId}")).StatusCode);
        var lista = await admin.GetFromJsonAsync<JsonElement>("/api/perfiles");
        var admPerfil = lista.EnumerateArray().First(p => p.GetProperty("nombre").GetString() == "Administrador");
        Assert.True(admPerfil.GetProperty("predefinido").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/perfiles/{admPerfil.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync($"/api/perfiles/{admPerfil.GetProperty("id").GetInt32()}", new { nombre = "Administrador", rol = "Colaborador" })).StatusCode);
    }
}
