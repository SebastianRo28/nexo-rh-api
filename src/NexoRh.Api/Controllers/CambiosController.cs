using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>Historial de cambios (Administración › Historial de cambios).</summary>
[ApiController]
[Route("api/cambios")]
[RequiereModulo(Modulos.Pro)]
[Authorize]
public class CambiosController(NexoDbContext db, Auditoria auditoria) : ControllerBase
{
    private static readonly string[] Acciones = ["Crear", "Modificar", "Eliminar"];

    /// <summary>Cambios más recientes primero. Filtros opcionales: texto, acción y rango de fechas (dd/MM/yyyy).</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<Pagina<CambioDto>>> Listar(string? q, string? accion, string? desde, string? hasta, int pagina = 1, int tamano = 500)
    {
        tamano = Math.Clamp(tamano, 1, 1000);
        pagina = Math.Max(1, pagina);
        var query = db.Cambios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(c => c.Colaborador.Contains(t) || c.Documento.Contains(t) || c.Entidad.Contains(t) || c.Usuario.Contains(t) || c.Propiedad.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(accion)) query = query.Where(c => c.Accion == accion);
        if (Fechas.TryLeer(desde, out var d)) { var u = d.ToDateTime(TimeOnly.MinValue) - Fechas.Lima; query = query.Where(c => c.Fecha >= u); }
        if (Fechas.TryLeer(hasta, out var h)) { var u = h.AddDays(1).ToDateTime(TimeOnly.MinValue) - Fechas.Lima; query = query.Where(c => c.Fecha < u); }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return new Pagina<CambioDto>(total, items.Select(c => new CambioDto(c.Id, c.Usuario, c.Origen, c.Colaborador, c.Documento, c.Entidad, c.Accion,
            Fechas.TextoHora(c.Fecha), c.Propiedad, c.Anterior, c.Actual)).ToList());
    }

    /// <summary>
    /// Registra un cambio hecho en un módulo del frontend que aún no tiene API propia. El usuario, la fecha y el
    /// origen los pone el servidor.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Registrar(CambioNuevo p)
    {
        var entidad = (p.Entidad ?? "").Trim();
        if (entidad.Length == 0) return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["entidad"] = ["Indica la entidad modificada."] }));
        if (!Acciones.Contains(p.Accion)) return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["accion"] = ["La acción debe ser Crear, Modificar o Eliminar."] }));
        // El colaborador y el documento llegan del cliente: son solo etiquetas, nunca se usan para permisos.
        auditoria.RegistrarTexto(entidad, p.Accion!, p.Colaborador, p.Documento, p.Propiedad, p.Anterior, p.Actual);
        await db.SaveChangesAsync();
        return NoContent();
    }

}
