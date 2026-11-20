using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Configuraciones de módulos (formularios de Configuraciones de cada módulo). Las de Control de Asistencia las leen
/// todos los usuarios; las demás, solo administradores. Solo los administradores las cambian.
/// </summary>
[ApiController]
[Route("api/configuracion")]
[Authorize]
public class ConfiguracionController(NexoDbContext db, Configuraciones configuraciones, Auditoria auditoria, Alcance alcance) : ControllerBase
{
    private static readonly Dictionary<string, string> Titulos = new()
    {
        ["ca-tolerancias"] = "Control de Asistencia · Tolerancias y alertas",
        ["ca-metodos"] = "Control de Asistencia · Métodos de marcación",
        ["documentos"] = "Documentos · Configuraciones",
        ["compensaciones"] = "Compensaciones · Configuraciones",
        ["encuestas"] = "Encuestas · Configuraciones",
        ["denuncias"] = "Canal de Denuncias · Configuraciones",
        ["sst"] = "SST · Configuraciones",
        ["notificaciones"] = "Notificaciones",
    };

    /// <summary>Todas las configuraciones que el usuario puede leer.</summary>
    [HttpGet]
    public async Task<Dictionary<string, Dictionary<string, object>>> Todas()
    {
        var r = new Dictionary<string, Dictionary<string, object>>();
        foreach (var c in configuraciones.Claves.Where(c => alcance.EsAdmin || Configuraciones.Publica(c))) r[c] = await configuraciones.LeerAsync(c);
        return r;
    }

    [HttpGet("{clave}")]
    public async Task<ActionResult<Dictionary<string, object>>> Leer(string clave)
    {
        if (!configuraciones.Existe(clave)) return NotFound();
        if (!alcance.EsAdmin && !Configuraciones.Publica(clave)) return Forbid();
        return await configuraciones.LeerAsync(clave);
    }

    /// <summary>Cambia los campos enviados (los demás quedan como están) y registra cada cambio en el historial.</summary>
    [HttpPut("{clave}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<Dictionary<string, object>>> Guardar(string clave, JsonElement valores)
    {
        if (!configuraciones.Existe(clave)) return NotFound();
        var errores = configuraciones.Validar(clave, valores, out var limpios);
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores) { Title = "Revisa los valores de la configuración." });

        var actual = await configuraciones.LeerAsync(clave);
        var cambios = limpios.Where(kv => !Equals(actual[kv.Key], kv.Value)).ToList();
        if (cambios.Count == 0) return actual;
        foreach (var (k, v) in cambios)
        {
            auditoria.Registrar(Titulos.GetValueOrDefault(clave, clave), "Modificar", propiedad: k, anterior: Texto(actual[k]), actual: Texto(v));
            actual[k] = v;
        }
        var fila = await db.Configuraciones.FindAsync(clave);
        if (fila is null) db.Configuraciones.Add(fila = new Configuracion { Clave = clave });
        fila.Valores = JsonSerializer.Serialize(actual);
        fila.ActualizadoEn = DateTime.UtcNow;
        fila.ModificadoPor = User.Nombre();
        await db.SaveChangesAsync();
        return actual;
    }

    private static string Texto(object v) => v switch { true => "Sí", false => "No", decimal d => d.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture), _ => v.ToString() ?? "—" };
}
