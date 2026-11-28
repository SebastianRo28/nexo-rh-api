using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>Reacciones y comentarios de un elemento del muro; `Mia` es la reacción de quien consulta (o null).</summary>
public record InteraccionMuroDto(string Elemento, int Id, int Like, int Love, int Smile, string? Mia, int Comentarios);
public record ComentarioMuroDto(int Id, string Autor, string Texto, string Fecha, bool Propio, bool PuedeBorrar);
public record ReaccionPeticion(string? Tipo);
public record ComentarioPeticion(string? Texto);

/// <summary>
/// Interacciones del muro de Mi Portal: reacciones (Me gusta, Me encanta, Me divierte; una por persona) y comentarios en las
/// publicaciones vigentes y en los reconocimientos. Las ven todos; el autor borra su comentario y un administrador cualquiera.
/// </summary>
[ApiController]
[Route("api/muro")]
[RequiereModulo(Modulos.Comms)]
[Authorize]
public class MuroController(NexoDbContext db, RelojAsistencia reloj) : ControllerBase
{
    private const int MaxComentario = 500;

    /// <summary>Usuario del tenant que hace la petición (null en una sesión de soporte).</summary>
    private int? UsuarioId => int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;
    private bool EsAdmin => User.IsInRole(Rol.Administrador.ToString());

    /// <summary>Contadores de todos los elementos con alguna interacción (el muro tiene pocos elementos vigentes).</summary>
    [HttpGet("interacciones")]
    public async Task<List<InteraccionMuroDto>> Interacciones()
    {
        var yo = UsuarioId;
        var reacciones = await db.ReaccionesMuro.AsNoTracking()
            .GroupBy(r => new { r.Elemento, r.ElementoId, r.Tipo }).Select(g => new { g.Key.Elemento, g.Key.ElementoId, g.Key.Tipo, N = g.Count() }).ToListAsync();
        var mias = yo is null ? [] : await db.ReaccionesMuro.AsNoTracking().Where(r => r.UsuarioId == yo).Select(r => new { r.Elemento, r.ElementoId, r.Tipo }).ToListAsync();
        var comentarios = await db.ComentariosMuro.AsNoTracking()
            .GroupBy(c => new { c.Elemento, c.ElementoId }).Select(g => new { g.Key.Elemento, g.Key.ElementoId, N = g.Count() }).ToListAsync();
        return reacciones.Select(r => (r.Elemento, r.ElementoId)).Concat(comentarios.Select(c => (c.Elemento, c.ElementoId))).Distinct()
            .Select(k => new InteraccionMuroDto(k.Elemento, k.ElementoId,
                reacciones.Where(r => r.Elemento == k.Elemento && r.ElementoId == k.ElementoId && r.Tipo == "like").Sum(r => r.N),
                reacciones.Where(r => r.Elemento == k.Elemento && r.ElementoId == k.ElementoId && r.Tipo == "love").Sum(r => r.N),
                reacciones.Where(r => r.Elemento == k.Elemento && r.ElementoId == k.ElementoId && r.Tipo == "smile").Sum(r => r.N),
                mias.FirstOrDefault(m => m.Elemento == k.Elemento && m.ElementoId == k.ElementoId)?.Tipo,
                comentarios.FirstOrDefault(c => c.Elemento == k.Elemento && c.ElementoId == k.ElementoId)?.N ?? 0))
            .ToList();
    }

    /// <summary>Reacciona (tipo like, love o smile) o quita la reacción (tipo null). Devuelve los contadores del elemento.</summary>
    [HttpPut("{elemento}/{id:int}/reaccion")]
    public async Task<ActionResult<InteraccionMuroDto>> Reaccionar(string elemento, int id, ReaccionPeticion p)
    {
        if (await Validar(elemento, id) is { } error) return error;
        if (UsuarioId is not int yo) return Problem("Una sesión de soporte no puede reaccionar.", statusCode: 403);
        if (p.Tipo is not null && !ElementosMuro.Reacciones.Contains(p.Tipo)) return Problem("Reacción no válida.", statusCode: 400);

        var actual = await db.ReaccionesMuro.FirstOrDefaultAsync(r => r.Elemento == elemento && r.ElementoId == id && r.UsuarioId == yo);
        if (p.Tipo is null) { if (actual is not null) db.ReaccionesMuro.Remove(actual); }
        else if (actual is null)
            db.ReaccionesMuro.Add(new ReaccionMuro { Elemento = elemento, ElementoId = id, UsuarioId = yo, ColaboradorId = ColaboradorId, Tipo = p.Tipo });
        else { actual.Tipo = p.Tipo; actual.Fecha = DateTime.UtcNow; }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Problem("No se pudo guardar la reacción. Vuelve a intentarlo.", statusCode: 409); } // doble clic simultáneo

