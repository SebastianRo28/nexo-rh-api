using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class MuroInteracciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComentariosMuro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Elemento = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ElementoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    ColaboradorId = table.Column<int>(type: "int", nullable: true),
                    Autor = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComentariosMuro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaccionesMuro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Elemento = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ElementoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    ColaboradorId = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaccionesMuro", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosMuro_Elemento_ElementoId",
                table: "ComentariosMuro",
                columns: new[] { "Elemento", "ElementoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosMuro_TenantId",
                table: "ComentariosMuro",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReaccionesMuro_Elemento_ElementoId",
                table: "ReaccionesMuro",
                columns: new[] { "Elemento", "ElementoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReaccionesMuro_TenantId_Elemento_ElementoId_UsuarioId",
                table: "ReaccionesMuro",
                columns: new[] { "TenantId", "Elemento", "ElementoId", "UsuarioId" },
                unique: true,
                filter: "[UsuarioId] IS NOT NULL");

            // Los contadores fijos de las publicaciones pasan a ser reacciones y comentarios de colaboradores; luego se quitan.
            migrationBuilder.Sql(SemillaComunicacion.SqlMigrarContadores);

            migrationBuilder.DropColumn(
                name: "Comentarios",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "Reacciones",
                table: "Publicaciones");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComentariosMuro");

            migrationBuilder.DropTable(
                name: "ReaccionesMuro");

            migrationBuilder.AddColumn<int>(
                name: "Comentarios",
                table: "Publicaciones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Reacciones",
                table: "Publicaciones",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
