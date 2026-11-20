using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class UsuariosSeguridad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_ColaboradorId",
                table: "Usuarios");

            migrationBuilder.AddColumn<bool>(
                name: "DebeCambiarPassword",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Sello",
                table: "Usuarios",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            // Cada cuenta existente recibe su propio sello (las sesiones abiertas antes de esta versión se cierran).
            migrationBuilder.Sql("UPDATE Usuarios SET Sello = REPLACE(CONVERT(varchar(36), NEWID()), '-', '')");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ColaboradorId",
                table: "Usuarios",
                column: "ColaboradorId",
                unique: true,
                filter: "[ColaboradorId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_ColaboradorId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DebeCambiarPassword",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Sello",
                table: "Usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ColaboradorId",
                table: "Usuarios",
                column: "ColaboradorId");
        }
    }
}
