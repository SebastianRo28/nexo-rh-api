using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Data;

/// <summary>
/// Datos iniciales de la empresa de ejemplo del diseño ("Comercial Andina S.A.C.", marzo 2026). Son los mismos que usa
/// el frontend, con los mismos ids. Solo se cargan si la tabla está vacía.
/// </summary>
public static class Semilla
{
    private const string Adm = "Departamento de Administración";

    public static async Task CargarAsync(NexoDbContext db, IConfiguration config, ILogger logger)
    {
        if (!await db.FamiliasCargo.AnyAsync())
        {
            db.FamiliasCargo.AddRange(new[] { "Intermedio", "Jefatura", "Senior", "Gerencia", "Junior" }
                .Select((n, i) => new FamiliaCargo { Id = i, Nombre = n }));
        }
        if (!await db.Competencias.AnyAsync())
        {
            db.Competencias.AddRange(
                new Competencia { Id = 0, Nombre = "Trabajo en equipo", Descripcion = "Colabora con otros para lograr objetivos comunes." },
                new Competencia { Id = 1, Nombre = "Orientación a resultados", Descripcion = "Cumple metas con calidad y en los plazos acordados." },
                new Competencia { Id = 2, Nombre = "Comunicación efectiva", Descripcion = "Transmite ideas con claridad y escucha activamente." },
                new Competencia { Id = 3, Nombre = "Liderazgo", Descripcion = "Guía y desarrolla a su equipo." },
                new Competencia { Id = 4, Nombre = "Adaptabilidad", Descripcion = "Responde con flexibilidad a los cambios." });
        }
        if (!await db.Cargos.AnyAsync()) db.Cargos.AddRange(Cargos());
        if (!await db.Colaboradores.AnyAsync()) db.Colaboradores.AddRange(Colaboradores());
        if (!await db.Cambios.AnyAsync()) db.Cambios.AddRange(Cambios());
        await db.SaveChangesAsync();
        await SemillaAsistencia.CargarAsync(db);

        if (!await db.Usuarios.AnyAsync())
        {
            var password = config["Semilla:PasswordInicial"];
            if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
            {
                logger.LogWarning("No se crearon usuarios: falta Semilla:PasswordInicial (mínimo 10 caracteres) en la configuración.");
                return;
            }
            var hasher = new PasswordHasher<Usuario>();
            var usuarios = new[]
            {
                new Usuario { Nombre = "Rodrigo Paredes", Correo = "r.paredes@comercialandina.pe", Rol = Rol.Administrador, Perfil = "Administrador" },
                new Usuario { Nombre = "Lucía Cáceres Huamán", Correo = "l.caceres@comercialandina.pe", Rol = Rol.Administrador, Perfil = "Administrador", ColaboradorId = 2 },
                new Usuario { Nombre = "Gonzalo Navarro Ríos", Correo = "g.navarro@comercialandina.pe", Rol = Rol.Supervisor, Perfil = "Supervisor", ColaboradorId = 11 },
                new Usuario { Nombre = "Diego Benavides Rojas", Correo = "d.benavides@comercialandina.pe", Rol = Rol.Supervisor, Perfil = "Supervisor", ColaboradorId = 1 },
                new Usuario { Nombre = "Florencia Alamos Ruiz", Correo = "f.alamos@comercialandina.pe", Rol = Rol.Colaborador, Perfil = "Normal / Colaborador", ColaboradorId = 0 },
            };
            foreach (var u in usuarios) u.PasswordHash = hasher.HashPassword(u, password);
            db.Usuarios.AddRange(usuarios);
            await db.SaveChangesAsync();
            logger.LogInformation("Se crearon {N} usuarios de ejemplo con la contraseña inicial de la configuración.", usuarios.Length);
        }
    }

    private static DateOnly F(string s) => DateOnly.ParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static Colaborador C(int id, string apellidos, string nombres, string documento, string cargo, string division, string area,
        string subArea, string ingreso, decimal sueldo, int? jefe) => new()
    {
        Id = id, Apellidos = apellidos, Nombres = nombres, Documento = documento, Cargo = cargo, Division = division, Area = area,
        SubArea = subArea, Ingreso = F(ingreso), SueldoNeto = sueldo, JefeId = jefe,
    };

