using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Data;

/// <summary>Activos, documentos, compensaciones y headcount de ejemplo (los mismos datos que mostraba el frontend).</summary>
public static class SemillaAdministracion
{
    private static DateOnly F(string s) => DateOnly.ParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static DateTime FH(string s) => DateTime.ParseExact(s, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture).AddHours(5); // Lima → UTC

    public static async Task CargarAsync(NexoDbContext db)
    {
        if (!await db.Colaboradores.AnyAsync()) return;
        var cols = await db.Colaboradores.AsNoTracking().ToListAsync();
        int? Id(string nombre) => cols.FirstOrDefault(c => $"{c.Apellidos}, {c.Nombres}" == nombre)?.Id;

        if (!await db.CategoriasActivo.AnyAsync())
        {
            var categorias = new[] { "Uniformes", "Tecnología", "Construcción", "Licencias", "Transporte", "EPP", "Computadores", "Mobiliario", "Telefonía" }
                .Select(n => new CategoriaActivo { Nombre = n }).ToList();
            db.CategoriasActivo.AddRange(categorias);
            await db.SaveChangesAsync();
            int Cat(string n) => categorias.First(c => c.Nombre == n).Id;
            var productos = new (string n, string c, string m)[]
            {
                ("Laptop ThinkPad E14 Gen 5", "Computadores", "Lenovo"), ("MacBook Air 13\"", "Computadores", "Apple"), ("Monitor 24\" P2422H", "Tecnología", "Dell"),
                ("Celular Galaxy A54", "Tecnología", "Samsung"), ("Casco de seguridad", "EPP", "3M"), ("Chaleco reflectivo", "EPP", "Steelpro"),
                ("Zapatos de seguridad T42", "EPP", "Caterpillar"), ("Polo corporativo M", "Uniformes", "Comercial Andina"), ("Licencia Microsoft 365", "Licencias", "Microsoft"),
                ("Camioneta Hilux", "Transporte", "Toyota"), ("Taladro percutor", "Construcción", "Bosch"),
            }.Select(p => new ProductoActivo { Nombre = p.n, CategoriaId = Cat(p.c), Marca = p.m }).ToList();
            db.ProductosActivo.AddRange(productos);
            await db.SaveChangesAsync();
            int Prod(string n) => productos.First(p => p.Nombre == n).Id;

            const string fa = "Alamos Ruiz, Florencia";
            var activos = new (string cod, string prod, string estado, string? asignado)[]
            {
                ("ACT-0042", "Laptop ThinkPad E14 Gen 5", "Asignado", fa), ("ACT-0043", "Laptop ThinkPad E14 Gen 5", "Disponible", null),
                ("ACT-0044", "Laptop ThinkPad E14 Gen 5", "Asignado", "Zapata Ugarte, Miguel"), ("ACT-0045", "Laptop ThinkPad E14 Gen 5", "Asignado", "Ríos Calderón, Ana"),
                ("ACT-0051", "MacBook Air 13\"", "Asignado", "Herrera Castillo, Valeria"), ("ACT-0107", "Monitor 24\" P2422H", "Asignado", fa),
                ("ACT-0108", "Monitor 24\" P2422H", "Mantenimiento", null), ("ACT-0213", "Celular Galaxy A54", "Asignado", fa),
                ("ACT-0214", "Celular Galaxy A54", "Asignado", "Benavides Rojas, Diego"), ("ACT-0301", "Casco de seguridad", "Asignado", "Pérez Soto, Luis"),
                ("ACT-0302", "Chaleco reflectivo", "Asignado", "Gutiérrez Paredes, Javier"), ("ACT-0303", "Zapatos de seguridad T42", "Disponible", null),
                ("ACT-0410", "Polo corporativo M", "Asignado", "Medina Torres, Sofía"), ("ACT-0502", "Licencia Microsoft 365", "Asignado", "Chávez Quispe, Renato"),
                ("ACT-0601", "Camioneta Hilux", "Mantenimiento", null), ("ACT-0702", "Taladro percutor", "Inactivo", null),
            };
            db.Activos.AddRange(activos.Select(a => new Activo { Codigo = a.cod, ProductoId = Prod(a.prod), Estado = a.estado, AsignadoId = a.asignado is null ? null : Id(a.asignado) }));
            await db.SaveChangesAsync();
            MovimientoActivo M(string cod, string fecha, string asignado, string estado, string comentario, string admin) =>
                new() { ActivoCodigo = cod, Fecha = FH(fecha), Asignado = asignado, Estado = estado, Comentario = comentario, Admin = admin };
            db.MovimientosActivo.AddRange(
                M("ACT-0042", "01/06/2023 09:00", "—", "Disponible", "Ingreso al inventario (factura F001-2231).", "Rodrigo Paredes"),
                M("ACT-0042", "02/06/2023 10:15", fa, "Asignado", "Entrega con acta firmada.", "Rodrigo Paredes"),
                M("ACT-0042", "14/01/2026 16:30", fa, "Mantenimiento", "Cambio de batería en garantía.", "Lucía Cáceres"),
                M("ACT-0042", "20/01/2026 11:00", fa, "Asignado", "Devuelto por el servicio técnico.", "Lucía Cáceres"));
            foreach (var a in activos.Where(a => a.cod != "ACT-0042"))
                db.MovimientosActivo.Add(M(a.cod, "02/01/2026 09:00", a.asignado ?? "—", a.estado, "Carga inicial del inventario.", "Rodrigo Paredes"));

            OrdenActivo O(string origen, string prod, string para, string area, string cargo, string sol, string solicitada, string asignar, string estado, string? activo = null) => new()
            {
                Origen = origen, ProductoId = Prod(prod), ParaId = Id(para), Para = para, Area = area, Cargo = cargo, Solicitante = sol, Solicitada = F(solicitada),
                AsignarEl = F(asignar), Estado = estado, ActivoCodigo = activo, Comentario = estado == "Rechazada" ? "Fuera del presupuesto del área" : "",
            };
            db.OrdenesActivo.AddRange(
                O("Gestión de activos", "MacBook Air 13\"", "Herrera Castillo, Valeria", "Desarrollo", "Diseñadora UX", "Rodrigo Paredes", "07/02/2025", "10/02/2025", "Asignada", "ACT-0051"),
                O("Workflow", "Camioneta Hilux", "Benavides Rojas, Diego", "Logística", "Jefe de Operaciones", "Benavides Rojas, Diego", "05/01/2026", "15/01/2026", "Rechazada"),
                O("Workflow", "Laptop ThinkPad E14 Gen 5", "Ortiz Ramírez, Daniela", "Recursos Humanos", "Practicante Profesional", "Cáceres Huamán, Lucía", "24/02/2026", "01/03/2026", "Pendiente"),
                O("Gestión de activos", "Zapatos de seguridad T42", "Gutiérrez Paredes, Javier", "Logística", "Asistente de Almacén", "Benavides Rojas, Diego", "10/03/2026", "20/03/2026", "Pendiente"),
                O("Workflow", "Celular Galaxy A54", "Salas Cornejo, Mateo", "Logística", "Asistente de Almacén", "Rodrigo Paredes", "11/03/2026", "01/04/2026", "Pendiente"),
                O("Gestión de activos", "Monitor 24\" P2422H", "Chávez Quispe, Renato", "Desarrollo", "Desarrollador Senior", "Chávez Quispe, Renato", "15/03/2026", "25/03/2026", "Pendiente"));
            await db.SaveChangesAsync();
        }

        if (!await db.PlantillasDocumento.AnyAsync())
        {
            const string ficha = "Ficha del Colaborador";
            db.PlantillasDocumento.AddRange(new (string n, string t, string c, bool a)[]
            {
                ("Contrato de trabajo – Indefinido", "Contrato", "…/CONTRATOS", true), ("Contrato de trabajo – Plazo fijo", "Contrato", "…/CONTRATOS", true),
                ("Contrato de practicante", "Contrato", "…/CONTRATOS", false), ("Anexo de cambio de cargo", "Anexo", "…/ANEXOS", true),
                ("Anexo de cambio de sueldo", "Anexo", "…/ANEXOS", true), ("Anexo de trabajo remoto", "Anexo de asistencia", "…/ANEXOS", true),
                ("Anexo de cambio de jornada", "Anexo de asistencia", "…/ANEXOS", true), ("Certificado de trabajo", "Certificado", ficha, true),
                ("Certificado de antigüedad", "Certificado de antigüedad", ficha, true), ("Certificado de renta de quinta", "Certificado", ficha, true),
                ("Carta de preaviso de término", "Carta de aviso", "…/CARTAS", true), ("Carta de amonestación", "Carta de aviso", "…/CARTAS", true),
                ("Acta de entrega de activos", "Acta de entrega", "…/ACTIVOS", false), ("Acta de devolución de activos", "Acta de entrega", "…/ACTIVOS", true),
                ("Acta de entrega de EPP", "Acta de entrega", "…/SST", true), ("Constancia de vacaciones", "Certificado", "…/VACACIONES", true),
                ("Solicitud de vacaciones", "Anexo de asistencia", "…/VACACIONES", true), ("Liquidación de beneficios sociales", "Certificado", "…/LIQUIDACIÓN", true),
                ("Carta de renuncia aceptada", "Carta de aviso", "…/CARTAS", false), ("Política de seguridad de la información", "Anexo", ficha, true),
                ("Reglamento interno de trabajo", "Anexo", ficha, true), ("Compromiso de confidencialidad", "Anexo", "…/ANEXOS", true),
                ("Autorización de descuento", "Anexo", "…/ANEXOS", true), ("Convenio de capacitación", "Anexo", "…/CAPACITACIONES", true),
                ("Constancia de entrega de boletas", "Certificado", ficha, true),
            }.Select(p => new PlantillaDocumento { Nombre = p.n, Tipo = p.t, Carpeta = p.c, Activa = p.a }));
            DocumentoColaborador D(string col, string archivo, string fecha, string rev, string fc, string fe, string? eliminado = null, string por = "") => new()
            {
                ColaboradorId = Id(col) ?? 0, Archivo = archivo, Fecha = F(fecha), Revision = rev, FirmaColaborador = fc, FirmaEmpresa = fe,
                EliminadoEn = eliminado is null ? null : F(eliminado).ToDateTime(new TimeOnly(15, 0)), EliminadoPor = por,
            };
            db.Documentos.AddRange(
                D("Alamos Ruiz, Florencia", "2026_03_12_10-14_Anexo_de_trabajo_remoto_Florencia_Alamos_F1.pdf", "12/03/2026", "si", "no", "si"),
                D("Chávez Quispe, Renato", "2026_03_12_10-14_Anexo_de_trabajo_remoto_Renato_Chavez_F1.pdf", "12/03/2026", "si", "no", "si"),
                D("Ortiz Ramírez, Daniela", "2026_03_01_09-02_Contrato_de_practicante_Daniela_Ortiz_F1.pdf", "01/03/2026", "na", "no", "no"),
                D("Herrera Castillo, Valeria", "2026_02_10_16-40_Acta_de_entrega_de_activos_Valeria_Herrera_F1.pdf", "10/02/2026", "na", "si", "si"),
                D("Gutiérrez Paredes, Javier", "2026_03_05_11-20_Carta_de_preaviso_Javier_Gutierrez_F1.pdf", "05/03/2026", "no", "no", "no"),
                D("Medina Torres, Sofía", "2026_01_20_08-30_Certificado_de_trabajo_Sofia_Medina_F1.pdf", "20/01/2026", "na", "na", "si"),
                D("Pérez Soto, Luis", "2026_03_10_15-05_Acta_de_entrega_de_EPP_Luis_Perez_F1.pdf", "10/03/2026", "na", "si", "no"),
                D("Zapata Ugarte, Miguel", "2026_02_02_09-10_Anexo_de_cambio_de_jornada_Miguel_Zapata_F1.pdf", "02/02/2026", "si", "no", "si", "05/02/2026", "Rodrigo Paredes"),
                D("Ríos Calderón, Ana", "2026_01_15_12-00_Carta_de_amonestacion_Ana_Rios_F1.pdf", "15/01/2026", "si", "no", "no", "20/01/2026", "Lucía Cáceres"));
            await db.SaveChangesAsync();
        }

        if (!await db.Bandas.AnyAsync())
        {
            db.Bandas.AddRange(new (string c, decimal o)[] { ("Analista Finanzas", 6500), ("Desarrollador Senior", 8000), ("Ejecutiva Comercial", 5000), ("Supervisor de Ventas", 6000), ("Asistente de Almacén", 2300), ("Contadora", 7000) }
                .Select(b => new BandaSalarial { Cargo = b.c, Objetivo = b.o }));
            foreach (var (c, mod, ev) in new (int, string, string)[]
            {
                (0, "01/01/2025", "Supera expectativas"), (3, "01/01/2026", "Cumple expectativas"), (4, "22/08/2025", "Cumple expectativas"),
                (9, "01/07/2025", "Supera expectativas"), (7, "18/11/2025", "Cumple expectativas"), (6, "05/09/2024", "En desarrollo"),
            })
                if (cols.Any(x => x.Id == c)) db.Compensaciones.Add(new CompensacionColaborador { ColaboradorId = c, UltimaModificacion = F(mod), Evaluacion = ev });
            CargoMercado CM(string cargo, string? interno, string desc, string ind, string tam, decimal p50) =>
                new() { Cargo = cargo, Interno = interno, Descripcion = desc, Industria = ind, Tamano = tam, P25 = p50 * 0.8m, P50 = p50, P75 = p50 * 1.25m, P90 = Math.Round(p50 * 1.55m) };
            db.EstudioSalarial.AddRange(
                CM("Analista de finanzas", "Analista Finanzas", "Elabora reportes financieros, flujo de caja y conciliaciones…", "Comercio mayorista", "Mediana Empresa", 4800),
                CM("Desarrollador de software senior", "Desarrollador Senior", "Diseña, desarrolla y mantiene aplicaciones…", "Tecnología", "Total de mercado", 7900),
                CM("Ejecutivo comercial", null, "Gestiona cartera de clientes corporativos y metas de venta…", "Comercio mayorista", "Mediana Empresa", 4100),
                CM("Supervisor de ventas", null, "Lidera equipos de venta en tienda o canal…", "Retail", "Gran Empresa", 5600),
                CM("Asistente de almacén", null, "Recepciona, almacena y despacha mercadería…", "Logística", "Mediana Empresa", 1900),
                CM("Contador general", null, "Responsable de la contabilidad, cierres y declaraciones…", "Total de mercado", "Total de mercado", 6800),
                CM("Analista de recursos humanos", null, "Gestiona procesos de RR.HH. como planillas y selección…", "Comercio mayorista", "Pequeña Empresa", 3900),
                CM("Diseñador UX", null, "Investiga usuarios y diseña interfaces digitales…", "Tecnología", "Total de mercado", 5400));
            await db.SaveChangesAsync();
        }

        if (!await db.PlanesHeadcount.AnyAsync())
        {
            foreach (var (a, n) in new (string, int)[] { ("Gerencia General", 1), ("Finanzas", 3), ("Recursos Humanos", 4), ("Contabilidad", 2), ("Logística", 2), ("Seguridad y Salud", 2), ("Ventas", 4), ("Marketing", 1), ("Desarrollo", 1), ("Infraestructura", 1) })
                db.PlanesHeadcount.Add(new PlanHeadcount { Tipo = "area", Nombre = a, Cantidad = n });
            foreach (var (c, n) in new (string, int)[] { ("Analista Finanzas", 1), ("Analista de Selección", 2), ("Asistente de Almacén", 1), ("Ejecutiva Comercial", 3) })
                db.PlanesHeadcount.Add(new PlanHeadcount { Tipo = "cargo", Nombre = c, Cantidad = n });
            Posicion P(string cod, string cargo, string div, string area, string sub, string ingreso, string? termino, bool vigente = true) =>
                new() { Codigo = cod, Cargo = cargo, Division = div, Area = area, SubArea = sub, Ingreso = F(ingreso), Termino = termino is null ? null : F(termino), Vigente = vigente };
            const string adm = "Departamento de Administración";
            db.Posiciones.AddRange(
                P("POS-0101", "Analista de Selección", adm, "Recursos Humanos", "Selección", "01/04/2026", null),
                P("POS-0102", "Analista de Selección", adm, "Recursos Humanos", "Selección", "01/05/2026", null),
                P("POS-0110", "Ejecutiva Comercial", "Comercial", "Ventas", "Corporativo", "15/04/2026", null),
                P("POS-0111", "Ejecutiva Comercial", "Comercial", "Ventas", "Corporativo", "15/04/2026", "31/12/2026"),
                P("POS-0120", "Prevencionista de Riesgos", "Operaciones", "Seguridad y Salud", "Prevención", "01/06/2026", null),
                P("POS-0090", "Analista Finanzas", adm, "Finanzas", "Tesorería", "01/06/2023", "31/12/2025", false));
            await db.SaveChangesAsync();
        }
    }
}
