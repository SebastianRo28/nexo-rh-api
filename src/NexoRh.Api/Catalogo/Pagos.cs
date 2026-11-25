using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Catalogo;

// Cobro automático de la suscripción con Izipay (API REST v1 de developers.izipay.pe):
// · el administrador registra su tarjeta en el formulario de Izipay (SDK web, action "register"): la tarjeta nunca pasa
//   por Nexo RH; solo se guarda el token (cifrado) con la marca y los últimos dígitos;
// · cada factura se cobra con ese token sin el cliente presente (action "pay_token", processType "mitrecurrent"), con
//   reintentos a los 3 y 7 días; si no se regulariza sigue el flujo de morosidad y suspensión del ciclo de vida;
// · Izipay confirma cada operación en la IPN (POST /api/publico/pagos/izipay/ipn), firmada con HMAC-SHA256.

/// <summary>Tarjeta registrada por un tenant para el cobro automático (solo el token de Izipay, cifrado).</summary>
public class MedioPago
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string Proveedor { get; set; } = "Izipay";
    /// <summary>Ambiente en que se registró (Simulado, Sandbox o Produccion): un token solo sirve en su ambiente.</summary>
    public string Modo { get; set; } = ModosPago.Simulado;
    /// <summary>cardToken de Izipay cifrado con Data Protection.</summary>
    public string TokenCifrado { get; set; } = "";
    /// <summary>merchantBuyerId: identificador del comprador en Izipay.</summary>
    public string CompradorId { get; set; } = "";
    /// <summary>VS, MC, AE o DN.</summary>
    public string Marca { get; set; } = "";
    /// <summary>Número enmascarado tal como lo devuelve Izipay (476134******0019).</summary>
    public string Tarjeta { get; set; } = "";
    public string Titular { get; set; } = "";
    /// <summary>Datos de facturación del tarjetahabiente (objeto billing de Izipay) para los cobros siguientes.</summary>
    public string Facturacion { get; set; } = "{}";
    public bool Activo { get; set; } = true;
    /// <summary>Autorización del cargo recurrente (requisito de las marcas para cobros sin el cliente presente).</summary>
    public string AutorizadoPor { get; set; } = "";
    public string? AutorizacionIp { get; set; }
    public DateTime RegistradoEn { get; set; } = DateTime.UtcNow;
    public DateTime? RetiradoEn { get; set; }
}

