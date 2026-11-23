using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Data;

/// <summary>
/// Datos de ejemplo de Asistencia y Control de Asistencia (febrero y marzo de 2026), los mismos que mostraba el frontend
/// cuando trabajaba en memoria. Las marcas se generan de forma determinista y coherente con turnos, ausencias y vacaciones.
/// Solo se cargan si no hay turnos.
/// </summary>
public static class SemillaAsistencia
{
    /// <summary>"Hoy" de los datos de ejemplo y hora de corte de ese día.</summary>
    public static readonly DateOnly Corte = F("16/03/2026");
    public const string HoraCorte = "11:45";
    public static readonly DateOnly InicioMarcas = F("01/02/2026");

    private const string Lucia = "Lucía Cáceres";

    public static async Task CargarAsync(NexoDbContext db)
    {
        if (!await db.Feriados.AnyAsync()) db.Feriados.AddRange(Feriados());
        if (await db.Turnos.AnyAsync() || !await db.Colaboradores.AnyAsync()) { await db.SaveChangesAsync(); return; }

        var recintos = Recintos();
        var turnos = Turnos();
        db.Recintos.AddRange(recintos);
        db.Turnos.AddRange(turnos);
        await db.SaveChangesAsync();

        // Datos de asistencia de cada colaborador.
        var colaboradores = await db.Colaboradores.ToListAsync();
        var principal = new Dictionary<int, int> { [0] = 1, [2] = 1, [3] = 1, [5] = 1, [6] = 1, [8] = 1, [10] = 1, [11] = 1, [12] = 1, [14] = 1, [15] = 1, [1] = 2, [7] = 2, [13] = 2, [4] = 3, [9] = 3 };
        var contratos = new Dictionary<int, string> { [7] = "Plazo fijo", [8] = "Plazo fijo", [10] = "Plazo fijo", [15] = "Plazo fijo", [12] = "Convenio de prácticas" };
        var saldos = new Dictionary<int, decimal> { [0] = 43.84m, [1] = 45, [2] = 15, [3] = 20, [4] = 4, [5] = 30, [6] = 30, [7] = 3.62m, [8] = 0, [9] = 12, [10] = 0, [11] = 40, [12] = 0, [13] = 5, [14] = 0, [15] = 0 };
        int[] usaApp = [1, 4, 7, 9, 13, 15];
        foreach (var c in colaboradores)
        {
            c.RecintoId = principal.TryGetValue(c.Id, out var r) ? r : null;
            c.UsaApp = usaApp.Contains(c.Id);
            c.SaldoVacacionesInicial = saldos.GetValueOrDefault(c.Id);
            c.Jornal = c.Id == 7 ? 71.68m : null;
            c.Contrato ??= contratos.GetValueOrDefault(c.Id, "Indefinido");
        }

        var asignaciones = Asignaciones();
        var cambios = new List<CambioTurno> { new() { ColaboradorId = 1, Fecha = F("04/03/2026"), Codigo = "COMP", CreadoPor = Lucia } };
        var vacaciones = Vacaciones();
        var ausencias = Ausencias();
        var recargos = Recargos();
        db.AsignacionesTurno.AddRange(asignaciones);
        db.CambiosTurno.AddRange(cambios);
        db.Vacaciones.AddRange(vacaciones);
        db.Ausencias.AddRange(ausencias);
        db.Recargos.AddRange(recargos);
        await db.SaveChangesAsync();

        db.Licencias.AddRange(Licencias(ausencias));
        db.SolicitudesAsistencia.AddRange(Solicitudes());
        db.MarcasFallidas.AddRange(Fallidas());
        db.NotasSupervisor.AddRange(
            new NotaSupervisor { ColaboradorId = 7, Fecha = F("12/03/2026"), Texto = "La app marcó fuera del radio en la puerta 2; se pidió ampliar el radio del almacén.", Autor = "Benavides Rojas, Diego" },
            new NotaSupervisor { ColaboradorId = 10, Fecha = F("10/03/2026"), Texto = "Dos lunes sin marcar. Se conversó con ella; presentará sustento.", Autor = "Espinoza Vargas, Martín" },
            new NotaSupervisor { ColaboradorId = 9, Fecha = F("06/03/2026"), Texto = "Llegadas tarde por cierre de tienda del día anterior. Ajustar turno los sábados.", Autor = "Espinoza Vargas, Martín" });

        var feriados = (await db.Feriados.Select(x => x.Fecha).ToListAsync()).ToHashSet();
        var gen = new Generador(colaboradores, recintos, turnos, asignaciones, cambios, vacaciones, ausencias, recargos, feriados);
        gen.Generar();
        db.Marcas.AddRange(gen.Marcas);
        db.AprobacionesHhee.AddRange(gen.Aprobaciones);
        await db.SaveChangesAsync();
    }

