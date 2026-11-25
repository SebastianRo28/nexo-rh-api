using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class RegistrosModulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Anexo",
                table: "Colaboradores",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegistrosModulo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Coleccion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ColaboradorId = table.Column<int>(type: "int", nullable: true),
                    Datos = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosModulo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosModulo_Coleccion_ColaboradorId",
                table: "RegistrosModulo",
                columns: new[] { "Coleccion", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosModulo_TenantId",
                table: "RegistrosModulo",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosModulo");

            migrationBuilder.DropColumn(
                name: "Anexo",
                table: "Colaboradores");
        }
    }
}
