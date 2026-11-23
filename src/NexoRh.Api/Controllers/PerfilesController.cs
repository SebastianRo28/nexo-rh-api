using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Administración › Perfiles. Cada perfil tiene un rol base (Administrador, Supervisor o Colaborador) que define el acceso
/// de sus usuarios. Cambiar el rol de un perfil cambia el de todos sus usuarios y cierra sus sesiones abiertas.
/// </summary>
[ApiController]
[Route("api/perfiles")]
[Authorize(Policy = Politicas.Administrador)]
public class PerfilesController(NexoDbContext db, Auditoria auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<List<PerfilDto>> Listar()
    {
        var conteo = await db.Usuarios.Where(u => u.PerfilId != null).GroupBy(u => u.PerfilId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var lista = await db.Perfiles.AsNoTracking().OrderByDescending(p => p.Predefinido).ThenBy(p => p.Nombre).ToListAsync();
        return lista.Select(p => Dto(p, conteo.GetValueOrDefault(p.Id))).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<PerfilDto>> Crear(PerfilGuardar g)
    {
        if (await Validar(g, null) is { } err) return err;
        var p = new Perfil { Nombre = g.Nombre!.Trim(), Rol = Enum.Parse<Rol>(g.Rol!, true), Descripcion = (g.Descripcion ?? "").Trim(), ModificadoPor = User.Nombre() };
        db.Perfiles.Add(p);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Perfil #{p.Id}", "Crear", actual: $"{p.Nombre} (rol {p.Rol})");
        await db.SaveChangesAsync();
        return Dto(p, 0);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PerfilDto>> Modificar(int id, PerfilGuardar g)
    {
        var p = await db.Perfiles.Buscar(id);
        if (p is null) return NotFound();
        if (await Validar(g, p) is { } err) return err;
        var nombre = g.Nombre!.Trim();
        var rol = Enum.Parse<Rol>(g.Rol!, true);
        if (p.Predefinido && (rol != p.Rol || nombre != p.Nombre)) return Problem("Los perfiles predefinidos no se pueden renombrar ni cambiar de rol.", statusCode: 400);

        var usuarios = await db.Usuarios.Where(u => u.PerfilId == id).ToListAsync();
        if (rol != p.Rol)
        {
            var yo = int.TryParse(User.FindFirst("sub")?.Value, out var y) ? y : -1;
            if (usuarios.Any(u => u.Id == yo)) return Problem("No puedes cambiar el rol de tu propio perfil.", statusCode: 400);
            if (rol != Rol.Administrador && usuarios.Any(u => u.ColaboradorId is null))
                return Problem("Hay usuarios de este perfil sin colaborador vinculado; vincúlalos antes de cambiar el rol.", statusCode: 400);
            if (p.Rol == Rol.Administrador && !await db.Usuarios.AnyAsync(u => u.Activo && u.Rol == Rol.Administrador && u.PerfilId != id))
                return Problem("Debe quedar al menos un administrador activo.", statusCode: 400);
        }

        var entidad = $"Perfil #{p.Id}";
        if (p.Nombre != nombre) auditoria.Registrar(entidad, "Modificar", propiedad: "nombre", anterior: p.Nombre, actual: nombre);
        if (p.Rol != rol) auditoria.Registrar(entidad, "Modificar", propiedad: "rol", anterior: p.Rol.ToString(), actual: rol.ToString());
        foreach (var u in usuarios)
        {
            u.Perfil = nombre;
            if (u.Rol != rol)
            {
                auditoria.RegistrarTexto($"Usuario #{u.Id}", "Modificar", u.Nombre, null, "rol", u.Rol.ToString(), rol.ToString());
                u.Rol = rol;
                Passwords.RenovarSello(u);
            }
        }
        p.Nombre = nombre;
        p.Rol = rol;
        p.Descripcion = (g.Descripcion ?? "").Trim();
        p.ActualizadoEn = DateTime.UtcNow;
        p.ModificadoPor = User.Nombre();
        await db.SaveChangesAsync();
        return Dto(p, usuarios.Count);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var p = await db.Perfiles.Buscar(id);
        if (p is null) return NotFound();
        if (p.Predefinido) return Problem($"“{p.Nombre}” es un perfil predefinido y no se puede eliminar.", statusCode: 400);
        var n = await db.Usuarios.CountAsync(u => u.PerfilId == id);
        if (n > 0) return Problem($"No se puede eliminar “{p.Nombre}”: tiene {n} usuario(s) asignado(s).", statusCode: 400);
        db.Perfiles.Remove(p);
        auditoria.Registrar($"Perfil #{p.Id}", "Eliminar", anterior: p.Nombre);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> Validar(PerfilGuardar g, Perfil? actual)
    {
        var e = new Dictionary<string, string[]>();
        var nombre = (g.Nombre ?? "").Trim();
        if (nombre.Length is 0 or > 60) e["nombre"] = ["Ingresa el nombre del perfil (máximo 60 caracteres)."];
        else if (await db.Perfiles.AnyAsync(x => x.Nombre == nombre && x.Id != (actual == null ? 0 : actual.Id))) e["nombre"] = ["Ya existe un perfil con ese nombre."];
        if (!Enum.TryParse<Rol>(g.Rol, true, out var r) || !Enum.IsDefined(r) || int.TryParse(g.Rol, out _)) e["rol"] = ["El rol debe ser Administrador, Supervisor o Colaborador."];
        if ((g.Descripcion ?? "").Trim().Length > 300) e["descripcion"] = ["La descripción admite hasta 300 caracteres."];
        return e.Count > 0 ? ValidationProblem(new ValidationProblemDetails(e) { Title = e.Values.First()[0] }) : null;
    }

    private static PerfilDto Dto(Perfil p, int usuarios) => new(p.Id, p.Nombre, p.Rol.ToString(), p.Predefinido, p.Descripcion, usuarios,
        Fechas.TextoHora(p.CreadoEn)[..10], Fechas.TextoHora(p.ActualizadoEn)[..10], p.ModificadoPor);
}