    private static DateOnly F(string s) => DateOnly.ParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static IEnumerable<Feriado> Feriados() => new (string f, string n)[]
    {
        ("01/01/2026", "Año Nuevo"), ("02/04/2026", "Jueves Santo"), ("03/04/2026", "Viernes Santo"), ("01/05/2026", "Día del Trabajo"),
        ("07/06/2026", "Batalla de Arica y Día de la Bandera"), ("29/06/2026", "San Pedro y San Pablo"), ("23/07/2026", "Día de la Fuerza Aérea del Perú"),
        ("28/07/2026", "Fiestas Patrias"), ("29/07/2026", "Fiestas Patrias"), ("06/08/2026", "Batalla de Junín"), ("30/08/2026", "Santa Rosa de Lima"),
        ("08/10/2026", "Combate de Angamos"), ("01/11/2026", "Todos los Santos"), ("08/12/2026", "Inmaculada Concepción"),
        ("09/12/2026", "Batalla de Ayacucho"), ("25/12/2026", "Navidad"),
    }.Select(x => new Feriado { Fecha = F(x.f), Nombre = x.n });

    private static List<Recinto> Recintos()
    {
        const string emp = "Comercial Andina S.A.C.", ruc = "20601234571";
        return
        [
            new() { Id = 1, Nombre = "Sede Central · San Isidro", Empresa = emp, Ruc = ruc, CodigoInterno = "CA-001", Departamento = "Lima", Provincia = "Lima", Distrito = "San Isidro",
                Direccion = "Av. Javier Prado Este 1234", Clave = "SC-01", Info = "Ingreso por recepción, piso 8. Estacionamiento en sótano 2.", JefaturaId = 11, AdministrativoId = 2,
                Lat = -12.0912, Lng = -77.0225, Radio = 150, Emails = ["asistencia@comercialandina.pe"], Usuarios = [2, 11, 5] },
            new() { Id = 2, Nombre = "Almacén Lurín", Empresa = emp, Ruc = ruc, CodigoInterno = "CA-002", Departamento = "Lima", Provincia = "Lima", Distrito = "Lurín",
                Direccion = "Mz. B Lt. 5, Parque Industrial", Clave = "AL-02", Info = "Turno de mañana. Uso obligatorio de EPP.", JefaturaId = 1, AdministrativoId = 2,
                Lat = -12.2745, Lng = -76.871, Radio = 250, Emails = ["almacen@comercialandina.pe", "d.benavides@comercialandina.pe"], Usuarios = [1, 2, 13] },
            new() { Id = 3, Nombre = "Tienda Miraflores", Empresa = emp, Ruc = ruc, CodigoInterno = "CA-003", Departamento = "Lima", Provincia = "Lima", Distrito = "Miraflores",
                Direccion = "Av. Larco 455", Clave = "TM-03", Info = "Horario de tienda de 10:00 a 21:00.", JefaturaId = 9, AdministrativoId = 2,
                Lat = -12.1218, Lng = -77.0302, Radio = 80, Emails = ["miraflores@comercialandina.pe"], Usuarios = [9, 2] },
            new() { Id = 4, Nombre = "Centro de distribución Callao", Empresa = emp, Ruc = ruc, CodigoInterno = "CA-004", Departamento = "Callao", Provincia = "Callao", Distrito = "Callao",
                Direccion = "Av. Néstor Gambetta 3450", Clave = "CD-04", Info = "Cerrado temporalmente desde enero de 2026.", Lat = -12.013, Lng = -77.118, Radio = 200,
                Activo = false, Usuarios = [2] },
        ];
    }

