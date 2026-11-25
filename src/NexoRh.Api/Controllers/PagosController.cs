using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

public record TarjetaDto(string Marca, string Tarjeta, string Titular, string Modo, string RegistradaEn, string AutorizadoPor);
public record CobroDto(int Id, string Factura, decimal Monto, string Origen, string Estado, string? Mensaje, string? CodigoAutorizacion, string Tarjeta, string Fecha, string Actor, string OrderNumber);
public record PagosCuenta(string Modo, bool Configurado, TarjetaDto? Tarjeta, List<CobroDto> Cobros, string TextoAutorizacion);
/// <summary>Respuesta del formulario de Izipay (payloadHttp y signature tal cual) o, en modo simulado, la tarjeta de prueba elegida.</summary>
public record RegistroTarjeta(bool Autorizo, string? PayloadHttp, string? Signature, string? Simulada);

/// <summary>⚙ Administración › Estado de cuenta: tarjeta para el cobro automático, cobros y “Pagar ahora”.</summary>
[ApiController]
[Authorize(Policy = Politicas.Administrador)]
[Route("api/cuenta")]
public class PagosController(CatalogoDbContext catalogo, NexoDbContext db, ContextoTenant tenant, PasarelaIzipay pasarela, TokensTarjeta tokens, CobrosFacturas cobros,
    CicloVidaTenants cicloVida, Auditoria auditoria) : ControllerBase
{
    public const string TextoAutorizacion = "Autorizo a Nexo RH a cargar automáticamente en esta tarjeta el importe de cada factura mensual de la suscripción "
        + "de mi empresa, según los términos del servicio. Puedo retirar la tarjeta en cualquier momento desde el Estado de cuenta.";
    private Guid TenantId => tenant.Id!.Value;

    [HttpGet("pagos")]
    public async Task<PagosCuenta> Ver()
    {
        var m = await catalogo.MediosPago.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Activo);
        var lista = await catalogo.Cobros.AsNoTracking().Where(c => c.TenantId == TenantId).OrderByDescending(c => c.CreadoEn).Take(50).ToListAsync();
        return new PagosCuenta(pasarela.Modo, pasarela.Configurado, m is null ? null : Dto(m), lista.Select(Dto).ToList(), TextoAutorizacion);
    }

    /// <summary>Abre una sesión de registro de tarjeta en Izipay (config del formulario, token de sesión y clave RSA).</summary>
    [HttpPost("pagos/sesion")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SesionRegistroTarjeta>> Sesion()
    {
        if (!pasarela.Configurado) return Problem("El cobro con tarjeta todavía no está disponible.", statusCode: 503);
        var t = await catalogo.Tenants.AsNoTracking().FirstAsync(x => x.Id == TenantId);
        var correo = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? t.CorreoFacturacion ?? "";
        try { return await pasarela.IniciarRegistroAsync(t, User.Identity?.Name ?? t.RazonSocial, correo); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        { return Problem("No se pudo conectar con Izipay. Inténtalo en unos minutos.", statusCode: 502); }
    }

    /// <summary>Guarda la tarjeta registrada (reemplaza la anterior y elimina su token en Izipay).</summary>
    [HttpPost("pagos/tarjeta")]
    public async Task<ActionResult<TarjetaDto>> Registrar(RegistroTarjeta p)
    {
        if (!p.Autorizo) return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["autorizo"] = ["Debes autorizar el cargo automático para registrar la tarjeta."] }));
        if (!pasarela.Configurado) return Problem("El cobro con tarjeta todavía no está disponible.", statusCode: 503);
        var r = pasarela.LeerRegistro(TenantId, p.PayloadHttp, p.Signature, p.Simulada, out var error);
        if (r is null) return Problem(error, statusCode: 400);
        var anterior = await catalogo.MediosPago.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Activo);
        if (anterior is not null) { anterior.Activo = false; anterior.RetiradoEn = DateTime.UtcNow; await catalogo.SaveChangesAsync(); }
        var m = new MedioPago
        {
            TenantId = TenantId, Modo = pasarela.Modo, TokenCifrado = tokens.Proteger(r.Token), CompradorId = r.CompradorId, Marca = r.Marca, Tarjeta = r.Tarjeta,
            Titular = r.Titular.Length > 120 ? r.Titular[..120] : r.Titular, Facturacion = r.Facturacion.Length > 2000 ? "{}" : r.Facturacion,
            AutorizadoPor = User.Firma(), AutorizacionIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
        };
        catalogo.MediosPago.Add(m);
        catalogo.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = User.Firma(), TenantId = TenantId, Accion = "payment_method.added",
            Detalle = $"{m.Marca} {CobrosFacturas.Ultimos(m.Tarjeta)} · cargo automático autorizado", Ip = m.AutorizacionIp });
        await catalogo.SaveChangesAsync();
        auditoria.RegistrarTexto("Medio de pago", "Crear", null, null, "tarjeta", anterior is null ? null : $"{anterior.Marca} {CobrosFacturas.Ultimos(anterior.Tarjeta)}", $"{m.Marca} {CobrosFacturas.Ultimos(m.Tarjeta)}");
        await db.SaveChangesAsync();
        if (anterior is not null && anterior.Modo == pasarela.Modo) await pasarela.EliminarTokenAsync(tokens.Leer(anterior.TokenCifrado), anterior.CompradorId);
        return Dto(m);
    }

    /// <summary>Retira la tarjeta: no habrá más cobros automáticos (las facturas se pagan con “Pagar ahora” tras registrar otra).</summary>
    [HttpDelete("pagos/tarjeta")]
    public async Task<IActionResult> Retirar()
    {
        var m = await catalogo.MediosPago.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Activo);
        if (m is null) return NotFound();
        m.Activo = false;
        m.RetiradoEn = DateTime.UtcNow;
        catalogo.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = User.Firma(), TenantId = TenantId, Accion = "payment_method.removed", Detalle = $"{m.Marca} {CobrosFacturas.Ultimos(m.Tarjeta)}" });
        await catalogo.SaveChangesAsync();
        auditoria.RegistrarTexto("Medio de pago", "Eliminar", null, null, "tarjeta", $"{m.Marca} {CobrosFacturas.Ultimos(m.Tarjeta)}", null);
        await db.SaveChangesAsync();
        if (m.Modo == pasarela.Modo) await pasarela.EliminarTokenAsync(tokens.Leer(m.TokenCifrado), m.CompradorId);
        return NoContent();
    }

    /// <summary>“Pagar ahora”: cobra una factura impaga con la tarjeta registrada y revisa el estado de la cuenta.</summary>
    [HttpPost("facturas/{numero}/pagar")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CobroDto>> Pagar(string numero)
    {
        var f = await catalogo.Facturas.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Numero == numero);
        if (f is null) return NotFound();
        if (f.Estado == "Pagada") return Problem("La factura ya está pagada.", statusCode: 409);
        if (!await catalogo.MediosPago.AnyAsync(x => x.TenantId == TenantId && x.Activo)) return Problem("Registra una tarjeta para pagar.", statusCode: 409);
        Cobro c;
        try { c = await cobros.CobrarAsync(f, OrigenesCobro.Cliente, User.Firma(), HttpContext.Connection.RemoteIpAddress?.ToString(), DateTime.UtcNow); }
        catch (InvalidOperationException e) { return Problem(e.Message, statusCode: 409); }
        if (c.Estado == EstadosCobro.Aprobado) await cicloVida.EjecutarAsync(DateTime.UtcNow, TenantId, User.Firma());
        return Dto(c);
    }

    public static TarjetaDto Dto(MedioPago m) => new(m.Marca, m.Tarjeta, m.Titular, m.Modo, Fechas.TextoHora(m.RegistradoEn), m.AutorizadoPor);
    public static CobroDto Dto(Cobro c) => new(c.Id, c.Factura, c.Monto, c.Origen, c.Estado, c.Mensaje, c.CodigoAutorizacion, c.Tarjeta, Fechas.TextoHora(c.CreadoEn), c.Actor, c.OrderNumber);
}

