using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Catalogo.Migraciones
{
    /// <inheritdoc />
    public partial class CicloVida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladoEn",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificadoEliminacion",
                table: "Tenants",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EliminadoEn",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Usos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Colaboradores = table.Column<int>(type: "int", nullable: false),
                    UsuariosActivos = table.Column<int>(type: "int", nullable: false),
                    AlmacenamientoMb = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    LlamadasApi = table.Column<int>(type: "int", nullable: false),
                    CalculadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usos_TenantId_Periodo",
                table: "Usos",
                columns: new[] { "TenantId", "Periodo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Usos");

            migrationBuilder.DropColumn(
                name: "CanceladoEn",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CertificadoEliminacion",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EliminadoEn",
                table: "Tenants");
        }
    }
}
