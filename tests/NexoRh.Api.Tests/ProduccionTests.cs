using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Data;

namespace NexoRh.Api.Tests;

/// <summary>Configuración de producción: permisos de todos los endpoints, cabeceras, arranque seguro e instalación limpia.</summary>
[Collection("api")]
public class ProduccionTests(ApiFixture api)
{
    /// <summary>Ningún endpoint queda abierto por olvido: cada acción exige sesión salvo las públicas a propósito.</summary>
    [Fact]
    public void Todos_los_endpoints_exigen_sesion_salvo_los_publicos()
    {
        var publicos = new HashSet<string>
        {
            "AuthController.Login", "AuthController.DobleFactor", "AuthController.Canjear", "AuthController.Olvide", "AuthController.VerToken",
            "AuthController.Restablecer", "AuthController.Activar",
            "PublicoController.VerEntrada", "PublicoController.EnviarEnlaces", "PublicoController.BuscarCuentas",
            "PublicoController.VerMarca", "PublicoController.VerArchivo", "AuthController.Soporte",
            // El proxy pregunta si puede emitir el certificado de un dominio propio (solo responde 200 o 404).
            "PublicoController.PermitirCertificado",
            // Notificaciones de Izipay (IPN): sin sesión, autenticadas por la firma HMAC del payload.
            "IpnIzipayController.Notificar",
            // Consola del proveedor: login con doble factor (solo en admin.{dominio}).
            "PlataformaAuthController.Login", "PlataformaAuthController.Verificar",
            // Solo responden en Development (404 en cualquier otro entorno).
            "DesarrolloController.Listar", "DesarrolloController.Ver", "DnsDesarrolloController.Publicar", "DnsDesarrolloController.Quitar",
        };
        var abiertos = new List<string>();
        foreach (var ctrl in typeof(Program).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var claseAutorizada = ctrl.GetCustomAttributes<AuthorizeAttribute>(true).Any();
            foreach (var m in ctrl.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute)))
            {
                var nombre = $"{ctrl.Name}.{m.Name}";
                var anonimo = m.GetCustomAttributes<AllowAnonymousAttribute>().Any() || ctrl.GetCustomAttributes<AllowAnonymousAttribute>().Any();
                if (anonimo ? !publicos.Contains(nombre) : !claseAutorizada && !m.GetCustomAttributes<AuthorizeAttribute>().Any()) abiertos.Add(nombre);
            }
        }
        Assert.Empty(abiertos);
    }

    [Fact]
    public async Task Cabeceras_de_seguridad_y_salud()
    {
        var r = await api.CreateClient().GetAsync("/api/salud");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("ok", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("baseDatos").GetString());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(r.Headers.Contains("Server"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/reportes")).StatusCode);
    }

    private sealed class Fabrica(string entorno) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(entorno);
    }

    /// <summary>Corre la acción con variables de entorno temporales (la API lee su configuración al construirse).</summary>
    private async Task ConVariables(Dictionary<string, string?> vars, Func<Task> accion)
    {
        _ = api.Services;   // la API compartida ya quedó construida con su propia configuración
        var antes = vars.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        foreach (var (k, v) in vars) Environment.SetEnvironmentVariable(k, v);
        try { await accion(); }
        finally { foreach (var (k, v) in antes) Environment.SetEnvironmentVariable(k, v); }
    }

    [Fact]
    public async Task Produccion_no_arranca_con_configuracion_de_desarrollo()
    {
        await ConVariables(new() { ["Cors__Origenes__0"] = "https://rrhh.ejemplo.pe", ["BaseDatos__MigrarAlIniciar"] = "false" }, () =>
        {
            // La fecha fija de los datos de ejemplo (la deja el fixture) no se permite en producción.
            using var f = new Fabrica("Production");
            var e = Assert.ThrowsAny<Exception>(() => f.CreateClient());
            Assert.Contains("FechaCorte", e.ToString());
            return Task.CompletedTask;
        });
        await ConVariables(new() { ["Asistencia__FechaCorte"] = null, ["Semilla__DatosDemo"] = null, ["Cors__Origenes__0"] = "http://localhost:4200", ["BaseDatos__MigrarAlIniciar"] = "false" }, () =>
        {
            using var f = new Fabrica("Production");
            var e = Assert.ThrowsAny<Exception>(() => f.CreateClient());
            Assert.Contains("Cors:Origenes", e.ToString());
            return Task.CompletedTask;
        });
    }

    /// <summary>Sin datos de ejemplo: solo datos base, la planilla del mes y un administrador que debe cambiar su contraseña.</summary>
    [Fact]
    public async Task Instalacion_limpia_sin_datos_de_ejemplo()
    {
        var servidor = Environment.GetEnvironmentVariable("NEXO_PRUEBAS_SQL") ?? @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True";
        var password = "Inicial-" + RandomNumberGenerator.GetInt32(100_000, 999_999) + "x";
        await ConVariables(new()
        {
            ["ConnectionStrings__Nexo"] = $"{servidor};Database=NexoRh_Limpia_{Guid.NewGuid():N}",
            ["Semilla__DatosDemo"] = "false", ["Semilla__AdminCorreo"] = "Admin@Empresa.pe", ["Semilla__AdminNombre"] = "Administración RR.HH.",
            ["Semilla__PasswordInicial"] = password, ["Asistencia__FechaCorte"] = "20/11/2026",
            ["Semilla__TenantSlug"] = "empresa", ["Semilla__TenantNombre"] = "Empresa Nueva S.A.C.", ["Plataforma__TenantPorDefecto"] = "empresa",
        }, async () =>
        {
            await using var f = new Fabrica("Pruebas");
            try
            {
                var c = f.CreateClient();
                var login = await c.PostAsJsonAsync("/api/auth/login", new { correo = "admin@empresa.pe", password });
                login.EnsureSuccessStatusCode();
                var s = (await login.Content.ReadFromJsonAsync<Sesion>())!;
                Assert.True(s.Usuario.DebeCambiarPassword);
                c.DefaultRequestHeaders.Authorization = new("Bearer", s.Token);
                Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/colaboradores")).StatusCode);

                using var scope = f.Services.CreateScope();
                var catalogo = scope.ServiceProvider.GetRequiredService<NexoRh.Api.Catalogo.CatalogoDbContext>();
                var tenant = Assert.Single(await catalogo.Tenants.ToListAsync());
                Assert.Equal("empresa", tenant.Slug);
                scope.ServiceProvider.GetRequiredService<NexoRh.Api.Catalogo.ContextoTenant>().Usar(tenant);
                var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
                Assert.Equal(0, await db.Colaboradores.CountAsync());
                Assert.Equal(1, await db.Usuarios.CountAsync());
                Assert.Equal(3, await db.Perfiles.CountAsync());
                Assert.True(await db.TiposItem.AnyAsync() && await db.ParametrosRem.AnyAsync() && await db.Feriados.AnyAsync() && await db.FilasCatalogo.AnyAsync());
                Assert.Equal([202611], await db.Periodos.Select(p => p.Id).ToListAsync());
                Assert.False(await db.Reportes.AnyAsync() || await db.Activos.AnyAsync() || await db.Encuestas.AnyAsync());
            }
            finally
            {
                using var scope = f.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NexoDbContext>().Database.EnsureDeletedAsync();
                await scope.ServiceProvider.GetRequiredService<NexoRh.Api.Catalogo.CatalogoDbContext>().Database.EnsureDeletedAsync();
            }
        });
    }
}
