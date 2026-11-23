using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;

namespace NexoRh.Api.Controllers;

public record ModuloPlan(string Codigo, string Nombre, bool Incluido, decimal PrecioAdicional);
public record PlanConsola(string Codigo, string Nombre, decimal PrecioPorColaborador, int LimiteEmpresas, int LimiteAlmacenamientoGb, int LimiteAdmins, int LimiteApiPorMinuto,
    List<string> Funciones, List<ModuloPlan> Modulos, int Tenants);
public record PlanGuardar(string Codigo, string Nombre, decimal PrecioPorColaborador, int LimiteEmpresas, int LimiteAlmacenamientoGb, int LimiteAdmins, int LimiteApiPorMinuto,
    List<string>? ModulosIncluidos, List<string>? Funciones);
public record ModuloPrecio(string Nombre, string Descripcion, decimal PrecioPorColaborador, List<string>? Beneficios);
public record CatalogoConsola(string Clave, string Nombre, string Columnas, string? Version, int Registros, DateTime? PublicadaEn, DateOnly? VigenteDesde, int Versiones, string Estado);
public record VersionPublicada(string Version, int Registros, int Agregados, int Modificados, int Eliminados);
public record AnuncioConsola(int Id, string Titulo, string Cuerpo, string Tipo, List<string> Planes, List<string> Modulos, DateTime? Desde, DateTime? Hasta, string Canal, string Estado, int Alcance);
public record AnuncioGuardar(string Titulo, string Cuerpo, string Tipo, List<string>? Planes, List<string>? Modulos, DateTime? Desde, DateTime? Hasta, string Canal, bool Publicar);
public record FlagTenant(Guid TenantId, string Slug, bool Activo, bool ModuloContratado);
public record FlagConsola(string Clave, string Descripcion, string? Modulo, List<FlagTenant> Tenants, int Activos);
public record FlagNuevo(string Clave, string Descripcion, string? Modulo);
public record FlagCambio(bool Activo);
public record BaseDatosSalud(string Nombre, string Tipo, int Tenants, string Esquema, int Pendientes, string Estado);
public record TenantSalud(Guid TenantId, string Nombre, double Rpm, double P95Ms, double PctErrores);
public record SaludPlataforma(DateTime Desde, double MinutosEnLinea, double P95Ms, int Requests, List<BaseDatosSalud> BasesDatos, List<TenantSalud> Tenants, List<object> TrabajosFallidos);

