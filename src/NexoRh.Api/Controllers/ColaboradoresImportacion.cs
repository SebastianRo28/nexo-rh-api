using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Domain;
using NexoRh.Api.Dtos;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record ErrorCargaDto(int Fila, string Campo, string Mensaje);
public record ResultadoImportacion(string Archivo, int Filas, int Creados, List<ErrorCargaDto> Errores, int? CargaId, string Estado);

/// <summary>
/// Importador de Colaboradores (Reportería › Importadores). Valida cada fila con las mismas reglas que el formulario de
/// alta y carga el archivo completo o nada: si alguna fila tiene errores no se crea ningún colaborador.
/// </summary>
public partial class ColaboradoresController
{
    public const string ImportadorColaboradores = "Colaboradores";
    public const int MaxFilasImportacion = 1000;

    /// <summary>Columnas del template en orden; las marcadas con * son obligatorias.</summary>
    public static readonly (string Columna, string Campo, bool Obligatoria, string Formato)[] ColumnasImportacion =
    [
        ("Apellidos", "apellidos", true, "Máximo 80 caracteres"),
        ("Nombres", "nombres", true, "Máximo 80 caracteres"),
        ("Tipo de documento", "tipoDocumento", false, "DNI, Carné de extranjería o Pasaporte (vacío = DNI)"),
        ("Número de documento", "documento", true, "DNI: 8 dígitos; otros: 6 a 12 letras o números. No debe existir en Nexo RH"),
        ("Cargo", "cargo", true, "Nombre exacto de un cargo activo de Organización › Cargos"),
        ("División", "division", true, "Texto"),
        ("Área", "area", true, "Texto"),
        ("Sub-área", "subArea", true, "Texto"),
        ("Fecha de ingreso", "ingreso", true, "DD/MM/AAAA"),
        ("Sueldo neto", "sueldoNeto", true, "Número mayor a 0 (ej. 3500.00)"),
        ("Sueldo base", "sueldoBase", false, "Número mayor a 0"),
        ("Correo corporativo", "emailCorporativo", false, "nombre@empresa.pe"),
        ("Teléfono", "telefono", false, "Texto"),
        ("Fecha de nacimiento", "nacimiento", false, "DD/MM/AAAA (mínimo 14 años)"),
        ("Jefatura", "jefeId", false, "“Apellidos, Nombres” de un colaborador ya registrado"),
    ];

