using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record ReconocimientoDto(int Id, string Para, List<int> ParaIds, string Autor, int? AutorId, string Valor, string Mensaje, string Fecha);
public record ValorReconocimientoDto(string Nombre, string Tono, string Icono);
public record ReconocimientoNuevo(List<int>? ParaIds, string? Valor, string? Mensaje);

/// <summary>Reconocimientos entre compañeros: los ven todos (muro de Mi Portal); cualquiera con colaborador vinculado reconoce a otros.</summary>
[ApiController]
[Route("api/reconocimientos")]
[RequiereModulo(Modulos.Comms)]
[Authorize]
public class ReconocimientosController(NexoDbContext db, Alcance alcance, Auditoria auditoria) : ControllerBase
{
    /// <summary>Valores por defecto mientras la empresa no registre los suyos (colección "reconocimiento-valores").</summary>
    public static readonly ValorReconocimientoDto[] PorDefecto =
    [
        new("Trabajo en equipo", "primary", "users"), new("Compromiso", "success", "heart"),
        new("Excelencia", "orange", "star"), new("Innovación", "info", "rocket"),
    ];

    /// <summary>Valores de la empresa para reconocer: los suyos o, si no registró ninguno, los valores por defecto.</summary>
    [HttpGet("valores")]
    public async Task<List<ValorReconocimientoDto>> Valores()
    {
        var propios = (await db.RegistrosModulo.AsNoTracking().Where(r => r.Coleccion == "reconocimiento-valores").OrderBy(r => r.Id).ToListAsync())
            .Select(r => JsonNode.Parse(r.Datos)!.AsObject())
            .Where(o => o["activo"]?.GetValue<bool>() != false && !string.IsNullOrWhiteSpace(o["nombre"]?.ToString()))
            .Select(o => new ValorReconocimientoDto(o["nombre"]!.ToString().Trim(), o["tono"]?.ToString() ?? "primary", o["icono"]?.ToString() ?? "star"))
            .ToList();
        return propios.Count > 0 ? propios : [.. PorDefecto];
    }

    private static ReconocimientoDto Dto(Reconocimiento r) => new(r.Id, r.Para, [.. r.ParaIds], r.Autor, r.AutorId, r.Valor, r.Mensaje, Fechas.Texto(r.Fecha));

    [HttpGet]
    public async Task<List<ReconocimientoDto>> Listar() =>
        (await db.Reconocimientos.AsNoTracking().OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id).ToListAsync()).Select(Dto).ToList();

    [HttpPost]
    public async Task<ActionResult<ReconocimientoDto>> Crear(ReconocimientoNuevo p)
    {
        if (alcance.Propio is not { } yo || await db.Colaboradores.Buscar(yo) is not { } autor)
            return Error("paraIds", "Tu usuario no está vinculado a un colaborador.");
        var ids = (p.ParaIds ?? []).Distinct().ToList();
        if (ids.Count is 0 or > 20) return Error("paraIds", "Elige a quién reconoces (hasta 20 personas).");
        if (ids.Contains(yo)) return Error("paraIds", "No puedes reconocerte a ti mismo.");
        var para = await db.Colaboradores.Where(c => ids.Contains(c.Id)).OrderBy(c => c.Apellidos).ToListAsync();
        if (para.Count != ids.Count) return Error("paraIds", "Algún colaborador no existe.");
        if (!(await Valores()).Any(v => v.Nombre == p.Valor)) return Error("valor", "Elige uno de los valores de la empresa.");
        var mensaje = (p.Mensaje ?? "").Trim();
        if (mensaje.Length is 0 or > 300) return Error("mensaje", "Escribe el mensaje (máximo 300 caracteres).");
        var r = new Reconocimiento
        {
            Para = string.Join("; ", para.Select(c => $"{c.Apellidos}, {c.Nombres}")), ParaIds = ids, Autor = $"{autor.Apellidos}, {autor.Nombres}", AutorId = yo,
            Valor = p.Valor!, Mensaje = mensaje, Fecha = Fechas.HoyLima(),
        };
        if (r.Para.Length > 500) r.Para = r.Para[..499] + "…";
        db.Reconocimientos.Add(r);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Reconocimiento #{r.Id}", "Crear", autor, actual: $"{r.Valor} → {r.Para}");
        await db.SaveChangesAsync();
        return Dto(r);
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
