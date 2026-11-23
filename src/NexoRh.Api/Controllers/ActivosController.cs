using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record ActivoDto(string Codigo, int ProductoId, string Producto, string Categoria, string Marca, string Estado, int? AsignadoId, string Asignado);
public record MovimientoDto(string Fecha, string Asignado, string Estado, string Comentario, string Admin);
public record OrdenDto(int Id, string Origen, int ProductoId, string Producto, string Categoria, int? ParaId, string Para, string Area, string Cargo, string Solicitante,
    string Solicitada, string AsignarEl, string Estado, string? ActivoCodigo, string Comentario);
public record CategoriaDto(int Id, string Nombre, int Productos);
public record ProductoDto(int Id, string Nombre, int CategoriaId, string Categoria, string Marca, int Activos);
public record DatosActivos(List<ActivoDto> Activos, List<OrdenDto> Ordenes, List<CategoriaDto> Categorias, List<ProductoDto> Productos);
public record CambioActivo(string? Estado, int? AsignadoId, string? Comentario);
public record ActivosNuevos(int ProductoId, int Cantidad, string? Comentario);
public record OrdenNueva(int ProductoId, int ParaId, string? AsignarEl, string? Comentario);
public record AsignarOrden(string? ActivoCodigo);
public record RechazarOrden(string? Comentario);
public record OrdenAtendida(string ActivoCodigo);
public record NombreDto(string? Nombre);
public record ProductoNuevo(string? Nombre, int CategoriaId, string? Marca);

/// <summary>
/// Gestión de Activos: inventario (cada cambio de estado o de asignación queda en el historial del activo), órdenes de
/// asignación (se atienden con una unidad disponible del producto) y configuración de categorías y productos.
/// </summary>
[ApiController]
[Route("api/activos")]
[RequiereModulo(Modulos.Assets)]
[Authorize(Policy = Politicas.Administrador)]
public class ActivosController(NexoDbContext db, Auditoria auditoria, RelojAsistencia reloj) : ControllerBase
{
    private static readonly string[] Estados = ["Disponible", "Asignado", "Mantenimiento", "Inactivo"];

    [HttpGet]
    public async Task<DatosActivos> Datos()
    {
        var cats = await db.CategoriasActivo.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync();
        var prods = await db.ProductosActivo.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();
        var activos = await db.Activos.AsNoTracking().OrderBy(a => a.Codigo).ToListAsync();
        var cols = await db.Colaboradores.AsNoTracking().ToDictionaryAsync(c => c.Id, c => $"{c.Apellidos}, {c.Nombres}");
        string Cat(int id) => cats.FirstOrDefault(c => c.Id == id)?.Nombre ?? "—";
        ProductoActivo P(int id) => prods.First(p => p.Id == id);
        var ordenes = await db.OrdenesActivo.AsNoTracking().OrderByDescending(o => o.Id).ToListAsync();
        return new DatosActivos(
            activos.Select(a => new ActivoDto(a.Codigo, a.ProductoId, P(a.ProductoId).Nombre, Cat(P(a.ProductoId).CategoriaId), P(a.ProductoId).Marca, a.Estado, a.AsignadoId,
                a.AsignadoId is { } x && cols.TryGetValue(x, out var n) ? n : "—")).ToList(),
            ordenes.Select(o => new OrdenDto(o.Id, o.Origen, o.ProductoId, P(o.ProductoId).Nombre, Cat(P(o.ProductoId).CategoriaId), o.ParaId, o.Para, o.Area, o.Cargo,
                o.Solicitante, Fechas.Texto(o.Solicitada), Fechas.Texto(o.AsignarEl), o.Estado == "Pendiente" && o.AsignarEl < reloj.Hoy ? "Atrasado" : o.Estado,
                o.ActivoCodigo, o.Comentario)).ToList(),
            cats.Select(c => new CategoriaDto(c.Id, c.Nombre, prods.Count(p => p.CategoriaId == c.Id))).ToList(),
            prods.Select(p => new ProductoDto(p.Id, p.Nombre, p.CategoriaId, Cat(p.CategoriaId), p.Marca, activos.Count(a => a.ProductoId == p.Id))).ToList());
    }

