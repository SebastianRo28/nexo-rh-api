using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NexoRh.Api.Catalogo.Migraciones
{
    /// <inheritdoc />
    public partial class Planes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorreoFacturacion",
                table: "Tenants",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionFiscal",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanCodigo",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PRO");

            migrationBuilder.AddColumn<bool>(
                name: "ModoImplementacion",
                table: "Ajustes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoporteHasta",
                table: "Ajustes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Facturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Emitida = table.Column<DateOnly>(type: "date", nullable: false),
                    Vence = table.Column<DateOnly>(type: "date", nullable: false),
                    Colaboradores = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Igv = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SunatEstado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facturas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Flag = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Valor = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flags_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Modulos",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Beneficios = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    PrecioPorColaborador = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modulos", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "Planes",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PrecioPorColaborador = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    LimiteEmpresas = table.Column<int>(type: "int", nullable: false),
                    LimiteAlmacenamientoGb = table.Column<int>(type: "int", nullable: false),
                    LimiteAdmins = table.Column<int>(type: "int", nullable: false),
                    LimiteApiPorMinuto = table.Column<int>(type: "int", nullable: false),
                    ModulosIncluidos = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Funciones = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planes", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "Solicitudes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solicitudes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Solicitudes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantModulos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActivoDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    ActivoHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    PrecioPactado = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantModulos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantModulos_Modulos_Codigo",
                        column: x => x.Codigo,
                        principalTable: "Modulos",
                        principalColumn: "Codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantModulos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Modulos",
                columns: new[] { "Codigo", "Beneficios", "Descripcion", "Nombre", "Orden", "PrecioPorColaborador" },
                values: new object[,]
                {
                    { "ASSETS", "Inventario con códigos y estados|Asignación y devolución con actas|Alertas de mantenimiento", "Inventario, asignaciones y actas de entrega.", "Gestión de Activos", 5, 2m },
                    { "ATTENDANCE", "Marcación web, móvil y biométrica|Turnos y tolerancias por recinto|Reportes de horas extras y tardanzas", "Turnos, marcaciones, ausencias y horas extras.", "Control de Asistencia", 3, 4m },
                    { "COMMS", "Publicaciones segmentadas|Reconocimientos con los valores de la empresa|Métricas de lectura", "Muro, publicaciones y reconocimientos.", "Comunicación", 11, 2m },
                    { "COMP", "Bandas por cargo|Comparación con el mercado|Ciclos de revisión salarial", "Bandas salariales, estudio de mercado y revisiones.", "Compensaciones", 8, 3m },
                    { "DOCS", "Contratos y constancias desde plantillas|Circuito de revisión y firma|Repositorio por colaborador", "Plantillas, generación masiva y firma de documentos.", "Documentos y Firma", 4, 3m },
                    { "HEADCOUNT", "Plan de dotación por área|Control de vacantes y aprobaciones|Proyección de costo", "Planificación de dotación y vacantes.", "Headcount", 9, 2m },
                    { "PAYROLL", "Cálculo mensual con AFP/ONP, 5ta categoría y CTS|Boletas en el portal del colaborador|Archivos para bancos y PLAME", "Cálculo de remuneraciones, boletas y archivos de pago.", "Planillas", 2, 6m },
                    { "PRO", "Flujos de aprobación configurables|Historial de cambios con auditoría completa|Accesos API e integraciones", "Workflows, historial de cambios, analítica, permisos granulares y API.", "Plataforma Pro", 1, 0m },
                    { "SERVICE_DESK", "Tickets por categoría|Tiempos de respuesta medidos|Base de respuestas frecuentes", "Mesa de ayuda para consultas de RR.HH.", "Servicio al colaborador", 7, 2m },
                    { "SST", "Matriz de requisitos legales|Evidencias con vencimientos|Indicadores de cumplimiento", "Requisitos legales, evidencias y cumplimiento (Ley 29783).", "Seguridad y Salud en el Trabajo", 10, 3m },
                    { "SURVEYS", "Encuestas anónimas de clima|Resultados por área|Encuestas libres y pulsos", "Clima laboral y encuestas libres.", "Encuestas", 12, 2m },
                    { "WHISTLEBLOWER", "Canal anónimo y confidencial|Seguimiento con plazos legales|Reportes para el comité", "Denuncias anónimas con seguimiento (Ley 27942).", "Canal de Denuncias", 6, 2m }
                });

            migrationBuilder.InsertData(
                table: "Planes",
                columns: new[] { "Codigo", "Funciones", "LimiteAdmins", "LimiteAlmacenamientoGb", "LimiteApiPorMinuto", "LimiteEmpresas", "ModulosIncluidos", "Nombre", "Orden", "PrecioPorColaborador" },
                values: new object[,]
                {
                    { "BASICO", "", 3, 5, 0, 1, "", "Básico", 1, 12m },
                    { "ENTERPRISE", "SSO_SOCIAL,SAML,CUSTOM_DOMAIN,HIDE_POWERED_BY,DEDICATED_DB", 100, 200, 600, 50, "PRO", "Enterprise", 3, 25m },
                    { "PRO", "SSO_SOCIAL", 10, 20, 60, 3, "PRO", "Pro", 2, 18m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_TenantId_Periodo",
                table: "Facturas",
                columns: new[] { "TenantId", "Periodo" });

            migrationBuilder.CreateIndex(
                name: "IX_Flags_TenantId_Flag",
                table: "Flags",
                columns: new[] { "TenantId", "Flag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_CreadaEn",
                table: "Solicitudes",
                column: "CreadaEn");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_TenantId",
                table: "Solicitudes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantModulos_Codigo",
                table: "TenantModulos",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_TenantModulos_TenantId_Codigo",
                table: "TenantModulos",
                columns: new[] { "TenantId", "Codigo" });

            // Los tenants que ya existían conservan todo lo que tenían: plan Pro y todos los módulos adicionales.
            migrationBuilder.Sql(@"INSERT INTO TenantModulos (TenantId, Codigo, ActivoDesde)
                SELECT t.Id, m.Codigo, CAST(SYSUTCDATETIME() AS date) FROM Tenants t CROSS JOIN Modulos m WHERE m.Codigo <> 'PRO';");
            // Facturas de muestra de la empresa de ejemplo (antes eran fijas en el frontend).
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM Tenants WHERE Id = '8C1F5A2E-4B7D-4E19-9A0C-2F6D1E3B7A44')
                INSERT INTO Facturas (TenantId, Numero, Periodo, Emitida, Vence, Colaboradores, Subtotal, Igv, Estado, SunatEstado) VALUES
                ('8C1F5A2E-4B7D-4E19-9A0C-2F6D1E3B7A44', 'F001-000250', '2025-12', '2025-12-01', '2025-12-31', 15, 270, 48.60, 'Pagada', 'Pendiente de integración'),
                ('8C1F5A2E-4B7D-4E19-9A0C-2F6D1E3B7A44', 'F001-000271', '2026-01', '2026-01-01', '2026-01-31', 15, 270, 48.60, 'Pagada', 'Pendiente de integración'),
                ('8C1F5A2E-4B7D-4E19-9A0C-2F6D1E3B7A44', 'F001-000297', '2026-02', '2026-02-01', '2026-02-28', 15, 270, 48.60, 'Pagada', 'Pendiente de integración'),
                ('8C1F5A2E-4B7D-4E19-9A0C-2F6D1E3B7A44', 'F001-000318', '2026-03', '2026-03-01', '2026-03-31', 16, 288, 51.84, 'Pendiente', 'Pendiente de integración');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Facturas");

            migrationBuilder.DropTable(
                name: "Flags");

            migrationBuilder.DropTable(
                name: "Planes");

            migrationBuilder.DropTable(
                name: "Solicitudes");

            migrationBuilder.DropTable(
                name: "TenantModulos");

            migrationBuilder.DropTable(
                name: "Modulos");

            migrationBuilder.DropColumn(
                name: "CorreoFacturacion",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DireccionFiscal",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PlanCodigo",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ModoImplementacion",
                table: "Ajustes");

            migrationBuilder.DropColumn(
                name: "SoporteHasta",
                table: "Ajustes");
        }
    }
}
