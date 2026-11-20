using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NexoRh.Api.Tests;

[CollectionDefinition("api")]
public class ColeccionApi : ICollectionFixture<ApiFixture>;

[Collection("api")]
public class AutenticacionTests(ApiFixture api)
{
    [Fact]
    public async Task Sin_token_responde_401()
    {
        var r = await api.CreateClient().GetAsync("/api/colaboradores");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Contraseña_errada_y_correo_inexistente_dan_el_mismo_error()
    {
        var c = api.CreateClient();
        var a = await c.PostAsJsonAsync("/api/auth/login", new { correo = ApiFixture.Admin, password = "incorrecta-123" });
        var b = await c.PostAsJsonAsync("/api/auth/login", new { correo = "nadie@comercialandina.pe", password = "incorrecta-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, a.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, b.StatusCode);
        Assert.Equal((await a.Content.ReadFromJsonAsync<Problema>())!.Detail, (await b.Content.ReadFromJsonAsync<Problema>())!.Detail);
    }

    [Fact]
    public async Task Login_devuelve_rol_y_colaborador()
    {
        var (_, s) = await api.Login(ApiFixture.Colaborador, api.PasswordSemilla);
        Assert.Equal("Colaborador", s.Usuario.Rol);
        Assert.Equal(0, s.Usuario.ColaboradorId);
        Assert.False(s.Usuario.DebeCambiarPassword);
    }

    [Fact]
    public async Task Cinco_fallos_bloquean_la_cuenta()
    {
        var (correo, _) = await Usuarios.CrearConPassword(api, "Colaborador", 3);
        var c = api.CreateClient();
        for (var i = 0; i < 5; i++) await c.PostAsJsonAsync("/api/auth/login", new { correo, password = "incorrecta-123" });
        var r = await c.PostAsJsonAsync("/api/auth/login", new { correo, password = "incorrecta-123" });
        Assert.Equal((HttpStatusCode)423, r.StatusCode);
    }
}

[Collection("api")]
public class VisibilidadTests(ApiFixture api)
{
    [Fact]
    public async Task Colaborador_solo_ve_completos_sus_propios_datos()
    {
        var c = await api.ClienteDe(ApiFixture.Colaborador);
        var lista = (await c.GetFromJsonAsync<List<ColaboradorVista>>("/api/colaboradores"))!;
        var completos = lista.Where(x => !x.Reservado).Select(x => x.Id).ToList();
        Assert.Equal([0], completos);
        var otro = lista.First(x => x.Id != 0);
        Assert.Null(otro.SueldoNeto);
        Assert.StartsWith("••••", otro.Documento);
    }

    [Fact]
    public async Task Supervisor_ve_completo_a_su_equipo()
    {
        var c = await api.ClienteDe(ApiFixture.Supervisor);
        var lista = (await c.GetFromJsonAsync<List<ColaboradorVista>>("/api/colaboradores"))!;
        var completos = lista.Where(x => !x.Reservado).Select(x => x.Id).Order().ToList();
        Assert.Equal([0, 6, 11, 14], completos);
    }

    [Fact]
    public async Task Colaborador_no_puede_crear_colaboradores_ni_ver_usuarios()
    {
        var c = await api.ClienteDe(ApiFixture.Colaborador);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/colaboradores", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/cambios")).StatusCode);
    }