/// <summary>Intento de cobro de una factura con la tarjeta registrada.</summary>
public class Cobro
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int FacturaId { get; set; }
    public string Factura { get; set; } = "";
    public decimal Monto { get; set; }
    /// <summary>Automatico (ciclo de vida), Cliente (Pagar ahora en Estado de cuenta) o Consola.</summary>
    public string Origen { get; set; } = OrigenesCobro.Automatico;
    public string Estado { get; set; } = EstadosCobro.Procesando;
    public string OrderNumber { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string? Codigo { get; set; }
    public string? Mensaje { get; set; }
    public string? CodigoAutorizacion { get; set; }
    public string? Referencia { get; set; }
    public string Tarjeta { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ResueltoEn { get; set; }
}

public static class ModosPago { public const string Simulado = "Simulado", Sandbox = "Sandbox", Produccion = "Produccion"; }
public static class OrigenesCobro { public const string Automatico = "Automatico", Cliente = "Cliente", Consola = "Consola"; }
public static class EstadosCobro
{
    public const string Procesando = "Procesando", Aprobado = "Aprobado", Rechazado = "Rechazado", Error = "Error", PorConfirmar = "Por confirmar";
    /// <summary>Estados que impiden otro cobro de la misma factura (índice único filtrado).</summary>
    public static readonly string[] Bloquean = [Procesando, Aprobado, PorConfirmar];
}

/// <summary>Sección Izipay de appsettings. Las claves van en la configuración local o en variables de entorno, nunca en el repositorio.</summary>
public class OpcionesIzipay
{
    /// <summary>Simulado (sin Izipay: tarjetas de prueba internas), Sandbox o Produccion.</summary>
    public string Modo { get; set; } = ModosPago.Simulado;
    public string? CodigoComercio { get; set; }
    /// <summary>“Clave de API del nuevo botón de pagos” (publicKey del token de sesión).</summary>
    public string? ClaveApi { get; set; }
    /// <summary>“Clave Hash” para validar la firma de las respuestas y notificaciones.</summary>
    public string? ClaveHash { get; set; }
    /// <summary>“Clave pública izipay” (RSA, PEM o Base64) para cifrar el monto en la API de autorización.</summary>
    public string? ClavePublicaRsa { get; set; }
    /// <summary>Relleno del cifrado RSA: Pkcs1 (por defecto) u OaepSha1.</summary>
    public string RellenoRsa { get; set; } = "Pkcs1";
    public string? UrlApi { get; set; }
    public string? UrlCheckout { get; set; }
    /// <summary>URL pública de la IPN (https://api.tu-dominio/api/publico/pagos/izipay/ipn). Vacía: sin notificaciones.</summary>
    public string? UrlIpn { get; set; }
    /// <summary>IP que se informa al antifraude en los cobros sin el cliente presente.</summary>
    public string IpServidor { get; set; } = "127.0.0.1";
    /// <summary>userScoring del antifraude si el token de sesión no lo trae.</summary>
    public string UserScoring { get; set; } = "izipay_low";

    public bool EsSandbox => string.Equals(Modo, ModosPago.Sandbox, StringComparison.OrdinalIgnoreCase);
    public bool EsSimulado => !EsSandbox && !string.Equals(Modo, ModosPago.Produccion, StringComparison.OrdinalIgnoreCase);
    public string ModoNormal => EsSimulado ? ModosPago.Simulado : EsSandbox ? ModosPago.Sandbox : ModosPago.Produccion;
    public string Api => (UrlApi ?? (EsSandbox ? "https://sandbox-api-pw.izipay.pe" : "https://api-pw.izipay.pe")).TrimEnd('/');
    public string Checkout => UrlCheckout ?? (EsSandbox ? "https://sandbox-checkout.izipay.pe/payments/v1/js/index.js" : "https://checkout.izipay.pe/payments/v1/js/index.js");
    public bool Configurado => EsSimulado || !(string.IsNullOrWhiteSpace(CodigoComercio) || string.IsNullOrWhiteSpace(ClaveApi) || string.IsNullOrWhiteSpace(ClaveHash) || string.IsNullOrWhiteSpace(ClavePublicaRsa));
}

/// <summary>Lo que el navegador necesita para abrir el formulario de registro de Izipay (config, token de sesión y clave RSA).</summary>
public record SesionRegistroTarjeta(string Modo, string? Script, JsonObject? Config, string? Authorization, string? KeyRsa);
public record TarjetaRegistrada(string Token, string CompradorId, string Marca, string Tarjeta, string Titular, string Facturacion);
public record ResultadoCobro(string Estado, string? Codigo, string? Mensaje, string? CodigoAutorizacion, string? Referencia);

/// <summary>Cliente de la API de Izipay (o su simulación local cuando no hay credenciales).</summary>
public class PasarelaIzipay(HttpClient http, IOptions<OpcionesIzipay> opciones, IMemoryCache cache, IHostEnvironment entorno, ILogger<PasarelaIzipay> log)
{
    private readonly OpcionesIzipay o = opciones.Value;
    public string Modo => o.ModoNormal;
    public bool Configurado => o.Configurado && !(o.EsSimulado && entorno.IsProduction());

    /// <summary>Tarjetas de la simulación: “aprobada” cobra bien; “rechazada” siempre es rechazada por el banco.</summary>
    public const string SimuladaAprobada = "aprobada", SimuladaRechazada = "rechazada";

    private static string NuevoTransactionId() => $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{RandomNumberGenerator.GetInt32(100, 1000)}";
    /// <summary>Hora de Lima (UTC-5, sin horario de verano).</summary>
    private static DateTime Lima => DateTime.UtcNow.AddHours(-5);
    private static string Importe(decimal m) => m.ToString("0.00", CultureInfo.InvariantCulture);

    // --- Registro de tarjeta -----------------------------------------------------------------------------------------

    public async Task<SesionRegistroTarjeta> IniciarRegistroAsync(Tenant t, string nombre, string correo, CancellationToken ct = default)
    {
        if (o.EsSimulado) return new SesionRegistroTarjeta(ModosPago.Simulado, null, null, null, null);
        var transactionId = NuevoTransactionId();
        var orderNumber = $"R{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var comprador = $"NX{t.Id:N}"[..20];
        var token = await TokenSesionAsync(transactionId, orderNumber, 0, ct);
        var (nombres, apellidos) = Partir(nombre);
        var config = new JsonObject
        {
            ["transactionId"] = transactionId,
            ["action"] = "register",
            ["merchantCode"] = o.CodigoComercio,
            ["order"] = new JsonObject
            {
                ["orderNumber"] = orderNumber, ["currency"] = "PEN", ["amount"] = "0.00", ["processType"] = "AT",
                ["merchantBuyerId"] = comprador, ["dateTimeTransaction"] = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000).ToString(CultureInfo.InvariantCulture),
            },
            ["billing"] = new JsonObject
            {
                ["firstName"] = nombres, ["lastName"] = apellidos, ["email"] = correo, ["phoneNumber"] = "",
                ["street"] = Recortar(t.DireccionFiscal ?? "", 40), ["city"] = "Lima", ["state"] = "Lima", ["country"] = "PE", ["postalCode"] = "15000",
                ["documentType"] = "RUC", ["document"] = t.Ruc, ["companyName"] = Recortar(t.RazonSocial, 50),
            },
            ["render"] = new JsonObject { ["typeForm"] = "pop-up" },
        };
        if (!string.IsNullOrWhiteSpace(o.UrlIpn)) config["urlIPN"] = o.UrlIpn;
        // La respuesta del navegador solo se acepta para esta sesión y este tenant.
        cache.Set($"izipay-registro:{transactionId}", (t.Id, comprador), TimeSpan.FromMinutes(30));
        return new SesionRegistroTarjeta(o.ModoNormal, o.Checkout, config, token, ClaveRsaPem());
    }

    /// <summary>Valida la respuesta del formulario (firma y sesión) y extrae el token de la tarjeta.</summary>
    public TarjetaRegistrada? LeerRegistro(Guid tenantId, string? payloadHttp, string? signature, string? simulada, out string? error)
    {
        error = null;
        if (o.EsSimulado)
        {
            if (entorno.IsProduction()) { error = "El cobro con tarjeta no está configurado."; return null; }
            var rechazo = simulada == SimuladaRechazada;
            return new TarjetaRegistrada($"sim-{(rechazo ? SimuladaRechazada : SimuladaAprobada)}-{Guid.NewGuid():N}", $"NX{tenantId:N}"[..20], rechazo ? "MC" : "VS",
                rechazo ? "520474******1127" : "411111******1111", "Tarjeta de prueba", "{}");
        }
        if (string.IsNullOrEmpty(payloadHttp) || string.IsNullOrEmpty(signature) || !FirmaValida(payloadHttp, signature)) { error = "La respuesta de Izipay no tiene una firma válida."; return null; }
        JsonNode? p;
        try { p = JsonNode.Parse(payloadHttp); } catch (JsonException) { error = "La respuesta de Izipay no es válida."; return null; }
        var tx = Texto(p?["transactionId"]);
        if (!cache.TryGetValue($"izipay-registro:{tx}", out (Guid Tenant, string Comprador) sesion) || sesion.Tenant != tenantId)
        { error = "La sesión de registro venció. Vuelve a intentarlo."; return null; }
        if (Texto(p?["code"]) != "00") { error = Texto(p?["messageUser"]) ?? "Izipay no registró la tarjeta."; return null; }
        var r = p?["response"];
        var token = Texto(r?["token"]?["cardToken"]);
        if (string.IsNullOrEmpty(token)) { error = "Izipay no devolvió el token de la tarjeta."; return null; }
        cache.Remove($"izipay-registro:{tx}");
        var billing = r?["billing"] as JsonObject;
        var titular = $"{Texto(billing?["firstName"])} {Texto(billing?["lastName"])}".Trim();
        return new TarjetaRegistrada(token, Texto(r?["token"]?["merchantBuyerId"]) is { Length: > 0 } c ? c : sesion.Comprador, Texto(r?["card"]?["brand"]) ?? "",
            Texto(r?["card"]?["pan"]) ?? "", titular, billing?.ToJsonString() ?? "{}");
    }

    // --- Cobro sin el cliente presente -------------------------------------------------------------------------------

    public async Task<ResultadoCobro> CobrarAsync(string token, string compradorId, string facturacion, string orderNumber, string transactionId, decimal monto, string? ip, CancellationToken ct = default)
    {
        if (o.EsSimulado)
            return token.StartsWith($"sim-{SimuladaAprobada}-")
                ? new ResultadoCobro(EstadosCobro.Aprobado, "00", "Autorizado", RandomNumberGenerator.GetInt32(100000, 999999).ToString(), RandomNumberGenerator.GetInt32(1000000, 9999999).ToString())
                : new ResultadoCobro(EstadosCobro.Rechazado, "116", "Fondos insuficientes (tarjeta de prueba)", null, null);
        string sesion;
        try { sesion = await TokenSesionAsync(transactionId, orderNumber, monto, ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Sin token de sesión no se llegó a autorizar nada: se puede reintentar.
            log.LogWarning(e, "Izipay: no se obtuvo el token de sesión del cobro {Orden}.", orderNumber);
            return new ResultadoCobro(EstadosCobro.Error, null, "No se pudo conectar con Izipay.", null, null);
        }
        var billing = JsonNode.Parse(facturacion) as JsonObject ?? new JsonObject();
        var cuerpo = new JsonObject
        {
            ["action"] = "pay_token",
            ["merchantCode"] = o.CodigoComercio,
            ["facilitatorCode"] = "",
            ["language"] = "ESP",
            ["order"] = new JsonObject
            {
                ["orderNumber"] = orderNumber, ["currency"] = "PEN", ["amount"] = CifrarRsa(Importe(monto)), ["installments"] = "00", ["deferred"] = "0",
                ["payMethod"] = "card", ["channel"] = "ecommerce", ["processType"] = "mitrecurrent",
                ["datetimeTerminalTransaction"] = Lima.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), ["merchantBuyerId"] = compradorId,
            },
            ["token"] = new JsonObject { ["cardToken"] = token },
            ["billing"] = billing,
            ["antifraud"] = new JsonObject { ["clientIp"] = ip is { Length: >= 7 } ? ip : o.IpServidor, ["deviceFingerPrintId"] = transactionId, ["userScoring"] = Scoring(sesion) },
        };
        if (!string.IsNullOrWhiteSpace(o.UrlIpn)) cuerpo["urlIpn"] = o.UrlIpn;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{o.Api}/authorization/api/Process/Authorize") { Content = JsonContent(cuerpo) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion);
            req.Headers.Add("transactionId", transactionId);
            using var res = await http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return Interpretar(JsonNode.Parse(json));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            // La autorización pudo llegar al banco: queda por confirmar (la IPN o la consola la resuelven); no se reintenta sola.
            log.LogWarning(e, "Izipay: sin respuesta de la autorización {Orden}.", orderNumber);
            return new ResultadoCobro(EstadosCobro.PorConfirmar, null, "Izipay no respondió; se confirmará con su notificación.", null, null);
        }
    }

    /// <summary>Resultado de una respuesta o notificación de Izipay (code "00" y “Autorizado” = aprobado). Valida la firma.</summary>
    public ResultadoCobro Interpretar(JsonNode? r)
    {
        var codigo = Texto(r?["code"]);
        var payload = Texto(r?["payloadHttp"]);
        var firma = Texto(r?["signature"]);
        if (codigo is not ("021" or "COMMUNICATION_ERROR") && payload is not null && firma is not null && !FirmaValida(payload, firma))
            return new ResultadoCobro(EstadosCobro.PorConfirmar, codigo, "La respuesta de Izipay no tiene una firma válida.", null, null);
        var orden = r?["response"]?["order"]?[0];
        var estado = Texto(orden?["stateMessage"]) ?? "";
        var mensaje = Texto(r?["messageUser"]) ?? Texto(r?["message"]);
        return codigo == "00" && estado.StartsWith("Autoriz", StringComparison.OrdinalIgnoreCase)
            ? new ResultadoCobro(EstadosCobro.Aprobado, codigo, estado, Texto(orden?["codeAuth"]), Texto(orden?["referenceNumber"]) ?? Texto(orden?["uniqueId"]))
            : codigo is "021" or "COMMUNICATION_ERROR" ? new ResultadoCobro(EstadosCobro.PorConfirmar, codigo, mensaje, null, null)
            : new ResultadoCobro(EstadosCobro.Rechazado, codigo, mensaje ?? "Operación denegada.", null, null);
    }

    /// <summary>Elimina el token en Izipay (al retirar o reemplazar la tarjeta). Si falla, solo queda registrado.</summary>
    public async Task EliminarTokenAsync(string token, string compradorId, CancellationToken ct = default)
    {
        if (o.EsSimulado) return;
        var transactionId = NuevoTransactionId();
        try
        {
            var sesion = await TokenSesionAsync(transactionId, $"D{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", 0, ct);
            var cuerpo = new JsonObject
            {
                ["merchantCode"] = o.CodigoComercio, ["facilitatorCode"] = "", ["language"] = "ESP",
                ["datetimeTerminalTransaction"] = Lima.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                ["token"] = new JsonObject { ["cardToken"] = token }, ["buyer"] = new JsonObject { ["merchantBuyerId"] = compradorId },
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{o.Api}/tokenization/external/api/v1/tokens/delete") { Content = JsonContent(cuerpo) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion);
            req.Headers.Add("transactionId", transactionId);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) log.LogWarning("Izipay no eliminó el token de tarjeta ({Estado}).", (int)res.StatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            log.LogWarning(e, "Izipay: no se pudo eliminar el token de tarjeta.");
        }
    }

    /// <summary>signature = Base64(HMAC-SHA256(payloadHttp, Clave Hash)).</summary>
    public bool FirmaValida(string payload, string signature)
    {
        if (string.IsNullOrEmpty(o.ClaveHash)) return false;
        var calculada = HMACSHA256.HashData(Encoding.UTF8.GetBytes(o.ClaveHash), Encoding.UTF8.GetBytes(payload));
        try { return CryptographicOperations.FixedTimeEquals(calculada, Convert.FromBase64String(signature)); }
        catch (FormatException) { return false; }
    }

    // --- Apoyo -------------------------------------------------------------------------------------------------------

    private async Task<string> TokenSesionAsync(string transactionId, string orderNumber, decimal monto, CancellationToken ct)
    {
        var cuerpo = new JsonObject { ["requestSource"] = "ECOMMERCE", ["merchantCode"] = o.CodigoComercio, ["orderNumber"] = orderNumber, ["publicKey"] = o.ClaveApi, ["amount"] = Importe(monto) };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{o.Api}/security/v1/Token/Generate") { Content = JsonContent(cuerpo) };
        req.Headers.Add("transactionId", transactionId);
        using var res = await http.SendAsync(req, ct);
        var r = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct));
        return Texto(r?["response"]?["token"]) is { Length: > 0 } t && Texto(r?["code"]) == "00"
            ? t : throw new InvalidOperationException($"Izipay no generó el token de sesión: {Texto(r?["message"])}");
    }

    /// <summary>userScoring del antifraude: viene en el token de sesión (JWT) si Izipay lo incluye.</summary>
    private string Scoring(string jwt)
    {
        try
        {
            var parte = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
            var datos = JsonNode.Parse(Convert.FromBase64String(parte.PadRight(parte.Length + (4 - parte.Length % 4) % 4, '='))) as JsonObject;
            var s = datos?.FirstOrDefault(kv => kv.Key.Contains("scoring", StringComparison.OrdinalIgnoreCase)).Value;
            return Texto(s) is { Length: > 0 } v ? v : o.UserScoring;
        }
        catch (Exception e) when (e is FormatException or JsonException or IndexOutOfRangeException) { return o.UserScoring; }
    }

    private string ClaveRsaPem()
    {
        var k = (o.ClavePublicaRsa ?? "").Trim();
        return k.StartsWith("-----") ? k : $"-----BEGIN PUBLIC KEY-----\n{k}\n-----END PUBLIC KEY-----";
    }

    private string CifrarRsa(string texto)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(ClaveRsaPem());
        var relleno = string.Equals(o.RellenoRsa, "OaepSha1", StringComparison.OrdinalIgnoreCase) ? RSAEncryptionPadding.OaepSHA1 : RSAEncryptionPadding.Pkcs1;
        return Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(texto), relleno));
    }

    private static StringContent JsonContent(JsonNode n) => new(n.ToJsonString(), Encoding.UTF8, "application/json");
    private static string? Texto(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n is JsonValue ? n.ToJsonString() : null;
    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
    private static (string, string) Partir(string nombre)
    {
        var partes = nombre.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (Recortar(partes.ElementAtOrDefault(0) ?? "", 50), Recortar(partes.ElementAtOrDefault(1) ?? "", 50));
    }
}