    private static List<Turno> Turnos()
    {
        List<int> lv = [1, 2, 3, 4, 5];
        return
        [
            new() { Id = 1, Nombre = "Administrativo", Sigla = "ADM", Tipo = "Permanente", Entrada = "08:30", Salida = "18:00", Colacion = 60, Dias = [.. lv], Tolerancia = 10, Color = "#D6E4FF" },
            new() { Id = 2, Nombre = "Almacén mañana", Sigla = "ALM", Tipo = "Permanente", Entrada = "07:00", Salida = "15:30", Colacion = 45, Dias = [1, 2, 3, 4, 5, 6], Tolerancia = 5, Color = "#C7F1AF" },
            new() { Id = 3, Nombre = "Tienda rotativo", Sigla = "TR", Tipo = "Rotativo", Entrada = "11:00", Salida = "20:00", Colacion = 60, Dias = [0, 1, 2, 3, 4, 5, 6], Tolerancia = 10, Color = "#FFE8A3", CicloTrabajo = 5, CicloDescanso = 2 },
            new() { Id = 4, Nombre = "Flexible tecnología", Sigla = "FLX", Tipo = "Flexible", Entrada = "09:00", Salida = "18:00", Colacion = 60, Dias = [.. lv], Tolerancia = 30, Color = "#E9DDFF" },
            new() { Id = 5, Nombre = "Prácticas", Sigla = "PRA", Tipo = "Permanente", Entrada = "09:00", Salida = "15:00", Colacion = 0, Dias = [.. lv], Tolerancia = 10, Color = "#FFDDE7" },
            new() { Id = 6, Nombre = "Nocturno almacén", Sigla = "NOC", Tipo = "Permanente", Jornada = "Nocturno", Entrada = "22:00", Salida = "06:00", Colacion = 45, Dias = [.. lv], Tolerancia = 5, Color = "#ECEEF2", Activo = false },
        ];
    }

    private static List<AsignacionTurno> Asignaciones()
    {
        AsignacionTurno A(int c, int t, string desde, string? ciclo = null) =>
            new() { ColaboradorId = c, TurnoId = t, Desde = F(desde), CicloInicio = ciclo is null ? null : F(ciclo), CreadoPor = Lucia };
        return
        [
            .. new[] { 0, 2, 5, 6, 10, 11, 14 }.Select(c => A(c, 1, "01/01/2026")),
            .. new[] { 3, 8, 15 }.Select(c => A(c, 4, "01/01/2026")),
            .. new[] { 1, 7, 13 }.Select(c => A(c, 2, "01/01/2026")),
            A(4, 3, "01/01/2026", "29/12/2025"), A(9, 3, "01/01/2026", "31/12/2025"),
            A(12, 5, "03/03/2026"),
        ];
    }

    private static List<Vacacion> Vacaciones()
    {
        Vacacion V(int c, string d, string h, int dias, string origen = "Solicitud", string obs = "") =>
            new() { ColaboradorId = c, Desde = F(d), Hasta = F(h), Dias = dias, Origen = origen, Observacion = obs };
        return
        [
            V(0, "02/01/2025", "16/01/2025", 15, "Importador", "Saldo período 2023-2024"), V(0, "15/07/2025", "29/07/2025", 15), V(0, "22/12/2025", "04/01/2026", 14),
            V(1, "03/02/2025", "02/03/2025", 28, "Importador"), V(1, "01/09/2025", "30/09/2025", 30), V(1, "06/04/2026", "15/04/2026", 10, "Solicitud", "Programadas"),
            V(2, "14/07/2025", "27/07/2025", 14), V(2, "26/12/2025", "04/01/2026", 10),
            V(3, "05/01/2026", "25/01/2026", 21), V(3, "10/03/2026", "19/03/2026", 10, "Solicitud", "Viaje familiar"),
            V(4, "20/10/2025", "02/11/2025", 14),
            V(5, "06/01/2025", "04/02/2025", 30, "Importador"), V(5, "05/01/2026", "19/01/2026", 15),
            V(6, "03/03/2025", "01/04/2025", 30, "Importador"), V(6, "16/02/2026", "07/03/2026", 20),
            V(9, "04/08/2025", "23/08/2025", 20), V(9, "12/03/2026", "18/03/2026", 7, "Registro manual", "Regulariza días de febrero"),
            V(10, "02/02/2026", "08/02/2026", 7),
            V(11, "03/02/2025", "04/03/2025", 30, "Importador"), V(11, "02/02/2026", "16/02/2026", 15),
            V(13, "08/09/2025", "22/09/2025", 15), V(14, "13/10/2025", "19/10/2025", 7), V(15, "04/05/2026", "10/05/2026", 7, "Solicitud", "Programadas"),
        ];
    }