    [Fact]
    public async Task Crear_colaborador_valida_el_DNI()
    {
        var c = await api.ClienteDe(ApiFixture.Admin);
        var r = await c.PostAsJsonAsync("/api/colaboradores", new
        {
            apellidos = "Prueba", nombres = "Ana", documento = "12AB", cargo = "Analista de Planillas", division = "Administración",
            area = "Personas", subArea = "Compensaciones", ingreso = "01/03/2026", sueldoNeto = 3000, detalle = new { tipoDocumento = "DNI" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var p = (await r.Content.ReadFromJsonAsync<Problema>())!;
        Assert.Contains("documento", p.Errors!.Keys, StringComparer.OrdinalIgnoreCase);
    }
}

[Collection("api")]
public class UsuariosTests(ApiFixture api)
{
    [Fact]
    public async Task Usuario_nuevo_debe_cambiar_su_contraseña_temporal()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var creado = await Usuarios.Crear(admin, "Colaborador", 4);
        Assert.True(creado.Usuario.DebeCambiarPassword);
        Assert.True(creado.PasswordTemporal.Length >= 10);

        var (c, s) = await api.Login(creado.Usuario.Correo, creado.PasswordTemporal);
        Assert.True(s.Usuario.DebeCambiarPassword);
        var bloqueado = await c.GetAsync("/api/colaboradores");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
        Assert.Equal("password_temporal", (await bloqueado.Content.ReadFromJsonAsync<Problema>())!.Codigo);

        // Contraseña que no cumple la política.
        var corta = await c.PostAsJsonAsync("/api/auth/password", new { actual = creado.PasswordTemporal, nueva = "abc123" });
        Assert.Equal(HttpStatusCode.BadRequest, corta.StatusCode);

        var r = await c.PostAsJsonAsync("/api/auth/password", new { actual = creado.PasswordTemporal, nueva = "NuevaClave2026" });
        r.EnsureSuccessStatusCode();
        var nueva = (await r.Content.ReadFromJsonAsync<Sesion>())!;
        Assert.False(nueva.Usuario.DebeCambiarPassword);

        // El token anterior deja de funcionar; el nuevo sí funciona.
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nueva.Token);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/colaboradores")).StatusCode);
    }

    [Fact]
    public async Task Desactivar_cierra_la_sesion_al_instante()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var (correo, password) = await Usuarios.CrearConPassword(api, "Colaborador", 5);
        var (c, s) = await api.Login(correo, password);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/me")).StatusCode);

        (await admin.PostAsJsonAsync($"/api/usuarios/{s.Usuario.Id}/estado", new { activo = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        var login = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { correo, password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Administrador_no_puede_desactivarse_a_si_mismo()
    {
        var (admin, s) = await api.Login(ApiFixture.Admin, api.PasswordSemilla);
        var r = await admin.PostAsJsonAsync($"/api/usuarios/{s.Usuario.Id}/estado", new { activo = false });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Valida_correo_duplicado_y_colaborador_obligatorio()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var dup = await admin.PostAsJsonAsync("/api/usuarios", new { nombre = "Otra", correo = ApiFixture.Colaborador, rol = "Administrador" });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        var sinColab = await admin.PostAsJsonAsync("/api/usuarios", new { nombre = "Otra", correo = "otra.persona@comercialandina.pe", rol = "Supervisor" });
        Assert.Equal(HttpStatusCode.BadRequest, sinColab.StatusCode);
        var yaVinculado = await admin.PostAsJsonAsync("/api/usuarios", new { nombre = "Otra", correo = "otra2@comercialandina.pe", rol = "Colaborador", colaboradorId = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, yaVinculado.StatusCode);
    }

    [Fact]
    public async Task Restablecer_genera_otra_contraseña_temporal_y_queda_en_el_historial()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var creado = await Usuarios.Crear(admin, "Colaborador", 7);
        var r = await admin.PostAsync($"/api/usuarios/{creado.Usuario.Id}/restablecer", null);
        r.EnsureSuccessStatusCode();
        var nuevo = (await r.Content.ReadFromJsonAsync<UsuarioCreadoVista>())!;
        Assert.NotEqual(creado.PasswordTemporal, nuevo.PasswordTemporal);
        // La contraseña anterior ya no sirve.
        var viejo = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { correo = creado.Usuario.Correo, password = creado.PasswordTemporal });
        Assert.Equal(HttpStatusCode.Unauthorized, viejo.StatusCode);

        var historial = await admin.GetStringAsync($"/api/cambios?q=Usuario%20%23{creado.Usuario.Id}");
        Assert.Contains("Restablecida", historial);
    }
}

/// <summary>Ayudas para crear cuentas de prueba vinculadas a colaboradores de la semilla.</summary>
internal static class Usuarios
{
    public static async Task<UsuarioCreadoVista> Crear(HttpClient admin, string rol, int colaboradorId)
    {
        var correo = $"prueba.{Guid.NewGuid():N}"[..20] + "@comercialandina.pe";
        var r = await admin.PostAsJsonAsync("/api/usuarios", new { nombre = "Usuario de Prueba", correo, rol, colaboradorId });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<UsuarioCreadoVista>())!;
    }

    /// <summary>Crea la cuenta y fija una contraseña definitiva; devuelve correo y contraseña.</summary>
    public static async Task<(string correo, string password)> CrearConPassword(ApiFixture api, string rol, int colaboradorId)
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var creado = await Crear(admin, rol, colaboradorId);
        var (c, _) = await api.Login(creado.Usuario.Correo, creado.PasswordTemporal);
        const string password = "Definitiva2026x";
        (await c.PostAsJsonAsync("/api/auth/password", new { actual = creado.PasswordTemporal, nueva = password })).EnsureSuccessStatusCode();
        return (creado.Usuario.Correo, password);
    }
}
