using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>Configuraciones de módulos. Las leen todos los usuarios (las pantallas las usan para calcular); solo los administradores las cambian.</summary>
[ApiController]
[Route("api/configuracion")]
[Authorize]
public class ConfiguracionController(NexoDbContext db, Configuraciones configuraciones, Auditoria auditoria) : ControllerBase
{
    private static readonly Dictionary<string, string> Titulos = new()
    {
        ["ca-tolerancias"] = "Control de Asistencia · Tolerancias y alertas",
        ["ca-metodos"] = "Control de Asistencia · Métodos de marcación",
    };

    [HttpGet("{clave}")]
    public async Task<ActionResult<Dictionary<string, object>>> Leer(string clave) =>
        Configuraciones.Existe(clave) ? await configuraciones.LeerAsync(clave) : NotFound();

    /// <summary>Cambia los campos enviados (los demás quedan como están) y registra cada cambio en el historial.</summary>
    [HttpPut("{clave}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<Dictionary<string, object>>> Guardar(string clave, JsonElement valores)
    {
        if (!Configuraciones.Existe(clave)) return NotFound();
        var errores = Configuraciones.Validar(clave, valores, out var limpios);
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

    private static string Texto(object v) => v switch { true => "Sí", false => "No", _ => v.ToString() ?? "—" };
}
