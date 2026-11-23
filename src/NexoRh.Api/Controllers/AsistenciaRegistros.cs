using System.Globalization;
using NexoRh.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

// Vacaciones, ausencias, licencias médicas, solicitudes, recargos por domingo y jornales.
public partial class AsistenciaController
{
    private static readonly string[] TiposAusencia =
        ["Descanso médico", "Licencia por paternidad", "Licencia por maternidad", "Licencia por fallecimiento", "Permiso con goce", "Permiso sin goce", "Falta injustificada"];
    private static readonly string[] TiposSolicitud = ["Vacaciones", "Permiso con goce", "Permiso sin goce"];
    private static readonly string[] OrigenesVacacion = ["Solicitud", "Registro manual", "Importador"];
    private static readonly string[] EstadosLicencia = ["Recibida", "Validada", "Observada", "Rechazada"];

    private static int Dias(DateOnly d, DateOnly h) => h.DayNumber - d.DayNumber + 1;

    /// <summary>Valida el rango y que no se cruce con vacaciones o ausencias ya registradas.</summary>
    private async Task<ActionResult?> ValidarRango(int colaboradorId, string? desde, string? hasta, int? ignorarAusencia = null)
    {
        if (!Fechas.TryLeer(desde, out var d)) return Error("desde", "La fecha de inicio debe tener el formato DD/MM/AAAA.");
        if (!Fechas.TryLeer(hasta, out var h)) return Error("hasta", "La fecha de término debe tener el formato DD/MM/AAAA.");
        if (h < d) return Error("hasta", "La fecha de término debe ser igual o posterior a la de inicio.");
        if (Dias(d, h) > 366) return Error("hasta", "El rango no puede superar un año.");
        var v = await db.Vacaciones.AsNoTracking().FirstOrDefaultAsync(x => x.ColaboradorId == colaboradorId && x.Desde <= h && d <= x.Hasta);
        if (v is not null) return Error("desde", $"Se cruza con vacaciones del {Fechas.Texto(v.Desde)} al {Fechas.Texto(v.Hasta)}.");
        var a = await db.Ausencias.AsNoTracking().FirstOrDefaultAsync(x => x.ColaboradorId == colaboradorId && x.Id != ignorarAusencia && x.Desde <= h && d <= x.Hasta);
        if (a is not null) return Error("desde", $"Se cruza con “{a.Tipo}” del {Fechas.Texto(a.Desde)} al {Fechas.Texto(a.Hasta)}.");
        return null;
    }

    private static string Corto(string? s, int n) { var x = (s ?? "").Trim(); return x.Length > n ? x[..n] : x; }

    // --- Vacaciones ------------------------------------------------------------------------------------------------
    [HttpPost("vacaciones")]
    public async Task<ActionResult<VacacionDto>> CrearVacacion(VacacionNueva p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        if (!await alcance.GestionaAsync(c.Id)) return Prohibido("No puedes registrar vacaciones de este colaborador.");
        if (!OrigenesVacacion.Contains(p.Origen ?? "Registro manual")) return Error("origen", "Origen no válido.");
        if (await ValidarRango(c.Id, p.Desde, p.Hasta) is { } err) return err;
        Fechas.TryLeer(p.Desde, out var d); Fechas.TryLeer(p.Hasta, out var h);
        var v = new Vacacion { ColaboradorId = c.Id, Desde = d, Hasta = h, Dias = Dias(d, h), Origen = p.Origen ?? "Registro manual", Observacion = Corto(p.Observacion, 300) };
        db.Vacaciones.Add(v);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Vacaciones #{v.Id}", "Crear", c, actual: $"{v.Dias} días ({Fechas.Texto(d)} al {Fechas.Texto(h)})");
        await db.SaveChangesAsync();
        return Mapeo.Vacacion(v);
    }