    private static IEnumerable<Colaborador> Colaboradores()
    {
        // Jefaturas según el organigrama de Mi Portal.
        var florencia = C(0, "Alamos Ruiz", "Florencia", "72458193", "Analista Finanzas", Adm, "Finanzas", "Tesorería", "01/06/2023", 6240.5m, 11);
        florencia.EmailCorporativo = "f.alamos@comercialandina.pe";
        florencia.Telefono = "51 987 654 321";
        florencia.Nacimiento = F("14/04/1997");
        florencia.Direccion = "Av. Javier Prado Este 1250, San Isidro, Lima";
        florencia.Carrera = "Contabilidad y Finanzas";
        florencia.Institucion = "Universidad de Lima";
        florencia.PlanSalud = "EsSalud";
        florencia.Pension = "AFP Habitat · comisión Flujo";
        florencia.Cuspp = "612345FARRU3";
        florencia.Banco = "BCP · Cuenta de ahorros ****8012";
        florencia.FormaPago = "Transferencia";
        florencia.SueldoBase = 7500;
        florencia.Contrato = "Indefinido";
        florencia.Jornada = "Mensual 43.0 hrs (L, M, M, J, V)";
        florencia.Sindicalizado = "No";
        florencia.Aviso = "El colaborador se encuentra con Licencia hasta el viernes 06 de marzo de 2026.";
        // The boss must exist before the people who report to it, so Martín (5) goes first.
        return new[]
        {
            C(5, "Espinoza Vargas", "Martín", "41236987", "Gerente General", "Directorio", "Gerencia General", "Gerencia General", "01/03/2019", 18500, null),
            C(11, "Navarro Ríos", "Gonzalo", "43215698", "Jefe de Finanzas", Adm, "Finanzas", "Tesorería", "02/05/2018", 12340, 5),
            C(1, "Benavides Rojas", "Diego", "70125864", "Jefe de Operaciones", "Operaciones", "Logística", "Almacén Central", "15/02/2021", 9870, 5),
            C(3, "Chávez Quispe", "Renato", "72011548", "Desarrollador Senior", "Tecnología", "Desarrollo", "Aplicaciones", "10/01/2022", 8450, 5),
            C(9, "Huertas León", "Paolo", "47896521", "Supervisor de Ventas", "Comercial", "Ventas", "Retail", "12/06/2021", 7120.85m, 5),
            C(2, "Cáceres Huamán", "Lucía", "46892317", "Analista de RR.HH.", Adm, "Recursos Humanos", "Compensaciones", "03/04/2022", 5312.75m, 5),
            C(10, "Medina Torres", "Sofía", "72589634", "Analista de Marketing", "Comercial", "Marketing", "Digital", "20/01/2024", 4750, 5),
            florencia,
            C(6, "Flores Mendoza", "Carolina", "73354120", "Contadora", Adm, "Contabilidad", "Contabilidad General", "05/09/2020", 6980, 11),
            C(14, "Ríos Calderón", "Ana", "71458236", "Asistente Contable", Adm, "Contabilidad", "Cuentas por Pagar", "09/10/2023", 3210, 6),
            C(7, "Gutiérrez Paredes", "Javier", "70458812", "Asistente de Almacén", "Operaciones", "Logística", "Almacén Central", "18/11/2024", 2150.4m, 1),
            C(13, "Pérez Soto", "Luis", "40123578", "Coordinador de SST", "Operaciones", "Seguridad y Salud", "Prevención", "14/07/2022", 6050.3m, 1),
            C(8, "Herrera Castillo", "Valeria", "74120369", "Diseñadora UX", "Tecnología", "Desarrollo", "Producto", "07/02/2025", 5600, 3),
            C(15, "Zapata Ugarte", "Miguel", "44785123", "Técnico de Soporte", "Tecnología", "Infraestructura", "Mesa de Ayuda", "25/04/2024", 3480.6m, 3),
            C(4, "Delgado Salazar", "Andrea", "45781236", "Ejecutiva Comercial", "Comercial", "Ventas", "Corporativo", "22/08/2023", 4980.2m, 9),
            C(12, "Ortiz Ramírez", "Daniela", "75236981", "Practicante Profesional", Adm, "Recursos Humanos", "Selección", "01/03/2026", 1130, 2),
        };
    }

