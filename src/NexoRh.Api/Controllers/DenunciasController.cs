using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record DenunciaNueva(string? Tipo, string? Relacion, string? Descripcion, bool Anonima = true);
public record DenunciaCreada(string Codigo, string Estado);
public record MensajeDto(string Autor, string Texto, string Fecha);
public record SeguimientoDenuncia(string Codigo, string Tipo, string Relacion, string Estado, string Creada, string Actualizada, string Resolucion, List<MensajeDto> Mensajes);
public record DenunciaResumen(int Id, string Codigo, int NuevosMensajes, string Tipo, string Relacion, string Creada, string Estado, string Actualizada);
public record DenunciaDetalle(DenunciaResumen Resumen, string Descripcion, bool Anonima, string? Denunciante, string Resolucion, List<MensajeDto> Mensajes);
public record TextoDenuncia(string? Texto);
public record EstadoDenuncia(string? Estado, string? Resolucion);

/// <summary>
/// Canal de denuncias. Cualquier usuario denuncia (de forma anónima si la configuración lo permite) y recibe un código
/// de seguimiento, con el que consulta el estado y conversa con el comité. Si es anónima no se guarda quién la hizo y
/// el historial de cambios no registra datos del denunciante. El comité son los administradores.
/// </summary>
[ApiController]
[Route("api/denuncias")]
[Authorize]
public class DenunciasController(NexoDbContext db, Auditoria auditoria, Configuraciones configuraciones) : ControllerBase
{
    public static readonly string[] Tipos =
        ["Acoso laboral", "Acoso sexual", "Discriminación", "Conflicto de intereses", "Fraude o corrupción", "Hurto o robo", "Carga de trabajo",
         "Información confidencial", "Seguridad y salud en el trabajo", "Otro"];
    public static readonly string[] Relaciones = ["Colaborador", "Ex colaborador", "Proveedor", "Contratista", "Cliente", "Otro"];
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static string Codigo() => string.Concat(Enumerable.Range(0, 12).Select(_ => Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]));
    private static string T(DateTime utc) => Fechas.TextoHora(utc)[..10];
    private static List<MensajeDto> Mensajes(Denuncia d) => d.Mensajes.OrderBy(m => m.Fecha).Select(m => new MensajeDto(m.Autor, m.Texto, Fechas.TextoHora(m.Fecha))).ToList();
    private static DenunciaResumen Resumen(Denuncia d) => new(d.Id, d.Codigo, d.Mensajes.Count(m => m.Autor == "Denunciante" && !m.Leido), d.Tipo, d.Relacion,
        T(d.CreadaEn), d.Estado, T(d.ActualizadaEn));
    private static bool Abierta(Denuncia d) => d.Estado is "Nueva" or "En curso";

    [HttpGet("opciones")]
    public async Task<object> Opciones() => new { tipos = Tipos, relaciones = Relaciones, anonimo = (await configuraciones.LeerAsync("denuncias"))["anonimo"] is true };

    // --- Denunciante -------------------------------------------------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<DenunciaCreada>> Crear(DenunciaNueva p)
    {
        if (!Tipos.Contains(p.Tipo)) return Error("tipo", "Elige el tipo de denuncia.");
        if (!Relaciones.Contains(p.Relacion)) return Error("relacion", "Indica tu relación con la empresa.");
        var desc = (p.Descripcion ?? "").Trim();
        if (desc.Length is < 20 or > 4000) return Error("descripcion", "Describe los hechos (entre 20 y 4000 caracteres).");
        if (p.Anonima && (await configuraciones.LeerAsync("denuncias"))["anonimo"] is not true) return Error("anonima", "La empresa no recibe denuncias anónimas: indica tu identidad.");
        string codigo;
        do codigo = Codigo(); while (await db.Denuncias.AnyAsync(d => d.Codigo == codigo));
        var d = new Denuncia
        {
            Codigo = codigo, Tipo = p.Tipo!, Relacion = p.Relacion!, Descripcion = desc, Anonima = p.Anonima,
            DenuncianteUsuarioId = p.Anonima ? null : int.TryParse(User.FindFirst("sub")?.Value, out var u) ? u : null,
            DenuncianteNombre = p.Anonima ? null : User.Nombre(),
        };
        db.Denuncias.Add(d);
        await db.SaveChangesAsync();
        // Sin datos del denunciante ni del código: solo que se recibió.
        auditoria.RegistrarTexto($"Denuncia #{d.Id}", "Crear", null, null, null, null, $"{d.Tipo} · {(d.Anonima ? "anónima" : "con identidad")}");
        db.ChangeTracker.Entries<Cambio>().Last().Entity.Usuario = "Canal de denuncias";
        await db.SaveChangesAsync();
        return new DenunciaCreada(codigo, d.Estado);
    }

    [HttpGet("seguimiento/{codigo}")]
    public async Task<ActionResult<SeguimientoDenuncia>> Seguimiento(string codigo)
    {
        var d = await db.Denuncias.AsNoTracking().Include(x => x.Mensajes).FirstOrDefaultAsync(x => x.Codigo == codigo.Trim().ToUpper());
        if (d is null) return NotFound();
        return new SeguimientoDenuncia(d.Codigo, d.Tipo, d.Relacion, d.Estado, T(d.CreadaEn), T(d.ActualizadaEn), d.Resolucion, Mensajes(d));
    }

    [HttpPost("seguimiento/{codigo}/mensajes")]
    public async Task<ActionResult<SeguimientoDenuncia>> MensajeDenunciante(string codigo, TextoDenuncia p)
    {
        var d = await db.Denuncias.Include(x => x.Mensajes).FirstOrDefaultAsync(x => x.Codigo == codigo.Trim().ToUpper());
        if (d is null) return NotFound();
        if (!Abierta(d)) return Error("estado", "La denuncia está cerrada.");
        var texto = (p.Texto ?? "").Trim();
        if (texto.Length is 0 or > 2000) return Error("texto", "Escribe el mensaje (máximo 2000 caracteres).");
        d.Mensajes.Add(new MensajeDenuncia { Autor = "Denunciante", Texto = texto });
        d.ActualizadaEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new SeguimientoDenuncia(d.Codigo, d.Tipo, d.Relacion, d.Estado, T(d.CreadaEn), T(d.ActualizadaEn), d.Resolucion, Mensajes(d));
    }

    // --- Comité (administradores) ------------------------------------------------------------------------------------
    [HttpGet]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<List<DenunciaResumen>> Listar() =>
        (await db.Denuncias.AsNoTracking().Include(d => d.Mensajes).OrderByDescending(d => d.ActualizadaEn).ToListAsync()).Select(Resumen).ToList();

    /// <summary>Detalle; marca como leídos los mensajes del denunciante.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<DenunciaDetalle>> Detalle(int id)
    {
        var d = await db.Denuncias.Include(x => x.Mensajes).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        var r = Resumen(d);
        foreach (var m in d.Mensajes.Where(m => m.Autor == "Denunciante")) m.Leido = true;
        await db.SaveChangesAsync();
        return new DenunciaDetalle(r, d.Descripcion, d.Anonima, d.DenuncianteNombre, d.Resolucion, Mensajes(d));
    }

    [HttpPost("{id:int}/mensajes")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<DenunciaDetalle>> MensajeComite(int id, TextoDenuncia p)
    {
        var d = await db.Denuncias.Include(x => x.Mensajes).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (!Abierta(d)) return Error("estado", "La denuncia está cerrada.");
        var texto = (p.Texto ?? "").Trim();
        if (texto.Length is 0 or > 2000) return Error("texto", "Escribe el mensaje (máximo 2000 caracteres).");
        d.Mensajes.Add(new MensajeDenuncia { Autor = "Comité", Texto = texto, Leido = true });
        if (d.Estado == "Nueva") d.Estado = "En curso";
        d.ActualizadaEn = DateTime.UtcNow;
        auditoria.Registrar($"Denuncia #{d.Id}", "Modificar", propiedad: "mensaje", actual: "Respuesta del comité");
        await db.SaveChangesAsync();
        return await Detalle(id);
    }

    [HttpPost("{id:int}/estado")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<DenunciaDetalle>> CambiarEstado(int id, EstadoDenuncia p)
    {
        var d = await db.Denuncias.FindAsync(id);
        if (d is null) return NotFound();
        if (p.Estado is not ("Nueva" or "En curso" or "Finalizada" or "Descartada")) return Error("estado", "Estado no válido.");
        var resolucion = (p.Resolucion ?? "").Trim();
        if (p.Estado is "Finalizada" or "Descartada" && resolucion.Length < 10) return Error("resolucion", "Indica la conclusión o el motivo para cerrar la denuncia.");
        if (resolucion.Length > 1000) return Error("resolucion", "Máximo 1000 caracteres.");
        if (d.Estado == p.Estado) return await Detalle(id);
        auditoria.Registrar($"Denuncia #{d.Id}", "Modificar", propiedad: "estado", anterior: d.Estado, actual: p.Estado!);
        d.Estado = p.Estado!;
        if (resolucion.Length > 0) d.Resolucion = resolucion;
        d.ActualizadaEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await Detalle(id);
    }

    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
