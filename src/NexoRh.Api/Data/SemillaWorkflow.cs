using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Data;

/// <summary>Plantillas, solicitudes de workflow y reconocimientos de ejemplo (los que mostraba el frontend).</summary>
public static class SemillaWorkflow
{
    private static DateOnly F(string s) => DateOnly.ParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static async Task CargarAsync(NexoDbContext db)
    {
        if (!await db.Colaboradores.AnyAsync()) return;
        if (!await db.PlantillasWorkflow.AnyAsync())
        {
            PlantillaWorkflow P(int id, string nombre, string tipo, string estado, int ops, string fecha) =>
                new() { Id = id, Nombre = nombre, Tipo = tipo, Estado = estado, Operaciones = ops, ActualizadaEn = F(fecha).ToDateTime(new TimeOnly(14, 0)), ModificadoPor = "Rodrigo Paredes" };
            db.PlantillasWorkflow.AddRange(
                P(716, "Alta simple", "Alta", "Activa", 4, "28/02/2026"), P(715, "Alta con documentos y cursos", "Alta", "Activa", 7, "27/02/2026"),
                P(714, "Solicitud bono", "Flexible", "Activa", 3, "26/02/2026"), P(713, "Solicitud de Pre Ingreso", "Pre-Ingreso", "Activa", 5, "25/02/2026"),
                P(712, "Cambio de cargo", "Movimiento", "Activa", 4, "24/02/2026"), P(711, "Cambio de sueldo", "Movimiento", "Incompleta", 2, "23/02/2026"),
                P(710, "Baja voluntaria", "Baja", "Activa", 6, "22/02/2026"), P(709, "Baja por término de contrato", "Baja", "Inactiva", 5, "21/02/2026"),
                P(708, "Solicitud de activo", "Flexible", "Activa", 3, "20/02/2026"), P(707, "Generar contrato al crear trabajo", "Alta", "Activa", 2, "19/02/2026"),
                P(706, "Generar anexo al cambiar trabajo", "Movimiento", "Error", 2, "18/02/2026"), P(705, "Generar acta al terminar trabajo", "Baja", "Activa", 2, "17/02/2026"),
                P(704, "Constancia de trabajo", "Documento", "Activa", 2, "16/02/2026"), P(703, "Adelanto de sueldo", "Flexible", "Activa", 3, "16/02/2026"));
            await db.SaveChangesAsync();
        }

        if (!await db.SolicitudesWorkflow.AnyAsync())
        {
            var cols = await db.Colaboradores.AsNoTracking().ToListAsync();
            var usuarios = await db.Usuarios.AsNoTracking().ToListAsync();
            var plantillas = await db.PlantillasWorkflow.AsNoTracking().ToListAsync();
            SolicitudWorkflow S(string titulo, string estado, string tipo, string solicitante, string afectado, string doc, int progreso, string creada, string actualizada,
                string? plantilla = null, string? proceso = null, string comentario = "")
            {
                var colSol = cols.FirstOrDefault(c => $"{c.Apellidos}, {c.Nombres}" == solicitante);
                var usr = usuarios.FirstOrDefault(u => (colSol != null && u.ColaboradorId == colSol.Id) || u.Nombre == solicitante);
                var colAf = cols.FirstOrDefault(c => $"{c.Apellidos}, {c.Nombres}" == afectado);
                return new SolicitudWorkflow
                {
                    Titulo = titulo, Estado = estado, Tipo = tipo, Solicitante = solicitante, SolicitanteUsuarioId = usr?.Id, AfectadoId = colAf?.Id, Afectado = afectado,
                    Documento = doc, Progreso = progreso, Creada = F(creada), Actualizada = F(actualizada), Comentario = comentario, Proceso = proceso,
                    PlantillaId = plantilla is null ? null : plantillas.FirstOrDefault(p => p.Nombre == plantilla)?.Id,
                    ResueltaPor = estado == "Pendiente" ? "" : "Rodrigo Paredes",
                };
            }
            // En orden de creación (el número de solicitud es correlativo).
            db.SolicitudesWorkflow.AddRange(new[]
            {
                S("Cambio de cargo – Analista Finanzas", "Aprobada", "Movimiento", "Navarro Ríos, Gonzalo", "Alamos Ruiz, Florencia", "72458193", 100, "15/12/2024", "01/01/2025", "Cambio de cargo"),
                S("Solicitud de activo – Laptop", "Aprobada", "Flexible", "Herrera Castillo, Valeria", "Herrera Castillo, Valeria", "74120369", 100, "07/02/2025", "10/02/2025", null, "activo"),
                S("Cambio de sueldo – Supervisor de Ventas", "Rechazada", "Movimiento", "Huertas León, Paolo", "Huertas León, Paolo", "47896521", 100, "02/02/2026", "05/02/2026", "Cambio de sueldo", null, "Fuera del ciclo de revisión salarial"),
                S("Alta simple – Ortiz Ramírez", "Aprobada", "Alta", "Rodrigo Paredes", "Ortiz Ramírez, Daniela", "75236981", 100, "24/02/2026", "01/03/2026", "Alta simple"),
                S("Baja voluntaria", "Pendiente", "Baja", "Benavides Rojas, Diego", "Gutiérrez Paredes, Javier", "70458812", 66, "05/03/2026", "08/03/2026", "Baja voluntaria"),
                S("Solicitud de Pre Ingreso", "Pendiente", "Pre-Ingreso", "Rodrigo Paredes", "Salas Cornejo, Mateo", "76321458", 20, "09/03/2026", "09/03/2026", "Solicitud de Pre Ingreso"),
                S("Solicitud bono – cierre de planillas", "Pendiente", "Flexible", "Cáceres Huamán, Lucía", "Chávez Quispe, Renato", "72011548", 50, "10/03/2026", "11/03/2026", "Solicitud bono"),
                S("Aporte Patronal", "Pendiente", "Flexible", "Navarro Ríos, Gonzalo", "Alamos Ruiz, Florencia", "72458193", 33, "12/03/2026", "12/03/2026", "Solicitud bono"),
                S("Constancia de trabajo – Entidad bancaria", "Pendiente", "Documento", "Medina Torres, Sofía", "Medina Torres, Sofía", "72589634", 0, "13/03/2026", "13/03/2026", null, "constancia"),
            }.Select(s => { s.Detalle = s.Titulo.Contains(" – ") ? s.Titulo[(s.Titulo.IndexOf(" – ") + 3)..] : ""; return s; }));
            await db.SaveChangesAsync();
        }

        if (!await db.Reconocimientos.AnyAsync())
        {
            var cols = await db.Colaboradores.AsNoTracking().ToListAsync();
            int? Id(string nombre) => cols.FirstOrDefault(c => $"{c.Apellidos}, {c.Nombres}" == nombre)?.Id;
            Reconocimiento R(string para, string autor, string valor, string mensaje, string fecha) => new()
            {
                Para = para, ParaIds = Id(para) is { } p ? [p] : [], Autor = autor, AutorId = Id(autor), Valor = valor, Mensaje = mensaje, Fecha = F(fecha),
            };
            db.Reconocimientos.AddRange(
                R("Alamos Ruiz, Florencia", "Navarro Ríos, Gonzalo", "Nos mueve la excelencia", "Conciliaciones bancarias impecables en el cierre anual.", "15/01/2026"),
                R("Herrera Castillo, Valeria", "Chávez Quispe, Renato", "Al infinito y más allá", "El rediseño del portal quedó increíble.", "24/02/2026"),
                R("Equipo de Ventas Retail", "Espinoza Vargas, Martín", "Pasión por el servicio", "Superaron la meta de satisfacción de clientes de febrero.", "02/03/2026"),
                R("Gutiérrez Paredes, Javier", "Benavides Rojas, Diego", "Nos mueve la excelencia", "Inventario cerrado sin diferencias por tercer mes seguido.", "06/03/2026"),
                R("Chávez Quispe, Renato", "Cáceres Huamán, Lucía", "Vamos por más!", "Gracias por apoyar el cierre de planillas de febrero fuera de horario.", "10/03/2026"));
            await db.SaveChangesAsync();
        }
    }
}