    private static IEnumerable<Cargo> Cargos()
    {
        (string nombre, int subAreas, string familia, bool activo)[] filas =
        {
            ("Analista Finanzas", 1, "Intermedio", true), ("Jefe de Operaciones", 1, "Jefatura", true), ("Analista de RR.HH.", 2, "Intermedio", true),
            ("Desarrollador Senior", 2, "Senior", true), ("Ejecutiva Comercial", 1, "Intermedio", true), ("Gerente General", 1, "Gerencia", true),
            ("Contadora", 1, "Senior", true), ("Asistente de Almacén", 1, "Junior", true), ("Diseñadora UX", 1, "Intermedio", true),
            ("Supervisor de Ventas", 2, "Jefatura", true), ("Analista de Marketing", 1, "Intermedio", true), ("Jefe de Finanzas", 1, "Jefatura", true),
            ("Practicante Profesional", 3, "Junior", true), ("Coordinador de SST", 1, "Senior", true), ("Asistente Contable", 2, "Junior", true),
            ("Técnico de Soporte", 1, "Junior", true), ("Asistente Administrativo", 2, "Junior", true), ("Auxiliar de Almacén", 1, "Junior", true),
            ("Chofer", 1, "Junior", false), ("Recepcionista", 1, "Junior", false), ("Analista de Tesorería", 1, "Intermedio", true),
            ("Analista Contable", 1, "Intermedio", true), ("Jefe de Contabilidad", 2, "Jefatura", true), ("Jefe de RR.HH.", 3, "Jefatura", true),
            ("Analista de Selección", 1, "Intermedio", true), ("Analista de Planillas", 1, "Intermedio", true), ("Jefe Comercial", 3, "Jefatura", true),
            ("Ejecutivo de Cuentas", 1, "Intermedio", true), ("Vendedor Retail", 1, "Junior", true), ("Analista de Datos", 1, "Intermedio", true),
            ("Desarrollador Junior", 2, "Junior", true), ("Arquitecto de Software", 1, "Senior", true), ("Jefe de Tecnología", 2, "Jefatura", true),
            ("Soporte Técnico Nivel 2", 1, "Intermedio", true), ("Prevencionista de Riesgos", 1, "Intermedio", true), ("Supervisor de Almacén", 1, "Jefatura", true),
            ("Analista de Compras", 1, "Intermedio", true), ("Asistente de Marketing", 1, "Junior", true), ("Community Manager", 1, "Junior", true),
        };
        return filas.Select((f, i) => new Cargo { Id = i, Nombre = f.nombre, Codigo = CodigoDe(f.nombre), SubAreas = f.subAreas, Familia = f.familia, Activo = f.activo });
    }

    /// <summary>"Analista de Planillas" → "ANA-PLA".</summary>
    public static string CodigoDe(string nombre) =>
        string.Join("-", nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2)
            .Select(w => w[..3].ToUpperInvariant().Replace(".", "")));

    private static IEnumerable<Cambio> Cambios()
    {
        const string rp = "Rodrigo Paredes · Administrador", lc = "Lucía Cáceres · Administrador";
        // Las fechas de ejemplo están en hora de Lima (UTC-5); se guardan en UTC.
        static DateTime L(string s) => DateTime.ParseExact(s, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture).AddHours(5);
        Cambio X(string usuario, string origen, string colaborador, string documento, string entidad, string accion, string fecha, string propiedad, string anterior, string actual) =>
            new() { Usuario = usuario, Origen = origen, Colaborador = colaborador, Documento = documento, Entidad = entidad, Accion = accion, Fecha = L(fecha), Propiedad = propiedad, Anterior = anterior, Actual = actual };
        return new[]
        {
            X(rp, "Web", "Alamos Ruiz, Florencia", "72458193", "Trabajo #1043-3", "Modificar", "01/01/2025 09:12", "cargo", "Asistente de Finanzas", "Analista Finanzas"),
            X(rp, "Web", "Alamos Ruiz, Florencia", "72458193", "Trabajo #1043-3", "Modificar", "01/01/2025 09:12", "sueldo_base", "S/ 5,200.00", "S/ 7,500.00"),
            X(lc, "Importador", "Ortiz Ramírez, Daniela", "75236981", "Colaborador #2210", "Crear", "01/03/2026 08:40", "—", "—", "Creado"),
            X("Sistema · API", "API", "Chávez Quispe, Renato", "72011548", "Colaborador #1987", "Modificar", "10/03/2026 14:02", "telefono_particular", "51 912 345 678", "51 987 001 224"),
            X(lc, "Web", "Huertas León, Paolo", "47896521", "Vacaciones #5541", "Eliminar", "05/02/2026 11:30", "—", "3 días (10/02/2026)", "—"),
            X(rp, "Web", "Pérez Soto, Luis", "40123578", "Colaborador #1660", "Modificar", "12/03/2026 16:18", "email_corporativo", "lperez@comercialandina.pe", "l.perez@comercialandina.pe"),
            X(lc, "Importador", "Gutiérrez Paredes, Javier", "70458812", "Trabajo #2011-1", "Modificar", "28/02/2026 10:05", "centro_costo", "CC-120", "CC-200"),
            X(rp, "Web", "Herrera Castillo, Valeria", "74120369", "Activo #ACT-0051", "Crear", "10/02/2025 09:00", "estado", "—", "Asignado"),
        };
    }
}