    private static List<Ausencia> Ausencias()
    {
        Ausencia A(int c, string tipo, string d, string h, int dias, string obs) =>
            new() { ColaboradorId = c, Tipo = tipo, Desde = F(d), Hasta = F(h), Dias = dias, Observacion = obs };
        return
        [
            A(0, "Descanso médico", "23/02/2026", "06/03/2026", 12, "CITT EsSalud A-2026-118734"),
            A(7, "Falta injustificada", "09/03/2026", "09/03/2026", 1, "No se presentó al turno de mañana"),
            A(13, "Licencia por paternidad", "02/03/2026", "11/03/2026", 10, "Ley 29409"),
            A(8, "Permiso con goce", "13/03/2026", "13/03/2026", 1, "Trámite notarial"),
            A(4, "Descanso médico", "16/03/2026", "18/03/2026", 3, "Clínica Internacional"),
            A(15, "Permiso sin goce", "20/02/2026", "20/02/2026", 1, "Asunto personal"),
            A(2, "Licencia por fallecimiento", "26/01/2026", "30/01/2026", 5, "Fallecimiento de familiar directo"),
        ];
    }

    private static IEnumerable<LicenciaMedica> Licencias(List<Ausencia> ausencias)
    {
        LicenciaMedica L(string folio, int c, string emisor, string diag, string d, string h, int dias, string estado, string recibida, Ausencia? a = null) =>
            new() { Folio = folio, ColaboradorId = c, Emisor = emisor, Diagnostico = diag, Desde = F(d), Hasta = F(h), Dias = dias, Estado = estado, Recibida = F(recibida), AusenciaId = a?.Id };
        return
        [
            L("A-2026-118734", 0, "EsSalud · Hospital Rebagliati", "Cirugía ambulatoria", "23/02/2026", "06/03/2026", 12, "Validada", "23/02/2026", ausencias[0]),
            L("CI-55120", 4, "Clínica Internacional", "Faringitis aguda", "16/03/2026", "18/03/2026", 3, "Validada", "16/03/2026", ausencias[4]),
            L("A-2026-120455", 1, "EsSalud · Policlínico Chincha", "Lumbalgia", "17/03/2026", "23/03/2026", 7, "Recibida", "16/03/2026"),
            L("CR-80931", 14, "Clínica Ricardo Palma", "Gastroenteritis", "05/03/2026", "06/03/2026", 2, "Observada", "09/03/2026"),
            L("SP-11204", 10, "Consultorio particular", "Migraña", "27/02/2026", "27/02/2026", 1, "Rechazada", "05/03/2026"),
        ];
    }

    private static IEnumerable<SolicitudAsistencia> Solicitudes()
    {
        SolicitudAsistencia S(int c, string tipo, string d, string h, int dias, string motivo, string creada, string estado, string aprobador, string comentario = "") =>
            new() { ColaboradorId = c, Tipo = tipo, Desde = F(d), Hasta = F(h), Dias = dias, Motivo = motivo, Creada = F(creada), Estado = estado, Aprobador = aprobador, Comentario = comentario };
        return
        [
            S(9, "Vacaciones", "23/02/2026", "06/03/2026", 12, "Descanso", "10/02/2026", "Rechazada", "Lucía Cáceres", "Cierre de campaña; reprogramar"),
            S(3, "Vacaciones", "10/03/2026", "19/03/2026", 10, "Viaje familiar", "20/02/2026", "Aprobada", "Rodrigo Paredes", "Coordinado con su jefatura"),
            S(8, "Vacaciones", "20/03/2026", "27/03/2026", 8, "Viaje", "11/03/2026", "Pendiente", ""),
            S(12, "Permiso con goce", "19/03/2026", "19/03/2026", 1, "Sustentación de tesis", "12/03/2026", "Pendiente", ""),
            S(10, "Vacaciones", "13/04/2026", "24/04/2026", 12, "Descanso", "13/03/2026", "Pendiente", ""),
            S(7, "Permiso sin goce", "23/03/2026", "24/03/2026", 2, "Mudanza", "14/03/2026", "Pendiente", ""),
        ];
    }