/// <summary>Notificaciones de Izipay (IPN): confirman cobros que quedaron por confirmar. Sin sesión; se valida la firma.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/publico/pagos/izipay")]
public class IpnIzipayController(CatalogoDbContext catalogo, PasarelaIzipay pasarela, CobrosFacturas cobros, CicloVidaTenants cicloVida, ILogger<IpnIzipayController> log) : ControllerBase
{
    [HttpPost("ipn")]
    public async Task<IActionResult> Notificar([FromBody] JsonElement cuerpo)
    {
        if (cuerpo.ValueKind != JsonValueKind.Object) return BadRequest();
        string? Texto(string campo) => cuerpo.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var payload = Texto("payloadHttp");
        var firma = Texto("signature") ?? Request.Headers["Signature"].ToString();
        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(firma) || !pasarela.FirmaValida(payload, firma))
        {
            log.LogWarning("IPN de Izipay con firma inválida.");
            return BadRequest();
        }
        JsonNode? datos;
        try { datos = JsonNode.Parse(payload); } catch (JsonException) { return BadRequest(); }
        var orden = datos?["response"]?["order"]?[0]?["orderNumber"]?.GetValue<string>();
        var cobro = orden is null ? null : await catalogo.Cobros.FirstOrDefaultAsync(c => c.OrderNumber == orden);
        // Registros de tarjeta y órdenes ajenas: se aceptan sin hacer nada.
        if (cobro is null || cobro.Estado is EstadosCobro.Aprobado or EstadosCobro.Rechazado) return Ok();
        var f = await catalogo.Facturas.FirstAsync(x => x.Id == cobro.FacturaId);
        // La firma de la notificación ya se validó: se interpreta el payload (que trae code, response y su propia firma omitida).
        var r = pasarela.Interpretar(new JsonObject { ["code"] = datos?["code"]?.DeepClone(), ["message"] = datos?["message"]?.DeepClone(), ["messageUser"] = datos?["messageUser"]?.DeepClone(), ["response"] = datos?["response"]?.DeepClone() });
        if (r.Estado == EstadosCobro.PorConfirmar) return Ok();
        await cobros.AplicarAsync(cobro, f, r, DateTime.UtcNow);
        if (r.Estado == EstadosCobro.Aprobado) await cicloVida.EjecutarAsync(DateTime.UtcNow, f.TenantId, "Izipay");
        return Ok();
    }
}