/// <summary>Token de la tarjeta cifrado en el catálogo (Data Protection).</summary>
public class TokensTarjeta(IDataProtectionProvider proteccion)
{
    private readonly IDataProtector p = proteccion.CreateProtector("NexoRh.TokenTarjeta.v1");
    public string Proteger(string token) => p.Protect(token);
    public string Leer(string cifrado) => p.Unprotect(cifrado);
}

/// <summary>
/// Cobro de facturas con la tarjeta registrada: automático (ciclo de vida: al emitirse y reintentos a los 3 y 7 días) o
/// manual (“Pagar ahora” del cliente o la consola). Un índice único impide dos cobros en curso o aprobados de la misma factura.
/// </summary>
public class CobrosFacturas(CatalogoDbContext catalogo, PasarelaIzipay pasarela, TokensTarjeta tokens, ConsultasPlataforma consultas, ILogger<CobrosFacturas> log)
{
    /// <summary>Días desde el primer intento automático: el día de emisión, a los 3 y a los 7 días.</summary>
    public static readonly int[] DiasReintento = [0, 3, 7];
    private static readonly string[] Cobrables = ["Pendiente", "Vencida"];

    /// <summary>Próximo cobro automático de una factura impaga (null: sin más intentos automáticos).</summary>
    public static DateOnly? ProximoIntento(Factura f, IReadOnlyCollection<Cobro> cobros)
    {
        if (!Cobrables.Contains(f.Estado) || cobros.Any(c => EstadosCobro.Bloquean.Contains(c.Estado))) return null;
        var autos = cobros.Where(c => c.Origen == OrigenesCobro.Automatico).OrderBy(c => c.CreadoEn).ToList();
        if (autos.Count >= DiasReintento.Length) return null;
        return autos.Count == 0 ? f.Emitida : DateOnly.FromDateTime(autos[0].CreadoEn).AddDays(DiasReintento[autos.Count]);
    }

