using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Acceso al tenant del host (pantalla P-MT.1): login con correo o N° de documento, doble factor, recuperación de
/// contraseña, activación de cuentas invitadas y entrada desde el selector de cuenta.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(NexoDbContext db, TokenService tokens, Auditoria auditoria, ContextoTenant tenant, AjustesTenant ajustes,
    TokensAcceso unUso, SecretosTotp secretos, EnviadorCorreo correo, Enlaces enlaces, Invitaciones invitaciones, CatalogoDbContext catalogo) : ControllerBase
{
    private const string Invalido = "Correo o contraseña incorrectos.";
    private static readonly TimeSpan VigenciaRestablecer = TimeSpan.FromHours(1);
    private static readonly TimeSpan VigenciaDobleFactor = TimeSpan.FromMinutes(5);

    private async Task<PoliticaAcceso> Politica() => (await ajustes.DeAsync(tenant.Id!.Value)).Acceso;

    /// <summary>Inicia sesión con correo (o N° de documento, si el tenant lo permite) y contraseña. Si la cuenta tiene doble
    /// factor, responde mfaRequerido con un mfaToken para POST /api/auth/mfa.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginRespuesta>> Login(LoginPeticion p)
    {
        var usuario = (p.Correo ?? "").Trim();
        if (usuario.Length == 0 || string.IsNullOrEmpty(p.Password))
            return Problem("Ingresa tu usuario y tu contraseña.", statusCode: 400);
        var politica = await Politica();
        var u = await BuscarPorUsuario(usuario, politica);
        // Mismo mensaje para cuenta inexistente o contraseña errada, para no revelar qué cuentas existen.
        if (u is null || !u.Activo || u.PendienteActivacion) return Problem(Invalido, statusCode: 401);
        var bloqueo = await VerificarPassword(u, p.Password, politica);
        if (bloqueo is not null) return bloqueo;
        return await Continuar(u, politica);
    }

    /// <summary>Segundo paso del login: código de 6 dígitos de la app de autenticación.</summary>
    [HttpPost("mfa")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginRespuesta>> DobleFactor(DobleFactorPeticion p)
    {
        var r = await unUso.VerAsync(p.MfaToken, TiposToken.DobleFactor);
        if (r is not { } x || !x.Usuario.TotpActivo || x.Usuario.TotpSecreto is null)
            return Error("El paso de verificación venció. Vuelve a iniciar sesión.", 401, "mfa_vencido");
        var u = x.Usuario;
        var politica = await Politica();
        if (u.BloqueadoHasta > DateTime.UtcNow) return Bloqueada(u);
        if (!Totp.Verificar(secretos.Descifrar(u.TotpSecreto), p.Codigo))
        {
            Fallo(u, politica);
            await db.SaveChangesAsync();
            return u.BloqueadoHasta > DateTime.UtcNow ? Bloqueada(u) : Error("El código no es correcto. Revisa tu app de autenticación.", 401, "mfa_invalido");
        }
        x.Token.Usado = DateTime.UtcNow;
        return await Emitir(u, politica);
    }

    /// <summary>Entrada desde el selector de cuenta de app.{dominio}: canjea el código de un solo uso (2 minutos).</summary>
    [HttpPost("canjear")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginRespuesta>> Canjear(CodigoPeticion p)
    {
        var u = await unUso.ConsumirAsync(p.Codigo, TiposToken.Acceso);
        if (u is null) return Error("El enlace de acceso venció. Vuelve a iniciar sesión.", 401, "acceso_vencido");
        return await Continuar(u, await Politica());
    }

    /// <summary>
    /// Entrada de una sesión de soporte de Nexo RH (enlace de un solo uso abierto desde la consola). Emite un token de
    /// administrador sin usuario del tenant detrás, marcado con la sesión y su modo (lectura o escritura).
    /// </summary>
    [HttpPost("soporte")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginRespuesta>> Soporte(CodigoPeticion p)
    {
        var hash = TokensAcceso.Hash((p.Codigo ?? "").Trim());
        var s = await catalogo.SesionesSoporte.FirstOrDefaultAsync(x => x.TokenHash == hash && x.TenantId == tenant.Id);
        if (s is null || s.Canjeada is not null || s.Fin is not null || s.Expira <= DateTime.UtcNow)
            return Error("El enlace de soporte venció o ya se usó. Abre otra sesión desde la consola.", 401, "soporte_invalido");
        s.Canjeada = DateTime.UtcNow;
        await catalogo.SaveChangesAsync();
        var correoAgente = await catalogo.UsuariosPlataforma.Where(u => u.Id == s.UsuarioPlataformaId).Select(u => u.Correo).FirstAsync();
        var (token, expira) = tokens.CrearSoporte(s, correoAgente);
        var usuario = new UsuarioSesion(0, $"{s.Agente} (soporte Nexo RH)", correoAgente, Rol.Administrador.ToString(), "Soporte Nexo RH", null, "NX", false, true, false);
        return new LoginRespuesta(token, expira, usuario, Soporte: new SoporteInfo(s.Id, s.Agente, s.Modo, s.Motivo, s.Ticket, s.Expira));
    }

    /// <summary>“Terminar sesión” del banner de soporte: cierra la sesión en la consola y avisa a los administradores.</summary>
    [HttpPost("soporte/terminar")]
    [Authorize]
    public async Task<IActionResult> TerminarSoporte()
    {
        if (!Guid.TryParse(User.FindFirst(Claves.SesionSoporte)?.Value, out var id)) return BadRequest();
        var s = await catalogo.SesionesSoporte.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id);
        if (s is null) return NotFound();
        if (s.Fin is null)
        {
            s.Fin = DateTime.UtcNow;
            catalogo.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = s.Agente, Accion = "support.ended", TenantId = s.TenantId, Detalle = "Terminada desde la app del cliente",
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString() });
            await catalogo.SaveChangesAsync();
            auditoria.RegistrarTexto("Acceso de soporte", "Modificar", null, null, "sesión de soporte", null, $"Terminada por {s.Agente} (Nexo RH)");
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    /// <summary>Datos del usuario de la sesión actual.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioSesion>> Yo()
    {
        var u = await Actual();
        return u is null ? Unauthorized() : Sesion(u, await Politica());
    }

    /// <summary>Cambia la contraseña del usuario de la sesión (obligatorio si es temporal). Devuelve un token nuevo:
    /// los tokens anteriores, incluso los de otros navegadores, dejan de funcionar.</summary>
    [HttpPost("password")]
    [Authorize]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginRespuesta>> CambiarPassword(CambioPassword p)
    {
        var u = await Actual();
        if (u is null) return Unauthorized();
        var politica = await Politica();
        if (!Passwords.Verificar(u, p.Actual ?? "")) return Problem("La contraseña actual no es correcta.", statusCode: 400);
        var error = Passwords.Validar(p.Nueva, u.Correo, politica.PasswordMinimo);
        if (error is not null) return Problem(error, statusCode: 400);
        if (Passwords.Verificar(u, p.Nueva)) return Problem("La nueva contraseña debe ser distinta de la actual.", statusCode: 400);
        Passwords.Asignar(u, p.Nueva, temporal: false);
        auditoria.RegistrarTexto($"Usuario #{u.Id}", "Modificar", u.Nombre, null, "contraseña", null, "Actualizada por el usuario");
        return await Emitir(u, politica);
    }

    // --- Recuperación y activación ----------------------------------------------------------------------------------

    /// <summary>“¿Olvidaste tu contraseña?”: si la cuenta existe envía un enlace de 1 hora. La respuesta es siempre la
    /// misma, exista o no la cuenta.</summary>
    [HttpPost("olvide")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Olvide(OlvidePeticion p)
    {
        var politica = await Politica();
        var u = await BuscarPorUsuario((p.Correo ?? "").Trim(), politica);
        if (u is { Activo: true })
        {
            // Una cuenta invitada que nunca se activó recibe de nuevo su invitación.
            if (u.PendienteActivacion) await invitaciones.EnviarAsync(u);
            else
            {
                var token = await unUso.CrearAsync(u, TiposToken.Restablecer, VigenciaRestablecer);
                await correo.EnviarAsync(new MensajeCorreo(u.Correo, $"Restablece tu contraseña de {tenant.Nombre}", "Restablece tu contraseña",
                    $"<p>Hola {System.Net.WebUtility.HtmlEncode(Primero(u.Nombre))}:</p><p>Recibimos una solicitud para restablecer tu contraseña. El enlace vence en 1 hora y se puede usar una sola vez.</p>",
                    "Crear nueva contraseña", enlaces.Ruta("/restablecer", token)));
            }
        }
        return Accepted(new { mensaje = "Si el correo está registrado, te enviaremos un enlace para restablecer tu contraseña." });
    }

    /// <summary>Datos para la pantalla de un enlace de activación o de restablecer (sin consumirlo).</summary>
    [HttpGet("token")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<TokenInfo>> VerToken([FromQuery] string tipo, [FromQuery] string token)
    {
        if (tipo is not (TiposToken.Activacion or TiposToken.Restablecer)) return BadRequest();
        var r = await unUso.VerAsync(token, tipo);
        if (r is not { } x) return Error("El enlace venció o ya se usó. Solicita uno nuevo.", 404, "token_invalido");
        var a = await ajustes.DeAsync(tenant.Id!.Value);
        return new TokenInfo(tipo, x.Usuario.Nombre, Enmascarar(x.Usuario.Correo), a.Acceso.PasswordMinimo, a.PrivacidadUrl);
    }

    /// <summary>Crea la nueva contraseña con el enlace de “¿Olvidaste tu contraseña?”.</summary>
    [HttpPost("restablecer")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Restablecer(NuevaPasswordPeticion p)
    {
        var r = await unUso.VerAsync(p.Token, TiposToken.Restablecer);
        if (r is not { } x) return Error("El enlace venció o ya se usó. Solicita uno nuevo.", 404, "token_invalido");
        var error = Passwords.Validar(p.Password, x.Usuario.Correo, (await Politica()).PasswordMinimo);
        if (error is not null) return Problem(error, statusCode: 400);
        x.Token.Usado = DateTime.UtcNow;
        Passwords.Asignar(x.Usuario, p.Password, temporal: false);
        auditoria.RegistrarTexto($"Usuario #{x.Usuario.Id}", "Modificar", x.Usuario.Nombre, null, "contraseña", null, "Restablecida por correo");
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>“Activa tu cuenta”: la persona invitada crea su contraseña y acepta la política de privacidad.</summary>
    [HttpPost("activar")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Activar(NuevaPasswordPeticion p)
    {
        var r = await unUso.VerAsync(p.Token, TiposToken.Activacion);
        if (r is not { } x) return Error("La invitación venció o ya se usó. Pide a tu área de RR.HH. que la envíe de nuevo.", 404, "token_invalido");
        if (p.AceptaPrivacidad != true) return Problem("Debes aceptar la política de privacidad para activar tu cuenta.", statusCode: 400);
        var error = Passwords.Validar(p.Password, x.Usuario.Correo, (await Politica()).PasswordMinimo);
        if (error is not null) return Problem(error, statusCode: 400);
        x.Token.Usado = DateTime.UtcNow;
        Passwords.Asignar(x.Usuario, p.Password, temporal: false);
        x.Usuario.PendienteActivacion = false;
        x.Usuario.PrivacidadAceptada = DateTime.UtcNow;
        auditoria.RegistrarTexto($"Usuario #{x.Usuario.Id}", "Modificar", x.Usuario.Nombre, null, "cuenta", "Invitada", "Activada");
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Doble factor (configuración desde la sesión) ---------------------------------------------------------------

    /// <summary>Genera una clave nueva para la app de autenticación (queda pendiente hasta confirmarla con un código).</summary>
    [HttpPost("2fa/iniciar")]
    [Authorize]
    public async Task<ActionResult<DobleFactorInicio>> IniciarDobleFactor()
    {
        var u = await Actual();
        if (u is null) return Unauthorized();
        if (u.TotpActivo) return Problem("El doble factor ya está activo.", statusCode: 400);
        var clave = Totp.NuevaClave();
        u.TotpSecreto = secretos.Cifrar(clave);
        await db.SaveChangesAsync();
        return new DobleFactorInicio(clave, Totp.Uri($"{tenant.Nombre} · Nexo RH", u.Correo, clave));
    }

    /// <summary>Confirma la clave con el primer código y activa el doble factor (cierra las otras sesiones).</summary>
    [HttpPost("2fa/confirmar")]
    [Authorize]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginRespuesta>> ConfirmarDobleFactor(CodigoPeticion p)
    {
        var u = await Actual();
        if (u is null) return Unauthorized();
        if (u.TotpActivo || u.TotpSecreto is null) return Problem("Primero genera la clave de tu app de autenticación.", statusCode: 400);
        if (!Totp.Verificar(secretos.Descifrar(u.TotpSecreto), p.Codigo)) return Problem("El código no es correcto. Revisa la hora de tu teléfono e intenta de nuevo.", statusCode: 400);
        u.TotpActivo = true;
        Passwords.RenovarSello(u);
        auditoria.RegistrarTexto($"Usuario #{u.Id}", "Modificar", u.Nombre, null, "doble factor", "Inactivo", "Activo");
        return await Emitir(u, await Politica());
    }

    /// <summary>Desactiva el doble factor (pide la contraseña). No se permite si la política del tenant lo exige.</summary>
    [HttpPost("2fa/desactivar")]
    [Authorize]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginRespuesta>> DesactivarDobleFactor(PasswordPeticion p)
    {
        var u = await Actual();
        if (u is null) return Unauthorized();
        var politica = await Politica();
        if (AjustesTenant.ExigeDobleFactor(politica, u.Rol)) return Problem("Tu empresa exige el doble factor para tu perfil.", statusCode: 400);
        if (!Passwords.Verificar(u, p.Password ?? "")) return Problem("La contraseña no es correcta.", statusCode: 400);
        u.TotpActivo = false;
        u.TotpSecreto = null;
        Passwords.RenovarSello(u);
        auditoria.RegistrarTexto($"Usuario #{u.Id}", "Modificar", u.Nombre, null, "doble factor", "Activo", "Inactivo");
        return await Emitir(u, politica);
    }

    // ---------------------------------------------------------------------------------------------------------------

    private async Task<Usuario?> Actual()
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var id)) return null;
        var u = await db.Usuarios.Buscar(id);
        return u is { Activo: true } ? u : null;
    }

    /// <summary>Correo, o N° de documento del colaborador vinculado si el tenant lo permite.</summary>
    private async Task<Usuario?> BuscarPorUsuario(string usuario, PoliticaAcceso politica)
    {
        if (usuario.Length == 0) return null;
        if (usuario.Contains('@'))
        {
            var correo = usuario.ToLowerInvariant();
            return await db.Usuarios.FirstOrDefaultAsync(x => x.Correo == correo);
        }
        if (!politica.LoginDocumento) return null;
        var colaborador = await db.Colaboradores.Where(c => c.Documento == usuario).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        return colaborador is null ? null : await db.Usuarios.FirstOrDefaultAsync(x => x.ColaboradorId == colaborador);
    }

    /// <summary>Verifica la contraseña con el bloqueo de la política. Devuelve el error a responder, o null.</summary>
    private async Task<ActionResult?> VerificarPassword(Usuario u, string password, PoliticaAcceso politica)
    {
        if (u.BloqueadoHasta > DateTime.UtcNow) return Bloqueada(u);
        if (Passwords.Verificar(u, password)) return null;
        Fallo(u, politica);
        await db.SaveChangesAsync();
        return u.BloqueadoHasta > DateTime.UtcNow ? Bloqueada(u) : Problem(Invalido, statusCode: 401);
    }

    private static void Fallo(Usuario u, PoliticaAcceso politica)
    {
        u.IntentosFallidos++;
        if (u.IntentosFallidos >= Math.Max(3, politica.MaxIntentos))
        {
            u.BloqueadoHasta = DateTime.UtcNow.AddMinutes(Math.Max(1, politica.BloqueoMinutos));
            u.IntentosFallidos = 0;
        }
    }

    private ObjectResult Bloqueada(Usuario u) =>
        Error($"Cuenta bloqueada por intentos fallidos. Intenta de nuevo en {Math.Ceiling((u.BloqueadoHasta!.Value - DateTime.UtcNow).TotalMinutes)} minutos.", 423, "cuenta_bloqueada");

    /// <summary>Contraseña correcta: pide el segundo factor si está activo; si no, emite la sesión.</summary>
    private async Task<ActionResult<LoginRespuesta>> Continuar(Usuario u, PoliticaAcceso politica)
    {
        // Cuenta suspendida o cancelada: solo entran los administradores (a la pantalla de estado de cuenta y exportación).
        if (tenant.Estado is EstadosTenant.Suspendido or EstadosTenant.Cancelado && u.Rol != Rol.Administrador)
            return Error("La plataforma de tu empresa no está disponible temporalmente. Contacta a tu área de RR.HH.", 423, tenant.Estado == EstadosTenant.Suspendido ? "tenant_suspended" : "tenant_cancelled");
        u.IntentosFallidos = 0;
        if (u.TotpActivo)
        {
            await db.SaveChangesAsync();
            var mfa = await unUso.CrearAsync(u, TiposToken.DobleFactor, VigenciaDobleFactor);
            return new LoginRespuesta(null, null, null, MfaRequerido: true, MfaToken: mfa);
        }
        return await Emitir(u, politica);
    }

    private async Task<LoginRespuesta> Emitir(Usuario u, PoliticaAcceso politica)
    {
        u.IntentosFallidos = 0;
        u.BloqueadoHasta = null;
        u.UltimoAcceso = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var (token, expira) = tokens.Crear(u, politica.SesionMinutos);
        return new LoginRespuesta(token, expira, Sesion(u, politica));
    }

    /// <summary>ProblemDetails con un código estable para el frontend (mfa_vencido, cuenta_bloqueada, token_invalido…).</summary>
    private ObjectResult Error(string detalle, int estado, string codigo)
    {
        var r = (ObjectResult)Problem(detalle, statusCode: estado);
        ((ProblemDetails)r.Value!).Extensions["codigo"] = codigo;
        return r;
    }

    private static string Primero(string nombre) => nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nombre;

    /// <summary>a••••@empresa.pe</summary>
    public static string Enmascarar(string correo)
    {
        var i = correo.IndexOf('@');
        return i <= 1 ? correo : $"{correo[0]}{new string('•', Math.Min(6, i - 1))}{correo[i..]}";
    }

    public static UsuarioSesion Sesion(Usuario u, PoliticaAcceso politica)
    {
        var partes = u.Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ini = string.Concat(partes.Take(2).Select(x => char.ToUpperInvariant(x[0])));
        return new UsuarioSesion(u.Id, u.Nombre, u.Correo, u.Rol.ToString(), u.Perfil, u.ColaboradorId, ini, u.DebeCambiarPassword,
            u.TotpActivo, !u.TotpActivo && AjustesTenant.ExigeDobleFactor(politica, u.Rol));
    }
}