    [HttpDelete("vacaciones/{id:int}")]
    public async Task<IActionResult> EliminarVacacion(int id)
    {
        var v = await db.Vacaciones.Buscar(id);
        if (v is null) return NotFound();
        if (!await alcance.GestionaAsync(v.ColaboradorId)) return Prohibido("No puedes eliminar vacaciones de este colaborador.");
        var c = await db.Colaboradores.Buscar(v.ColaboradorId);
        db.Vacaciones.Remove(v);
        auditoria.Registrar($"Vacaciones #{v.Id}", "Eliminar", c, anterior: $"{v.Dias} días ({Fechas.Texto(v.Desde)})");
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Ausencias -------------------------------------------------------------------------------------------------
    [HttpPost("ausencias")]
    public async Task<ActionResult<AusenciaDto>> CrearAusencia(AusenciaNueva p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        if (!await alcance.GestionaAsync(c.Id)) return Prohibido("No puedes registrar ausencias de este colaborador.");
        if (!TiposAusencia.Contains(p.Tipo)) return Error("tipo", "Elige el tipo de ausencia.");
        if (await ValidarRango(c.Id, p.Desde, p.Hasta) is { } err) return err;
        var a = await NuevaAusencia(c, p.Tipo!, p.Desde!, p.Hasta!, p.Observacion);
        await db.SaveChangesAsync();
        return Mapeo.Ausencia(a);
    }

    private async Task<Ausencia> NuevaAusencia(Colaborador c, string tipo, string desde, string hasta, string? observacion)
    {
        Fechas.TryLeer(desde, out var d); Fechas.TryLeer(hasta, out var h);
        var a = new Ausencia { ColaboradorId = c.Id, Tipo = tipo, Desde = d, Hasta = h, Dias = Dias(d, h), Observacion = Corto(observacion, 300) };
        db.Ausencias.Add(a);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Ausencia #{a.Id}", "Crear", c, actual: $"{tipo} · {a.Dias} días");
        return a;
    }

    [HttpDelete("ausencias/{id:int}")]
    public async Task<IActionResult> EliminarAusencia(int id)
    {
        var a = await db.Ausencias.Buscar(id);
        if (a is null) return NotFound();
        if (!await alcance.GestionaAsync(a.ColaboradorId)) return Prohibido("No puedes eliminar ausencias de este colaborador.");
        var c = await db.Colaboradores.Buscar(a.ColaboradorId);
        foreach (var l in await db.Licencias.Where(x => x.AusenciaId == id).ToListAsync()) l.AusenciaId = null;
        db.Ausencias.Remove(a);
        auditoria.Registrar($"Ausencia #{a.Id}", "Eliminar", c, anterior: $"{a.Tipo} · {a.Dias} días");
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Licencias médicas -----------------------------------------------------------------------------------------
    [HttpPost("licencias")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<LicenciaDto>> CrearLicencia(LicenciaNueva p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        var folio = Corto(p.Folio, 30);
        if (folio.Length == 0) return Error("folio", "Ingresa el folio del certificado.");
        if (await db.Licencias.AnyAsync(x => x.Folio == folio)) return Error("folio", "Ya existe un certificado con ese folio.");
        if (Corto(p.Emisor, 120).Length == 0) return Error("emisor", "Indica quién emite el certificado.");
        if (!Fechas.TryLeer(p.Desde, out var d) || !Fechas.TryLeer(p.Hasta, out var h) || h < d) return Error("hasta", "Revisa las fechas del certificado.");
        var l = new LicenciaMedica
        {
            ColaboradorId = c.Id, Folio = folio, Emisor = Corto(p.Emisor, 120), Diagnostico = Corto(p.Diagnostico, 200), Desde = d, Hasta = h, Dias = Dias(d, h),
            Estado = "Recibida", Recibida = Fechas.HoyLima(),
        };
        db.Licencias.Add(l);
        auditoria.Registrar($"Licencia {folio}", "Crear", c, actual: "Recibida");
        await db.SaveChangesAsync();
        return Mapeo.Licencia(l);
    }

    /// <summary>Al validar un certificado se crea su ausencia "Descanso médico" (una sola vez).</summary>
    [HttpPost("licencias/{id:int}/estado")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<LicenciaResultado>> CambiarEstadoLicencia(int id, EstadoLicencia p)
    {
        var l = await db.Licencias.Buscar(id);
        if (l is null) return NotFound();
        if (!EstadosLicencia.Contains(p.Estado)) return Error("estado", "Estado no válido.");
        var c = (await db.Colaboradores.Buscar(l.ColaboradorId))!;
        var anterior = l.Estado;
        l.Estado = p.Estado!;
        Ausencia? nueva = null;
        if (l.Estado == "Validada" && l.AusenciaId is null)
        {
            nueva = await NuevaAusencia(c, "Descanso médico", Fechas.Texto(l.Desde), Fechas.Texto(l.Hasta), $"{l.Emisor} · {l.Folio}");
            l.AusenciaId = nueva.Id;
        }
        auditoria.Registrar($"Licencia {l.Folio}", "Modificar", c, "estado", anterior, l.Estado);
        await db.SaveChangesAsync();
        return new LicenciaResultado(Mapeo.Licencia(l), nueva is null ? null : Mapeo.Ausencia(nueva));
    }

    // --- Solicitudes -----------------------------------------------------------------------------------------------
    /// <summary>El colaborador solicita para sí; un administrador puede registrarla a nombre de otro.</summary>
    [HttpPost("solicitudes")]
    public async Task<ActionResult<SolicitudAsistenciaDto>> CrearSolicitud(SolicitudNueva p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        if (!alcance.EsAdmin && c.Id != alcance.Propio) return Prohibido("Solo puedes crear solicitudes para ti.");
        if (!TiposSolicitud.Contains(p.Tipo)) return Error("tipo", "El tipo debe ser Vacaciones, Permiso con goce o Permiso sin goce.");
        if (await ValidarRango(c.Id, p.Desde, p.Hasta) is { } err) return err;
        Fechas.TryLeer(p.Desde, out var d); Fechas.TryLeer(p.Hasta, out var h);
        if (!alcance.EsAdmin && d < reloj.Hoy) return Error("desde", "La fecha de inicio no puede estar en el pasado.");
        if (await db.SolicitudesAsistencia.AnyAsync(x => x.ColaboradorId == c.Id && x.Estado == "Pendiente" && x.Desde <= h && d <= x.Hasta))
            return Error("desde", "Ya tienes una solicitud pendiente para esas fechas.");
        var motivo = Corto(p.Motivo, 300);
        if (motivo.Length == 0) return Error("motivo", "Indica el motivo.");
        var s = new SolicitudAsistencia { ColaboradorId = c.Id, Tipo = p.Tipo!, Desde = d, Hasta = h, Dias = Dias(d, h), Motivo = motivo, Creada = Fechas.HoyLima() };
        db.SolicitudesAsistencia.Add(s);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Solicitud #{s.Id}", "Crear", c, actual: $"{s.Tipo} · {s.Dias} días ({Fechas.Texto(d)} al {Fechas.Texto(h)})");
        await db.SaveChangesAsync();
        return Mapeo.Solicitud(s);
    }

    /// <summary>Aprueba o rechaza: la aprobación registra las vacaciones o el permiso en el calendario.</summary>
    [HttpPost("solicitudes/{id:int}/resolver")]
    public async Task<ActionResult<SolicitudResultado>> Resolver(int id, ResolverSolicitud p)
    {
        var s = await db.SolicitudesAsistencia.Buscar(id);
        if (s is null) return NotFound();
        if (!await alcance.GestionaAsync(s.ColaboradorId)) return Prohibido("No puedes resolver solicitudes de este colaborador.");
        if (s.Estado != "Pendiente") return Error("estado", $"La solicitud ya fue {s.Estado.ToLowerInvariant()}.");
        var comentario = Corto(p.Comentario, 300);
        if (!p.Aprobar && comentario.Length == 0) return Error("comentario", "Indica el motivo del rechazo.");
        var c = (await db.Colaboradores.Buscar(s.ColaboradorId))!;
        Vacacion? vac = null;
        Ausencia? aus = null;
        if (p.Aprobar)
        {
            if (await ValidarRango(c.Id, Fechas.Texto(s.Desde), Fechas.Texto(s.Hasta)) is { } err) return err;
            if (s.Tipo == "Vacaciones")
            {
                vac = new Vacacion { ColaboradorId = c.Id, Desde = s.Desde, Hasta = s.Hasta, Dias = s.Dias, Origen = "Solicitud", Observacion = s.Motivo };
                db.Vacaciones.Add(vac);
                await db.SaveChangesAsync();
                auditoria.Registrar($"Vacaciones #{vac.Id}", "Crear", c, actual: $"{vac.Dias} días ({Fechas.Texto(s.Desde)} al {Fechas.Texto(s.Hasta)})");
            }
            else aus = await NuevaAusencia(c, s.Tipo, Fechas.Texto(s.Desde), Fechas.Texto(s.Hasta), s.Motivo);
        }
        s.Estado = p.Aprobar ? "Aprobada" : "Rechazada";
        s.Comentario = comentario;
        s.Aprobador = User.Nombre();
        auditoria.Registrar($"Solicitud #{s.Id}", "Modificar", c, "estado", "Pendiente", s.Estado + (comentario.Length > 0 ? $" · {comentario}" : ""));
        await db.SaveChangesAsync();
        return new SolicitudResultado(Mapeo.Solicitud(s), vac is null ? null : Mapeo.Vacacion(vac), aus is null ? null : Mapeo.Ausencia(aus));
    }

    // --- Recargos por domingo o feriado trabajado ------------------------------------------------------------------
    [HttpPost("recargos")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<RecargoDto>> CrearRecargo(RecargoNuevo p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        if (!Fechas.TryLeer(p.Fecha, out var f)) return Error("fecha", "La fecha debe tener el formato DD/MM/AAAA.");
        if (p.Horas is <= 0 or > 24) return Error("horas", "Las horas deben estar entre 0 y 24.");
        if (await db.Recargos.AnyAsync(x => x.ColaboradorId == c.Id && x.Fecha == f)) return Error("fecha", "Ya hay un recargo registrado ese día.");
        var r = new RecargoDomingo { ColaboradorId = c.Id, Fecha = f, Horas = Math.Round(p.Horas, 2), DescansoSustitutorio = p.DescansoSustitutorio, Observacion = Corto(p.Observacion, 300) };
        db.Recargos.Add(r);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Recargo domingo #{r.Id}", "Crear", c, actual: $"{Fechas.Texto(f)} · {r.Horas.ToString(CultureInfo.InvariantCulture)} h");
        await db.SaveChangesAsync();
        return Mapeo.Recargo(r);
    }

    [HttpDelete("recargos/{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> EliminarRecargo(int id)
    {
        var r = await db.Recargos.Buscar(id);
        if (r is null) return NotFound();
        var c = await db.Colaboradores.Buscar(r.ColaboradorId);
        db.Recargos.Remove(r);
        auditoria.Registrar($"Recargo domingo #{r.Id}", "Eliminar", c, anterior: $"{Fechas.Texto(r.Fecha)} · {r.Horas.ToString(CultureInfo.InvariantCulture)} h");
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Jornal diario (null = remuneración mensual).</summary>
    [HttpPut("jornal/{colaboradorId:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Jornal(int colaboradorId, JornalGuardar p)
    {
        var c = await db.Colaboradores.Buscar(colaboradorId);
        if (c is null) return NotFound();
        if (p.Jornal is { } j && (j <= 0 || j > 10_000)) return Error("jornal", "El jornal debe ser mayor a 0 y menor a S/ 10,000.");
        string Texto(decimal? x) => x is { } v ? $"Diario S/ {v.ToString("N2", CultureInfo.GetCultureInfo("en-US"))}" : "Mensual";
        var anterior = Texto(c.Jornal);
        c.Jornal = p.Jornal is { } n ? Math.Round(n, 2) : null;
        auditoria.Registrar($"Trabajo · {c.Cargo}", "Modificar", c, "tipo_remuneracion", anterior, Texto(c.Jornal));
        await db.SaveChangesAsync();
        return NoContent();
    }
}
