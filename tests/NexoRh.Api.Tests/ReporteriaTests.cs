using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NexoRh.Api.Tests;

[Collection("api")]
public class ReporteriaTests(ApiFixture api)
{
    private const string Cabecera = "Apellidos;Nombres;Tipo de documento;Número de documento;Cargo;División;Área;Sub-área;Fecha de ingreso;Sueldo neto;Sueldo base;Correo corporativo;Teléfono;Fecha de nacimiento;Jefatura";

    private static MultipartFormDataContent Archivo(string contenido, string nombre = "altas.csv", Encoding? enc = null)
    {
        var bytes = (enc ?? Encoding.UTF8).GetBytes(contenido);
        var c = new ByteArrayContent(bytes);
        c.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { c, "archivo", nombre } };
    }

    private static async Task<string> Texto(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        var b = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], b[..3]);   // BOM: Excel lo abre con tildes correctas
        return Encoding.UTF8.GetString(b[3..]);
    }

    [Fact]
    public async Task Solo_administradores()
    {
        var sup = await api.ClienteDe(ApiFixture.Supervisor);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.GetAsync("/api/reportes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.GetAsync("/api/cargas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsync("/api/colaboradores/importar", Archivo(Cabecera))).StatusCode);
    }

    [Fact]
    public async Task Exportadores_generan_con_datos_reales()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var datos = await admin.GetFromJsonAsync<JsonElement>("/api/reportes");
        Assert.True(datos.GetProperty("reportes").GetArrayLength() >= 6);
        Assert.Contains("02/2026", datos.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/reportes", new { plantilla = "CTS" })).StatusCode);

        var maestro = await (await admin.PostAsJsonAsync("/api/reportes", new { plantilla = "Maestro de empleados", nombre = "Maestro QA" })).Content.ReadFromJsonAsync<JsonElement>();
        var id = maestro.GetProperty("id").GetInt32();
        var csv = await Texto(await admin.GetAsync($"/api/reportes/{id}/archivo"));
        var lineas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Apellidos;Nombres;Tipo de documento;Número de documento", lineas[0]);
        var colaboradores = (await admin.GetFromJsonAsync<JsonElement>("/api/colaboradores")).GetArrayLength();
        Assert.Equal(colaboradores + 1, lineas.Length);

        // Planilla: exige un período con boletas.
        var nomina = await (await admin.PostAsJsonAsync("/api/reportes", new { plantilla = "Boletas de Pago" })).Content.ReadFromJsonAsync<JsonElement>();
        var nid = nomina.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/reportes/{nid}/archivo")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/reportes/{nid}/archivo?periodo=12/2020")).StatusCode);
        var boletas = await Texto(await admin.GetAsync($"/api/reportes/{nid}/archivo?periodo=02/2026"));
        Assert.Contains("02/2026;", boletas);

        foreach (var p in new[] { "Saldos de Vacaciones", "Compensaciones", "Posiciones del plan de headcount", "Resumen de Procesos", "Registro de Vacaciones", "Maestro de empleados · RR.HH." })
        {
            var r = await (await admin.PostAsJsonAsync("/api/reportes", new { plantilla = p })).Content.ReadFromJsonAsync<JsonElement>();
            var rid = r.GetProperty("id").GetInt32();
            Assert.True((await Texto(await admin.GetAsync($"/api/reportes/{rid}/archivo"))).Length > 20, p);
            (await admin.PostAsJsonAsync("/api/reportes/eliminar", new { ids = new[] { rid } })).EnsureSuccessStatusCode();
        }
        foreach (var p in new[] { "Nómina", "Retención de 5ta", "Ítems" })
        {
            var r = await (await admin.PostAsJsonAsync("/api/reportes", new { plantilla = p })).Content.ReadFromJsonAsync<JsonElement>();
            var rid = r.GetProperty("id").GetInt32();
            Assert.True((await Texto(await admin.GetAsync($"/api/reportes/{rid}/archivo?periodo=02/2026"))).Split("\r\n").Length > 2, p);
            (await admin.PostAsJsonAsync("/api/reportes/eliminar", new { ids = new[] { rid } })).EnsureSuccessStatusCode();
        }

        // Historial: cada archivo queda registrado y se puede volver a descargar con el mismo período.
        datos = await admin.GetFromJsonAsync<JsonElement>("/api/reportes");
        var ejec = datos.GetProperty("historial").EnumerateArray().First(e => e.GetProperty("reporte").GetString() == "Maestro QA");
        Assert.Equal(colaboradores, ejec.GetProperty("filas").GetInt32());
        var conPeriodo = datos.GetProperty("historial").EnumerateArray().First(e => e.GetProperty("plantilla").GetString() == "Boletas de Pago");
        Assert.Equal("02/2026", conPeriodo.GetProperty("periodo").GetString());
        await Texto(await admin.GetAsync($"/api/reportes/historial/{conPeriodo.GetProperty("id").GetInt32()}/archivo"));

        var copia = await (await admin.PostAsync($"/api/reportes/{id}/duplicar", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Maestro QA (copia)", copia.GetProperty("nombre").GetString());
        (await admin.PostAsJsonAsync("/api/reportes/eliminar", new { ids = new[] { id, nid, copia.GetProperty("id").GetInt32() } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/reportes/{id}/archivo")).StatusCode);
    }

    [Fact]
    public async Task Importador_valida_y_carga_todo_o_nada()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        var antes = (await admin.GetFromJsonAsync<JsonElement>("/api/colaboradores")).GetArrayLength();
        var template = await Texto(await admin.GetAsync("/api/colaboradores/importar/template"));
        Assert.Equal(Cabecera, template.TrimEnd());

        var malo = string.Join("\r\n", Cabecera,
            "Quispe Rojas;Ana;DNI;7523698;Analista Finanzas;Departamento de Administración;Finanzas;Tesorería;01/04/2026;3500;;;;;",
            "Torres Vega;Luis;;81234567;Cargo Inexistente;Comercial;Ventas;Retail;31/02/2026;abc;;;;;Nadie Existe, Juan",
            "Lima Paz;Rosa;;81234567;Analista Finanzas;Comercial;Ventas;Retail;01/04/2026;2800;;correo-malo;;;");
        var v = await (await admin.PostAsync("/api/colaboradores/importar?validar=true", Archivo(malo))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, v.GetProperty("cargaId").ValueKind);
        var campos = v.GetProperty("errores").EnumerateArray().Select(e => (e.GetProperty("fila").GetInt32(), e.GetProperty("campo").GetString())).ToList();
        Assert.Contains((2, "Número de documento"), campos);
        Assert.Contains((3, "Cargo"), campos);
        Assert.Contains((3, "Fecha de ingreso"), campos);
        Assert.Contains((3, "Sueldo neto"), campos);
        Assert.Contains((3, "Jefatura"), campos);
        Assert.Contains((4, "Número de documento"), campos);   // repetido en la fila 3
        Assert.Contains((4, "Correo corporativo"), campos);

        // Cargar con errores: se registra la carga, pero no se crea nadie.
        var r = await (await admin.PostAsync("/api/colaboradores/importar", Archivo(malo))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Con errores", r.GetProperty("estado").GetString());
        Assert.Equal(0, r.GetProperty("creados").GetInt32());
        Assert.Equal(antes, (await admin.GetFromJsonAsync<JsonElement>("/api/colaboradores")).GetArrayLength());

        // Columnas cambiadas: carga fallida.
        var f = await (await admin.PostAsync("/api/colaboradores/importar", Archivo("Nombre,DNI\r\nAna,12345678"))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Fallida", f.GetProperty("estado").GetString());

        // Archivo correcto, guardado por Excel en ANSI y con montos en formato local.
        var bueno = string.Join("\r\n", Cabecera,
            "Ñahui Cárdenas;Mónica;DNI;70112233;Analista Finanzas;Departamento de Administración;Finanzas;Tesorería;01/04/2026;\"3,500.00\";4200;monica.nahui@comercialandina.pe;987654321;15/05/1995;",
            "Vílchez Soto;Raúl;Carné de extranjería;CE0099887;Analista Finanzas;Departamento de Administración;Finanzas;Tesorería;15/04/2026;2800,50;;;;;") + "\r\n";
        var ok = await (await admin.PostAsync("/api/colaboradores/importar", Archivo(bueno, "altas-abril.csv", Encoding.Latin1))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Exitosa", ok.GetProperty("estado").GetString());
        Assert.Equal(2, ok.GetProperty("creados").GetInt32());
        var lista = await admin.GetFromJsonAsync<JsonElement>("/api/colaboradores");
        Assert.Equal(antes + 2, lista.GetArrayLength());
        var monica = lista.EnumerateArray().First(c => c.GetProperty("documento").GetString() == "70112233");
        Assert.Equal("Ñahui Cárdenas", monica.GetProperty("apellidos").GetString());
        Assert.Equal(3500m, monica.GetProperty("sueldoNeto").GetDecimal());

        // El mismo archivo otra vez: los documentos ya existen.
        var otra = await (await admin.PostAsync("/api/colaboradores/importar?validar=true", Archivo(bueno, "altas-abril.csv", Encoding.Latin1))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, otra.GetProperty("errores").GetArrayLength());

        var cargas = await admin.GetFromJsonAsync<JsonElement>("/api/cargas");
        var ultima = cargas[0];
        Assert.Equal("altas-abril.csv", ultima.GetProperty("archivo").GetString());
        Assert.Equal(2, ultima.GetProperty("registros").GetInt32());
        Assert.Contains(cargas.EnumerateArray(), c => c.GetProperty("estado").GetString() == "Con errores" && c.GetProperty("errores").GetArrayLength() >= 7);
    }
}
