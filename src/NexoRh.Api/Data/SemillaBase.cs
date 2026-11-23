using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Data;

/// <summary>
/// Datos que necesita cualquier instalación de Nexo RH, también la de producción: niveles de cargo, competencias base,
/// perfiles predefinidos, feriados nacionales, catálogo de ítems y parámetros legales de planilla y catálogos genéricos.
/// No crea colaboradores ni datos de la empresa de ejemplo.
/// </summary>
public static class SemillaBase
{
    public static async Task CargarAsync(NexoDbContext db, Genericos genericos)
    {
        if (!await db.FamiliasCargo.AnyAsync())
            db.FamiliasCargo.AddRange(new[] { "Intermedio", "Jefatura", "Senior", "Gerencia", "Junior" }.Select((n, i) => new FamiliaCargo { Id = i, Nombre = n }));
        if (!await db.Competencias.AnyAsync())
        {
            db.Competencias.AddRange(
                new Competencia { Id = 0, Nombre = "Trabajo en equipo", Descripcion = "Colabora con otros para lograr objetivos comunes." },
                new Competencia { Id = 1, Nombre = "Orientación a resultados", Descripcion = "Cumple metas con calidad y en los plazos acordados." },
                new Competencia { Id = 2, Nombre = "Comunicación efectiva", Descripcion = "Transmite ideas con claridad y escucha activamente." },
                new Competencia { Id = 3, Nombre = "Liderazgo", Descripcion = "Guía y desarrolla a su equipo." },
                new Competencia { Id = 4, Nombre = "Adaptabilidad", Descripcion = "Responde con flexibilidad a los cambios." });
        }
        var perfiles = (await db.Perfiles.Select(p => p.Nombre).ToListAsync()).ToHashSet();
        db.Perfiles.AddRange(new[]
        {
            new Perfil { Nombre = "Administrador", Rol = Rol.Administrador, Predefinido = true, Descripcion = "Acceso completo a Nexo RH.", ModificadoPor = "Sistema" },
            new Perfil { Nombre = "Supervisor", Rol = Rol.Supervisor, Predefinido = true, Descripcion = "Su portal, su equipo y su asistencia.", ModificadoPor = "Sistema" },
            new Perfil { Nombre = "Normal / Colaborador", Rol = Rol.Colaborador, Predefinido = true, Descripcion = "Solo su portal.", ModificadoPor = "Sistema" },
        }.Where(p => !perfiles.Contains(p.Nombre)));
        if (!await db.Feriados.AnyAsync()) db.Feriados.AddRange(SemillaAsistencia.Feriados());
        await db.SaveChangesAsync();
        await SemillaRemuneraciones.CargarBaseAsync(db);
        await SemillaCatalogos.CargarAsync(db, genericos);
    }

    /// <summary>
    /// Instalación sin datos de ejemplo: abre la planilla del mes actual y crea el primer administrador con
    /// Semilla:AdminCorreo, Semilla:AdminNombre y Semilla:PasswordInicial (debe cambiarla al iniciar sesión).
    /// </summary>
    /// <summary>Período inicial y, con crearAdmin, el primer administrador desde Semilla:* (la consola crea el suyo por invitación).</summary>
    public static async Task CargarProduccionAsync(NexoDbContext db, IConfiguration config, RelojAsistencia reloj, ILogger logger, bool crearAdmin = true)
    {
        if (!await db.Periodos.AnyAsync())
        {
            db.Periodos.Add(new PeriodoPlanilla { Id = reloj.Hoy.Year * 100 + reloj.Hoy.Month });
            await db.SaveChangesAsync();
        }
        if (!crearAdmin || await db.Usuarios.AnyAsync()) return;

        var correo = (config["Semilla:AdminCorreo"] ?? "").Trim().ToLowerInvariant();
        var nombre = (config["Semilla:AdminNombre"] ?? "").Trim();
        var password = config["Semilla:PasswordInicial"] ?? "";
        if (correo.Length == 0 || !correo.Contains('@') || nombre.Length == 0 || password.Length < 12)
        {
            logger.LogWarning("No hay usuarios: configura Semilla:AdminCorreo, Semilla:AdminNombre y Semilla:PasswordInicial (mínimo 12 caracteres) para crear el primer administrador.");
            return;
        }
        var perfil = await db.Perfiles.FirstAsync(p => p.Nombre == "Administrador");
        var u = new Usuario { Nombre = nombre, Correo = correo, Rol = Rol.Administrador, Perfil = perfil.Nombre, PerfilId = perfil.Id, DebeCambiarPassword = true };
        u.PasswordHash = new PasswordHasher<Usuario>().HashPassword(u, password);
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        logger.LogInformation("Se creó el primer administrador ({Correo}); deberá cambiar la contraseña inicial al entrar.", correo);
    }
}
