using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Asistencia y Control de Asistencia. Cada usuario ve los datos de las personas a su alcance (administradores: todos;
/// supervisores: su equipo; colaboradores: los propios). Recintos, turnos y feriados los ven todos.
/// Gestionar (marcas manuales, turnos, horas extras, solicitudes) es de administradores y de supervisores sobre su equipo.
/// </summary>
[ApiController]
[Route("api/asistencia")]
[RequiereModulo(Modulos.Attendance)]
[Authorize]
public partial class AsistenciaController(NexoDbContext db, Auditoria auditoria, Alcance alcance, RelojAsistencia reloj, Configuraciones configuraciones, Planilla planilla, ServicioUbigeo ubigeo) : ControllerBase
{
    private static readonly string[] Metodos = ["Web", "App", "QR", "Foto"];
    private static readonly string[] TiposMarca = ["Entrada", "Salida"];
    private static readonly string[] CodigosDia = ["DES", "LIB", "COMP"];
    private static readonly string[] TiposTurno = ["Permanente", "Transitorio", "Flexible", "Rotativo"];
    private static readonly string[] Jornadas = ["Diurno", "Nocturno", "Mixto"];

    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")] private static partial Regex HoraValida();
    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")] private static partial Regex ColorValido();
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[A-Za-z]{2,}$")] private static partial Regex Email();

    // --- Lectura ---------------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<AsistenciaDatos> Datos()
    {
        var ids = await alcance.VisiblesAsync();
        bool Ve(int c) => ids.Contains(c);
        var cols = await db.Colaboradores.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        // Las listas se filtran en memoria: son pocas filas por colaborador y el filtro "Contains" de un HashSet grande
        // no se traduce bien a SQL.
        var marcas = (await db.Marcas.AsNoTracking().OrderBy(m => m.Fecha).ThenBy(m => m.Hora).ToListAsync()).Where(m => Ve(m.ColaboradorId));
        return new AsistenciaDatos(
            Fechas.Texto(reloj.Hoy), reloj.HoraCorte, Fechas.Texto(SemillaAsistencia.InicioMarcas), alcance.EsAdmin || User.Rol() == Rol.Supervisor,
            (await db.Feriados.AsNoTracking().OrderBy(f => f.Fecha).ToListAsync()).Select(f => new FeriadoDto(Fechas.Texto(f.Fecha), f.Nombre)).ToList(),
            (await db.Recintos.AsNoTracking().OrderBy(r => r.Id).ToListAsync()).Select(Mapeo.Recinto).ToList(),
            (await db.Turnos.AsNoTracking().OrderBy(t => t.Id).ToListAsync()).Select(Mapeo.Turno).ToList(),
            cols.Select(c => new PerfilAsistenciaDto(c.Id, c.RecintoId, c.UsaApp, c.Jornal, c.SaldoVacacionesInicial, c.Contrato ?? "Indefinido")).ToList(),
            (await db.AsignacionesTurno.AsNoTracking().ToListAsync()).Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Asignacion).ToList(),
            (await db.CambiosTurno.AsNoTracking().ToListAsync()).Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Cambio).ToList(),
            marcas.Select(Mapeo.Marca).ToList(),
            (await db.MarcasFallidas.AsNoTracking().OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Hora).ToListAsync())
                .Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Fallida).ToList(),
            // Las observaciones del supervisor no las ve el propio colaborador.
            (await db.NotasSupervisor.AsNoTracking().OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id).ToListAsync())
                .Where(x => Ve(x.ColaboradorId) && (alcance.EsAdmin || x.ColaboradorId != alcance.Propio)).Select(Mapeo.Nota).ToList(),
            (await db.Vacaciones.AsNoTracking().OrderBy(x => x.Desde).ToListAsync()).Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Vacacion).ToList(),
            (await db.Ausencias.AsNoTracking().OrderByDescending(x => x.Desde).ToListAsync()).Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Ausencia).ToList(),
            (await db.Licencias.AsNoTracking().OrderByDescending(x => x.Recibida).ToListAsync()).Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Licencia).ToList(),
            (await db.SolicitudesAsistencia.AsNoTracking().OrderByDescending(x => x.Creada).ThenByDescending(x => x.Id).ToListAsync())
                .Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Solicitud).ToList(),
            (await db.Recargos.AsNoTracking().OrderByDescending(x => x.Fecha).ToListAsync()).Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Recargo).ToList(),
            (await db.AprobacionesHhee.AsNoTracking().ToListAsync()).Where(x => Ve(x.ColaboradorId)).Select(Mapeo.Aprobacion).ToList(),
            new Dictionary<string, Dictionary<string, object>>
            {
                ["ca-tolerancias"] = await configuraciones.LeerAsync("ca-tolerancias"),
                ["ca-metodos"] = await configuraciones.LeerAsync("ca-metodos"),
            });
    }

    // --- Marcas ----------------------------------------------------------------------------------------------------
    /// <summary>Marca propia (widget de Mi Portal o app): fecha y hora las pone el servidor.</summary>
    [HttpPost("marcar")]
    public async Task<ActionResult<MarcaDto>> MarcarPropia(Marcar p)
    {
        if (alcance.Propio is not { } id) return Error("colaborador", "Tu usuario no está vinculado a un colaborador.");
        var c = await db.Colaboradores.Buscar(id);
        if (c is null) return Error("colaborador", "Tu usuario no está vinculado a un colaborador.");
        if (!TiposMarca.Contains(p.Tipo)) return Error("tipo", "El tipo de marca debe ser Entrada o Salida.");
        var metodo = p.Metodo ?? "Web";
        if (!Metodos.Contains(metodo)) return Error("metodo", "Método de marcación no válido.");
        var rc = await db.Recintos.Buscar(p.RecintoId ?? c.RecintoId ?? 0);
        if (rc is null || !rc.Activo) return Error("recintoId", "Selecciona un recinto activo.");

        var hoy = reloj.Hoy;
        var hora = reloj.HoraReal;
        // Método desactivado en Configuraciones › Métodos de marcación: queda como marca fallida.
        if (!await configuraciones.InterruptorAsync("ca-metodos", metodo.ToLowerInvariant()))
        {
            db.MarcasFallidas.Add(new MarcaFallida { ColaboradorId = id, RecintoId = rc.Id, Fecha = hoy, Hora = hora, Tipo = p.Tipo!, Metodo = metodo, Motivo = "Método no permitido" });
            await db.SaveChangesAsync();
            return Error("metodo", $"La marcación por {(metodo == "Web" ? "portal web" : metodo)} está desactivada. Avisa a RR.HH.");
        }
        var previas = await db.Marcas.Where(m => m.ColaboradorId == id && m.Fecha == hoy).ToListAsync();
        var ultima = previas.OrderBy(m => m.Hora).LastOrDefault();
        if (p.Tipo == "Salida" && (ultima is null || ultima.Tipo == "Salida")) return Error("tipo", "Primero registra tu entrada de hoy.");
        if (p.Tipo == "Entrada" && ultima?.Tipo == "Entrada") return Error("tipo", $"Ya registraste tu entrada a las {ultima.Hora}.");
        if (ultima is not null && string.CompareOrdinal(hora, ultima.Hora) < 0) return Error("hora", $"La hora no puede ser anterior a tu última marca ({ultima.Hora}).");

        // Portal web: ubicación real del navegador y selfie, según Configuraciones › Métodos de marcación.
        var cfg = await configuraciones.LeerAsync("ca-metodos");
        var web = metodo == "Web";
        byte[]? foto = null;
        if (!string.IsNullOrEmpty(p.Foto) && (foto = LeerFoto(p.Foto)) is null) return Error("foto", "La foto no es válida. Vuelve a tomarla.");
        if (web && cfg["fotoWeb"] is true && foto is null)
        {
            await RegistrarFallida(id, rc.Id, hoy, hora, p.Tipo!, metodo, "Sin foto", null);
            return Error("foto", "Toma una selfie para registrar tu marca.");
        }
        double lat = rc.Lat, lng = rc.Lng;
        int? distancia = null, precision = null;
        var dentro = true;
        if (web && cfg["ubicacionWeb"] is true)
        {
            if (p.Lat is not (>= -90.0 and <= 90.0) || p.Lng is not (>= -180.0 and <= 180.0))
                return Error("ubicacion", "Permite el acceso a tu ubicación en el navegador para registrar la marca.");
            (lat, lng) = (p.Lat.Value, p.Lng.Value);
            precision = p.Precision is >= 0 and < 100_000 ? (int)Math.Round(p.Precision.Value) : null;
            distancia = (int)Math.Round(Geo.Metros(rc.Lat, rc.Lng, lat, lng));
            // El GPS de un navegador puede errar unas decenas de metros: se tolera hasta 50 m de su precisión.
            dentro = distancia <= rc.Radio + Math.Min(precision ?? 0, 50);
            if (!dentro && cfg["exigirRadio"] is true)
            {
                await RegistrarFallida(id, rc.Id, hoy, hora, p.Tipo!, metodo, "Fuera del radio", distancia);
                return Error("ubicacion", $"Estás a {distancia:N0} m de {rc.Nombre} y el radio permitido es de {rc.Radio} m. Acércate al recinto o avisa a tu jefatura.");
            }
        }

        var m = new Marca
        {
            ColaboradorId = id, RecintoId = rc.Id, Fecha = hoy, Hora = hora, Tipo = p.Tipo!, Metodo = metodo, Lat = lat, Lng = lng,
            DentroRadio = dentro, Distancia = distancia, Precision = precision, Foto = foto is not null,
            Observacion = web ? "Desde Mi Portal" : "",
        };
        db.Marcas.Add(m);
        await db.SaveChangesAsync();
        if (foto is not null)
        {
            db.FotosMarca.Add(new FotoMarca { MarcaId = m.Id, Contenido = foto });
            await db.SaveChangesAsync();
        }
        return Mapeo.Marca(m);
    }

    /// <summary>Selfie de una marca. La ven quienes pueden ver los datos de esa persona.</summary>
    [HttpGet("marcas/{id:int}/foto")]
    public async Task<IActionResult> FotoDeMarca(int id)
    {
        var m = await db.Marcas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (m is null || !(await alcance.VisiblesAsync()).Contains(m.ColaboradorId)) return NotFound();
        var f = await db.FotosMarca.AsNoTracking().FirstOrDefaultAsync(x => x.MarcaId == id);
        if (f is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return File(f.Contenido, "image/jpeg");
    }

    private const int MaxFoto = 400 * 1024;

    /// <summary>JPEG en base64 de hasta 400 KB, o null si no es válido.</summary>
    private static byte[]? LeerFoto(string texto)
    {
        var coma = texto.StartsWith("data:", StringComparison.Ordinal) ? texto.IndexOf(',') : -1;
        if (coma >= 0 && !texto[..coma].Equals("data:image/jpeg;base64", StringComparison.OrdinalIgnoreCase)) return null;
        var b64 = coma >= 0 ? texto[(coma + 1)..] : texto;
        if (b64.Length > MaxFoto * 4 / 3 + 4) return null;
        var buffer = new byte[b64.Length * 3 / 4 + 3];
        if (!Convert.TryFromBase64String(b64, buffer, out var n) || n < 3) return null;
        if (buffer[0] != 0xFF || buffer[1] != 0xD8 || buffer[2] != 0xFF) return null;
        return buffer[..n];
    }

    private async Task RegistrarFallida(int colaboradorId, int recintoId, DateOnly fecha, string hora, string tipo, string metodo, string motivo, int? distancia)
    {
        db.MarcasFallidas.Add(new MarcaFallida
        {
            ColaboradorId = colaboradorId, RecintoId = recintoId, Fecha = fecha, Hora = hora, Tipo = tipo, Metodo = metodo, Motivo = motivo, Distancia = distancia,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Marca propia rechazada en el dispositivo (método desactivado, fuera del radio, sin foto).</summary>
    [HttpPost("fallidas")]
    public async Task<ActionResult<MarcaFallidaDto>> Fallida(FallidaNueva p)
    {
        if (alcance.Propio is not { } id) return Error("colaborador", "Tu usuario no está vinculado a un colaborador.");
        string[] motivos = ["Fuera del radio", "Sin foto", "Sin turno asignado", "Método no permitido"];
        if (!TiposMarca.Contains(p.Tipo) || !Metodos.Contains(p.Metodo) || !motivos.Contains(p.Motivo)) return Error("motivo", "Datos de la marca no válidos.");
        if (!await db.Recintos.AnyAsync(r => r.Id == p.RecintoId)) return Error("recintoId", "El recinto no existe.");
        var f = new MarcaFallida
        {
            ColaboradorId = id, RecintoId = p.RecintoId!.Value, Fecha = reloj.Hoy, Hora = reloj.HoraReal, Tipo = p.Tipo!, Metodo = p.Metodo!,
            Motivo = p.Motivo!, Distancia = p.Distancia is >= 0 and < 1_000_000 ? p.Distancia : null,
        };
        db.MarcasFallidas.Add(f);
        await db.SaveChangesAsync();
        return Mapeo.Fallida(f);
    }

    /// <summary>Marca manual registrada por RR.HH. o la jefatura (queda en el historial con su motivo).</summary>
    [HttpPost("marcas")]
    public async Task<ActionResult<MarcaDto>> Manual(MarcaManual p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        if (!await alcance.GestionaAsync(c.Id)) return Prohibido("No puedes registrar marcas de este colaborador.");
        if (!Fechas.TryLeer(p.Fecha, out var fecha)) return Error("fecha", "La fecha debe tener el formato DD/MM/AAAA.");
        if (fecha > reloj.Hoy) return Error("fecha", "No se pueden registrar marcas futuras.");
        if (fecha < c.Ingreso) return Error("fecha", "La fecha es anterior al ingreso del colaborador.");
        if (!HoraValida().IsMatch(p.Hora ?? "")) return Error("hora", "La hora debe tener el formato HH:MM.");
        if (!TiposMarca.Contains(p.Tipo)) return Error("tipo", "El tipo de marca debe ser Entrada o Salida.");
        var motivo = (p.Motivo ?? "").Trim();
        if (motivo.Length is < 3 or > 300) return Error("motivo", "Indica el motivo de la marca manual.");
        var rc = await db.Recintos.Buscar(c.RecintoId ?? 0) ?? await db.Recintos.OrderBy(r => r.Id).FirstAsync();
        var m = new Marca
        {
            ColaboradorId = c.Id, RecintoId = rc.Id, Fecha = fecha, Hora = p.Hora!, Tipo = p.Tipo!, Metodo = "Manual", Lat = rc.Lat, Lng = rc.Lng,
            CreadoPor = User.Nombre(), Motivo = motivo,
        };
        db.Marcas.Add(m);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Marca manual #{m.Id}", "Crear", c, actual: $"{m.Tipo} {Fechas.Texto(fecha)} {m.Hora} · {motivo}");
        await db.SaveChangesAsync();
        return Mapeo.Marca(m);
    }

    [HttpDelete("marcas/{id:int}")]
    public async Task<IActionResult> EliminarMarca(int id)
    {
        var m = await db.Marcas.Buscar(id);
        if (m is null) return NotFound();
        if (!await alcance.GestionaAsync(m.ColaboradorId)) return Prohibido("No puedes eliminar marcas de este colaborador.");
        if (m.Metodo != "Manual") return Error("id", "Solo se pueden eliminar marcas manuales.");
        var c = await db.Colaboradores.Buscar(m.ColaboradorId);
        db.Marcas.Remove(m);
        auditoria.Registrar($"Marca manual #{m.Id}", "Eliminar", c, anterior: $"{m.Tipo} {Fechas.Texto(m.Fecha)} {m.Hora}");
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Observaciones del supervisor ------------------------------------------------------------------------------
    [HttpPost("notas")]
    public async Task<ActionResult<NotaDto>> Nota(NotaNueva p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        if (!await alcance.GestionaAsync(c.Id)) return Prohibido("No puedes registrar observaciones de este colaborador.");
        var texto = (p.Texto ?? "").Trim();
        if (texto.Length is 0 or > 1000) return Error("texto", "Escribe la observación (máximo 1000 caracteres).");
        var n = new NotaSupervisor { ColaboradorId = c.Id, Fecha = Fechas.HoyLima(), Texto = texto, Autor = User.Nombre() };
        db.NotasSupervisor.Add(n);
        auditoria.Registrar("Observación del supervisor", "Crear", c, actual: texto.Length > 60 ? texto[..60] : texto);
        await db.SaveChangesAsync();
        return Mapeo.Nota(n);
    }

    // --- Horas extras ----------------------------------------------------------------------------------------------
    [HttpPost("hhee")]
    public async Task<ActionResult<AprobacionHheeDto?>> Decidir(DecisionHhee p)
    {
        var c = await db.Colaboradores.Buscar(p.ColaboradorId);
        if (c is null) return Error("colaboradorId", "El colaborador no existe.");
        if (!await alcance.GestionaAsync(c.Id)) return Prohibido("No puedes aprobar horas extras de este colaborador.");
        if (!Fechas.TryLeer(p.Fecha, out var fecha) || fecha > reloj.Hoy) return Error("fecha", "La fecha no es válida.");
        if (await db.Periodos.AnyAsync(x => x.Id == Planilla.De(fecha) && x.Cerrado))
            return Error("fecha", $"El período de planilla {Planilla.Texto(Planilla.De(fecha))} está cerrado: sus horas extras ya no se modifican.");
        var actual = await db.AprobacionesHhee.FirstOrDefaultAsync(x => x.ColaboradorId == c.Id && x.Fecha == fecha);
        if (actual?.Enviado == true && !alcance.EsAdmin) return Prohibido("Estas horas extras ya se enviaron a planilla; solo un administrador puede cambiarlas.");
        var previo = actual?.Estado ?? "En espera";
        var entidad = $"Horas extras {Fechas.Texto(fecha)}";

        if (p.Accion == "revertir")
        {
            if (actual is not null) db.AprobacionesHhee.Remove(actual);
            auditoria.Registrar(entidad, "Modificar", c, "estado", previo, "En espera");
            await db.SaveChangesAsync();
            return Ok(null);
        }
        if (p.Accion is not ("aprobar" or "rechazar")) return Error("accion", "La acción debe ser aprobar, rechazar o revertir.");
        var obs = (p.Observacion ?? "").Trim();
        if (obs.Length > 300) return Error("observacion", "La observación admite hasta 300 caracteres.");
        var lineas = p.Accion == "aprobar" ? p.Lineas ?? [] : [];
        if (p.Accion == "aprobar")
        {
            if (lineas.Count == 0 || lineas.Any(l => l.Minutos <= 0 || l.Tipo is not (25 or 35 or 100))) return Error("lineas", "Indica los minutos y la sobretasa (25 %, 35 % o 100 %) de cada línea.");
            if (p.MinutosExceso <= 0 || p.MinutosExceso > 24 * 60) return Error("minutosExceso", "El exceso del día no es válido.");
            if (lineas.Sum(l => l.Minutos) > p.MinutosExceso) return Error("lineas", "No se pueden aprobar más minutos que el exceso registrado.");
        }
        else if (obs.Length == 0) return Error("observacion", "Indica el motivo del rechazo.");

        var a = actual ?? db.AprobacionesHhee.Add(new AprobacionHhee { ColaboradorId = c.Id, Fecha = fecha }).Entity;
        var total = lineas.Sum(l => l.Minutos);
        a.Estado = p.Accion == "rechazar" ? "Rechazado" : total >= p.MinutosExceso ? "Aprobado" : "Aprobado parcial";
        a.Lineas = lineas.Select(l => new LineaHhee { Minutos = l.Minutos, Tipo = l.Tipo }).ToList();
        a.Observacion = obs;
        a.AprobadoPor = User.Nombre();
        a.Enviado = false;
        auditoria.Registrar(entidad, "Modificar", c, "estado", previo, a.Estado == "Rechazado" ? "Rechazado" : $"{a.Estado} · {total / 60}:{total % 60:00} h");
        await db.SaveChangesAsync();
        return Mapeo.Aprobacion(a);
    }

    /// <summary>Marca como enviadas a planilla las horas extras aprobadas del período abierto de planilla (se pagan al procesarla).</summary>
    [HttpPost("hhee/enviar")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<EnvioHheeResultado> Enviar(EnvioHhee p)
    {
        var omitidos = new List<string>();
        var enviados = 0;
        var abierto = await planilla.AbiertoAsync();
        foreach (var k in p.Items ?? [])
        {
            if (!Fechas.TryLeer(k.Fecha, out var f)) continue;
            var a = await db.AprobacionesHhee.FirstOrDefaultAsync(x => x.ColaboradorId == k.ColaboradorId && x.Fecha == f);
            if (a is null || a.Estado is not ("Aprobado" or "Aprobado parcial")) { omitidos.Add($"{k.ColaboradorId} ({k.Fecha}): no está aprobada"); continue; }
            if (Planilla.De(f) != abierto) { omitidos.Add($"{k.ColaboradorId} ({k.Fecha}): el período {Planilla.Texto(Planilla.De(f))} no es el período abierto de planilla"); continue; }
            if (a.Enviado) continue;
            a.Enviado = true;
            enviados++;
            var c = await db.Colaboradores.Buscar(a.ColaboradorId);
            auditoria.Registrar($"Horas extras {Fechas.Texto(f)}", "Modificar", c, "enviado_planilla", "No", "Sí");
        }
        await db.SaveChangesAsync();
        return new EnvioHheeResultado(enviados, omitidos);
    }

    // --- Turnos ----------------------------------------------------------------------------------------------------
    [HttpPost("turnos")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<TurnoDto>> CrearTurno(TurnoGuardar p)
    {
        if (await ValidarTurno(p, null) is { } err) return err;
        var t = new Turno { Id = (await db.Turnos.MaxAsync(x => (int?)x.Id) ?? 0) + 1 };
        AplicarTurno(t, p);
        db.Turnos.Add(t);
        auditoria.Registrar($"Turno {t.Sigla}", "Crear", actual: $"{t.Nombre} · {t.Entrada}–{t.Salida}");
        await db.SaveChangesAsync();
        return Mapeo.Turno(t);
    }

    [HttpPut("turnos/{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<TurnoDto>> ModificarTurno(int id, TurnoGuardar p)
    {
        var t = await db.Turnos.Buscar(id);
        if (t is null) return NotFound();
        if (await ValidarTurno(p, t) is { } err) return err;
        if (!p.Activo && t.Activo && await EnUso(t.Id)) return Error("activo", "No se puede desactivar: hay colaboradores con este turno vigente.");
        AplicarTurno(t, p);
        auditoria.Registrar($"Turno {t.Sigla}", "Modificar", actual: $"{t.Nombre} · {t.Entrada}–{t.Salida}");
        await db.SaveChangesAsync();
        return Mapeo.Turno(t);
    }

    [HttpPost("turnos/{id:int}/estado")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<TurnoDto>> EstadoTurno(int id, EstadoActivo p)
    {
        var t = await db.Turnos.Buscar(id);
        if (t is null) return NotFound();
        if (!p.Activo && t.Activo && await EnUso(t.Id))
        {
            var hoy = reloj.Hoy;
            var n = (await db.AsignacionesTurno.Where(a => a.TurnoId == id && a.Desde <= hoy && (a.Hasta == null || a.Hasta >= hoy)).Select(a => a.ColaboradorId).Distinct().CountAsync());
            return Error("activo", $"No se puede desactivar: {n} colaborador(es) tienen este turno vigente.");
        }
        if (t.Activo != p.Activo)
        {
            t.Activo = p.Activo;
            auditoria.Registrar($"Turno {t.Sigla}", "Modificar", propiedad: "estado", anterior: p.Activo ? "Inactivo" : "Activo", actual: p.Activo ? "Activo" : "Inactivo");
            await db.SaveChangesAsync();
        }
        return Mapeo.Turno(t);
    }

    private async Task<bool> EnUso(int turnoId)
    {
        var hoy = reloj.Hoy;
        // Turno vigente hoy: la asignación más reciente que cubre la fecha.
        var vigentes = await db.AsignacionesTurno.Where(a => a.Desde <= hoy && (a.Hasta == null || a.Hasta >= hoy)).ToListAsync();
        return vigentes.GroupBy(a => a.ColaboradorId).Any(g => g.MaxBy(a => a.Desde)!.TurnoId == turnoId);
    }

    private async Task<ActionResult?> ValidarTurno(TurnoGuardar p, Turno? actual)
    {
        var e = new Dictionary<string, string[]>();
        void Err(string k, string m) => e[k] = [m];
        var nombre = (p.Nombre ?? "").Trim();
        var sigla = (p.Sigla ?? "").Trim().ToUpperInvariant();
        if (nombre.Length is 0 or > 60) Err("nombre", "Ingresa el nombre del turno (máximo 60 caracteres).");
        if (sigla.Length is 0 or > 5) Err("sigla", "La sigla debe tener entre 1 y 5 caracteres.");
        else if (await db.Turnos.AnyAsync(x => x.Sigla == sigla && x.Id != (actual == null ? 0 : actual.Id))) Err("sigla", "Ya existe un turno con esa sigla.");
        if (!TiposTurno.Contains(p.Tipo)) Err("tipo", "El tipo debe ser Permanente, Transitorio, Flexible o Rotativo.");
        if (!Jornadas.Contains(p.Jornada)) Err("jornada", "La jornada debe ser Diurno, Nocturno o Mixto.");
        if (!HoraValida().IsMatch(p.Entrada ?? "")) Err("entrada", "La hora de entrada debe tener el formato HH:MM.");
        if (!HoraValida().IsMatch(p.Salida ?? "")) Err("salida", "La hora de salida debe tener el formato HH:MM.");
        else if (p.Entrada == p.Salida) Err("salida", "La salida debe ser distinta de la entrada.");
        if (p.Colacion is < 0 or > 180) Err("colacion", "La colación debe estar entre 0 y 180 minutos.");
        if (p.Tolerancia is < 0 or > 120) Err("tolerancia", "La tolerancia debe estar entre 0 y 120 minutos.");
        if (p.Tipo == "Rotativo")
        {
            if (p.Ciclo is not { Trabajo: >= 1 and <= 30, Descanso: >= 1 and <= 30 }) Err("ciclo", "Indica los días de trabajo y de descanso del ciclo (1 a 30).");
        }
        else if (p.Dias is not { Count: > 0 } || p.Dias.Any(d => d is < 0 or > 6)) Err("dias", "Elige al menos un día de la semana.");
        if (p.Color is { Length: > 0 } col && !ColorValido().IsMatch(col)) Err("color", "El color debe tener el formato #RRGGBB.");
        return e.Count > 0 ? ValidationProblem(new ValidationProblemDetails(e) { Title = "Revisa los datos del turno." }) : null;
    }

    private static void AplicarTurno(Turno t, TurnoGuardar p)
    {
        t.Nombre = p.Nombre!.Trim();
        t.Sigla = p.Sigla!.Trim().ToUpperInvariant();
        t.Tipo = p.Tipo!;
        t.Jornada = p.Jornada!;
        t.Entrada = p.Entrada!;
        t.Salida = p.Salida!;
        t.Colacion = p.Colacion;
        t.Tolerancia = p.Tolerancia;
        t.Dias = p.Tipo == "Rotativo" ? [0, 1, 2, 3, 4, 5, 6] : p.Dias!.Distinct().Order().ToList();
        t.CicloTrabajo = p.Tipo == "Rotativo" ? p.Ciclo!.Trabajo : null;
        t.CicloDescanso = p.Tipo == "Rotativo" ? p.Ciclo!.Descanso : null;
        t.Color = string.IsNullOrWhiteSpace(p.Color) ? t.Color : p.Color;
        t.Activo = p.Activo;
    }

    /// <summary>
    /// Asigna turno: "dia" = solo esa fecha; "hasta" = de la fecha a "hasta" en los días de la semana elegidos;
    /// "permanente" = nuevo turno base desde la fecha (los códigos de descanso no pueden ser permanentes).
    /// Devuelve las asignaciones y cambios resultantes de esos colaboradores.
    /// </summary>
    [HttpPost("asignaciones")]
    public async Task<ActionResult<object>> Asignar(AsignarTurno p)
    {
        var ids = (p.ColaboradorIds ?? []).Distinct().ToList();
        if (ids.Count is 0 or > 500) return Error("colaboradorIds", "Elige al menos un colaborador.");
        var cols = await db.Colaboradores.Where(c => ids.Contains(c.Id)).ToListAsync();
        if (cols.Count != ids.Count) return Error("colaboradorIds", "Algún colaborador no existe.");
        foreach (var c in cols) if (!await alcance.GestionaAsync(c.Id)) return Prohibido($"No puedes asignar turnos a {c.Apellidos}, {c.Nombres}.");
        if (!Fechas.TryLeer(p.Fecha, out var fecha)) return Error("fecha", "La fecha debe tener el formato DD/MM/AAAA.");
        Turno? turno = null;
        if (p.TurnoId is { } tid)
        {
            turno = await db.Turnos.Buscar(tid);
            if (turno is null || !turno.Activo) return Error("turnoId", "El turno no existe o está inactivo.");
        }
        else if (!CodigosDia.Contains(p.Codigo)) return Error("codigo", "Elige un turno o un código de descanso.");
        var modo = p.Modo ?? "dia";
        if (modo is not ("dia" or "hasta" or "permanente")) return Error("modo", "Modo no válido.");
        if (modo == "permanente" && turno is null) return Error("modo", "Un código de descanso no puede asignarse de forma permanente.");
        var fechas = new List<DateOnly>();
        if (modo == "dia") fechas.Add(fecha);
        else if (modo == "hasta")
        {
            if (!Fechas.TryLeer(p.Hasta, out var hasta) || hasta < fecha) return Error("hasta", "La fecha final debe ser igual o posterior a la inicial.");
            if (hasta.DayNumber - fecha.DayNumber > 370) return Error("hasta", "El rango no puede superar un año.");
            var dias = p.Dias ?? [];
            for (var f = fecha; f <= hasta; f = f.AddDays(1)) if (dias.Count == 0 || dias.Contains((int)f.DayOfWeek)) fechas.Add(f);
        }
        var etiqueta = turno?.Sigla ?? p.Codigo switch { "DES" => "Descanso", "LIB" => "Libre", _ => "Compensado" };
        var autor = User.Nombre();

        foreach (var c in cols)
        {
            if (modo == "permanente")
            {
                foreach (var a in await db.AsignacionesTurno.Where(x => x.ColaboradorId == c.Id).ToListAsync())
                {
                    if (a.Desde >= fecha) db.AsignacionesTurno.Remove(a);
                    else if (a.Hasta is null || a.Hasta >= fecha) a.Hasta = fecha.AddDays(-1);
                }
                db.AsignacionesTurno.Add(new AsignacionTurno
                {
                    ColaboradorId = c.Id, TurnoId = turno!.Id, Desde = fecha, CicloInicio = turno.Tipo == "Rotativo" ? fecha : null, CreadoPor = autor,
                });
                db.CambiosTurno.RemoveRange(db.CambiosTurno.Where(x => x.ColaboradorId == c.Id && x.Fecha >= fecha));
                auditoria.Registrar("Asignación de turno", "Crear", c, actual: $"{etiqueta} desde el {Fechas.Texto(fecha)} (permanente)");
            }
            else
            {
                db.CambiosTurno.RemoveRange(db.CambiosTurno.Where(x => x.ColaboradorId == c.Id && fechas.Contains(x.Fecha)));
                db.CambiosTurno.AddRange(fechas.Select(f => new CambioTurno { ColaboradorId = c.Id, Fecha = f, TurnoId = turno?.Id, Codigo = turno is null ? p.Codigo : null, CreadoPor = autor }));
                auditoria.Registrar("Asignación de turno", "Modificar", c, actual: modo == "dia"
                    ? $"{etiqueta} el {Fechas.Texto(fecha)}"
                    : $"{etiqueta} del {Fechas.Texto(fecha)} al {p.Hasta} ({fechas.Count} días)");
            }
        }
        await db.SaveChangesAsync();
        return Ok(new
        {
            asignaciones = (await db.AsignacionesTurno.AsNoTracking().Where(x => ids.Contains(x.ColaboradorId)).ToListAsync()).Select(Mapeo.Asignacion),
            cambiosTurno = (await db.CambiosTurno.AsNoTracking().Where(x => ids.Contains(x.ColaboradorId)).ToListAsync()).Select(Mapeo.Cambio),
        });
    }

    // --- Recintos --------------------------------------------------------------------------------------------------
    [HttpPost("recintos")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<RecintoDto>> CrearRecinto(RecintoGuardar p)
    {
        p = await ConUbigeoOficial(p);
        if (await ValidarRecinto(p, null) is { } err) return err;
        var r = new Recinto { Id = (await db.Recintos.MaxAsync(x => (int?)x.Id) ?? 0) + 1 };
        AplicarRecinto(r, p);
        db.Recintos.Add(r);
        auditoria.Registrar($"Recinto · {r.Nombre}", "Crear", actual: $"{r.Direccion}, {r.Distrito} · radio {r.Radio} m");
        await db.SaveChangesAsync();
        return Mapeo.Recinto(r);
    }

    [HttpPut("recintos/{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<RecintoDto>> ModificarRecinto(int id, RecintoGuardar p)
    {
        var r = await db.Recintos.Buscar(id);
        if (r is null) return NotFound();
        p = await ConUbigeoOficial(p);
        if (await ValidarRecinto(p, r) is { } err) return err;
        if (!p.Activo && r.Activo && await db.Colaboradores.AnyAsync(c => c.RecintoId == id))
            return Error("activo", "No se puede desactivar: hay colaboradores con este recinto como principal.");
        var antes = Mapeo.Recinto(r);
        var nombreAntes = r.Nombre;
        AplicarRecinto(r, p);
        var despues = Mapeo.Recinto(r);
        foreach (var (campo, a, d) in Diferencias(antes, despues))
            auditoria.Registrar($"Recinto · {nombreAntes}", "Modificar", propiedad: campo, anterior: a, actual: d);
        await db.SaveChangesAsync();
        return despues;
    }

    private static IEnumerable<(string, string, string)> Diferencias(RecintoDto a, RecintoDto b)
    {
        (string campo, string x, string y)[] campos =
        [
            ("nombre", a.Nombre, b.Nombre), ("dirección", a.Direccion, b.Direccion), ("distrito", a.Distrito, b.Distrito), ("radio", $"{a.Radio}", $"{b.Radio}"),
            ("jefatura", $"{a.JefaturaId}", $"{b.JefaturaId}"), ("administrativo", $"{a.AdministrativoId}", $"{b.AdministrativoId}"),
            ("estado", a.Activo ? "Activo" : "Inactivo", b.Activo ? "Activo" : "Inactivo"), ("latitud", $"{a.Lat}", $"{b.Lat}"), ("longitud", $"{a.Lng}", $"{b.Lng}"),
            ("correos de alertas", string.Join(", ", a.Emails), string.Join(", ", b.Emails)), ("usuarios con acceso", string.Join(", ", a.Usuarios), string.Join(", ", b.Usuarios)),
            ("información adicional", a.Info, b.Info), ("clave", a.Clave, b.Clave),
        ];
        return campos.Where(c => c.x != c.y).Select(c => (c.campo, c.x.Length == 0 ? "—" : c.x, c.y.Length == 0 ? "—" : c.y));
    }

    private async Task<ActionResult?> ValidarRecinto(RecintoGuardar p, Recinto? actual)
    {
        var e = new Dictionary<string, string[]>();
        void Err(string k, string m) => e[k] = [m];
        var nombre = (p.Nombre ?? "").Trim();
        if (nombre.Length is 0 or > 80) Err("nombre", "Ingresa el nombre del recinto (máximo 80 caracteres).");
        else if (await db.Recintos.AnyAsync(x => x.Nombre == nombre && x.Id != (actual == null ? 0 : actual.Id))) Err("nombre", "Ya existe un recinto con ese nombre.");
        if ((p.Direccion ?? "").Trim().Length is 0 or > 200) Err("direccion", "Ingresa la dirección.");
        if (p.Radio is < 20 or > 5000) Err("radio", "El radio debe estar entre 20 y 5000 metros.");
        // En el Perú, departamento, provincia y distrito deben existir en el ubigeo vigente (ya normalizados por ConUbigeoOficial).
        if (Ubigeo.EsPeru(p.Pais) && await ubigeo.DepartamentosAsync() is { Count: > 0 } deps)
        {
            var dep = deps.FirstOrDefault(d => d.Nombre == p.Departamento);
            var prov = dep?.Provincias.FirstOrDefault(x => x.Nombre == p.Provincia);
            if (dep is null) Err("departamento", "Elige un departamento de la lista.");
            else if (prov is null) Err("provincia", $"Elige una provincia de {dep.Nombre}.");
            else if (!prov.Distritos.Any(x => x.Nombre == p.Distrito)) Err("distrito", $"Elige un distrito de la provincia de {prov.Nombre}.");
        }
        if (p.Lat is < -90 or > 90 || p.Lng is < -180 or > 180) Err("lat", "Las coordenadas no son válidas.");
        var emails = p.Emails ?? [];
        if (emails.Count > 5) Err("emails", "Máximo 5 correos de alertas.");
        else if (emails.Any(m => !Email().IsMatch(m.Trim()))) Err("emails", "Algún correo de alertas no es válido.");
        var refs = new[] { p.JefaturaId, p.AdministrativoId }.Where(x => x is not null).Select(x => x!.Value).Concat(p.Usuarios ?? []).Distinct().ToList();
        if (refs.Count > 0 && await db.Colaboradores.CountAsync(c => refs.Contains(c.Id)) != refs.Count) Err("usuarios", "Algún colaborador indicado no existe.");
        if ((p.Ruc ?? "") is { Length: > 0 } ruc && !Regex.IsMatch(ruc, @"^\d{11}$")) Err("ruc", "El RUC debe tener 11 dígitos.");
        return e.Count > 0 ? ValidationProblem(new ValidationProblemDetails(e) { Title = "Revisa los datos del recinto." }) : null;
    }

    /// <summary>Lleva departamento, provincia y distrito a los nombres del ubigeo vigente si coinciden sin tildes ("Lurín" → "Lurin").</summary>
    private async Task<RecintoGuardar> ConUbigeoOficial(RecintoGuardar p)
    {
        if (!Ubigeo.EsPeru(p.Pais)) return p;
        var dep = (await ubigeo.DepartamentosAsync()).FirstOrDefault(d => Ubigeo.Igual(d.Nombre, p.Departamento));
        if (dep is null) return p;
        var prov = dep.Provincias.FirstOrDefault(x => Ubigeo.Igual(x.Nombre, p.Provincia));
        var dist = prov?.Distritos.FirstOrDefault(x => Ubigeo.Igual(x.Nombre, p.Distrito));
        return p with { Departamento = dep.Nombre, Provincia = prov?.Nombre ?? p.Provincia, Distrito = dist?.Nombre ?? p.Distrito };
    }

    private static void AplicarRecinto(Recinto r, RecintoGuardar p)
    {
        static string T(string? s, int n) { var x = (s ?? "").Trim(); return x.Length > n ? x[..n] : x; }
        r.Nombre = T(p.Nombre, 80); r.Empresa = T(p.Empresa, 120); r.Ruc = T(p.Ruc, 11); r.CodigoInterno = T(p.CodigoInterno, 20);
        r.Pais = T(p.Pais, 40) is { Length: > 0 } pais ? pais : "Perú"; r.Departamento = T(p.Departamento, 40); r.Provincia = T(p.Provincia, 40);
        r.Distrito = T(p.Distrito, 60); r.Direccion = T(p.Direccion, 200); r.Clave = T(p.Clave, 20); r.Info = T(p.Info, 500);
        r.JefaturaId = p.JefaturaId; r.AdministrativoId = p.AdministrativoId; r.Lat = p.Lat; r.Lng = p.Lng; r.Radio = p.Radio;
        r.Emails = (p.Emails ?? []).Select(m => m.Trim().ToLowerInvariant()).Distinct().ToList();
        r.Usuarios = (p.Usuarios ?? []).Distinct().ToList();
        r.Activo = p.Activo;
    }

    // --- Ayudas ----------------------------------------------------------------------------------------------------
    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });

    private ObjectResult Prohibido(string mensaje) => Problem(title: mensaje, detail: mensaje, statusCode: StatusCodes.Status403Forbidden);
}