/// <summary>P-SA.6 Planes y precios · P-SA.7 Catálogos globales · P-SA.8 Novedades · P-SA.9 Feature flags · P-SA.10 Salud.</summary>
[ApiController]
[Route("api/platform")]
[SoloConsola]
[Authorize(Policy = Politicas.Plataforma)]
public class PlataformaConfigController(CatalogoDbContext catalogo, AuditoriaConsola auditoria, Suscripciones suscripciones, MetricasTenants metricas,
    IServiceScopeFactory scopes) : ControllerBase
{
    // --- P-SA.6 Planes y precios -------------------------------------------------------------------------------------

    [HttpGet("plans")]
    public async Task<List<PlanConsola>> Planes()
    {
        var modulos = await catalogo.ModulosComerciales.AsNoTracking().OrderBy(m => m.Orden).ToListAsync();
        var conteo = await catalogo.Tenants.AsNoTracking().Where(t => t.Estado != EstadosTenant.Eliminado && t.Estado != EstadosTenant.Cancelado)
            .GroupBy(t => t.PlanCodigo).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        return (await catalogo.Planes.AsNoTracking().OrderBy(p => p.Orden).ToListAsync()).Select(p =>
        {
            var inc = Modulos.Lista(p.ModulosIncluidos);
            return new PlanConsola(p.Codigo, p.Nombre, p.PrecioPorColaborador, p.LimiteEmpresas, p.LimiteAlmacenamientoGb, p.LimiteAdmins, p.LimiteApiPorMinuto,
                [.. Modulos.Lista(p.Funciones)], modulos.Select(m => new ModuloPlan(m.Codigo, m.Nombre, inc.Contains(m.Codigo), m.PrecioPorColaborador)).ToList(),
                conteo.FirstOrDefault(c => c.Key == p.Codigo)?.N ?? 0);
        }).ToList();
    }

    [HttpPost("plans")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> CrearPlan(PlanGuardar p)
    {
        var codigo = (p.Codigo ?? "").Trim().ToUpperInvariant();
        if (!Regex.IsMatch(codigo, "^[A-Z][A-Z0-9_]{1,19}$")) return Problem("El código usa mayúsculas, números y guion bajo (2 a 20).", statusCode: 400);
        if (await catalogo.Planes.AnyAsync(x => x.Codigo == codigo || x.Nombre == p.Nombre.Trim())) return Problem("Ya existe un plan con ese código o nombre.", statusCode: 409);
        var plan = new Plan { Codigo = codigo, Orden = (await catalogo.Planes.MaxAsync(x => (int?)x.Orden) ?? 0) + 1 };
        if (Aplicar(plan, p) is { } error) return error;
        catalogo.Planes.Add(plan);
        auditoria.Registrar("plan.created", detalle: $"{plan.Codigo} · S/ {plan.PrecioPorColaborador}");
        await catalogo.SaveChangesAsync();
        return Created($"/api/platform/plans/{codigo}", (await Planes()).First(x => x.Codigo == codigo));
    }

    /// <summary>Un cambio de precio aplica a altas y renovaciones; los tenants con precio pactado no cambian.</summary>
    [HttpPut("plans/{codigo}")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> GuardarPlan(string codigo, PlanGuardar p)
    {
        var plan = await catalogo.Planes.FirstOrDefaultAsync(x => x.Codigo == codigo);
        if (plan is null) return NotFound();
        if (await catalogo.Planes.AnyAsync(x => x.Codigo != codigo && x.Nombre == p.Nombre.Trim())) return Problem("Ya existe un plan con ese nombre.", statusCode: 409);
        var antes = $"S/ {plan.PrecioPorColaborador} · {plan.ModulosIncluidos}";
        if (Aplicar(plan, p) is { } error) return error;
        auditoria.Registrar("plan.updated", detalle: $"{plan.Codigo}: {antes} → S/ {plan.PrecioPorColaborador} · {plan.ModulosIncluidos}");
        await catalogo.SaveChangesAsync();
        OlvidarTodos();
        return Ok((await Planes()).First(x => x.Codigo == codigo));
    }

    [HttpDelete("plans/{codigo}")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> EliminarPlan(string codigo)
    {
        var plan = await catalogo.Planes.FirstOrDefaultAsync(x => x.Codigo == codigo);
        if (plan is null) return NotFound();
        if (await catalogo.Tenants.AnyAsync(t => t.PlanCodigo == codigo)) return Problem("El plan tiene tenants: cámbialos de plan antes de eliminarlo.", statusCode: 409);
        catalogo.Planes.Remove(plan);
        auditoria.Registrar("plan.deleted", detalle: codigo);
        await catalogo.SaveChangesAsync();
        return NoContent();
    }

    private ObjectResult? Aplicar(Plan plan, PlanGuardar p)
    {
        if (string.IsNullOrWhiteSpace(p.Nombre) || p.Nombre.Trim().Length > 60) return Problem("Ingresa el nombre del plan.", statusCode: 400);
        if (p.PrecioPorColaborador <= 0) return Problem("El precio por colaborador debe ser mayor que cero.", statusCode: 400);
        if (new[] { p.LimiteEmpresas, p.LimiteAlmacenamientoGb, p.LimiteAdmins, p.LimiteApiPorMinuto }.Any(x => x < 0)) return Problem("Los límites no pueden ser negativos.", statusCode: 400);
        var validos = Modulos.Catalogo.Select(m => m.Codigo).ToHashSet();
        var incluidos = (p.ModulosIncluidos ?? []).Distinct().ToList();
        if (incluidos.Any(m => !validos.Contains(m))) return Problem("Hay un módulo que no existe.", statusCode: 400);
        (plan.Nombre, plan.PrecioPorColaborador, plan.LimiteEmpresas, plan.LimiteAlmacenamientoGb, plan.LimiteAdmins, plan.LimiteApiPorMinuto) =
            (p.Nombre.Trim(), p.PrecioPorColaborador, p.LimiteEmpresas, p.LimiteAlmacenamientoGb, p.LimiteAdmins, p.LimiteApiPorMinuto);
        plan.ModulosIncluidos = string.Join(",", incluidos);
        plan.Funciones = string.Join(",", (p.Funciones ?? []).Select(f => f.Trim().ToUpperInvariant()).Where(f => f.Length > 0).Distinct());
        return null;
    }

    [HttpPut("modules/{codigo}")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> GuardarModulo(string codigo, ModuloPrecio p)
    {
        var m = await catalogo.ModulosComerciales.FirstOrDefaultAsync(x => x.Codigo == codigo);
        if (m is null) return NotFound();
        if (string.IsNullOrWhiteSpace(p.Nombre) || p.PrecioPorColaborador < 0) return Problem("Revisa el nombre y el precio del módulo.", statusCode: 400);
        var antes = m.PrecioPorColaborador;
        (m.Nombre, m.Descripcion, m.PrecioPorColaborador) = (p.Nombre.Trim(), (p.Descripcion ?? "").Trim(), p.PrecioPorColaborador);
        if (p.Beneficios is { } b) m.Beneficios = string.Join("|", b.Select(x => x.Trim()).Where(x => x.Length > 0));
        auditoria.Registrar("module.updated", detalle: $"{codigo}: S/ {antes} → S/ {m.PrecioPorColaborador}");
        await catalogo.SaveChangesAsync();
        return Ok(new { m.Codigo, m.Nombre, m.PrecioPorColaborador });
    }

    // --- P-SA.7 Catálogos globales ------------------------------------------------------------------------------------

    [HttpGet("catalogs")]
    public async Task<List<CatalogoConsola>> Catalogos()
    {
        var versiones = await catalogo.VersionesCatalogo.AsNoTracking().Select(v => new { v.Clave, v.Version, v.Registros, v.PublicadaEn, v.VigenteDesde }).ToListAsync();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return (await catalogo.CatalogosGlobales.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync()).Select(c =>
        {
            var vs = versiones.Where(v => v.Clave == c.Clave).OrderByDescending(v => v.VigenteDesde).ThenByDescending(v => v.PublicadaEn).ToList();
            var vigente = vs.FirstOrDefault(v => v.VigenteDesde <= hoy);
            var ultima = vs.FirstOrDefault();
            return new CatalogoConsola(c.Clave, c.Nombre, c.Columnas, ultima?.Version, ultima?.Registros ?? 0, ultima?.PublicadaEn, ultima?.VigenteDesde, vs.Count,
                ultima is null ? "Sin publicar" : vigente is null || ultima.VigenteDesde > hoy ? "Programada" : "Publicada");
        }).ToList();
    }

    /// <summary>Publica una versión inmutable desde un CSV con las columnas esperadas; devuelve las diferencias con la anterior.</summary>
    [HttpPost("catalogs/{clave}/versions")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<VersionPublicada>> Publicar(string clave, [FromForm] string? version, [FromForm] DateOnly? vigenteDesde, [FromForm] IFormFile? archivo, [FromForm] string? notas)
    {
        var c = await catalogo.CatalogosGlobales.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave);
        if (c is null) return NotFound();
        var v = (version ?? "").Trim();
        if (!Regex.IsMatch(v, "^v\\d{4}\\.\\d{1,2}$")) return Problem("La versión usa el formato vAAAA.N, p. ej. v2027.1.", statusCode: 400);
        if (vigenteDesde is null) return Problem("Indica la fecha de vigencia.", statusCode: 400);
        if (archivo is null || archivo.Length == 0) return Problem("Adjunta el archivo CSV.", statusCode: 400);
        if (await catalogo.VersionesCatalogo.AnyAsync(x => x.Clave == clave && x.Version == v)) return Problem("Esa versión ya existe.", statusCode: 409);
        using var lector = new StreamReader(archivo.OpenReadStream(), System.Text.Encoding.UTF8);
        var lineas = (await lector.ReadToEndAsync()).Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var esperadas = c.Columnas.Split(',');
        if (lineas.Count < 2 || !lineas[0].Split(',', ';').Select(x => x.Trim().ToLowerInvariant()).SequenceEqual(esperadas))
            return Problem($"El archivo debe tener encabezado y estas columnas: {c.Columnas}.", statusCode: 400);
        var anterior = await catalogo.VersionesCatalogo.AsNoTracking().Where(x => x.Clave == clave).OrderByDescending(x => x.PublicadaEn).Select(x => x.Contenido).FirstOrDefaultAsync();
        var filas = lineas.Skip(1).ToList();
        string Llave(string l) => l.Split(',', ';')[0].Trim();
        var previas = (anterior ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToDictionary(Llave, l => l);
        var nuevas = filas.GroupBy(Llave).ToDictionary(g => g.Key, g => g.Last());
        var agregados = nuevas.Keys.Count(k => !previas.ContainsKey(k));
        var modificados = nuevas.Count(kv => previas.TryGetValue(kv.Key, out var antes) && antes != kv.Value);
        var eliminados = previas.Keys.Count(k => !nuevas.ContainsKey(k));
        catalogo.VersionesCatalogo.Add(new VersionCatalogo { Clave = clave, Version = v, VigenteDesde = vigenteDesde.Value, Registros = filas.Count, Notas = notas?.Trim(),
            Contenido = string.Join("\n", lineas), PublicadaPor = User.Identity?.Name ?? "—" });
        auditoria.Registrar("catalog.version.published", detalle: $"{clave} {v} · {filas.Count} registros · +{agregados} ~{modificados} -{eliminados}");
        await catalogo.SaveChangesAsync();
        return Created($"/api/platform/catalogs/{clave}/versions/{v}", new VersionPublicada(v, filas.Count, agregados, modificados, eliminados));
    }

    // --- P-SA.8 Novedades --------------------------------------------------------------------------------------------

    [HttpGet("announcements")]
    public async Task<List<AnuncioConsola>> Anuncios()
    {
        var tenants = await catalogo.Tenants.AsNoTracking().Where(t => t.Estado != EstadosTenant.Eliminado && t.Estado != EstadosTenant.Cancelado).ToListAsync();
        var ahora = DateTime.UtcNow;
        var lista = await catalogo.Anuncios.AsNoTracking().OrderByDescending(a => a.Id).ToListAsync();
        var r = new List<AnuncioConsola>();
        foreach (var a in lista) r.Add(await Dto(a, tenants, ahora));
        return r;
    }

    private async Task<AnuncioConsola> Dto(Anuncio a, List<Tenant> tenants, DateTime ahora)
    {
        var alcance = 0;
        foreach (var t in tenants) if (Aplica(a, t.PlanCodigo, await suscripciones.DeAsync(t.Id))) alcance++;
        return new AnuncioConsola(a.Id, a.Titulo, a.Cuerpo, a.Tipo, [.. Modulos.Lista(a.Planes)], [.. Modulos.Lista(a.Modulos)], a.Desde, a.Hasta, a.Canal, a.Estado(ahora), alcance);
    }

    /// <summary>Segmentación: plan dentro de la lista (o todos) y al menos uno de los módulos (o todos).</summary>
    public static bool Aplica(Anuncio a, string plan, Suscripcion s)
    {
        var planes = Modulos.Lista(a.Planes);
        var modulos = Modulos.Lista(a.Modulos);
        return (planes.Count == 0 || planes.Contains(plan)) && (modulos.Count == 0 || modulos.Overlaps(s.Modulos));
    }

    [HttpPost("announcements")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> CrearAnuncio(AnuncioGuardar p)
    {
        var a = new Anuncio { CreadoPor = User.Identity?.Name ?? "—" };
        if (Aplicar(a, p) is { } error) return error;
        catalogo.Anuncios.Add(a);
        auditoria.Registrar(p.Publicar ? "announcement.published" : "announcement.created", detalle: a.Titulo);
        await catalogo.SaveChangesAsync();
        return Created($"/api/platform/announcements/{a.Id}", (await Anuncios()).First(x => x.Id == a.Id));
    }

    [HttpPut("announcements/{id:int}")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> GuardarAnuncio(int id, AnuncioGuardar p)
    {
        var a = await catalogo.Anuncios.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (a.Estado(DateTime.UtcNow) == "Publicada" && p.Publicar) return Problem("El anuncio ya está publicado: despublícalo para editarlo.", statusCode: 409);
        if (Aplicar(a, p) is { } error) return error;
        auditoria.Registrar(p.Publicar ? "announcement.published" : "announcement.updated", detalle: a.Titulo);
        await catalogo.SaveChangesAsync();
        return Ok((await Anuncios()).First(x => x.Id == id));
    }

    [HttpDelete("announcements/{id:int}")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> EliminarAnuncio(int id)
    {
        var a = await catalogo.Anuncios.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        catalogo.Anuncios.Remove(a);
        auditoria.Registrar("announcement.deleted", detalle: a.Titulo);
        await catalogo.SaveChangesAsync();
        return NoContent();
    }

    private ObjectResult? Aplicar(Anuncio a, AnuncioGuardar p)
    {
        var titulo = (p.Titulo ?? "").Trim();
        var cuerpo = (p.Cuerpo ?? "").Trim();
        if (titulo.Length is 0 or > 80) return Problem("El título es obligatorio (máximo 80 caracteres).", statusCode: 400);
        if (cuerpo.Length is 0 or > 2000) return Problem("El contenido es obligatorio (máximo 2,000 caracteres).", statusCode: 400);
        if (!TiposAnuncio.Todos.Contains(p.Tipo)) return Problem("Elige el tipo de anuncio.", statusCode: 400);
        if (p.Canal is not ("banner" or "modal" or "banner_correo")) return Problem("Elige el canal.", statusCode: 400);
        if (p.Desde is { } d && p.Hasta is { } h && h <= d) return Problem("La fecha de fin debe ser posterior a la de inicio.", statusCode: 400);
        (a.Titulo, a.Cuerpo, a.Tipo, a.Canal, a.Desde, a.Hasta, a.Publicado) = (titulo, cuerpo, p.Tipo, p.Canal, p.Desde, p.Hasta, p.Publicar);
        a.Planes = string.Join(",", p.Planes ?? []);
        a.Modulos = string.Join(",", p.Modulos ?? []);
        a.ActualizadoEn = DateTime.UtcNow;
        return null;
    }

    // --- P-SA.9 Feature flags ----------------------------------------------------------------------------------------

    [HttpGet("feature-flags")]
    public async Task<List<FlagConsola>> Flags()
    {
        var tenants = await catalogo.Tenants.AsNoTracking().Where(t => t.Estado != EstadosTenant.Eliminado && t.Estado != EstadosTenant.Cancelado).OrderBy(t => t.Slug).ToListAsync();
        var filas = await catalogo.Flags.AsNoTracking().ToListAsync();
        var subs = new Dictionary<Guid, Suscripcion>();
        foreach (var t in tenants) subs[t.Id] = await suscripciones.DeAsync(t.Id);
        return (await catalogo.DefinicionesFlag.AsNoTracking().OrderBy(f => f.Clave).ToListAsync()).Select(f =>
        {
            var ts = tenants.Select(t => new FlagTenant(t.Id, t.Slug, filas.Any(x => x.TenantId == t.Id && x.Flag == f.Clave && x.Valor),
                f.Modulo is null || subs[t.Id].Tiene(f.Modulo))).ToList();
            return new FlagConsola(f.Clave, f.Descripcion, f.Modulo, ts, ts.Count(x => x.Activo));
        }).ToList();
    }

    [HttpPost("feature-flags")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> NuevoFlag(FlagNuevo p)
    {
        var clave = (p.Clave ?? "").Trim();
        if (!Regex.IsMatch(clave, "^[a-z][a-z0-9_]{2,39}$")) return Problem("La clave usa snake_case (letras minúsculas, números y _).", statusCode: 400);
        if (string.IsNullOrWhiteSpace(p.Descripcion)) return Problem("Describe el flag.", statusCode: 400);
        if (p.Modulo is { Length: > 0 } m && !Modulos.Catalogo.Any(x => x.Codigo == m)) return Problem("Ese módulo no existe.", statusCode: 400);
        if (await catalogo.DefinicionesFlag.AnyAsync(f => f.Clave == clave)) return Problem("Ya existe un flag con esa clave.", statusCode: 409);
        catalogo.DefinicionesFlag.Add(new DefinicionFlag { Clave = clave, Descripcion = p.Descripcion.Trim(), Modulo = string.IsNullOrWhiteSpace(p.Modulo) ? null : p.Modulo });
        auditoria.Registrar("feature_flag.created", detalle: clave);
        await catalogo.SaveChangesAsync();
        return Created($"/api/platform/feature-flags/{clave}", (await Flags()).First(f => f.Clave == clave));
    }

    /// <summary>Activa o apaga un flag para un tenant (efectivo en ≤ 60 s, solo si tiene el módulo dueño).</summary>
    [HttpPut("feature-flags/{clave}/tenants/{tenantId:guid}")]
    [Authorize(Policy = Politicas.PlataformaAdmin)]
    public async Task<IActionResult> CambiarFlag(string clave, Guid tenantId, FlagCambio p)
    {
        var f = await catalogo.DefinicionesFlag.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave);
        if (f is null || !await catalogo.Tenants.AnyAsync(t => t.Id == tenantId)) return NotFound();
        if (p.Activo && f.Modulo is not null && !(await suscripciones.DeAsync(tenantId)).Tiene(f.Modulo))
            return Problem("El tenant no tiene contratado el módulo de este flag.", statusCode: 422);
        var fila = await catalogo.Flags.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Flag == clave);
        if (p.Activo && fila is null) catalogo.Flags.Add(new TenantFlag { TenantId = tenantId, Flag = clave, Valor = true });
        else if (!p.Activo && fila is not null) catalogo.Flags.Remove(fila);
        auditoria.Registrar("feature_flag.toggled", tenantId, $"{clave} = {(p.Activo ? "on" : "off")}");
        await catalogo.SaveChangesAsync();
        suscripciones.Olvidar(tenantId);
        return Ok(new { clave, tenantId, activo = p.Activo });
    }

    // --- P-SA.10 Salud -----------------------------------------------------------------------------------------------

    [HttpGet("health")]
    public async Task<SaludPlataforma> Salud()
    {
        var tenants = await catalogo.Tenants.AsNoTracking().ToListAsync();
        var bases = new List<BaseDatosSalud>();
        using (var scope = scopes.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ContextoTenant>().ActivarModoPlataforma();
            var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
            bases.Add(await Base(db, "Negocio (compartida)", "Compartida", tenants.Count(t => t.CadenaConexion is null)));
        }
        var aplicadas = (await catalogo.Database.GetAppliedMigrationsAsync()).ToList();
        var pendientesCat = (await catalogo.Database.GetPendingMigrationsAsync()).Count();
        bases.Insert(0, new BaseDatosSalud($"Catálogo ({catalogo.Database.GetDbConnection().Database})", "Catálogo", tenants.Count, Corta(aplicadas.LastOrDefault()), pendientesCat, pendientesCat == 0 ? "Al día" : "Pendiente"));
        foreach (var t in tenants.Where(t => t.CadenaConexion is not null))
        {
            using var scope = scopes.CreateScope();
            scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(t);
            bases.Add(await Base(scope.ServiceProvider.GetRequiredService<NexoDbContext>(), $"Dedicada · {t.Slug}", "Dedicada", 1));
        }
        var nombres = tenants.ToDictionary(t => t.Id, t => t.Nombre);
        var resumen = metricas.Resumen().OrderByDescending(x => x.Rpm).ToList();
        var total = resumen.Sum(x => x.Total);
        var p95 = total == 0 ? 0 : Math.Round(resumen.Sum(x => x.P95 * x.Total) / total);
        return new SaludPlataforma(metricas.Inicio, Math.Round((DateTime.UtcNow - metricas.Inicio).TotalMinutes), p95, total, bases,
            resumen.Select(x => new TenantSalud(x.Tenant, nombres.GetValueOrDefault(x.Tenant) ?? x.Tenant.ToString(), x.Rpm, x.P95, x.PctErrores)).ToList(), []);
    }

    private static async Task<BaseDatosSalud> Base(NexoDbContext db, string nombre, string tipo, int tenants)
    {
        try
        {
            var aplicadas = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            var pendientes = (await db.Database.GetPendingMigrationsAsync()).Count();
            return new BaseDatosSalud($"{nombre} · {db.Database.GetDbConnection().Database}", tipo, tenants, Corta(aplicadas.LastOrDefault()), pendientes, pendientes == 0 ? "Al día" : "Pendiente");
        }
        catch { return new BaseDatosSalud(nombre, tipo, tenants, "—", 0, "Sin conexión"); }
    }

    private static string Corta(string? migracion) => migracion is null ? "—" : migracion.Length > 15 ? $"{migracion[..8]} · {migracion[15..]}" : migracion;

    private void OlvidarTodos()
    {
        foreach (var id in catalogo.Tenants.AsNoTracking().Select(t => t.Id).ToList()) suscripciones.Olvidar(id);
    }
}
