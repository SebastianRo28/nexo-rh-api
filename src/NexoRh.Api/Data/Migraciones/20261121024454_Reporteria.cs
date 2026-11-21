using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class Reporteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cargas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Importador = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Archivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Registros = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Errores = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cargas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EjecucionesReporte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reporte = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Plantilla = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Periodo = table.Column<int>(type: "int", nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Filas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionesReporte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reportes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Plantilla = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Creado = table.Column<DateOnly>(type: "date", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reportes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesReporte_Fecha",
                table: "EjecucionesReporte",
                column: "Fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cargas");

            migrationBuilder.DropTable(
                name: "EjecucionesReporte");

            migrationBuilder.DropTable(
                name: "Reportes");
        }
    }
}
