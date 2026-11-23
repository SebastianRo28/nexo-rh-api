using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Data;

namespace NexoRh.Api.Tests;

/// <summary>
/// Levanta la API en memoria contra una base de SQL Server propia de la corrida (se crea con las migraciones y los datos
/// de ejemplo, y se borra al terminar). La cadena base se toma de NEXO_PRUEBAS_SQL o usa .\SQLEXPRESS.
/// </summary>
public class ApiFixture : WebApplicationFactory<Program>
{
    /// <summary>Contraseña de los usuarios de ejemplo en esta corrida (aleatoria, solo existe en memoria).</summary>
    public string PasswordSemilla { get; } = "Prueba" + RandomNumberGenerator.GetInt32(100_000, 999_999) + "x";

    private readonly string baseDatos = $"NexoRh_Pruebas_{Guid.NewGuid():N}";

    public ApiFixture()
    {
        var servidor = Environment.GetEnvironmentVariable("NEXO_PRUEBAS_SQL") ?? @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True";
        // Program lee la configuración antes de Build(): las variables de entorno llegan a tiempo.
        Environment.SetEnvironmentVariable("ConnectionStrings__Nexo", $"{servidor};Database={baseDatos}");
        Environment.SetEnvironmentVariable("Jwt__Clave", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        Environment.SetEnvironmentVariable("Semilla__PasswordInicial", PasswordSemilla);
        Environment.SetEnvironmentVariable("BaseDatos__MigrarAlIniciar", "true");
        Environment.SetEnvironmentVariable("Seguridad__LoginPorMinuto", "1000");
        Environment.SetEnvironmentVariable("Semilla__DatosDemo", "true");
        // El cliente de pruebas llama a http://localhost: se resuelve al tenant de ejemplo. Otros tenants, por su subdominio.
        Environment.SetEnvironmentVariable("Plataforma__Dominio", "localhost");
        Environment.SetEnvironmentVariable("Plataforma__TenantPorDefecto", "comercialandina");
        Environment.SetEnvironmentVariable("Seguridad__ForzarHttps", "false");
        Environment.SetEnvironmentVariable("BaseDatos__EsperaMantenimientoSegundos", "0");
        // Los datos de ejemplo de asistencia son de marzo de 2026.
        Environment.SetEnvironmentVariable("Asistencia__FechaCorte", "16/03/2026");
        Environment.SetEnvironmentVariable("Asistencia__HoraCorte", "11:45");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Pruebas");

    public const string Admin = "r.paredes@comercialandina.pe";
    public const string Supervisor = "g.navarro@comercialandina.pe";
    public const string Colaborador = "f.alamos@comercialandina.pe";

    /// <summary>Cliente con la sesión de un usuario de ejemplo.</summary>
    public async Task<HttpClient> ClienteDe(string correo, string? password = null)
    {
        var (c, _) = await Login(correo, password ?? PasswordSemilla);
        return c;
    }

    public async Task<(HttpClient cliente, Sesion sesion)> Login(string correo, string password)
    {
        var c = CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { correo, password });
        r.EnsureSuccessStatusCode();
        var s = (await r.Content.ReadFromJsonAsync<Sesion>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        return (c, s);
    }

    public override async ValueTask DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            // BD dedicadas que hayan quedado de las pruebas (NexoRh_Pruebas_…_{slug}).
            var cadena = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Nexo")!);
            var maestra = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cadena.ConnectionString) { InitialCatalog = "master" };
            var dedicadas = new List<string>();
            await using (var c = new Microsoft.Data.SqlClient.SqlConnection(maestra.ConnectionString))
            {
                await c.OpenAsync();
                var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT name FROM sys.databases WHERE name LIKE @p", c);
                cmd.Parameters.AddWithValue("@p", baseDatos + "[_]%");
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) dedicadas.Add(r.GetString(0));
            }
            foreach (var d in dedicadas)
                await NexoRh.Api.Catalogo.MudanzaBaseDedicada.BorrarBaseAsync(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cadena.ConnectionString) { InitialCatalog = d }.ConnectionString, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<NexoDbContext>().Database.EnsureDeletedAsync();
            await scope.ServiceProvider.GetRequiredService<NexoRh.Api.Catalogo.CatalogoDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

public record SesionUsuario(int Id, string Nombre, string Correo, string Rol, int? ColaboradorId, bool DebeCambiarPassword);
public record Sesion(string Token, SesionUsuario Usuario);
public record ColaboradorVista(int Id, string Apellidos, string Documento, decimal? SueldoNeto, bool Reservado);
public record UsuarioVista(int Id, string Nombre, string Correo, string Rol, int? ColaboradorId, bool Activo, bool Bloqueado, bool DebeCambiarPassword);
public record UsuarioCreadoVista(UsuarioVista Usuario, string PasswordTemporal);
public record Problema(string? Title, string? Detail, string? Codigo, Dictionary<string, string[]>? Errors);
