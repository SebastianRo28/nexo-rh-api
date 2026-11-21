using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record CargaDto(int Id, string Importador, string Archivo, string Usuario, string Fecha, int Registros, string Estado, List<ErrorCargaDto> Errores);

/// <summary>Reportería › Importadores › Historial de Cargas.</summary>
[ApiController]
[Route("api/cargas")]
[Authorize(Policy = Politicas.Administrador)]
public class CargasController(NexoDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<CargaDto>> Listar() =>
        (await db.Cargas.AsNoTracking().OrderByDescending(c => c.Id).Take(500).ToListAsync())
            .Select(c => new CargaDto(c.Id, c.Importador, c.Archivo, c.Usuario, Fechas.TextoHora(c.Fecha), c.Registros, c.Estado,
                c.Errores.Select(e => new ErrorCargaDto(e.Fila, e.Campo, e.Mensaje)).ToList())).ToList();
}