    /// <summary>Cobros automáticos que tocan hoy. Devuelve cuántos se intentaron.</summary>
    public async Task<int> EjecutarAsync(DateTime ahora, Guid? soloTenant = null, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(ahora);
        var conTarjeta = await catalogo.MediosPago.AsNoTracking().Where(m => m.Activo && (soloTenant == null || m.TenantId == soloTenant)).Select(m => m.TenantId).ToListAsync(ct);
        if (conTarjeta.Count == 0) return 0;
        var vivos = await catalogo.Tenants.AsNoTracking().Where(t => conTarjeta.Contains(t.Id) && t.Estado != EstadosTenant.Cancelado && t.Estado != EstadosTenant.Eliminado).Select(t => t.Id).ToListAsync(ct);
        var facturas = await catalogo.Facturas.Where(f => vivos.Contains(f.TenantId) && Cobrables.Contains(f.Estado)).OrderBy(f => f.Emitida).ToListAsync(ct);
        var ids = facturas.Select(f => f.Id).ToList();
        var cobros = (await catalogo.Cobros.AsNoTracking().Where(c => ids.Contains(c.FacturaId)).ToListAsync(ct)).ToLookup(c => c.FacturaId);
        var n = 0;
        foreach (var f in facturas)
        {
            if (ProximoIntento(f, cobros[f.Id].ToList()) is not { } dia || dia > hoy) continue;
            await CobrarAsync(f, OrigenesCobro.Automatico, "Cobro automático", null, ahora, ct);
            n++;
        }
        return n;
    }