    [HttpGet("importar/template")]
    [Authorize(Policy = Politicas.Administrador)]
    public IActionResult TemplateImportacion()
    {
        var csv = string.Join(';', ColumnasImportacion.Select(c => c.Columna)) + "\r\n";
        return File([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)], "text/csv; charset=utf-8", "template-importador-colaboradores.csv");
    }

    /// <summary>Con validar=true solo revisa el archivo; sin él, lo carga (todo o nada) y lo registra en el Historial de Cargas.</summary>
    [HttpPost("importar")]
    [Authorize(Policy = Politicas.Administrador)]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<ResultadoImportacion>> Importar(IFormFile? archivo, [FromQuery] bool validar = false)
    {
        if (archivo is null || archivo.Length == 0)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["archivo"] = ["Selecciona el archivo CSV."] }) { Title = "Selecciona el archivo CSV." });
        var nombre = Path.GetFileName(archivo.FileName);
        if (nombre.Length > 200) nombre = nombre[..200];

        var errores = new List<ErrorCargaDto>();
        List<string[]> filas = [];
        if (!nombre.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) errores.Add(new(0, "archivo", "El archivo debe ser .csv (en Excel: Guardar como › CSV UTF-8)."));
        else if (archivo.Length > 2 * 1024 * 1024) errores.Add(new(0, "archivo", "El archivo pesa más de 2 MB."));
        else
        {
            byte[] bytes;
            using (var ms = new MemoryStream()) { await archivo.CopyToAsync(ms); bytes = ms.ToArray(); }
            filas = Csv.Leer(Csv.Texto(bytes));
            var cab = filas.FirstOrDefault() ?? [];
            var esperado = ColumnasImportacion.Select(c => c.Columna).ToArray();
            if (cab.Length < esperado.Length || esperado.Where((c, i) => !string.Equals(cab[i].Trim(), c, StringComparison.OrdinalIgnoreCase)).Any())
                errores.Add(new(1, "columnas", $"Las columnas no coinciden con el template. Se esperaba: {string.Join("; ", esperado)}."));
            else if (filas.Count < 2) errores.Add(new(0, "archivo", "El archivo no tiene filas de datos."));
            else if (filas.Count - 1 > MaxFilasImportacion) errores.Add(new(0, "archivo", $"El archivo tiene más de {MaxFilasImportacion} filas; divídelo en varias cargas."));
        }
        if (errores.Count > 0) return await Registrar(nombre, 0, 0, errores, validar, "Fallida");

        // Validación fila a fila (la fila 1 es la cabecera).
        var datos = filas.Skip(1).Select((f, i) => (Fila: i + 2, Celdas: f)).Where(x => x.Celdas.Any(c => c.Trim().Length > 0)).ToList();
        var porCampo = ColumnasImportacion.ToDictionary(c => c.Campo, c => c.Columna);
        var guardar = new List<(int Fila, ColaboradorGuardar P)>();
        var docs = new Dictionary<string, int>();
        foreach (var (fila, celdas) in datos)
        {
            string V(int i) => i < celdas.Length ? celdas[i].Trim() : "";
            var p = new ColaboradorGuardar
            {
                Apellidos = V(0), Nombres = V(1), Documento = V(3), Cargo = V(4), Division = V(5), Area = V(6), SubArea = V(7), Ingreso = V(8),
                Detalle = new DetalleDto { TipoDocumento = V(2).Length > 0 ? V(2) : "DNI", EmailCorporativo = V(11), Telefono = V(12), Nacimiento = V(13), Supervisor = V(14) },
            };
            var filaErr = new List<ErrorCargaDto>();
            if (Monto(V(9)) is { } neto) p.SueldoNeto = neto; else if (V(9).Length > 0) filaErr.Add(new(fila, porCampo["sueldoNeto"], $"“{V(9)}” no es un número."));
            if (V(10).Length > 0) { if (Monto(V(10)) is { } b) p.Detalle.SueldoBase = b; else filaErr.Add(new(fila, porCampo["sueldoBase"], $"“{V(10)}” no es un número.")); }
            foreach (var (campo, msgs) in await Validar(p, null))
                if (!filaErr.Any(e => e.Campo == porCampo.GetValueOrDefault(campo, campo)))
                    filaErr.Add(new(fila, porCampo.GetValueOrDefault(campo, campo), msgs[0]));
            if (V(14).Length > 0 && await ResolverJefe(p) is null) filaErr.Add(new(fila, porCampo["jefeId"], $"No hay un colaborador registrado llamado “{V(14)}”."));
            if (p.Documento is { Length: > 0 } d)
            {
                if (docs.TryGetValue(d, out var otra)) filaErr.Add(new(fila, porCampo["documento"], $"El documento {d} se repite en la fila {otra}."));
                else docs[d] = fila;
            }
            errores.AddRange(filaErr);
            if (filaErr.Count == 0) guardar.Add((fila, p));
        }
        if (errores.Count > 0 || validar) return await Registrar(nombre, datos.Count, 0, errores, validar, errores.Count > 0 ? "Con errores" : "Exitosa");

        await using var tx = await db.Database.BeginTransactionAsync();
        var id = (await db.Colaboradores.MaxAsync(x => (int?)x.Id) ?? -1) + 1;
        foreach (var (_, p) in guardar)
        {
            var c = new Colaborador { Id = id++ };
            await Aplicar(c, p);
            db.Colaboradores.Add(c);
            auditoria.Registrar($"Colaborador #{c.Id}", "Crear", c, actual: $"Importador de colaboradores ({nombre})", origen: "Importador");
        }
        var r = await Registrar(nombre, datos.Count, guardar.Count, errores, false, "Exitosa");
        await tx.CommitAsync();
        return r;
    }

    private async Task<ActionResult<ResultadoImportacion>> Registrar(string archivo, int filas, int creados, List<ErrorCargaDto> errores, bool validar, string estado)
    {
        int? cargaId = null;
        if (!validar)
        {
            var carga = new CargaImportacion
            {
                Importador = ImportadorColaboradores, Archivo = archivo, Usuario = User.Nombre(), Registros = creados, Estado = estado,
                Errores = errores.Take(500).Select(e => new ErrorCarga { Fila = e.Fila, Campo = e.Campo, Mensaje = e.Mensaje }).ToList(),
            };
            db.Cargas.Add(carga);
            await db.SaveChangesAsync();
            cargaId = carga.Id;
        }
        return new ResultadoImportacion(archivo, filas, creados, errores, cargaId, estado);
    }

    /// <summary>Acepta 3500, 3500.50, 3,500.50 o 3500,50 (con o sin “S/”).</summary>
    private static decimal? Monto(string s)
    {
        var x = s.Replace("S/", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "");
        if (x.Length == 0) return null;
        if (x.Contains(',') && x.Contains('.')) x = x.Replace(",", "");
        else if (x.Contains(',')) x = x.Replace(',', '.');
        return decimal.TryParse(x, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
