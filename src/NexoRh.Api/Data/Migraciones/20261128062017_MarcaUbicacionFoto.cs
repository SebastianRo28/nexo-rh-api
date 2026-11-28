using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class MarcaUbicacionFoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Distancia",
                table: "Marcas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Precision",
                table: "Marcas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FotosMarca",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarcaId = table.Column<int>(type: "int", nullable: false),
                    Contenido = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosMarca", x => new { x.TenantId, x.MarcaId });
                    table.ForeignKey(
                        name: "FK_FotosMarca_Marcas_MarcaId",
                        column: x => x.MarcaId,
                        principalTable: "Marcas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosMarca_MarcaId",
                table: "FotosMarca",
                column: "MarcaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FotosMarca_TenantId_MarcaId",
                table: "FotosMarca",
                columns: new[] { "TenantId", "MarcaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosMarca");

            migrationBuilder.DropColumn(
                name: "Distancia",
                table: "Marcas");

            migrationBuilder.DropColumn(
                name: "Precision",
                table: "Marcas");
        }
    }
}