    private static List<RecargoDomingo> Recargos() =>
    [
        new() { ColaboradorId = 7, Fecha = F("01/03/2026"), Horas = 8, Observacion = "Inventario mensual" },
        new() { ColaboradorId = 1, Fecha = F("01/03/2026"), Horas = 6, DescansoSustitutorio = true, Observacion = "Inventario mensual · descansa el 04/03" },
        new() { ColaboradorId = 7, Fecha = F("08/03/2026"), Horas = 8, Observacion = "Recepción de mercadería" },
        new() { ColaboradorId = 15, Fecha = F("15/03/2026"), Horas = 4, Observacion = "Migración de servidores" },
        new() { ColaboradorId = 9, Fecha = F("15/02/2026"), Horas = 8, Observacion = "Campaña de verano" },
    ];

    private static IEnumerable<MarcaFallida> Fallidas() =>
    [
        new() { ColaboradorId = 7, RecintoId = 2, Fecha = F("12/03/2026"), Hora = "06:58", Tipo = "Entrada", Metodo = "App", Motivo = "Fuera del radio", Distancia = 412 },
        new() { ColaboradorId = 4, RecintoId = 3, Fecha = F("14/03/2026"), Hora = "10:55", Tipo = "Entrada", Metodo = "App", Motivo = "Sin foto" },
        new() { ColaboradorId = 12, RecintoId = 1, Fecha = F("02/03/2026"), Hora = "08:57", Tipo = "Entrada", Metodo = "Web", Motivo = "Sin turno asignado" },
        new() { ColaboradorId = 15, RecintoId = 1, Fecha = F("16/03/2026"), Hora = "08:41", Tipo = "Entrada", Metodo = "App", Motivo = "Fuera del radio", Distancia = 6830 },
        new() { ColaboradorId = 13, RecintoId = 2, Fecha = F("10/03/2026"), Hora = "15:41", Tipo = "Salida", Metodo = "Foto", Motivo = "Método no permitido" },
    ];

