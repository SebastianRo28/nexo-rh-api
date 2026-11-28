using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Data;

/// <summary>Encuestas (con respuestas generadas de forma determinista), denuncias y publicaciones de ejemplo.</summary>
public static class SemillaComunicacion
{
    private static DateOnly F(string s) => DateOnly.ParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static async Task CargarAsync(NexoDbContext db)
    {
        if (!await db.Colaboradores.AnyAsync()) return;
        var cols = await db.Colaboradores.AsNoTracking().OrderBy(c => c.Id).ToListAsync();

        if (!await db.Encuestas.AnyAsync())
        {
            Encuesta E(string nombre, string tipo, bool anonima, string inicio, string termino, string creada, string plantilla, List<int>? dest = null, string desc = "") => new()
            {
                Nombre = nombre, Tipo = tipo, Anonima = anonima, Inicio = F(inicio), Termino = F(termino), Creada = F(creada), CreadaPor = "Lucía Cáceres",
                Preguntas = PlantillasEncuesta.Preguntas(plantilla), Destinatarios = dest ?? [], Descripcion = desc,
            };
            var hasta2025 = cols.Where(c => c.Ingreso <= F("01/10/2025")).Select(c => c.Id).ToList();
            var operaciones = cols.Where(c => c.Division is "Operaciones" or "Comercial").Select(c => c.Id).ToList();
            var encuestas = new (Encuesta e, int[] responden)[]
            {
                (E("Clima laboral 2025", "Clima", true, "06/10/2025", "31/10/2025", "01/10/2025", "Clima laboral", hasta2025), hasta2025.Where(i => i != 10).ToArray()),
                (E("Estrés laboral", "Libre", true, "05/12/2025", "19/12/2025", "02/12/2025", "Estrés laboral", hasta2025), hasta2025.Where(i => i is not (4 or 9 or 14)).ToArray()),
                (E("Buzón de sugerencias 2026", "Libre", false, "06/01/2026", "31/12/2026", "05/01/2026", "Buzón de sugerencias", null, "Comparte ideas para mejorar procesos, beneficios o el día a día."), [1, 3, 5, 6, 8, 11, 13]),
                (E("Salida voluntaria", "Libre", false, "01/01/2026", "31/12/2026", "01/01/2026", "Salida voluntaria", [7]), [7]),
                (E("Percepción de seguridad laboral", "Libre", true, "05/02/2026", "20/02/2026", "01/02/2026", "Percepción de seguridad laboral", [0, 1, 2, 5, 6, 7, 11, 13, 14], "Encuesta de SST de febrero."), [0, 1, 2, 5, 6, 7, 11, 13]),
                (E("Evaluación de la inducción", "Libre", true, "01/03/2026", "31/03/2026", "20/02/2026", "Evaluación de la inducción", [8, 10, 12, 15]), [8, 10, 15]),
                (E("Clima organizacional 2026", "Clima", true, "09/03/2026", "31/03/2026", "02/03/2026", "Clima organizacional by Nexo", null, "Cuéntanos cómo te sientes trabajando en Comercial Andina. Toma 3 minutos."), cols.Select(c => c.Id).Where(i => i is not (0 or 4 or 9)).ToArray()),
                (E("Valoración de beneficios", "Libre", false, "23/03/2026", "06/04/2026", "10/03/2026", "Valoración de beneficios"), []),
                (E("Pulso de clima · Operaciones", "Clima", true, "01/04/2026", "15/04/2026", "15/03/2026", "Clima laboral", operaciones), []),
            };
            db.Encuestas.AddRange(encuestas.Select(x => x.e));
            await db.SaveChangesAsync();

            foreach (var (e, responden) in encuestas)
            {
                foreach (var c in responden)
                {
                    var r = new Random(e.Id * 1009 + c * 31);
                    db.ParticipacionesEncuesta.Add(new ParticipacionEncuesta { EncuestaId = e.Id, ColaboradorId = c, Fecha = e.Inicio.AddDays(r.Next(Math.Max(1, Math.Min(10, e.Termino.DayNumber - e.Inicio.DayNumber)))) });
                    db.RespuestasEncuesta.Add(new RespuestaEncuesta { EncuestaId = e.Id, ColaboradorId = e.Anonima ? null : c, Valores = PlantillasEncuesta.Responder(e.Preguntas, r) });
                }
            }
            await db.SaveChangesAsync();
        }

        if (!await db.Denuncias.AnyAsync())
        {
            Denuncia D(string codigo, string tipo, string relacion, string creada, string estado, string actualizada, string descripcion, params (string autor, string texto, string fecha, bool leido)[] mensajes) => new()
            {
                Codigo = codigo, Tipo = tipo, Relacion = relacion, Estado = estado, Descripcion = descripcion, Anonima = true,
                CreadaEn = F(creada).ToDateTime(new TimeOnly(15, 0)), ActualizadaEn = F(actualizada).ToDateTime(new TimeOnly(15, 0)),
                Resolucion = estado switch { "Finalizada" => "Se redistribuyeron las tareas del área y se reforzó el equipo.", "Descartada" => "No se encontraron indicios; información insuficiente.", _ => "" },
                Mensajes = mensajes.Select(m => new MensajeDenuncia { Autor = m.autor, Texto = m.texto, Fecha = F(m.fecha).ToDateTime(new TimeOnly(16, 0)), Leido = m.leido }).ToList(),
            };
            db.Denuncias.AddRange(
                D("774102938465", "Información confidencial", "Otro", "05/12/2025", "Descartada", "10/12/2025", "Se habría compartido información de precios con terceros por correo personal."),
                D("109283746501", "Carga de trabajo", "Colaborador", "12/01/2026", "Finalizada", "20/02/2026", "El equipo trabaja de forma sostenida más de 11 horas diarias sin registro de horas extras.",
                    ("Comité", "Recibimos tu denuncia. Revisaremos los registros de asistencia del área.", "14/01/2026", true)),
                D("208374615920", "Hurto o robo", "Contratista", "04/03/2026", "Nueva", "04/03/2026", "Faltan herramientas del almacén después del turno de limpieza contratado."),
                D("512903847261", "Conflicto de intereses", "Proveedor", "11/03/2026", "En curso", "15/03/2026", "Un evaluador de compras sería familiar de uno de los proveedores adjudicados.",
                    ("Comité", "Gracias por la información. ¿Puedes indicar el nombre del proceso de compra?", "15/03/2026", true)),
                D("350197411370", "Acoso laboral", "Colaborador", "13/03/2026", "En curso", "15/03/2026", "Comentarios ofensivos reiterados de una jefatura hacia el equipo en reuniones semanales.",
                    ("Comité", "Recibimos tu denuncia y abrimos la investigación. Tu identidad está protegida.", "14/03/2026", true),
                    ("Denunciante", "Ayer volvió a ocurrir en la reunión del lunes. Hubo dos testigos.", "15/03/2026", false)),
                D("406042314536", "Discriminación", "Colaborador", "15/03/2026", "Nueva", "15/03/2026", "Se excluye a una compañera de las capacitaciones por su edad.",
                    ("Denunciante", "Adjunto la lista de convocados donde no figura.", "15/03/2026", false),
                    ("Denunciante", "¿Me pueden confirmar que la recibieron?", "16/03/2026", false)));
            await db.SaveChangesAsync();
        }

        if (!await db.Publicaciones.AnyAsync())
        {
            Publicacion P(string titulo, string texto, string? publicar, string? expira, bool fijada, int vistas, string autor = "Gestión de Personas") => new()
            {
                Titulo = titulo, Texto = texto, PublicarEl = publicar is null ? null : F(publicar), Expira = expira is null ? null : F(expira), Fijada = fijada,
                Vistas = vistas, Autor = autor,
            };
            db.Publicaciones.AddRange(
                P("Nuevos beneficios de salud", "Desde febrero el plan de salud incluye atención dental y oftalmológica sin costo adicional para titulares.", "15/02/2026", null, false, 14),
                P("Bienvenidos a Nexo RH", "¡Bienvenidos a Nexo RH! Desde este mes tus boletas de pago, solicitudes de vacaciones y documentos estarán disponibles en el portal. Si tienes dudas, escríbenos por el chat de Servicio al Colaborador.", "02/03/2026", null, true, 16),
                P("Recordatorio: cierre de la sede el 13/03", "Recordatorio: el viernes 13/03/2026 la sede central cerrará a las 3:00 p. m. por mantenimiento eléctrico. El trabajo remoto está habilitado para todas las áreas administrativas.", "09/03/2026", "14/03/2026", false, 15, "Comunicaciones Internas"),
                P("Campaña de vacunación 2026", "Del 20 de marzo al 20 de abril tendremos vacunación contra la influenza en todas las sedes. Inscríbete con tu jefatura.", "20/03/2026", "20/04/2026", false, 0),
                P("Resultados de la encuesta de clima 2025", "Compartimos los principales resultados y el plan de acción por área.", null, null, false, 0),
                P("Feriados de abril", "Jueves y Viernes Santo (02 y 03/04) son feriados nacionales.", null, null, false, 0));
            await db.SaveChangesAsync();
        }

        if (!await db.ReaccionesMuro.AnyAsync() && !await db.ComentariosMuro.AnyAsync())
        {
            await InteraccionesAsync(db, cols);
        }
    }

