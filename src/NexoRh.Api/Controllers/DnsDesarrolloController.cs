using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Controllers;

public record RegistroSimulado(string Tipo, string Nombre, string Valor);

/// <summary>
/// DNS simulado de desarrollo: publica registros (TXT, CNAME, A) en memoria para probar la verificación de dominios propios
/// y del remitente de correo sin un DNS real. Solo existe en Development con Dns:Modo = Simulado (el valor por defecto ahí).
/// </summary>
[ApiController]
[Route("api/dev/dns")]
[AllowAnonymous]
public class DnsDesarrolloController(IResolutorDns dns, IWebHostEnvironment entorno) : ControllerBase
{
    [HttpPost]
    public IActionResult Publicar(RegistroSimulado r)
    {
        if (!entorno.IsDevelopment() || dns is not ResolutorDnsSimulado simulado) return NotFound();
        if (r.Tipo?.ToUpperInvariant() is not ("TXT" or "CNAME" or "A") || string.IsNullOrWhiteSpace(r.Nombre) || string.IsNullOrWhiteSpace(r.Valor))
            return Problem("Tipo TXT, CNAME o A, con nombre y valor.", statusCode: 400);
        simulado.Publicar(r.Tipo, r.Nombre, r.Valor.Trim());
        return NoContent();
    }

    [HttpDelete]
    public IActionResult Quitar([FromQuery] string tipo, [FromQuery] string nombre)
    {
        if (!entorno.IsDevelopment() || dns is not ResolutorDnsSimulado simulado) return NotFound();
        simulado.Quitar(tipo, nombre);
        return NoContent();
    }
}