    /// <summary>Genera las marcas de ejemplo y las decisiones sobre horas extras ya tomadas.</summary>
    private sealed class Generador(List<Colaborador> cols, List<Recinto> recintos, List<Turno> turnos, List<AsignacionTurno> asignaciones,
        List<CambioTurno> cambios, List<Vacacion> vacaciones, List<Ausencia> ausencias, List<RecargoDomingo> recargos, HashSet<DateOnly> feriados)
    {
        public readonly List<Marca> Marcas = [];
        public readonly List<AprobacionHhee> Aprobaciones = [];

        private static int Min(string h) => int.Parse(h[..2]) * 60 + int.Parse(h[3..]);
        private static string Hora(int m) { m = ((m % 1440) + 1440) % 1440; return $"{m / 60:00}:{m % 60:00}"; }

        private Turno? TurnoDe(int c, DateOnly f)
        {
            var cambio = cambios.FirstOrDefault(x => x.ColaboradorId == c && x.Fecha == f);
            if (cambio is not null) return cambio.TurnoId is { } id ? turnos.First(t => t.Id == id) : null;
            var a = asignaciones.Where(x => x.ColaboradorId == c && x.Desde <= f && (x.Hasta is null || x.Hasta >= f)).MaxBy(x => x.Desde);
            var t = a is null ? null : turnos.First(x => x.Id == a.TurnoId);
            if (t is null) return null;
            if (t.Tipo == "Rotativo" && t.CicloTrabajo is { } tr && t.CicloDescanso is { } de)
            {
                var dias = f.DayNumber - (a!.CicloInicio ?? a.Desde).DayNumber;
                var n = tr + de;
                return ((dias % n) + n) % n < tr ? t : null;
            }
            return t.Dias.Contains((int)f.DayOfWeek) ? t : null;
        }

        public void Generar()
        {
            var noMarca = new HashSet<string> { "10|02/03/2026", "10|09/03/2026", "14|16/03/2026" };
            var sinSalida = new HashSet<string> { "14|13/03/2026", "8|13/03/2026" };
            var sinFoto = new HashSet<string> { "7|05/03/2026", "7|06/03/2026", "4|11/03/2026" };
            var ahora = Min(HoraCorte);
            static string K(int c, DateOnly f) => $"{c}|{f:dd/MM/yyyy}";
            bool Ausente(int c, DateOnly f) => ausencias.Any(a => a.ColaboradorId == c && a.Desde <= f && f <= a.Hasta)
                                              || vacaciones.Any(v => v.ColaboradorId == c && v.Desde <= f && f <= v.Hasta);

            foreach (var c in cols.OrderBy(x => x.Id))
            {
                if (c.RecintoId is not { } recintoId) continue;
                var rc = recintos.First(r => r.Id == recintoId);
                var di = -1;
                for (var f = InicioMarcas; f <= Corte; f = f.AddDays(1))
                {
                    di++;
                    if (f < c.Ingreso || Ausente(c.Id, f) || feriados.Contains(f) || noMarca.Contains(K(c.Id, f))) continue;
                    var r = new Random(c.Id * 7919 + di * 104729 + 17);
                    if (c.Id != 0 && f != Corte && r.NextDouble() < 0.035) continue;
                    var t = TurnoDe(c.Id, f);
                    var dow = (int)f.DayOfWeek;
                    var marzo = f.Month == 3;
                    int ent, sal, fin = 0;
                    if (t is null)
                    {
                        var rec = recargos.FirstOrDefault(x => x.ColaboradorId == c.Id && x.Fecha == f);
                        if (rec is null) continue;
                        ent = 8 * 60 - 5 + r.Next(10);
                        sal = ent + (int)(rec.Horas * 60) + r.Next(8);
                    }
                    else
                    {
                        var ini = Min(t.Entrada);
                        fin = Min(t.Salida); if (fin <= ini) fin += 1440;
                        ent = t.Tipo == "Flexible" ? ini - 30 + r.Next(50) : ini - 14 + r.Next(15);
                        if (t.Tipo != "Flexible" && r.NextDouble() < 0.07) ent = ini + t.Tolerancia + 3 + r.Next(22);
                        if (c.Id == 9 && f.Month == 3 && f.Day is 3 or 5 or 12) ent = ini + 22 + r.Next(8);
                        var largo = fin - ini;
                        sal = (t.Tipo == "Flexible" ? ent + largo : fin) + r.Next(9);
                        if (t.Tipo == "Flexible" && ent >= ini - 60 && ent <= ini + t.Tolerancia) fin = ent + largo;
                        if (r.NextDouble() < 0.04) sal -= 20 + r.Next(30);
                        // Patrones de horas extras.
                        if (c.Id == 7 && dow is 2 or 4) sal += marzo ? 100 + r.Next(50) : 70 + r.Next(30);
                        if (c.Id == 1 && dow == 5) sal += 60 + r.Next(25);
                        if (c.Id == 13 && dow == 3) sal += 45 + r.Next(20);
                        if (c.Id == 3 && dow == 3) sal += 60 + r.Next(35);
                        if (c.Id == 9 && dow == 6) sal += 50 + r.Next(15);
                        if (c.Id == 2 && f == new DateOnly(2026, 3, 13)) sal += 75;
                    }
                    var metodo = c.Id == 13 ? "QR" : c.UsaApp ? "App" : "Web";
                    var conFoto = metodo != "Web" && !sinFoto.Contains(K(c.Id, f));
                    Marca Nueva(string tipo, int min)
                    {
                        var (lat, lng) = metodo == "Web" ? (rc.Lat, rc.Lng)
                            : (rc.Lat + (r.NextDouble() - 0.5) * 2 * rc.Radio * 0.7 / 111_000, rc.Lng + (r.NextDouble() - 0.5) * 2 * rc.Radio * 0.7 / 108_000);
                        return new Marca { ColaboradorId = c.Id, RecintoId = rc.Id, Fecha = f, Hora = Hora(min), Tipo = tipo, Metodo = metodo, Lat = lat, Lng = lng, Foto = conFoto };
                    }
                    if (f == Corte) { if (ent <= ahora) Marcas.Add(Nueva("Entrada", ent)); continue; }
                    Marcas.Add(Nueva("Entrada", ent));
                    if (sinSalida.Contains(K(c.Id, f))) continue;
                    Marcas.Add(Nueva("Salida", sal));

                    // Horas extras ya revisadas (como en el frontend: exceso de 30 minutos o más después del turno).
                    var descanso = t is null;
                    var exceso = descanso ? sal - ent : sal - fin;
                    if (exceso >= 30) Decidir(c.Id, f, exceso, descanso);
                }
            }

            // Marcas manuales de RR.HH. (reemplazan la marca generada de ese día y tipo).
            (int c, string f, string h, string tipo, string por, string motivo)[] manuales =
            [
                (6, "11/03/2026", "18:05", "Salida", Lucia, "Olvidó marcar la salida"),
                (11, "04/03/2026", "08:20", "Entrada", Lucia, "Reunión externa a primera hora"),
                (13, "10/03/2026", "15:40", "Salida", "Diego Benavides", "Marca con foto rechazada (método no permitido)"),
            ];
            foreach (var m in manuales)
            {
                var f = F(m.f);
                var rc = recintos.First(r => r.Id == cols.First(x => x.Id == m.c).RecintoId);
                Marcas.RemoveAll(x => x.ColaboradorId == m.c && x.Fecha == f && x.Tipo == m.tipo);
                Marcas.Add(new Marca { ColaboradorId = m.c, RecintoId = rc.Id, Fecha = f, Hora = m.h, Tipo = m.tipo, Metodo = "Manual", Lat = rc.Lat, Lng = rc.Lng, CreadoPor = m.por, Motivo = m.motivo });
            }
        }

        private static List<LineaHhee> Sugeridas(int minutos, bool descanso)
        {
            if (descanso) return [new() { Minutos = minutos, Tipo = 100 }];
            var a = Math.Min(120, minutos);
            return minutos > a ? [new() { Minutos = a, Tipo = 25 }, new() { Minutos = minutos - a, Tipo = 35 }] : [new() { Minutos = a, Tipo = 25 }];
        }

        private void Decidir(int c, DateOnly f, int minutos, bool descanso)
        {
            AprobacionHhee A(string estado, List<LineaHhee> lineas, string obs, string por, bool enviado) =>
                new() { ColaboradorId = c, Fecha = f, Estado = estado, Lineas = lineas, Observacion = obs, AprobadoPor = por, Enviado = enviado };
            var recargo = recargos.FirstOrDefault(r => r.ColaboradorId == c && r.Fecha == f);
            if (recargo is not null)
                Aprobaciones.Add(recargo.DescansoSustitutorio
                    ? A("Rechazado", [], "Se otorgó descanso sustitutorio", Lucia, false)
                    : A("Aprobado", Sugeridas(minutos, descanso), "Registrado en Recargo Domingo", Lucia, true));
            else if (f.Month == 2) Aprobaciones.Add(A("Aprobado", Sugeridas(minutos, descanso), "", Lucia, true));
            else if (f <= new DateOnly(2026, 3, 6))
            {
                if (c == 13) Aprobaciones.Add(A("Rechazado", [], "No fue solicitada por la jefatura", "Diego Benavides", false));
                else if (c == 3) Aprobaciones.Add(A("Aprobado parcial", [new() { Minutos = 60, Tipo = 25 }], "Se aprueba 1 hora; el resto no fue coordinado", Lucia, false));
                else Aprobaciones.Add(A("Aprobado", Sugeridas(minutos, descanso), "", Lucia, false));
            }
        }
    }
}