    /// <summary>Comentarios de ejemplo del muro (se reparten en orden entre los elementos).</summary>
    private static readonly string[] TextosComentario =
    [
        "¡Excelente noticia! Gracias por la información.", "Muy útil, ya revisé mis boletas en el portal.", "¡Felicitaciones, muy merecido!",
        "Gracias por compartirlo.", "¿Desde cuándo aplica? Me interesa inscribirme.", "¡Así se hace, equipo!", "Súper práctico tener todo en un solo lugar.",
        "Bien merecido, siempre dispuesto a ayudar.", "Gracias por el aviso.", "¡Qué buena iniciativa!",
    ];

    /// <summary>
    /// Reacciones y comentarios de ejemplo de colaboradores (sin usuario) en las publicaciones vigentes y los reconocimientos,
    /// para que el muro de la empresa de ejemplo se vea con actividad. Determinista.
    /// </summary>
    private static async Task InteraccionesAsync(NexoDbContext db, List<Colaborador> cols)
    {
        var pubs = await db.Publicaciones.AsNoTracking().Where(p => p.PublicarEl != null).OrderBy(p => p.Id).ToListAsync();
        var recs = await db.Reconocimientos.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        // Cantidad de reacciones y comentarios por elemento: más en la publicación fijada.
        var elementos = pubs.Select(p => (e: ElementosMuro.Publicacion, id: p.Id, r: p.Fijada ? 39 : p.Vistas > 0 ? 6 + p.Id % 13 : 0, c: p.Fijada ? 5 : p.Vistas > 0 ? p.Id % 3 : 0))
            .Concat(recs.Select(r => (e: ElementosMuro.Reconocimiento, id: r.Id, r: 3 + r.Id * 5 % 11, c: r.Id % 3)))
            .ToList();
        var k = 0;
        foreach (var (e, id, nr, nc) in elementos)
        {
            for (var i = 0; i < Math.Min(nr, cols.Count); i++)
            {
                var c = cols[(id * 7 + i) % cols.Count];
                if (db.ReaccionesMuro.Local.Any(x => x.Elemento == e && x.ElementoId == id && x.ColaboradorId == c.Id)) continue;
                db.ReaccionesMuro.Add(new ReaccionMuro { Elemento = e, ElementoId = id, ColaboradorId = c.Id, Tipo = (i % 6) switch { 2 => "love", 4 => "smile", _ => "like" } });
            }
            for (var i = 0; i < Math.Min(nc, cols.Count); i++, k++)
            {
                var c = cols[(id * 3 + i * 5 + 1) % cols.Count];
                db.ComentariosMuro.Add(new ComentarioMuro
                {
                    Elemento = e, ElementoId = id, ColaboradorId = c.Id, Autor = $"{c.Nombres} {c.Apellidos}", Texto = TextosComentario[k % TextosComentario.Length],
                    Fecha = DateTime.UtcNow.AddHours(-(nc - i) * 3),
                });
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Para la migración MuroInteracciones: pasa los contadores fijos que tenían las publicaciones a reacciones y comentarios
    /// de colaboradores, así las empresas existentes no pierden la actividad que veían en su muro.
    /// </summary>
    public const string SqlMigrarContadores = """
        INSERT INTO ReaccionesMuro (TenantId, Elemento, ElementoId, UsuarioId, ColaboradorId, Tipo, Fecha)
        SELECT p.TenantId, 'pub', p.Id, NULL, c.Id, CASE c.Orden % 6 WHEN 2 THEN 'love' WHEN 4 THEN 'smile' ELSE 'like' END, SYSUTCDATETIME()
        FROM Publicaciones p
        CROSS APPLY (SELECT TOP (p.Reacciones) x.Id, ROW_NUMBER() OVER (ORDER BY x.Id) AS Orden FROM Colaboradores x WHERE x.TenantId = p.TenantId ORDER BY x.Id) c
        WHERE p.Reacciones > 0;

        INSERT INTO ComentariosMuro (TenantId, Elemento, ElementoId, UsuarioId, ColaboradorId, Autor, Texto, Fecha)
        SELECT p.TenantId, 'pub', p.Id, NULL, c.Id, LEFT(c.Nombres + ' ' + c.Apellidos, 170),
            CASE (c.Orden + p.Id) % 6 WHEN 0 THEN N'¡Excelente noticia! Gracias por la información.' WHEN 1 THEN N'Muy útil, ya revisé mis boletas en el portal.'
                WHEN 2 THEN N'Gracias por compartirlo.' WHEN 3 THEN N'Súper práctico tener todo en un solo lugar.' WHEN 4 THEN N'Gracias por el aviso.'
                ELSE N'¡Qué buena iniciativa!' END,
            DATEADD(HOUR, -3 * (p.Comentarios - c.Orden + 1), SYSUTCDATETIME())
        FROM Publicaciones p
        CROSS APPLY (SELECT TOP (p.Comentarios) x.Id, x.Nombres, x.Apellidos, ROW_NUMBER() OVER (ORDER BY x.Id DESC) AS Orden FROM Colaboradores x WHERE x.TenantId = p.TenantId ORDER BY x.Id DESC) c
        WHERE p.Comentarios > 0;
        """;
}

/// <summary>Preguntas de las plantillas de encuesta (las mismas opciones que ofrece el frontend).</summary>
public static class PlantillasEncuesta
{
    public static readonly string[] Libres = ["En blanco", "Onboarding", "Salida voluntaria", "Evaluación del Área", "Percepción de seguridad laboral",
        "Buzón de sugerencias", "Valoración de beneficios", "Estrés laboral", "Evaluación de la inducción", "Teletrabajo"];
    public static readonly string[] Clima = ["Clima organizacional by Nexo", "Clima laboral", "En blanco"];
    private static readonly List<string> Escala = ["1", "2", "3", "4", "5"];

    private static PreguntaEncuesta Esc(string id, string texto) => new() { Codigo = id, Texto = texto, Tipo = "escala", Opciones = [.. Escala], Obligatoria = true };
    private static PreguntaEncuesta Op(string id, string texto, params string[] op) => new() { Codigo = id, Texto = texto, Tipo = "opcion", Opciones = [.. op], Obligatoria = true };
    private static PreguntaEncuesta Tx(string id, string texto, bool obligatoria = false) => new() { Codigo = id, Texto = texto, Tipo = "texto", Obligatoria = obligatoria };

    public static List<PreguntaEncuesta> Preguntas(string plantilla) => plantilla switch
    {
        "Clima organizacional by Nexo" or "Clima laboral" =>
        [
            Esc("c1", "Me siento orgulloso(a) de trabajar aquí."), Esc("c2", "Mi jefatura me da retroalimentación útil."),
            Esc("c3", "Tengo las herramientas para hacer bien mi trabajo."), Op("c4", "¿Recomendarías la empresa como lugar de trabajo?", "Sí", "Tal vez", "No"),
            Tx("c5", "¿Qué mejorarías?"),
        ],
        "Buzón de sugerencias" => [Op("s1", "Tema de tu sugerencia", "Procesos", "Beneficios", "Infraestructura", "Otro"), Tx("s2", "Tu sugerencia", true)],
        "Percepción de seguridad laboral" => [Esc("p1", "Conozco las rutas de evacuación de mi sede."), Esc("p2", "Cuento con el equipo de protección que necesito."), Tx("p3", "¿Qué riesgo te preocupa?")],
        "Salida voluntaria" => [Op("v1", "Motivo principal de tu salida", "Remuneración", "Desarrollo", "Clima", "Personal", "Otro"), Esc("v2", "¿Cómo calificas tu experiencia en la empresa?"), Tx("v3", "Comentarios")],
        "En blanco" => [Tx("b1", "Comentarios")],
        _ => [Esc("g1", $"En general, ¿cómo calificas: {plantilla.ToLowerInvariant()}?"), Op("g2", "¿Cumplió tus expectativas?", "Sí", "En parte", "No"), Tx("g3", "¿Qué mejorarías?")],
    };

    private static readonly string[] Comentarios = ["Más capacitaciones.", "Mejorar la comunicación entre áreas.", "Flexibilidad de horario.", "", "Todo bien por ahora."];

    /// <summary>Respuestas plausibles (deterministas) para los datos de ejemplo.</summary>
    public static Dictionary<string, string> Responder(List<PreguntaEncuesta> preguntas, Random r)
    {
        var v = new Dictionary<string, string>();
        foreach (var p in preguntas)
        {
            var x = p.Tipo switch
            {
                "escala" => (3 + r.Next(3) - (r.NextDouble() < 0.15 ? 2 : 0)).ToString(CultureInfo.InvariantCulture),
                "opcion" => p.Opciones[r.NextDouble() < 0.6 ? 0 : r.Next(p.Opciones.Count)],
                _ => Comentarios[r.Next(Comentarios.Length)],
            };
            if (x.Length > 0) v[p.Codigo] = x;
        }
        return v;
    }
}
