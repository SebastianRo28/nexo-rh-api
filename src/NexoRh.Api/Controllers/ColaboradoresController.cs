using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// Colaboradores. Todos los usuarios pueden ver el directorio; los datos personales y salariales solo los ven los
/// administradores, la propia persona y su cadena de jefaturas. Crear y modificar es solo para administradores.
/// </summary>
[ApiController]
[Route("api/colaboradores")]
[Authorize]
public partial class ColaboradoresController(NexoDbContext db, Auditoria auditoria) : ControllerBase
{
    private static readonly string[] TiposDocumento = ["DNI", "Carné de extranjería", "Pasaporte"];
    private static readonly CultureInfo Pe = CultureInfo.GetCultureInfo("en-US");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ColaboradorDto>>> Listar()
    {
        var todos = await db.Colaboradores.AsNoTracking().OrderBy(c => c.Apellidos).ThenBy(c => c.Nombres).ToListAsync();
        var visibles = Alcance(todos);
        return todos.Select(c => ADto(c, todos, visibles.Contains(c.Id))).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ColaboradorDto>> Obtener(int id)
    {
        var todos = await db.Colaboradores.AsNoTracking().ToListAsync();
        var c = todos.FirstOrDefault(x => x.Id == id);
        if (c is null) return NotFound();
        return ADto(c, todos, Alcance(todos).Contains(id));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<ColaboradorDto>> Crear(ColaboradorGuardar p)
    {
        var errores = await Validar(p, null);
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores) { Title = "Revisa los datos del colaborador." });
        var c = new Colaborador { Id = (await db.Colaboradores.MaxAsync(x => (int?)x.Id) ?? -1) + 1 };
        await Aplicar(c, p);
        db.Colaboradores.Add(c);
        auditoria.Registrar($"Colaborador #{c.Id}", "Crear", c, actual: "Creado");
        await db.SaveChangesAsync();
        var todos = await db.Colaboradores.AsNoTracking().ToListAsync();
        return CreatedAtAction(nameof(Obtener), new { id = c.Id }, ADto(c, todos, true));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<ActionResult<ColaboradorDto>> Modificar(int id, ColaboradorGuardar p)
    {
        var c = await db.Colaboradores.FindAsync(id);
        if (c is null) return NotFound();
        var errores = await Validar(p, c);
        if (errores.Count > 0) return ValidationProblem(new ValidationProblemDetails(errores) { Title = "Revisa los datos del colaborador." });
        var antes = Instantanea(c);
        await Aplicar(c, p);
        c.ActualizadoEn = DateTime.UtcNow;
        var despues = Instantanea(c);
        foreach (var (k, v) in despues.Where(kv => antes[kv.Key] != kv.Value))
            auditoria.Registrar($"Colaborador #{c.Id}", "Modificar", c, k, antes[k], v);
        await db.SaveChangesAsync();
        var todos = await db.Colaboradores.AsNoTracking().ToListAsync();
        return ADto(c, todos, true);
    }

    // --- Permisos -------------------------------------------------------------------------------------------------
    /// <summary>Ids cuyos datos completos puede ver el usuario actual.</summary>
    private HashSet<int> Alcance(List<Colaborador> todos)
    {
        if (User.Rol() == Rol.Administrador) return todos.Select(c => c.Id).ToHashSet();
        var propio = User.ColaboradorId();
        var set = new HashSet<int>();
        if (propio is null) return set;
        set.Add(propio.Value);
        if (User.Rol() == Rol.Supervisor)
        {
            // Todo el equipo que depende de él, directa o indirectamente.
            var cola = new Queue<int>([propio.Value]);
            while (cola.Count > 0)
            {
                var jefe = cola.Dequeue();
                foreach (var h in todos.Where(x => x.JefeId == jefe && set.Add(x.Id))) cola.Enqueue(h.Id);
            }
        }
        return set;
    }

    private static ColaboradorDto ADto(Colaborador c, List<Colaborador> todos, bool completo)
    {
        var jefe = todos.FirstOrDefault(x => x.Id == c.JefeId);
        var dto = new ColaboradorDto
        {
            Id = c.Id, Apellidos = c.Apellidos, Nombres = c.Nombres, Cargo = c.Cargo, Division = c.Division, Area = c.Area, SubArea = c.SubArea,
            Ingreso = Fechas.Texto(c.Ingreso), JefeId = c.JefeId, Reservado = !completo,
            Documento = completo ? c.Documento : "••••" + c.Documento[^Math.Min(3, c.Documento.Length)..],
            SueldoNeto = completo ? c.SueldoNeto : null,
        };
        dto.Detalle = completo
            ? new DetalleDto
            {
                TipoDocumento = c.TipoDocumento, EmailCorporativo = c.EmailCorporativo, Telefono = c.Telefono, Nacimiento = Fechas.Texto(c.Nacimiento),
                Direccion = c.Direccion, Carrera = c.Carrera, Institucion = c.Institucion, PlanSalud = c.PlanSalud, Pension = c.Pension, Cuspp = c.Cuspp,
                Banco = c.Banco, FormaPago = c.FormaPago, Supervisor = jefe is null ? null : $"{jefe.Apellidos}, {jefe.Nombres}", SueldoBase = c.SueldoBase,
                Contrato = c.Contrato, Jornada = c.Jornada, Sindicalizado = c.Sindicalizado, Aviso = c.Aviso,
            }
            : new DetalleDto { EmailCorporativo = c.EmailCorporativo, Supervisor = jefe is null ? null : $"{jefe.Apellidos}, {jefe.Nombres}" };
        return dto;
    }

    // --- Validación y guardado ------------------------------------------------------------------------------------
    [GeneratedRegex(@"^\d{8}$")] private static partial Regex Dni();
    [GeneratedRegex(@"^[A-Za-z0-9]{6,12}$")] private static partial Regex DocAlfa();
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[A-Za-z]{2,}$")] private static partial Regex Email();

    private async Task<Dictionary<string, string[]>> Validar(ColaboradorGuardar p, Colaborador? actual)
    {
        var e = new Dictionary<string, string[]>();
        void Err(string k, string m) => e[k] = [m];
        string T(string? s) => (s ?? "").Trim();

        if (T(p.Apellidos).Length is 0 or > 80) Err("apellidos", "Ingresa los apellidos (máximo 80 caracteres).");
        if (T(p.Nombres).Length is 0 or > 80) Err("nombres", "Ingresa los nombres (máximo 80 caracteres).");
        var tipo = T(p.Detalle?.TipoDocumento) is { Length: > 0 } t ? t : actual?.TipoDocumento ?? "DNI";
        if (!TiposDocumento.Contains(tipo)) Err("tipoDocumento", "El tipo de documento debe ser DNI, Carné de extranjería o Pasaporte.");
        var doc = T(p.Documento);
        if (tipo == "DNI" ? !Dni().IsMatch(doc) : !DocAlfa().IsMatch(doc))
            Err("documento", tipo == "DNI" ? "El DNI debe tener 8 dígitos." : "El documento debe tener entre 6 y 12 letras o números.");
        else if (await db.Colaboradores.AnyAsync(x => x.Documento == doc && x.Id != (actual == null ? -1 : actual.Id)))
            Err("documento", "Ya existe un colaborador con ese documento.");
        var cargo = T(p.Cargo);
        if (cargo.Length == 0) Err("cargo", "Elige el cargo.");
        else if (!await db.Cargos.AnyAsync(x => x.Nombre == cargo && (x.Activo || (actual != null && actual.Cargo == cargo))))
            Err("cargo", "El cargo no existe o está inactivo.");
        if (T(p.Division).Length == 0) Err("division", "Elige la división.");
        if (T(p.Area).Length == 0) Err("area", "Elige el área.");
        if (T(p.SubArea).Length == 0) Err("subArea", "Elige la sub-área.");
        if (!Fechas.TryLeer(p.Ingreso, out var ingreso)) Err("ingreso", "La fecha de ingreso debe tener el formato DD/MM/AAAA.");
        else if (ingreso > Fechas.HoyLima().AddDays(180)) Err("ingreso", "La fecha de ingreso no puede ser más de 6 meses en el futuro.");
        else if (ingreso.Year < 1950) Err("ingreso", "Revisa la fecha de ingreso.");
        if (p.SueldoNeto is not { } s || s <= 0 || s > 1_000_000) Err("sueldoNeto", "El sueldo debe ser mayor a 0 y menor a S/ 1,000,000.");
        if (p.Detalle?.SueldoBase is { } sb && (sb <= 0 || sb > 1_000_000)) Err("sueldoBase", "El sueldo base debe ser mayor a 0 y menor a S/ 1,000,000.");
        if (T(p.Detalle?.EmailCorporativo) is { Length: > 0 } mail && !Email().IsMatch(mail)) Err("emailCorporativo", "El correo corporativo no es válido.");
        if (T(p.Detalle?.Nacimiento) is { Length: > 0 } nac)
        {
            if (!Fechas.TryLeer(nac, out var n)) Err("nacimiento", "La fecha de nacimiento debe tener el formato DD/MM/AAAA.");
            else if (n > Fechas.HoyLima().AddYears(-14)) Err("nacimiento", "El colaborador debe tener al menos 14 años.");
        }
        var jefeId = await ResolverJefe(p);
        if (p.JefeId is { } j && jefeId is null) Err("jefeId", "La jefatura indicada no existe.");
        if (actual != null && jefeId == actual.Id) Err("jefeId", "Un colaborador no puede ser su propia jefatura.");
        return e;
    }

    /// <summary>Jefatura por id o, si el formulario solo envía el nombre ("Apellidos, Nombres"), por nombre.</summary>
    private async Task<int?> ResolverJefe(ColaboradorGuardar p)
    {
        if (p.JefeId is { } id) return await db.Colaboradores.AnyAsync(x => x.Id == id) ? id : null;
        var nombre = (p.Detalle?.Supervisor ?? "").Trim();
        if (nombre.Length == 0) return null;
        var lista = await db.Colaboradores.AsNoTracking().Select(x => new { x.Id, x.Apellidos, x.Nombres }).ToListAsync();
        return lista.FirstOrDefault(x => $"{x.Apellidos}, {x.Nombres}".Equals(nombre, StringComparison.OrdinalIgnoreCase)
                                      || $"{x.Nombres} {x.Apellidos}".Equals(nombre, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private async Task Aplicar(Colaborador c, ColaboradorGuardar p)
    {
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        c.Apellidos = p.Apellidos!.Trim();
        c.Nombres = p.Nombres!.Trim();
        c.Documento = p.Documento!.Trim();
        c.Cargo = p.Cargo!.Trim();
        c.Division = p.Division!.Trim();
        c.Area = p.Area!.Trim();
        c.SubArea = p.SubArea!.Trim();
        Fechas.TryLeer(p.Ingreso, out var ingreso);
        c.Ingreso = ingreso;
        c.SueldoNeto = Math.Round(p.SueldoNeto!.Value, 2);
        c.JefeId = await ResolverJefe(p);
        var d = p.Detalle;
        if (d is null) return;
        c.TipoDocumento = N(d.TipoDocumento) ?? c.TipoDocumento;
        c.EmailCorporativo = N(d.EmailCorporativo)?.ToLowerInvariant();
        c.Telefono = N(d.Telefono);
        c.Nacimiento = Fechas.TryLeer(d.Nacimiento, out var n) ? n : null;
        c.Direccion = N(d.Direccion);
        c.Carrera = N(d.Carrera);
        c.Institucion = N(d.Institucion);
        c.PlanSalud = N(d.PlanSalud);
        c.Pension = N(d.Pension);
        c.Cuspp = N(d.Cuspp);
        c.Banco = N(d.Banco);
        c.FormaPago = N(d.FormaPago);
        c.SueldoBase = d.SueldoBase is { } sb ? Math.Round(sb, 2) : null;
        c.Contrato = N(d.Contrato);
        c.Jornada = N(d.Jornada);
        c.Sindicalizado = N(d.Sindicalizado);
        c.Aviso = N(d.Aviso);
    }

    /// <summary>Valores comparables para el historial (claves en snake_case, como en el diseño).</summary>
    private static Dictionary<string, string> Instantanea(Colaborador c) => new()
    {
        ["apellidos"] = c.Apellidos, ["nombres"] = c.Nombres, ["documento"] = c.Documento, ["tipo_documento"] = c.TipoDocumento,
        ["cargo"] = c.Cargo, ["division"] = c.Division, ["area"] = c.Area, ["sub_area"] = c.SubArea, ["fecha_ingreso"] = Fechas.Texto(c.Ingreso),
        ["sueldo_neto"] = "S/ " + c.SueldoNeto.ToString("N2", Pe), ["sueldo_base"] = c.SueldoBase is { } b ? "S/ " + b.ToString("N2", Pe) : "—",
        ["jefatura"] = c.JefeId?.ToString() ?? "—", ["email_corporativo"] = c.EmailCorporativo ?? "—", ["telefono_particular"] = c.Telefono ?? "—",
        ["fecha_nacimiento"] = Fechas.Texto(c.Nacimiento) ?? "—", ["direccion"] = c.Direccion ?? "—", ["prevision"] = c.Pension ?? "—",
        ["banco"] = c.Banco ?? "—", ["contrato"] = c.Contrato ?? "—", ["jornada"] = c.Jornada ?? "—",
    };
}
