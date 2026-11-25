using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Catalogo.Migraciones
{
    /// <inheritdoc />
    public partial class CobroIzipay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MedioPago",
                table: "Facturas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PagadaEn",
                table: "Facturas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cobros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacturaId = table.Column<int>(type: "int", nullable: false),
                    Factura = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CodigoAutorizacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Tarjeta = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResueltoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cobros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cobros_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediosPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Proveedor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Modo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    TokenCifrado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CompradorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Tarjeta = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Titular = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Facturacion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    AutorizadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AutorizacionIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    RegistradoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetiradoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediosPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediosPago_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_FacturaId_Vigente",
                table: "Cobros",
                column: "FacturaId",
                unique: true,
                filter: "[Estado] IN ('Procesando', 'Aprobado', 'Por confirmar')");

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_OrderNumber",
                table: "Cobros",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_TenantId_CreadoEn",
                table: "Cobros",
                columns: new[] { "TenantId", "CreadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_MediosPago_TenantId",
                table: "MediosPago",
                column: "TenantId",
                unique: true,
                filter: "[Activo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cobros");

            migrationBuilder.DropTable(
                name: "MediosPago");

            migrationBuilder.DropColumn(
                name: "MedioPago",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "PagadaEn",
                table: "Facturas");
        }
    }
}
