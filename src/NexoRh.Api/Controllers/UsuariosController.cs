using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>Administración › Usuarios: cuentas de acceso a Nexo RH.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Politicas.Administrador)]
public class UsuariosController(NexoDbContext db, Auditoria auditoria, Invitaciones invitaciones, LimitesPlan limites) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> Listar()
    {
        var nombres = await NombresColaboradores();
        var lista = await db.Usuarios.AsNoTracking().OrderBy(u => u.Nombre).ToListAsync();
        return lista.Select(u => Dto(u, nombres)).ToList();
    }

    /// <summary>
    /// Crea la cuenta. Con invitar = true se envía el correo “Activa tu cuenta” (72 h) y la persona crea su contraseña;
    /// si no, se genera una contraseña temporal que se devuelve una sola vez para entregarla.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UsuarioCreado>> Crear(UsuarioGuardar p)
    {
        var u = new Usuario();
        var error = await Aplicar(u, p);
        if (error is not null) return error;
        if (u.Rol == Rol.Administrador && await limites.VerificarAsync(LimitesPlan.Admins) is { } limite) return limite;
        var invitar = p.Invitar == true;
        var temporal = Passwords.Temporal();
        Passwords.Asignar(u, temporal, temporal: !invitar);
        u.PendienteActivacion = invitar;
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        auditoria.RegistrarTexto($"Usuario #{u.Id}", "Crear", u.Nombre, null, "usuario", null, $"{u.Correo} ({u.Perfil})" + (invitar ? " · invitado por correo" : ""));
        await db.SaveChangesAsync();
        if (invitar) await invitaciones.EnviarAsync(u);
        return CreatedAtAction(nameof(Listar), new UsuarioCreado(Dto(u, await NombresColaboradores()), invitar ? null : temporal));
    }

    /// <summary>Reenvía la invitación “Activa tu cuenta” a una cuenta que aún no se activó.</summary>
    [HttpPost("{id:int}/invitar")]
    public async Task<ActionResult<UsuarioDto>> Invitar(int id)
    {
        var u = await db.Usuarios.Buscar(id);
        if (u is null) return NotFound();
        if (!u.Activo) return Problem("Activa la cuenta antes de enviar la invitación.", statusCode: 400);
        if (!u.PendienteActivacion) return Problem("La cuenta ya está activada. Usa Restablecer contraseña.", statusCode: 400);
        await invitaciones.EnviarAsync(u);
        auditoria.RegistrarTexto($"Usuario #{u.Id}", "Modificar", u.Nombre, null, "invitación", null, "Reenviada");
        await db.SaveChangesAsync();
        return Dto(u, await NombresColaboradores());
    }

    /// <summary>Quita el doble factor de una cuenta (p. ej. perdió su teléfono); deberá configurarlo de nuevo si la política lo exige.</summary>
    [HttpPost("{id:int}/2fa/quitar")]
    public async Task<ActionResult<UsuarioDto>> QuitarDobleFactor(int id)
    {
        var u = await db.Usuarios.Buscar(id);
        if (u is null) return NotFound();
        if (!u.TotpActivo) return Problem("La cuenta no tiene doble factor.", statusCode: 400);
        u.TotpActivo = false;
        u.TotpSecreto = null;
        Passwords.RenovarSello(u);
        auditoria.RegistrarTexto($"Usuario #{u.Id}", "Modificar", u.Nombre, null, "doble factor", "Activo", "Quitado por un administrador");
        await db.SaveChangesAsync();
        return Dto(u, await NombresColaboradores());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UsuarioDto>> Modificar(int id, UsuarioGuardar p)
    {
        var u = await db.Usuarios.Buscar(id);
        if (u is null) return NotFound();
        var antes = new { u.Nombre, u.Correo, u.Rol, u.ColaboradorId, u.Perfil };
        var error = await Aplicar(u, p);
        if (error is not null) return error;
        if (u.Rol != antes.Rol && u.Id == YoId()) return Problem("No puedes cambiar tu propio rol.", statusCode: 400);
        if (u.Activo && u.Rol == Rol.Administrador && antes.Rol != Rol.Administrador && await limites.VerificarAsync(LimitesPlan.Admins) is { } limite) return limite;
        if (antes.Rol == Rol.Administrador && u.Rol != Rol.Administrador && !await QuedaOtroAdmin(u.Id))
            return Problem("Debe quedar al menos un administrador activo.", statusCode: 400);
        // El rol y el colaborador van dentro del token: los cambios cierran las sesiones abiertas de esa persona.
        if (u.Rol != antes.Rol || u.ColaboradorId != antes.ColaboradorId || u.Correo != antes.Correo) Passwords.RenovarSello(u);
        var entidad = $"Usuario #{u.Id}";
        if (antes.Nombre != u.Nombre) auditoria.RegistrarTexto(entidad, "Modificar", u.Nombre, null, "nombre", antes.Nombre, u.Nombre);
        if (antes.Correo != u.Correo) auditoria.RegistrarTexto(entidad, "Modificar", u.Nombre, null, "correo", antes.Correo, u.Correo);
        if (antes.Rol != u.Rol) auditoria.RegistrarTexto(entidad, "Modificar", u.Nombre, null, "rol", antes.Rol.ToString(), u.Rol.ToString());
        if (antes.Perfil != u.Perfil) auditoria.RegistrarTexto(entidad, "Modificar", u.Nombre, null, "perfil", antes.Perfil, u.Perfil);
        if (antes.ColaboradorId != u.ColaboradorId)
            auditoria.RegistrarTexto(entidad, "Modificar", u.Nombre, null, "colaborador_id", antes.ColaboradorId?.ToString(), u.ColaboradorId?.ToString());
        await db.SaveChangesAsync();
        return Dto(u, await NombresColaboradores());
    }

    /// <summary>Activa o desactiva la cuenta. Al desactivarla, sus sesiones abiertas dejan de funcionar de inmediato.</summary>
    [HttpPost("{id:int}/estado")]
    public async Task<ActionResult<UsuarioDto>> CambiarEstado(int id, EstadoUsuario p)
    {
        var u = await db.Usuarios.Buscar(id);
        if (u is null) return NotFound();
        if (u.Activo == p.Activo) return Dto(u, await NombresColaboradores());
        if (!p.Activo)
        {
            if (u.Id == YoId()) return Problem("No puedes desactivar tu propia cuenta.", statusCode: 400);
            if (u.Rol == Rol.Administrador && !await QuedaOtroAdmin(u.Id)) return Problem("Debe quedar al menos un administrador activo.", statusCode: 400);
            Passwords.RenovarSello(u);
        }
        else if (u.Rol == Rol.Administrador && await limites.VerificarAsync(LimitesPlan.Admins) is { } limite) return limite;
        u.Activo = p.Activo;
        auditoria.RegistrarTexto($"Usuario #{u.Id}", "Modificar", u.Nombre, null, "estado", p.Activo ? "Inactivo" : "Activo", p.Activo ? "Activo" : "Inactivo");
        await db.SaveChangesAsync();
        return Dto(u, await NombresColaboradores());
    }

    /// <summary>Genera una nueva contraseña temporal (y desbloquea la cuenta); se devuelve una sola vez.</summary>
    [HttpPost("{id:int}/restablecer")]
    public async Task<ActionResult<UsuarioCreado>> Restablecer(int id)
    {
        var u = await db.Usuarios.Buscar(id);
        if (u is null) return NotFound();
        if (!u.Activo) return Problem("Activa la cuenta antes de restablecer su contraseña.", statusCode: 400);
        var temporal = Passwords.Temporal();
        Passwords.Asignar(u, temporal, temporal: true);
        auditoria.RegistrarTexto($"Usuario #{u.Id}", "Modificar", u.Nombre, null, "contraseña", null, "Restablecida");
        await db.SaveChangesAsync();
        return new UsuarioCreado(Dto(u, await NombresColaboradores()), temporal);
    }

    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>Valida y copia los datos al usuario. Devuelve el error a responder, o null.</summary>
    private async Task<ActionResult?> Aplicar(Usuario u, UsuarioGuardar p)
    {
        var errores = new Dictionary<string, string[]>();
        void Error(string campo, string m) => errores[campo] = [m];

        var nombre = (p.Nombre ?? "").Trim();
        var correo = (p.Correo ?? "").Trim().ToLowerInvariant();
        if (nombre.Length == 0) Error("nombre", "Ingresa el nombre.");
        else if (nombre.Length > 120) Error("nombre", "El nombre no puede superar 120 caracteres.");
        if (!EsCorreo(correo)) Error("correo", "Ingresa un correo válido.");
        else if (await db.Usuarios.AnyAsync(x => x.Correo == correo && x.Id != u.Id)) Error("correo", "Este correo ya está registrado.");
        // El rol viene del perfil; sin perfil, del rol indicado (con su perfil predefinido).
        Perfil? perfil = null;
        var rol = Rol.Colaborador;
        if (p.PerfilId is int pid)
        {
            perfil = await db.Perfiles.Buscar(pid);
            if (perfil is null) Error("perfilId", "El perfil no existe.");
            else rol = perfil.Rol;
        }
        else if (!Enum.TryParse(p.Rol, true, out rol) || !Enum.IsDefined(rol) || int.TryParse(p.Rol, out _))
            Error("rol", "El rol debe ser Administrador, Supervisor o Colaborador.");
        else perfil = await db.Perfiles.Where(x => x.Rol == rol && x.Predefinido).OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (p.ColaboradorId is int cid)
        {
            if (!await db.Colaboradores.AnyAsync(c => c.Id == cid)) Error("colaboradorId", "El colaborador no existe.");
            else if (await db.Usuarios.AnyAsync(x => x.ColaboradorId == cid && x.Id != u.Id)) Error("colaboradorId", "Ese colaborador ya tiene un usuario.");
        }
        else if (rol != Rol.Administrador) Error("colaboradorId", "Vincula el usuario a un colaborador: sus permisos dependen de su posición en la organización.");
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores) { Title = errores.Values.First()[0] });

        u.Nombre = nombre;
        u.Correo = correo;
        u.Rol = rol;
        u.PerfilId = perfil?.Id;
        u.Perfil = perfil?.Nombre ?? Passwords.PerfilDe(rol);
        u.ColaboradorId = p.ColaboradorId;
        return null;
    }

    private static bool EsCorreo(string s) =>
        s.Length is > 3 and <= 120 && MailAddress.TryCreate(s, out var m) && m.Address == s && s.Split('@')[1].Contains('.');

    private int? YoId() => int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    private Task<bool> QuedaOtroAdmin(int excepto) => db.Usuarios.AnyAsync(x => x.Id != excepto && x.Activo && x.Rol == Rol.Administrador);

    private Task<Dictionary<int, string>> NombresColaboradores() =>
        db.Colaboradores.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Apellidos + ", " + c.Nombres);

    private static UsuarioDto Dto(Usuario u, Dictionary<int, string> nombres) => new(
        u.Id, u.Nombre, u.Correo, u.Rol.ToString(), u.Perfil, u.PerfilId, u.ColaboradorId,
        u.ColaboradorId is int c && nombres.TryGetValue(c, out var n) ? n : null,
        u.Activo, u.UltimoAcceso is { } a ? Fechas.TextoHora(a) : "—", u.BloqueadoHasta > DateTime.UtcNow, u.DebeCambiarPassword, u.PendienteActivacion, u.TotpActivo);
}