    /// <summary>Cobra una factura con la tarjeta activa del tenant y avisa por correo del resultado.</summary>
    public async Task<Cobro> CobrarAsync(Factura f, string origen, string actor, string? ip, DateTime ahora, CancellationToken ct = default)
    {
        var medio = await catalogo.MediosPago.AsNoTracking().FirstOrDefaultAsync(m => m.TenantId == f.TenantId && m.Activo, ct)
            ?? throw new InvalidOperationException("La empresa no tiene una tarjeta registrada.");
        if (!Cobrables.Contains(f.Estado)) throw new InvalidOperationException("La factura ya está pagada.");
        var monto = f.Subtotal + f.Igv;
        var cobro = new Cobro { TenantId = f.TenantId, FacturaId = f.Id, Factura = f.Numero, Monto = monto, Origen = origen, Actor = actor, CreadoEn = ahora,
            Tarjeta = $"{medio.Marca} {Ultimos(medio.Tarjeta)}".Trim(), TransactionId = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{RandomNumberGenerator.GetInt32(100, 1000)}" };
        catalogo.Cobros.Add(cobro);
        try { await catalogo.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            catalogo.Entry(cobro).State = EntityState.Detached;
            throw new InvalidOperationException("Esta factura ya tiene un cobro en curso o aprobado.");
        }
        cobro.OrderNumber = $"NX{cobro.Id:D10}";
        await catalogo.SaveChangesAsync(ct);

        ResultadoCobro r;
        if (medio.Modo != pasarela.Modo) r = new ResultadoCobro(EstadosCobro.Error, null, $"La tarjeta se registró en el ambiente {medio.Modo}; vuelve a registrarla.", null, null);
        else
        {
            try { r = await pasarela.CobrarAsync(tokens.Leer(medio.TokenCifrado), medio.CompradorId, medio.Facturacion, cobro.OrderNumber, cobro.TransactionId, monto, ip, ct); }
            catch (CryptographicException e) { log.LogError(e, "No se pudo leer el token de la tarjeta del tenant {Tenant}.", f.TenantId); r = new ResultadoCobro(EstadosCobro.Error, null, "Vuelve a registrar la tarjeta.", null, null); }
        }
        await AplicarAsync(cobro, f, r, ahora, ct);
        return cobro;
    }

    /// <summary>Aplica el resultado al cobro y a la factura (también desde la IPN), con auditoría y correo.</summary>
    public async Task AplicarAsync(Cobro cobro, Factura f, ResultadoCobro r, DateTime ahora, CancellationToken ct = default)
    {
        var antes = cobro.Estado;
        (cobro.Estado, cobro.Codigo, cobro.Mensaje, cobro.CodigoAutorizacion, cobro.Referencia) = (r.Estado, r.Codigo, Recortar(r.Mensaje, 300), r.CodigoAutorizacion, r.Referencia);
        if (r.Estado != EstadosCobro.PorConfirmar) cobro.ResueltoEn = ahora;
        if (r.Estado == EstadosCobro.Aprobado && Cobrables.Contains(f.Estado))
        {
            f.Estado = "Pagada";
            f.PagadaEn = ahora;
            f.MedioPago = $"Tarjeta {cobro.Tarjeta}";
        }
        catalogo.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = cobro.Actor, TenantId = f.TenantId, En = ahora,
            Accion = r.Estado == EstadosCobro.Aprobado ? "invoice.paid" : r.Estado == EstadosCobro.PorConfirmar ? "payment.pending" : "payment.failed",
            Detalle = $"{f.Numero} · S/ {cobro.Monto:0.00} · {cobro.Tarjeta} · {r.Estado}{(r.Mensaje is null ? "" : $": {r.Mensaje}")}" });
        await catalogo.SaveChangesAsync(ct);
        if (antes == r.Estado || r.Estado == EstadosCobro.PorConfirmar) return;
        try { await AvisarAsync(cobro, f, ct); }
        catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "No se pudo avisar el resultado del cobro {Orden}.", cobro.OrderNumber); }
    }

    private async Task AvisarAsync(Cobro cobro, Factura f, CancellationToken ct)
    {
        var t = await catalogo.Tenants.AsNoTracking().FirstAsync(x => x.Id == f.TenantId, ct);
        string asunto, titulo, cuerpo;
        if (cobro.Estado == EstadosCobro.Aprobado)
            (asunto, titulo, cuerpo) = ($"Recibimos tu pago de la factura {f.Numero}", "Pago recibido",
                $"Cargamos S/ {cobro.Monto:0.00} en tu tarjeta {cobro.Tarjeta} por la factura {f.Numero} (período {f.Periodo}). Código de autorización: {cobro.CodigoAutorizacion ?? "—"}. Gracias.");
        else
        {
            var historia = await catalogo.Cobros.AsNoTracking().Where(c => c.FacturaId == f.Id).ToListAsync(ct);
            var proximo = ProximoIntento(f, historia);
            (asunto, titulo, cuerpo) = ($"No pudimos cobrar la factura {f.Numero}", "No pudimos procesar tu pago",
                $"Intentamos cargar S/ {cobro.Monto:0.00} en tu tarjeta {cobro.Tarjeta} por la factura {f.Numero} y la operación no se aprobó ({cobro.Mensaje}). "
                + (proximo is { } d ? $"Volveremos a intentarlo el {d:dd/MM/yyyy}. " : "")
                + "Puedes pagar ahora o registrar otra tarjeta desde Administración › Estado de cuenta para evitar la suspensión del servicio.");
        }
        await consultas.EnTenantAsync(t, async sp =>
        {
            var para = t.CorreoFacturacion is { Length: > 0 } c ? [c]
                : await sp.GetRequiredService<NexoDbContext>().Usuarios.AsNoTracking().Where(u => u.Rol == Rol.Administrador && u.Activo).Select(u => u.Correo).ToListAsync(ct);
            var enviador = sp.GetRequiredService<EnviadorCorreo>();
            foreach (var correo in para) await enviador.EnviarAsync(new MensajeCorreo(correo, asunto, titulo, cuerpo), ct);
            return para.Count;
        });
    }

    public static string Ultimos(string tarjeta) => tarjeta.Length >= 4 ? $"•••• {tarjeta[^4..]}" : tarjeta;
    private static string? Recortar(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
