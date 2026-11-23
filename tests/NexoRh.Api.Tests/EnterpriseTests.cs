using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Services;

namespace NexoRh.Api.Tests;

/// <summary>Enterprise (etapa 6.7): dominio propio, correos desde el dominio del cliente, “Powered by” y BD dedicada.</summary>
[Collection("api")]
public class EnterpriseTests(ApiFixture api)
{
    private async Task<Tenant> NuevoTenant(string plan = "PRO")
    {
        var id = Guid.NewGuid();
        var t = await api.Services.GetRequiredService<AprovisionadorTenants>().AsegurarAsync(
            new Tenant { Id = id, Slug = $"en{id:N}"[..14], Nombre = "Enterprise", RazonSocial = "Enterprise S.A.C.", Ruc = "20666666666", PlanCodigo = plan }, datosDemo: true);
        return t;
    }

    private HttpClient Cliente(string host, string esquema = "http") =>
        api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"{esquema}://{host}") });

    private async Task<HttpClient> Admin(Tenant t)
    {
        var c = Cliente($"{t.Slug}.localhost");
        var r = await c.PostAsJsonAsync("/api/auth/login", new { correo = ApiFixture.Admin, password = api.PasswordSemilla });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<Sesion>())!.Token);
        return c;
    }

    private async Task Plan(Tenant t, string plan)
    {
        using var scope = api.Services.CreateScope();
        var cat = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        (await cat.Tenants.FirstAsync(x => x.Id == t.Id)).PlanCodigo = plan;
        await cat.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<Suscripciones>().Olvidar(t.Id);
    }

    private ResolutorDnsSimulado Dns => api.Services.GetRequiredService<ResolutorDnsSimulado>();

    private T EnCatalogo<T>(Func<CatalogoDbContext, T> f)
    {
        using var scope = api.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<CatalogoDbContext>());
    }

    [Fact]
    public async Task Dominio_propio_verificado_por_DNS_con_certificado_automatico()
    {
        var t = await NuevoTenant();
        var admin = await Admin(t);
        var host = $"rrhh.{t.Slug}.pe";

        // Pro: se ve la pantalla, pero agregar un dominio es de Enterprise.
        var vista = await admin.GetFromJsonAsync<JsonElement>("/api/dominio");
        Assert.False(vista.GetProperty("permiteDominio").GetBoolean());
        Assert.True(vista.GetProperty("dnsSimulado").GetBoolean());
        var r = await admin.PostAsJsonAsync("/api/dominio/dominios", new { host });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("feature_not_enabled", (await r.Content.ReadFromJsonAsync<Problema>())!.Codigo);

        await Plan(t, "ENTERPRISE");
        var rr = await admin.PostAsJsonAsync("/api/dominio/dominios", new { host = "otro.localhost" });
        Assert.True(rr.StatusCode == HttpStatusCode.BadRequest, $"{rr.StatusCode} {await rr.Content.ReadAsStringAsync()}");
        r = await admin.PostAsJsonAsync("/api/dominio/dominios", new { host = $"https://{host.ToUpperInvariant()}/" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var d = await r.Content.ReadFromJsonAsync<JsonElement>();
        var id = d.GetProperty("id").GetInt32();
        Assert.Equal(host, d.GetProperty("host").GetString());
        Assert.False(d.GetProperty("verificado").GetBoolean());
        Assert.Equal("Pendiente", d.GetProperty("estadoCertificado").GetString());
        var registros = d.GetProperty("registros").EnumerateArray().ToList();
        var cname = registros.Single(x => x.GetProperty("tipo").GetString() == "CNAME");
        var txt = registros.Single(x => x.GetProperty("tipo").GetString() == "TXT");
        Assert.Equal($"{t.Slug}.localhost", cname.GetProperty("valor").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/dominio/dominios", new { host })).StatusCode);

        // Sin registros: sigue pendiente, con lo que falta. Un dominio sin verificar no abre la app ni emite certificado.
        var v = await (await admin.PostAsync($"/api/dominio/dominios/{id}/verificar", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(v.GetProperty("verificado").GetBoolean());
        Assert.Contains("CNAME", v.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente("localhost").GetAsync($"/api/publico/tls?domain={host}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsync($"/api/dominio/dominios/{id}/principal", null)).StatusCode);

        // Solo el CNAME: falta el TXT de propiedad.
        Dns.Publicar("CNAME", host, $"{t.Slug}.localhost");
        v = await (await admin.PostAsync($"/api/dominio/dominios/{id}/verificar", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(v.GetProperty("verificado").GetBoolean());
        Assert.Contains("TXT", v.GetProperty("error").GetString());

        Dns.Publicar("TXT", txt.GetProperty("nombre").GetString()!, txt.GetProperty("valor").GetString()!);
        v = await (await admin.PostAsync($"/api/dominio/dominios/{id}/verificar", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(v.GetProperty("verificado").GetBoolean());
        Assert.Equal("Emitiendo", v.GetProperty("estadoCertificado").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Cliente("localhost").GetAsync($"/api/publico/tls?domain={host}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente("localhost").GetAsync("/api/publico/tls?domain=nadie.pe")).StatusCode);

        // La app abre en el dominio propio; el primer acceso por HTTPS marca el certificado como activo.
        var entrada = await Cliente(host, "https").GetFromJsonAsync<JsonElement>("/api/publico/entrada");
        Assert.Equal("tenant", entrada.GetProperty("tipo").GetString());
        Assert.Equal(t.Slug, entrada.GetProperty("tenant").GetProperty("slug").GetString());
        Assert.Equal(EstadosCertificado.Activo, EnCatalogo(c => c.Dominios.AsNoTracking().First(x => x.Id == id).EstadoCertificado));
        var login = await Cliente(host, "https").PostAsJsonAsync("/api/auth/login", new { correo = ApiFixture.Admin, password = api.PasswordSemilla });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // Principal: los enlaces de los correos usan el dominio propio.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/dominio/dominios/{id}/principal", null)).StatusCode);
        vista = await admin.GetFromJsonAsync<JsonElement>("/api/dominio");
        Assert.True(vista.GetProperty("dominios").EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == id).GetProperty("principal").GetBoolean());

        // Al quitarlo vuelve a ser principal el subdominio y el host deja de ser de este tenant.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/dominio/dominios/{id}")).StatusCode);
        vista = await admin.GetFromJsonAsync<JsonElement>("/api/dominio");
        Assert.True(vista.GetProperty("dominios").EnumerateArray().Single().GetProperty("principal").GetBoolean());
        entrada = await Cliente(host, "https").GetFromJsonAsync<JsonElement>("/api/publico/entrada");
        Assert.NotEqual(t.Slug, entrada.TryGetProperty("tenant", out var x) && x.ValueKind == JsonValueKind.Object ? x.GetProperty("slug").GetString() : null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync($"/api/dominio/dominios/{vista.GetProperty("dominios")[0].GetProperty("id").GetInt32()}")).StatusCode);
    }

    [Fact]
    public async Task Correos_desde_el_dominio_del_cliente_con_SPF_DKIM_y_DMARC()
    {
        var t = await NuevoTenant("ENTERPRISE");
        var admin = await Admin(t);
        var dominio = $"{t.Slug}.pe";
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/dominio/correo", new { dominio = "nexo", remitente = "rrhh" })).StatusCode);
        var r = await admin.PutAsJsonAsync("/api/dominio/correo", new { dominio, remitente = "RRHH" });
        r.EnsureSuccessStatusCode();
        var c = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"rrhh@{dominio}", c.GetProperty("direccion").GetString());
        Assert.False(c.GetProperty("verificado").GetBoolean());
        var registros = c.GetProperty("registros").EnumerateArray().ToList();
        Assert.Equal(3, registros.Count);

        // Sin verificar: sale desde la dirección de la plataforma, sin firma propia.
        async Task<CorreoSaliente> Recuperar()
        {
            (await Cliente($"{t.Slug}.localhost").PostAsJsonAsync("/api/auth/olvide", new { correo = ApiFixture.Admin })).EnsureSuccessStatusCode();
            return EnCatalogo(cat => cat.Correos.AsNoTracking().Where(x => x.TenantId == t.Id && x.Para == ApiFixture.Admin).OrderByDescending(x => x.Id).First());
        }
        var antes = await Recuperar();
        Assert.DoesNotContain(dominio, antes.Remitente);
        Assert.Null(antes.DkimDominio);

        // SPF y DKIM sin DMARC: todavía no.
        foreach (var x in registros.Take(2)) Dns.Publicar("TXT", x.GetProperty("nombre").GetString()!, x.GetProperty("valor").GetString()!);
        c = await (await admin.PostAsync("/api/dominio/correo/verificar", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(c.GetProperty("spfOk").GetBoolean());
        Assert.True(c.GetProperty("dkimOk").GetBoolean());
        Assert.False(c.GetProperty("dmarcOk").GetBoolean());
        Assert.Null((await Recuperar()).DkimDominio);

        Dns.Publicar("TXT", registros[2].GetProperty("nombre").GetString()!, registros[2].GetProperty("valor").GetString()!);
        c = await (await admin.PostAsync("/api/dominio/correo/verificar", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(c.GetProperty("verificado").GetBoolean());
        Assert.True(c.GetProperty("enUso").GetBoolean());
        var despues = await Recuperar();
        Assert.Equal($"rrhh@{dominio}", despues.Remitente);
        Assert.Equal(dominio, despues.DkimDominio);

        // Si baja de plan, vuelve a salir desde la plataforma (sin borrar su configuración).
        await Plan(t, "PRO");
        Assert.Null((await Recuperar()).DkimDominio);
        Assert.False((await admin.GetFromJsonAsync<JsonElement>("/api/dominio")).GetProperty("correo").GetProperty("enUso").GetBoolean());
    }

    [Fact]
    public void La_firma_DKIM_se_verifica_con_la_clave_publica()
    {
        using var rsa = RSA.Create(2048);
        var mime = MensajeMime.Construir("RR.HH. Grupo Andino", "rrhh@grupoandino.pe", "ana@correo.pe", "Restablece tu contraseña · Ñandú", "<p>Hola</p>",
            DateTimeOffset.UtcNow, new FirmaDkim("grupoandino.pe", "nexorh202611", rsa));
        var (cabeceras, cuerpo) = Partir(mime);
        var firma = cabeceras.First(h => h.Nombre == "DKIM-Signature").Valor;
        var etiquetas = firma.Split(';').Select(x => x.Trim()).Where(x => x.Contains('=')).ToDictionary(x => x[..x.IndexOf('=')], x => x[(x.IndexOf('=') + 1)..]);
        Assert.Equal("grupoandino.pe", etiquetas["d"]);
        Assert.Equal("relaxed/relaxed", etiquetas["c"]);
        Assert.Equal(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(MensajeMime.CuerpoRelaxed(cuerpo)))), etiquetas["bh"]);
        var datos = new StringBuilder();
        foreach (var n in etiquetas["h"].Split(':'))
        {
            var h = cabeceras.First(x => x.Nombre.Equals(n, StringComparison.OrdinalIgnoreCase));
            datos.Append(MensajeMime.CabeceraRelaxed(h.Nombre, h.Valor)).Append("\r\n");
        }
        datos.Append(MensajeMime.CabeceraRelaxed("DKIM-Signature", firma[..(firma.LastIndexOf("; b=", StringComparison.Ordinal) + 4)]));
        using var publica = RSA.Create();
        publica.ImportSubjectPublicKeyInfo(rsa.ExportSubjectPublicKeyInfo(), out _);
        Assert.True(publica.VerifyData(Encoding.UTF8.GetBytes(datos.ToString()), Convert.FromBase64String(etiquetas["b"]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.Contains("=?utf-8?B?", cabeceras.First(h => h.Nombre == "Subject").Valor);
    }

    [Fact]
    public async Task El_cliente_SMTP_entrega_el_mensaje_firmado()
    {
        var servidor = new TcpListener(IPAddress.Loopback, 0);
        servidor.Start();
        var puerto = ((IPEndPoint)servidor.LocalEndpoint).Port;
        var recibido = new StringBuilder();
        var comandos = new List<string>();
        var atender = Task.Run(async () =>
        {
            using var cliente = await servidor.AcceptTcpClientAsync();
            var s = cliente.GetStream();
            var lector = new StreamReader(s, Encoding.UTF8);
            var escritor = new StreamWriter(s, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await escritor.WriteLineAsync("220 prueba ESMTP");
            var enDatos = false;
            while (await lector.ReadLineAsync() is { } linea)
            {
                if (enDatos)
                {
                    if (linea == ".") { enDatos = false; await escritor.WriteLineAsync("250 OK aceptado"); continue; }
                    recibido.Append(linea).Append("\r\n");
                    continue;
                }
                comandos.Add(linea);
                if (linea.StartsWith("EHLO")) { await escritor.WriteLineAsync("250-prueba"); await escritor.WriteLineAsync("250 AUTH PLAIN"); }
                else if (linea.StartsWith("AUTH PLAIN")) await escritor.WriteLineAsync("235 OK");
                else if (linea == "DATA") { enDatos = true; await escritor.WriteLineAsync("354 envía"); }
                else if (linea == "QUIT") { await escritor.WriteLineAsync("221 adiós"); break; }
                else await escritor.WriteLineAsync("250 OK");
            }
        });
        using var rsa = RSA.Create(2048);
        var mime = MensajeMime.Construir("Grupo Andino", "rrhh@grupoandino.pe", "ana@correo.pe", "Prueba", "<p>.línea con punto</p>", DateTimeOffset.UtcNow,
            new FirmaDkim("grupoandino.pe", "s1", rsa));
        await ClienteSmtp.EnviarAsync(new OpcionesCorreo { SmtpHost = "127.0.0.1", SmtpPuerto = puerto, SmtpSsl = false, SmtpUsuario = "u", SmtpClave = "c" },
            "rrhh@grupoandino.pe", "ana@correo.pe", mime);
        await atender;
        servidor.Stop();
        Assert.Contains("MAIL FROM:<rrhh@grupoandino.pe>", comandos);
        Assert.Contains("RCPT TO:<ana@correo.pe>", comandos);
        Assert.StartsWith("DKIM-Signature: v=1; a=rsa-sha256;", recibido.ToString());
        Assert.Equal(mime, recibido.ToString());
    }

    [Fact]
    public async Task Powered_by_solo_se_oculta_en_Enterprise()
    {
        var t = await NuevoTenant();
        var admin = await Admin(t);
        async Task<bool> Publicada() => (await Cliente($"{t.Slug}.localhost").GetFromJsonAsync<JsonElement>("/api/publico/marca"))
            .GetProperty("login").GetProperty("mostrarPoweredBy").GetBoolean();
        async Task Publicar()
        {
            var datos = new MarcaDatos();
            datos.Login.MostrarPoweredBy = false;
            (await admin.PostAsJsonAsync("/api/marca", new { datos, nota = "Marca blanca" })).EnsureSuccessStatusCode();
        }
        await Publicar();
        Assert.True(await Publicada());
        await Plan(t, "ENTERPRISE");
        api.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Remove($"tenant-marca:{t.Id}");
        await Publicar();
        Assert.False(await Publicada());
        Assert.False((await Cliente($"{t.Slug}.localhost").GetFromJsonAsync<JsonElement>("/api/publico/entrada")).GetProperty("tenant").GetProperty("poweredBy").GetBoolean());
        // Si baja de plan, “Powered by” vuelve sin que tenga que publicar otra vez.
        await Plan(t, "PRO");
        Assert.True(await Publicada());
    }

    [Fact]
    public async Task Mudanza_a_BD_dedicada_copia_verifica_y_deja_la_compartida_sin_sus_datos()
    {
        var t = await NuevoTenant("ENTERPRISE");
        var admin = await Admin(t);
        var antes = (await admin.GetFromJsonAsync<JsonElement>("/api/colaboradores")).GetArrayLength();
        Assert.True(antes > 0);

        // Modo mantenimiento: la app del tenant responde 503 (la consola y la marca pública siguen).
        await Mantenimiento(t, true);
        var m = await admin.GetAsync("/api/colaboradores");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, m.StatusCode);
        Assert.Equal("tenant_maintenance", (await m.Content.ReadFromJsonAsync<Problema>())!.Codigo);
        await Mantenimiento(t, false);

        var trabajos = api.Services.GetRequiredService<TrabajosAprovisionamiento>();
        var job = trabajos.Nuevo(MudanzaBaseDedicada.Pasos);
        await api.Services.GetRequiredService<MudanzaBaseDedicada>().MoverAsync(job.Id, t.Id, "pruebas");
        var estado = trabajos.Ver(job.Id)!;
        Assert.True(estado.Estado == "succeeded", string.Join(" | ", estado.Pasos.Select(p => $"{p.Clave}:{p.Estado}:{p.Error}")));

        var fila = EnCatalogo(c => c.Tenants.AsNoTracking().First(x => x.Id == t.Id));
        Assert.NotNull(fila.CadenaConexion);
        Assert.DoesNotContain("Database=", fila.CadenaConexion);
        Assert.False(fila.EnMantenimiento);
        var cadena = api.Services.GetRequiredService<CadenasConexion>().Descifrar(fila.CadenaConexion)!;
        var nombre = new SqlConnectionStringBuilder(cadena).InitialCatalog;
        Assert.EndsWith($"_{t.Slug}", nombre);

        // La app sigue igual (mismos datos e Id), ahora desde su BD.
        api.Services.GetRequiredService<BuscadorTenants>().Olvidar($"{t.Slug}.localhost");
        var admin2 = await Admin(t);
        Assert.Equal(antes, (await admin2.GetFromJsonAsync<JsonElement>("/api/colaboradores")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Created, (await admin2.PostAsJsonAsync("/api/dominio/dominios", new { host = $"rrhh.{t.Slug}.pe" })).StatusCode);

        // En la compartida no queda nada del tenant.
        using (var scope = api.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ContextoTenant>().Usar(new Tenant { Id = t.Id, Slug = t.Slug, Nombre = t.Nombre });
            var compartida = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
            Assert.Equal(0, await compartida.Colaboradores.CountAsync());
            Assert.Equal(0, await compartida.Usuarios.CountAsync());
        }

        // Eliminación definitiva: se borra también la BD dedicada.
        CertificadoEliminacion cert;
        using (var scope = api.Services.CreateScope())
        {
            var cat = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            var x = await cat.Tenants.FirstAsync(y => y.Id == t.Id);
            cert = await scope.ServiceProvider.GetRequiredService<CicloVidaTenants>().EliminarAsync(x, "pruebas", DateTime.UtcNow);
        }
        Assert.Equal(nombre, cert.BaseDedicadaEliminada);
        Assert.True(cert.FilasNegocio > 0);
        var master = new SqlConnectionStringBuilder(cadena) { InitialCatalog = "master" };
        await using var con = new SqlConnection(master.ConnectionString);
        await con.OpenAsync();
        var cmd = new SqlCommand("SELECT DB_ID(@n)", con);
        cmd.Parameters.AddWithValue("@n", nombre);
        Assert.Equal(DBNull.Value, await cmd.ExecuteScalarAsync());
    }

    private async Task Mantenimiento(Tenant t, bool valor)
    {
        using var scope = api.Services.CreateScope();
        var cat = scope.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        (await cat.Tenants.FirstAsync(x => x.Id == t.Id)).EnMantenimiento = valor;
        await cat.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<BuscadorTenants>().Olvidar($"{t.Slug}.localhost");
    }

    private static (List<(string Nombre, string Valor)> Cabeceras, string Cuerpo) Partir(string mime)
    {
        var i = mime.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var lista = new List<(string, string)>();
        foreach (var linea in mime[..i].Replace("\r\n ", " ").Split("\r\n"))
        {
            var p = linea.IndexOf(':');
            lista.Add((linea[..p], linea[(p + 1)..].TrimStart()));
        }
        return (lista, mime[(i + 4)..]);
    }
}
