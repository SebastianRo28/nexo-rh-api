using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Tests;

/// <summary>Fórmulas del motor de planilla con montos calculados a mano.</summary>
public class FormulasPlanillaTests
{
    private static readonly TramoRenta[] Tramos =
    [
        new() { Desde = 0, Hasta = 5, TasaPct = 8 }, new() { Desde = 5, Hasta = 20, TasaPct = 14 }, new() { Desde = 20, Hasta = 35, TasaPct = 17 },
        new() { Desde = 35, Hasta = 45, TasaPct = 20 }, new() { Desde = 45, Hasta = null, TasaPct = 30 },
    ];

    [Fact]
    public void Impuesto_por_tramos_progresivos()
    {
        // UIT 5,500: 27,500 al 8 % = 2,200 + (100,000 − 27,500) al 14 % = 10,150.
        Assert.Equal(12_350m, Planilla.Impuesto(100_000m, 5_500m, Tramos));
        Assert.Equal(800m, Planilla.Impuesto(10_000m, 5_500m, Tramos));
        Assert.Equal(0m, Planilla.Impuesto(0m, 5_500m, Tramos));
        // Llega al último tramo: 2,200 + 11,550 + 14,025 + 11,000 + (300,000 − 247,500) × 30 %.
        Assert.Equal(2_200m + 11_550m + 14_025m + 11_000m + 15_750m, Planilla.Impuesto(300_000m, 5_500m, Tramos));
    }

    [Fact]
    public void Retencion_mensual_segun_el_mes()
    {
        var ret = new Dictionary<int, decimal> { [1] = 1000, [2] = 1000, [3] = 1000, [4] = 1000 };
        Assert.Equal(1000m, Planilla.RetencionMes(2, 12_000m, ret));                     // ÷ 12
        Assert.Equal(1000m, Planilla.RetencionMes(4, 12_000m, ret));                     // (12,000 − 3,000) ÷ 9
        Assert.Equal(1000m, Planilla.RetencionMes(5, 12_000m, ret));                     // (12,000 − 4,000) ÷ 8
        var todo = Enumerable.Range(1, 11).ToDictionary(m => m, _ => 1000m);
        Assert.Equal(1000m, Planilla.RetencionMes(12, 12_000m, todo));                   // saldo
        Assert.Equal(0m, Planilla.RetencionMes(12, 10_000m, todo));                      // nunca negativa
    }

    [Theory]
    [InlineData("ONP", null, "ONP", null, false)]
    [InlineData("AFP Habitat · comisión Flujo", null, "AFP", "Habitat", true)]
    [InlineData("AFP Integra · comisión Saldo", null, "AFP", "Integra", false)]
    [InlineData("AFP Prima · comisión Flujo", "Convenio de prácticas", "Ninguno", null, false)]
    [InlineData(null, null, "Sin registrar", null, false)]
    public void Lee_el_regimen_pensionario(string? pension, string? contrato, string tipo, string? afp, bool flujo)
    {
        var r = Planilla.LeerRegimen(pension, contrato);
        Assert.Equal(tipo, r.Tipo);
        Assert.Equal(afp, r.Afp);
        Assert.Equal(flujo, r.ComisionFlujo);
    }

    [Fact]
    public void Periodos_en_texto()
    {
        Assert.True(Planilla.TryLeer("03/2026", out var p));
        Assert.Equal(202603, p);
        Assert.False(Planilla.TryLeer("3/2026", out _));
        Assert.False(Planilla.TryLeer("13/2026", out _));
        Assert.Equal(202701, Planilla.Siguiente(202612));
        Assert.Equal("03/2026", Planilla.Texto(202603));
    }
}

/// <summary>Planilla de marzo de 2026 con los datos de ejemplo.</summary>
[Collection("api")]
public class PlanillaTests(ApiFixture api)
{
    private static decimal Linea(JsonElement boleta, string codigo) =>
        boleta.GetProperty("lineas").EnumerateArray().Where(l => l.GetProperty("codigo").GetString() == codigo).Sum(l => l.GetProperty("monto").GetDecimal());

    private async Task<JsonElement> BoletaDeMarzo(HttpClient admin, int colaboradorId)
    {
        (await admin.PostAsync("/api/remuneraciones/periodos/03-2026/procesar", null)).EnsureSuccessStatusCode();
        var lista = await admin.GetFromJsonAsync<JsonElement>($"/api/remuneraciones/boletas?periodo=03/2026&colaboradorId={colaboradorId}");
        var id = lista.EnumerateArray().Single().GetProperty("id").GetInt32();
        return await admin.GetFromJsonAsync<JsonElement>($"/api/remuneraciones/boletas/{id}");
    }

