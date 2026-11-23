using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Controllers;

public record CorreoBandeja(long Id, string Para, string Asunto, string? Enlace, string Estado, DateTime CreadoEn, string? Remitente, string? DkimDominio);

/// <summary>Bandeja de correos de desarrollo: los correos que la plataforma habría enviado. Solo existe en Development.</summary>
[ApiController]
[Route("api/dev/correos")]
[AllowAnonymous]
public class DesarrolloController(CatalogoDbContext catalogo, IWebHostEnvironment entorno) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CorreoBandeja>>> Listar()
    {
        if (!entorno.IsDevelopment()) return NotFound();
        return await catalogo.Correos.AsNoTracking().OrderByDescending(c => c.Id).Take(50)
            .Select(c => new CorreoBandeja(c.Id, c.Para, c.Asunto, c.Enlace, c.Estado, c.CreadoEn, c.Remitente, c.DkimDominio)).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Ver(long id)
    {
        if (!entorno.IsDevelopment()) return NotFound();
        var c = await catalogo.Correos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        // La bandeja del frontend lo muestra en un iframe con srcdoc y sandbox (sin scripts).
        return Content(c.Html, "text/html; charset=utf-8");
    }
}
