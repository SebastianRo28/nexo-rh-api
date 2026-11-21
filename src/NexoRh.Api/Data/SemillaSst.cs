using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Data;

/// <summary>
/// Cumplimientos, requisitos, asignaciones y evidencias de SST de ejemplo. Las evidencias se generan de forma
/// determinista para que haya de todo al 16/03/2026: vigentes, por vencer, vencidas y sin evidencia.
/// </summary>
public static class SemillaSst
{
    private static DateOnly F(string s) => DateOnly.ParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture);
    public const string Empresa = "Comercial Andina S.A.C.";

    /// <summary>Biblioteca de cumplimientos frecuentes (Perú).</summary>
    public static readonly (string Nombre, string Normativa, string Categoria)[] Biblioteca =
    [
        ("Comité de SST", "Ley N° 29783", "General"), ("Registro de enfermedades ocupacionales", "R.M. N° 050-2013-TR", "Incidentes"),
        ("Mapa de riesgos", "D.S. N° 005-2012-TR", "Gestión de Riesgos"), ("Investigación de accidentes", "D.S. N° 005-2012-TR", "Incidentes"),
        ("Capacitaciones trimestrales en SST", "Ley N° 29783", "Capacitaciones"), ("Inventario de equipos de emergencia", "D.S. N° 005-2012-TR", "Activos"),
        ("Monitoreo de agentes físicos", "D.S. N° 005-2012-TR", "Gestión de Riesgos"), ("Auditoría del sistema de gestión", "ISO 45001", "General"),
        ("Política de SST firmada", "Ley N° 29783", "General"), ("Exámenes médicos de retiro", "D.S. N° 005-2012-TR", "Gestión de Riesgos"),
    ];

    public static async Task CargarAsync(NexoDbContext db)
    {
        if (await db.CumplimientosSst.AnyAsync() || !await db.Colaboradores.AnyAsync()) return;
        CumplimientoSst C(string n, string norma, string cat) => new() { Nombre = n, Normativa = norma, Categoria = cat, Descripcion = $"Obligaciones de {n.ToLowerInvariant()} según la normativa vigente." };
        var cumplimientos = new[]
        {
            C("Entrega de EPP", "Ley N° 29783", "Activos · Gestión de Riesgos"), C("Vigilancia de la salud", "D.S. N° 005-2012-TR", "Gestión de Riesgos"),
            C("Capacitaciones obligatorias", "Ley N° 29783", "Capacitaciones"), C("Programa anual de SST", "D.S. N° 005-2012-TR", "General"),
            C("Gestión de riesgos", "ISO 45001", "Gestión de Riesgos"), C("Sistema de gestión de SST", "Ley N° 29783", "General"),
            C("Preparación ante emergencias", "D.S. N° 005-2012-TR", "Incidentes"), C("Registros obligatorios", "R.M. N° 050-2013-TR", "Incidentes · General"),
            C("Seguros obligatorios", "Ley N° 26790", "General"),
        };
        db.CumplimientosSst.AddRange(cumplimientos);
        await db.SaveChangesAsync();
        int Cu(string n) => cumplimientos.First(c => c.Nombre == n).Id;

        RequisitoSst R(string n, string cu, string aplica, int? meses) => new() { Nombre = n, CumplimientoId = Cu(cu), AplicaA = aplica, VigenciaMeses = meses };
        var requisitos = new[]
        {
            R("Chaleco de seguridad", "Entrega de EPP", "Colaboradores", 12), R("Casco de seguridad", "Entrega de EPP", "Colaboradores", 24),
            R("Zapatos de seguridad", "Entrega de EPP", "Colaboradores", 12), R("Examen médico ocupacional", "Vigilancia de la salud", "Colaboradores", 24),
            R("Inducción en SST", "Capacitaciones obligatorias", "Colaboradores", null), R("Capacitación en trabajos en altura", "Capacitaciones obligatorias", "Colaboradores", 12),
            R("SCTR vigente", "Seguros obligatorios", "Colaboradores", 12),
            R("Plan anual de capacitaciones", "Programa anual de SST", "Empresas", 12), R("IPERC actualizado", "Gestión de riesgos", "Empresas", 12),
            R("Reglamento interno de SST", "Sistema de gestión de SST", "Empresas", 24), R("Simulacro de evacuación", "Preparación ante emergencias", "Empresas", 6),
            R("Registro de accidentes de trabajo", "Registros obligatorios", "Empresas", null),
        };
        db.RequisitosSst.AddRange(requisitos);
        await db.SaveChangesAsync();

        var cols = await db.Colaboradores.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        var todos = cols.Select(c => c.Id).ToList();
        var operaciones = cols.Where(c => c.Division == "Operaciones" || c.RecintoId == 2).Select(c => c.Id).ToList();
        var asignaciones = new List<(AsignacionSst a, RequisitoSst r)>();
        void Asignar(RequisitoSst r, IEnumerable<int>? ids = null, IEnumerable<string>? empresas = null)
        {
            foreach (var id in ids ?? []) asignaciones.Add((new AsignacionSst { RequisitoId = r.Id, ColaboradorId = id }, r));
            foreach (var e in empresas ?? []) asignaciones.Add((new AsignacionSst { RequisitoId = r.Id, Empresa = e }, r));
        }
        var req = requisitos.ToDictionary(r => r.Nombre);
        foreach (var n in new[] { "Chaleco de seguridad", "Casco de seguridad", "Zapatos de seguridad" }) Asignar(req[n], todos.Where(i => i is not (5 or 12)));
        Asignar(req["Examen médico ocupacional"], todos);
        Asignar(req["Inducción en SST"], todos);
        Asignar(req["SCTR vigente"], todos);
        Asignar(req["Capacitación en trabajos en altura"], operaciones);
        foreach (var n in new[] { "Plan anual de capacitaciones", "IPERC actualizado", "Reglamento interno de SST", "Registro de accidentes de trabajo" }) Asignar(req[n], empresas: [Empresa]);
        Asignar(req["Simulacro de evacuación"], empresas: [Empresa, "Andina Logística S.A.C."]);
        db.AsignacionesSst.AddRange(asignaciones.Select(x => x.a));
        await db.SaveChangesAsync();

        // Evidencias: la mayoría vigentes; algunas por vencer, vencidas o sin evidencia.
        var hoy = F("16/03/2026");
        foreach (var (a, r) in asignaciones)
        {
            var rnd = new Random(a.Id * 7 + r.Id * 101);
            var p = rnd.NextDouble();
            if (p < 0.08 && r.Nombre != "Inducción en SST") continue;                          // sin evidencia
            DateOnly fecha;
            if (r.VigenciaMeses is not { } m) fecha = hoy.AddDays(-rnd.Next(30, 700));
            else if (p < 0.16) fecha = hoy.AddMonths(-m).AddDays(-rnd.Next(5, 60));              // vencida
            else if (p < 0.26) fecha = hoy.AddMonths(-m).AddDays(rnd.Next(3, 28));               // por vencer
            else fecha = hoy.AddMonths(-rnd.Next(0, m - 1 < 1 ? 1 : m - 1)).AddDays(-rnd.Next(0, 25));
            db.EvidenciasSst.Add(new EvidenciaSst
            {
                AsignacionId = a.Id, Fecha = fecha, Vence = r.VigenciaMeses is { } v ? fecha.AddMonths(v) : null,
                Descripcion = r.AplicaA == "Empresas" ? $"{r.Nombre} {fecha.Year}" : $"Acta de {r.Nombre.ToLowerInvariant()}", RegistradaPor = "Pérez Soto, Luis",
                RegistradaEn = fecha.ToDateTime(new TimeOnly(15, 0)),
            });
        }
        await db.SaveChangesAsync();
    }
}