    [HttpGet("{codigo}/historial")]
    public async Task<List<MovimientoDto>> Historial(string codigo) =>
        (await db.MovimientosActivo.AsNoTracking().Where(m => m.ActivoCodigo == codigo).OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).ToListAsync())
            .Select(m => new MovimientoDto(Fechas.TextoHora(m.Fecha), m.Asignado, m.Estado, m.Comentario, m.Admin)).ToList();

    /// <summary>Cambia el estado o la asignación; "Asignado" exige un colaborador y los demás estados lo liberan.</summary>
    [HttpPut("{codigo}")]
    public async Task<IActionResult> Cambiar(string codigo, CambioActivo p)
    {
        var a = await db.Activos.Buscar(codigo);
        if (a is null) return NotFound();
        if (!Estados.Contains(p.Estado)) return Error("estado", "Estado no válido.");
        Colaborador? c = null;
        if (p.Estado == "Asignado")
        {
            if (p.AsignadoId is not { } id || (c = await db.Colaboradores.Buscar(id)) is null) return Error("asignadoId", "Elige al colaborador a quien se asigna.");
        }
        var comentario = Corto(p.Comentario, 300);
        await Mover(a, p.Estado!, c, comentario.Length > 0 ? comentario : "—");
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task Mover(Activo a, string estado, Colaborador? c, string comentario)
    {
        var antes = a.Estado;
        a.Estado = estado;
        a.AsignadoId = estado == "Asignado" ? c!.Id : null;
        var nombre = c is null ? "—" : $"{c.Apellidos}, {c.Nombres}";
        db.MovimientosActivo.Add(new MovimientoActivo { ActivoCodigo = a.Codigo, Asignado = nombre, Estado = estado, Comentario = comentario, Admin = User.Nombre() });
        auditoria.Registrar($"Activo {a.Codigo}", "Modificar", c, "estado", antes, estado == "Asignado" ? $"Asignado a {nombre}" : estado);
        await Task.CompletedTask;
    }

    /// <summary>Ingresa unidades nuevas de un producto (códigos correlativos ACT-0000).</summary>
    [HttpPost]
    public async Task<ActionResult<List<string>>> Ingresar(ActivosNuevos p)
    {
        if (!await db.ProductosActivo.AnyAsync(x => x.Id == p.ProductoId)) return Error("productoId", "El producto no existe.");
        if (p.Cantidad is < 1 or > 100) return Error("cantidad", "La cantidad debe estar entre 1 y 100.");
        var max = (await db.Activos.Select(a => a.Codigo).ToListAsync()).Select(c => int.TryParse(c.Replace("ACT-", ""), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        var codigos = new List<string>();
        for (var i = 1; i <= p.Cantidad; i++)
        {
            var codigo = $"ACT-{max + i:0000}";
            db.Activos.Add(new Activo { Codigo = codigo, ProductoId = p.ProductoId, Estado = "Disponible" });
            db.MovimientosActivo.Add(new MovimientoActivo { ActivoCodigo = codigo, Estado = "Disponible", Comentario = Corto(p.Comentario, 300) is { Length: > 0 } x ? x : "Ingreso al inventario.", Admin = User.Nombre() });
            codigos.Add(codigo);
        }
        auditoria.Registrar("Inventario de activos", "Crear", actual: $"{p.Cantidad} unidad(es): {string.Join(", ", codigos)}");
        await db.SaveChangesAsync();
        return codigos;
    }

    // --- Órdenes -----------------------------------------------------------------------------------------------------
    [HttpPost("ordenes")]
    public async Task<ActionResult<int>> CrearOrden(OrdenNueva p)
    {
        if (!await db.ProductosActivo.AnyAsync(x => x.Id == p.ProductoId)) return Error("productoId", "Elige el producto.");
        var c = await db.Colaboradores.Buscar(p.ParaId);
        if (c is null) return Error("paraId", "Elige al colaborador.");
        if (!Fechas.TryLeer(p.AsignarEl, out var f)) return Error("asignarEl", "La fecha debe tener el formato DD/MM/AAAA.");
        var o = new OrdenActivo
        {
            ProductoId = p.ProductoId, ParaId = c.Id, Para = $"{c.Apellidos}, {c.Nombres}", Area = c.Area, Cargo = c.Cargo, Solicitante = User.Nombre(),
            Solicitada = reloj.Hoy, AsignarEl = f, Comentario = Corto(p.Comentario, 300),
        };
        db.OrdenesActivo.Add(o);
        await db.SaveChangesAsync();
        auditoria.Registrar($"Orden de activo #{o.Id}", "Crear", c, actual: $"Asignar el {Fechas.Texto(f)}");
        await db.SaveChangesAsync();
        return o.Id;
    }

    /// <summary>Atiende la orden con la unidad indicada o con la primera disponible del producto.</summary>
    [HttpPost("ordenes/{id:int}/asignar")]
    public async Task<ActionResult<OrdenAtendida>> AtenderOrden(int id, AsignarOrden p)
    {
        var o = await db.OrdenesActivo.Buscar(id);
        if (o is null) return NotFound();
        if (o.Estado != "Pendiente") return Error("estado", $"La orden ya está {o.Estado.ToLowerInvariant()}.");
        var c = o.ParaId is { } pid ? await db.Colaboradores.Buscar(pid) : null;
        if (c is null) return Error("paraId", $"{o.Para} aún no está registrado como colaborador: créalo antes de entregarle el activo.");
        var unidad = p.ActivoCodigo is { Length: > 0 } cod
            ? await db.Activos.FirstOrDefaultAsync(a => a.Codigo == cod && a.ProductoId == o.ProductoId && a.Estado == "Disponible")
            : await db.Activos.Where(a => a.ProductoId == o.ProductoId && a.Estado == "Disponible").OrderBy(a => a.Codigo).FirstOrDefaultAsync();
        if (unidad is null)
        {
            var prod = await db.ProductosActivo.Buscar(o.ProductoId);
            return Error("activoCodigo", $"No hay unidades disponibles de “{prod?.Nombre}” en el inventario. La orden {o.Id} sigue pendiente.");
        }
        await Mover(unidad, "Asignado", c, $"Orden {o.Id} atendida.");
        o.Estado = "Asignada";
        o.ActivoCodigo = unidad.Codigo;
        auditoria.Registrar($"Orden de activo #{o.Id}", "Modificar", c, "estado", "Pendiente", $"Asignada ({unidad.Codigo})");
        await db.SaveChangesAsync();
        return new OrdenAtendida(unidad.Codigo);
    }

    [HttpPost("ordenes/{id:int}/rechazar")]
    public async Task<IActionResult> RechazarOrden(int id, RechazarOrden p)
    {
        var o = await db.OrdenesActivo.Buscar(id);
        if (o is null) return NotFound();
        if (o.Estado != "Pendiente") return Error("estado", $"La orden ya está {o.Estado.ToLowerInvariant()}.");
        var comentario = Corto(p.Comentario, 300);
        if (comentario.Length == 0) return Error("comentario", "Indica el motivo del rechazo.");
        o.Estado = "Rechazada";
        o.Comentario = comentario;
        var c = o.ParaId is { } pid ? await db.Colaboradores.Buscar(pid) : null;
        auditoria.Registrar($"Orden de activo #{o.Id}", "Modificar", c, "estado", "Pendiente", $"Rechazada · {comentario}");
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Configuración -----------------------------------------------------------------------------------------------
    [HttpPost("categorias")]
    public async Task<IActionResult> CrearCategoria(NombreDto p) => await GuardarCategoria(null, p);

    [HttpPut("categorias/{id:int}")]
    public async Task<IActionResult> ModificarCategoria(int id, NombreDto p) => await GuardarCategoria(id, p);

    private async Task<IActionResult> GuardarCategoria(int? id, NombreDto p)
    {
        var nombre = Corto(p.Nombre, 60);
        if (nombre.Length == 0) return Error("nombre", "Ingresa el nombre de la categoría.");
        if (await db.CategoriasActivo.AnyAsync(c => c.Nombre == nombre && c.Id != (id ?? 0))) return Error("nombre", "Ya existe una categoría con ese nombre.");
        if (id is { } i)
        {
            var c = await db.CategoriasActivo.Buscar(i);
            if (c is null) return NotFound();
            auditoria.Registrar($"Categoría de activos · {c.Nombre}", "Modificar", propiedad: "nombre", anterior: c.Nombre, actual: nombre);
            c.Nombre = nombre;
        }
        else
        {
            db.CategoriasActivo.Add(new CategoriaActivo { Nombre = nombre });
            auditoria.Registrar($"Categoría de activos · {nombre}", "Crear");
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("categorias/{id:int}")]
    public async Task<IActionResult> EliminarCategoria(int id)
    {
        var c = await db.CategoriasActivo.Buscar(id);
        if (c is null) return NotFound();
        var n = await db.ProductosActivo.CountAsync(p => p.CategoriaId == id);
        if (n > 0) return Error("id", $"No se puede eliminar “{c.Nombre}”: tiene {n} producto(s).");
        db.CategoriasActivo.Remove(c);
        auditoria.Registrar($"Categoría de activos · {c.Nombre}", "Eliminar");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("productos")]
    public async Task<IActionResult> CrearProducto(ProductoNuevo p)
    {
        var nombre = Corto(p.Nombre, 100);
        if (nombre.Length == 0) return Error("nombre", "Ingresa el nombre del producto.");
        if (await db.ProductosActivo.AnyAsync(x => x.Nombre == nombre)) return Error("nombre", "Ya existe un producto con ese nombre.");
        if (!await db.CategoriasActivo.AnyAsync(c => c.Id == p.CategoriaId)) return Error("categoriaId", "Elige la categoría.");
        db.ProductosActivo.Add(new ProductoActivo { Nombre = nombre, CategoriaId = p.CategoriaId, Marca = Corto(p.Marca, 60) });
        auditoria.Registrar($"Producto de activos · {nombre}", "Crear", actual: Corto(p.Marca, 60));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("productos/{id:int}")]
    public async Task<IActionResult> EliminarProducto(int id)
    {
        var p = await db.ProductosActivo.Buscar(id);
        if (p is null) return NotFound();
        var n = await db.Activos.CountAsync(a => a.ProductoId == id);
        if (n > 0) return Error("id", $"No se puede eliminar “{p.Nombre}”: tiene {n} activo(s) en el inventario.");
        if (await db.OrdenesActivo.AnyAsync(o => o.ProductoId == id)) return Error("id", $"No se puede eliminar “{p.Nombre}”: tiene órdenes registradas.");
        db.ProductosActivo.Remove(p);
        auditoria.Registrar($"Producto de activos · {p.Nombre}", "Eliminar");
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string Corto(string? s, int n) { var x = (s ?? "").Trim(); return x.Length > n ? x[..n] : x; }
    private ActionResult Error(string campo, string mensaje) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensaje] }) { Title = mensaje });
}
