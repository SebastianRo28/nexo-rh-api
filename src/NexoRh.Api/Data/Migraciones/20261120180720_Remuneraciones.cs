using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class Remuneraciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComisionesAfp",
                columns: table => new
                {
                    Afp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ComisionFlujoPct = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false),
                    ComisionSaldoPct = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComisionesAfp", x => x.Afp);
                });

            migrationBuilder.CreateTable(
                name: "ParametrosRem",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    Unidad = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    VigenteDesde = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Fuente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosRem", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "Periodos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Cerrado = table.Column<bool>(type: "bit", nullable: false),
                    ProcesadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcesadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CerradoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CerradoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Periodos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TiposItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Clase = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Plame = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Essalud = table.Column<bool>(type: "bit", nullable: false),
                    Pension = table.Column<bool>(type: "bit", nullable: false),
                    Quinta = table.Column<bool>(type: "bit", nullable: false),
                    Automatico = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TramosRenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Desde = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Hasta = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    TasaPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TramosRenta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Boletas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Periodo = table.Column<int>(type: "int", nullable: false),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false),
                    Documento = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Regimen = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DiasLaborados = table.Column<int>(type: "int", nullable: false),
                    DiasVacaciones = table.Column<int>(type: "int", nullable: false),
                    DiasNoTrabajados = table.Column<int>(type: "int", nullable: false),
                    Ingresos = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Descuentos = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Aportes = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Neto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BaseQuinta = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    RetencionQuinta = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Definitiva = table.Column<bool>(type: "bit", nullable: false),
                    CalculadaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Lineas = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boletas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boletas_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Boletas_Periodos_Periodo",
                        column: x => x.Periodo,
                        principalTable: "Periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    TipoItemId = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Desde = table.Column<int>(type: "int", nullable: false),
                    Hasta = table.Column<int>(type: "int", nullable: true),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesItem_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AsignacionesItem_TiposItem_TipoItemId",
                        column: x => x.TipoItemId,
                        principalTable: "TiposItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesItem_ColaboradorId",
                table: "AsignacionesItem",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesItem_TipoItemId",
                table: "AsignacionesItem",
                column: "TipoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Boletas_ColaboradorId",
                table: "Boletas",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Boletas_Periodo_ColaboradorId",
                table: "Boletas",
                columns: new[] { "Periodo", "ColaboradorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposItem_Codigo",
                table: "TiposItem",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionesItem");

            migrationBuilder.DropTable(
                name: "Boletas");

            migrationBuilder.DropTable(
                name: "ComisionesAfp");

            migrationBuilder.DropTable(
                name: "ParametrosRem");

            migrationBuilder.DropTable(
                name: "TramosRenta");

            migrationBuilder.DropTable(
                name: "TiposItem");

            migrationBuilder.DropTable(
                name: "Periodos");
        }
    }
}