        return await Resumen(elemento, id, yo);
    }

    [HttpGet("{elemento}/{id:int}/comentarios")]
    public async Task<ActionResult<List<ComentarioMuroDto>>> Comentarios(string elemento, int id)
    {
        if (await Validar(elemento, id) is { } error) return error;
        return (await db.ComentariosMuro.AsNoTracking().Where(c => c.Elemento == elemento && c.ElementoId == id).OrderBy(c => c.Fecha).ThenBy(c => c.Id).ToListAsync())
            .Select(Dto).ToList();
    }

    [HttpPost("{elemento}/{id:int}/comentarios")]
    public async Task<ActionResult<ComentarioMuroDto>> Comentar(string elemento, int id, ComentarioPeticion p)
    {
        if (await Validar(elemento, id) is { } error) return error;
        if (UsuarioId is not int yo) return Problem("Una sesión de soporte no puede comentar.", statusCode: 403);
        var texto = (p.Texto ?? "").Trim();
        if (texto.Length is 0 or > MaxComentario) return Problem($"Escribe el comentario (máximo {MaxComentario} caracteres).", statusCode: 400);
        var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == yo);
        if (u is null) return Unauthorized();
        var c = db.ComentariosMuro.Add(new ComentarioMuro
        {
            Elemento = elemento, ElementoId = id, UsuarioId = yo, ColaboradorId = ColaboradorId, Autor = u.Nombre.Length > 170 ? u.Nombre[..170] : u.Nombre, Texto = texto,
        }).Entity;
        await db.SaveChangesAsync();
        return Dto(c);
    }

    [HttpDelete("comentarios/{id:int}")]
    public async Task<IActionResult> BorrarComentario(int id)
    {
        var c = await db.ComentariosMuro.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (!PuedeBorrar(c)) return Problem("Solo puedes borrar tus comentarios.", statusCode: 403);
        db.ComentariosMuro.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private int? ColaboradorId => int.TryParse(User.FindFirst(Claves.ColaboradorId)?.Value, out var c) ? c : null;
    private bool PuedeBorrar(ComentarioMuro c) => EsAdmin || (c.UsuarioId is not null && c.UsuarioId == UsuarioId);
    private ComentarioMuroDto Dto(ComentarioMuro c) =>
        new(c.Id, c.Autor, c.Texto, Fechas.TextoHora(c.Fecha), c.UsuarioId is not null && c.UsuarioId == UsuarioId, PuedeBorrar(c));

    private async Task<InteraccionMuroDto> Resumen(string elemento, int id, int yo)
    {
        var rs = await db.ReaccionesMuro.AsNoTracking().Where(r => r.Elemento == elemento && r.ElementoId == id).Select(r => new { r.Tipo, r.UsuarioId }).ToListAsync();
        var n = await db.ComentariosMuro.CountAsync(c => c.Elemento == elemento && c.ElementoId == id);
        return new(elemento, id, rs.Count(r => r.Tipo == "like"), rs.Count(r => r.Tipo == "love"), rs.Count(r => r.Tipo == "smile"),
            rs.FirstOrDefault(r => r.UsuarioId == yo)?.Tipo, n);
    }

    /// <summary>El elemento existe y se ve en el muro: publicación publicada y vigente, o reconocimiento.</summary>
    private async Task<ActionResult?> Validar(string elemento, int id)
    {
        var hoy = reloj.Hoy;
        var existe = elemento switch
        {
            ElementosMuro.Publicacion => await db.Publicaciones.AnyAsync(p => p.Id == id && p.PublicarEl != null && p.PublicarEl <= hoy && (p.Expira == null || p.Expira >= hoy)),
            ElementosMuro.Reconocimiento => await db.Reconocimientos.AnyAsync(r => r.Id == id),
            _ => false,
        };
        return existe ? null : Problem("La publicación ya no está en el muro.", statusCode: 404);
    }
}