    [Fact]
    public async Task ONP_con_vacaciones_y_quinta_categoria()
    {
        // Carolina Flores (6): S/ 6,980 mensual, ONP, vacaciones del 16/02 al 07/03 (7 días en marzo).
        var b = await BoletaDeMarzo(await api.ClienteDe(ApiFixture.Admin), 6);
        Assert.Equal(5351.33m, Linea(b, "SUE-BAS"));   // 6,980 / 30 × 23
        Assert.Equal(1628.67m, Linea(b, "VAC-REM"));   // 6,980 / 30 × 7
        Assert.Equal(907.40m, Linea(b, "APO-ONP"));    // 13 % de 6,980
        // 5ta: 13,960 (ene–feb) + 6,980 + 6,980 × 9 + 2 × 6,980 × 1.09 = 98,976.40; − 7 UIT = 60,476.40;
        // impuesto 2,200 + 32,976.40 × 14 % = 6,816.70; ÷ 12 = 568.06.
        Assert.Equal(568.06m, Linea(b, "RET-5TA"));
        Assert.Equal(628.20m, Linea(b, "APO-ESS"));    // 9 % de 6,980 (empleador)
        var r = b.GetProperty("resumen");
        Assert.Equal(6980.00m, r.GetProperty("ingresos").GetDecimal());
        Assert.Equal(5504.54m, r.GetProperty("neto").GetDecimal());
        Assert.Equal(23, r.GetProperty("diasLaborados").GetInt32());
        Assert.Equal(7, r.GetProperty("diasVacaciones").GetInt32());
    }

    [Fact]
    public async Task AFP_con_comision_sobre_el_flujo()
    {
        // Florencia Alamos (0): sueldo base S/ 7,500, AFP Habitat comisión flujo (1.47 %).
        var b = await BoletaDeMarzo(await api.ClienteDe(ApiFixture.Admin), 0);
        Assert.Equal(7500.00m, Linea(b, "SUE-BAS"));
        Assert.Equal(750.00m, Linea(b, "AFP-APO"));    // 10 %
        Assert.Equal(102.75m, Linea(b, "AFP-PRI"));    // 1.37 %
        Assert.Equal(110.25m, Linea(b, "AFP-COM"));    // 1.47 %
        Assert.Equal(654.08m, Linea(b, "RET-5TA"));    // (106,350 − 38,500): 2,200 + 40,350 × 14 % = 7,849 ÷ 12
        Assert.Equal(5882.92m, b.GetProperty("resumen").GetProperty("neto").GetDecimal());
    }

    [Fact]
    public async Task Jornal_falta_recargo_y_adelanto()
    {
        // Javier Gutiérrez (7): jornal S/ 71.68, falta injustificada el 09/03, domingos 01/03 y 08/03 (8 h c/u), adelanto S/ 500.
        var b = await BoletaDeMarzo(await api.ClienteDe(ApiFixture.Admin), 7);
        Assert.Equal(2078.72m, Linea(b, "SUE-BAS"));   // 71.68 × 29
        Assert.Equal(286.72m, Linea(b, "REC-DOM"));    // 16 h × 8.96 × 2
        Assert.Equal(500.00m, Linea(b, "ADE-SUE"));
        Assert.Equal(1, b.GetProperty("resumen").GetProperty("diasNoTrabajados").GetInt32());
        // El adelanto no es base de ONP: 13 % de 2,365.44.
        Assert.Equal(307.51m, Linea(b, "APO-ONP"));
        // Debajo de 7 UIT al año: sin retención de 5ta.
        Assert.Equal(0m, Linea(b, "RET-5TA"));
    }

    [Fact]
    public async Task Cada_persona_ve_solo_sus_boletas_cerradas()
    {
        var colab = await api.ClienteDe(ApiFixture.Colaborador);
        var propias = await colab.GetFromJsonAsync<JsonElement>("/api/remuneraciones/boletas");
        var periodos = propias.EnumerateArray().Select(b => b.GetProperty("periodo").GetString()).ToList();
        Assert.Equal(["02/2026", "01/2026"], periodos);   // marzo sigue abierto: no se ve
        Assert.All(propias.EnumerateArray(), b => Assert.Equal(0, b.GetProperty("colaboradorId").GetInt32()));
        var otra = (await (await api.ClienteDe(ApiFixture.Admin)).GetFromJsonAsync<JsonElement>("/api/remuneraciones/boletas?periodo=02/2026&colaboradorId=6"))
            .EnumerateArray().Single().GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await colab.GetAsync($"/api/remuneraciones/boletas/{otra}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.PostAsync("/api/remuneraciones/periodos/03-2026/procesar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await colab.GetAsync("/api/remuneraciones/parametros")).StatusCode);
    }

    [Fact]
    public async Task Lo_cerrado_no_se_modifica()
    {
        var admin = await api.ClienteDe(ApiFixture.Admin);
        // Febrero está cerrado: no se asignan ítems ni se cambian horas extras de ese mes.
        var item = await admin.PostAsJsonAsync("/api/remuneraciones/asignaciones", new { colaboradorId = 6, tipoId = 9, monto = 100, desde = "02/2026" });
        Assert.Equal(HttpStatusCode.BadRequest, item.StatusCode);
        var hhee = await admin.PostAsJsonAsync("/api/asistencia/hhee", new { colaboradorId = 6, fecha = "10/02/2026", accion = "rechazar", observacion = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, hhee.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/remuneraciones/periodos/02-2026/procesar", null)).StatusCode);
        // Los ítems que calcula la planilla no se asignan a mano.
        var sueldo = await admin.PostAsJsonAsync("/api/remuneraciones/asignaciones", new { colaboradorId = 6, tipoId = 1, monto = 100, desde = "03/2026" });
        Assert.Equal(HttpStatusCode.BadRequest, sueldo.StatusCode);

        var periodos = await admin.GetFromJsonAsync<JsonElement>("/api/remuneraciones/periodos");
        var feb = periodos.EnumerateArray().First(p => p.GetProperty("periodo").GetString() == "02/2026");
        Assert.True(feb.GetProperty("cerrado").GetBoolean());
        Assert.True(feb.GetProperty("boletas").GetInt32() >= 15);
    }
}
