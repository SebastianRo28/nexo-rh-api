using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record PublicacionDto(int Id, string Titulo, string Texto, string Autor, bool Fijada, string Estado, string Publicada, string Expira,
    int Vistas, int Comentarios, int Reacciones, int? Engagement);
public record PublicacionGuardar(string? Titulo, string? Texto, string? Autor, bool Fijada, string? PublicarEl, string? Expira);

/// <summary>
/// Publicaciones del muro de Mi Portal (Comunicaciones › Portal). Las administra RR.HH.; todos ven las publicadas y
/// vigentes. Estado: Borrador (sin fecha), Programado (fecha futura) o Publicado.
/// </summary>
[ApiController]
[Route("api/publicaciones")]
[RequiereModulo(Modulos.Comms)]
[Authorize]
public class PublicacionesController(NexoDbContext db, Auditoria auditoria, RelojAsistencia reloj) : ControllerBase
{
    private string Estado(Publicacion p) => p.PublicarEl is null ? "Borrador" : p.PublicarEl > reloj.Hoy ? "Programado" : "Publicado";

    private PublicacionDto Dto(Publicacion p, int colaboradores, Dictionary<int, (int comentarios, int reacciones)> conteos) => new(p.Id, p.Titulo, p.Texto, p.Autor, p.Fijada, Estado(p),
        p.PublicarEl is { } f ? Fechas.Texto(f) : "—", p.Expira is { } e ? Fechas.Texto(e) : "—", p.Vistas,
        conteos.GetValueOrDefault(p.Id).comentarios, conteos.GetValueOrDefault(p.Id).reacciones,
        Estado(p) == "Publicado" && colaboradores > 0 ? (int)Math.Min(100, Math.Round(100.0 * p.Vistas / colaboradores)) : null);

    /// <summary>Comentarios y reacciones de cada publicación (muro de Mi Portal).</summary>
    private async Task<Dictionary<int, (int comentarios, int reacciones)>> ConteosAsync()
    {
        var c = await db.ComentariosMuro.Where(x => x.Elemento == ElementosMuro.Publicacion).GroupBy(x => x.ElementoId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var r = await db.ReaccionesMuro.Where(x => x.Elemento == ElementosMuro.Publicacion).GroupBy(x => x.ElementoId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return c.Keys.Union(r.Keys).ToDictionary(k => k, k => (c.GetValueOrDefault(k), r.GetValueOrDefault(k)));
    }

    /// <summary>Muro: publicadas y vigentes, primero las fijadas.</summary>
    [HttpGet("muro")]
    public async Task<List<PublicacionDto>> Muro()
    {
        var hoy = reloj.Hoy;
        var n = await db.Colaboradores.CountAsync();
        var conteos = await ConteosAsync();
        return (await db.Publicaciones.AsNoTracking().Where(p => p.PublicarEl != null && p.PublicarEl <= hoy && (p.Expira == null || p.Expira >= hoy))
            .OrderByDescending(p => p.Fijada).ThenByDescending(p => p.PublicarEl).ToListAsync()).Select(p => Dto(p, n, conteos)).ToList();
    }

    [HttpGet]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<List<PublicacionDto>> Listar()
    {
        var n = await db.Colaboradores.CountAsync();
        var conteos = await ConteosAsync();
        return (await db.Publicaciones.AsNoTracking().OrderByDescending(p => p.Fijada).ThenByDescending(p => p.PublicarEl ?? DateOnly.MaxValue).ToListAsync())
            .Select(p => Dto(p, n, conteos)).ToList();
    }

    [HttpPost]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<PublicacionDto>> Crear(PublicacionGuardar g) => await Guardar(null, g);

    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<PublicacionDto>> Modificar(int id, PublicacionGuardar g) => await Guardar(id, g);

    private async Task<ActionResult<PublicacionDto>> Guardar(int? id, PublicacionGuardar g)
    {
        Publicacion? p = null;
        if (id is { } i && (p = await db.Publicaciones.Buscar(i)) is null) return NotFound();
        var titulo = (g.Titulo ?? "").Trim();
        var texto = (g.Texto ?? "").Trim();
        if (titulo.Length is 0 or > 150) return Error("titulo", "Ingresa el título (máximo 150 caracteres).");
        if (texto.Length is 0 or > 4000) return Error("texto", "Escribe el contenido (máximo 4000 caracteres).");
        DateOnly? publicar = null, expira = null;
        if (!string.IsNullOrWhiteSpace(g.PublicarEl))
        {
            if (!Fechas.TryLeer(g.PublicarEl, out var f)) return Error("publicarEl", "La fecha de publicación debe tener el formato DD/MM/AAAA.");
            publicar = f;
        }
        if (!string.IsNullOrWhiteSpace(g.Expira))
        {
            if (!Fechas.TryLeer(g.Expira, out var e)) return Error("expira", "La fecha de expiración debe tener el formato DD/MM/AAAA.");
            if (publicar is null || e < publicar) return Error("expira", "La expiración debe ser posterior a la publicación.");
            expira = e;
        }
        var nueva = p is null;
        p ??= db.Publicaciones.Add(new Publicacion()).Entity;
        var antes = nueva ? null : Estado(p);
        p.Titulo = titulo;
        p.Texto = texto;
        p.Autor = (g.Autor ?? "").Trim() is { Length: > 0 and <= 80 } a ? a : "Gestión de Personas";
        p.Fijada = g.Fijada;
        p.PublicarEl = publicar;
        p.Expira = expira;
        p.ActualizadaEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (nueva) auditoria.Registrar($"Publicación #{p.Id}", "Crear", actual: $"{p.Titulo} · {Estado(p)}");
        else auditoria.Registrar($"Publicación #{p.Id}", "Modificar", propiedad: "contenido", anterior: antes!, actual: $"{p.Titulo} · {Estado(p)}");
        await db.SaveChangesAsync();
        return Dto(p, await db.Colaboradores.CountAsync(), await ConteosAsync());
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var p = await db.Publicaciones.Buscar(id);
        if (p is null) return NotFound();
        db.Publicaciones.Remove(p);
        // Sus reacciones y comentarios dejan de tener sentido.
        db.ReaccionesMuro.RemoveRange(db.ReaccionesMuro.Where(x => x.Elemento == ElementosMuro.Publicacion && x.ElementoId == id));
        db.ComentariosMuro.RemoveRange(db.ComentariosMuro.Where(x => x.Elemento == ElementosMuro.Publicacion && x.ElementoId == id));
        auditoria.Registrar($"Publicación #{p.Id}", "Eliminar", anterior: p.Titulo);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
