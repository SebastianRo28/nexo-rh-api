using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class AccesoUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PendienteActivacion",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrivacidadAceptada",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TotpActivo",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecreto",
                table: "Usuarios",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TokensUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Expira = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Usado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Creado = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensUsuario_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokensUsuario_TenantId_Hash",
                table: "TokensUsuario",
                columns: new[] { "TenantId", "Hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensUsuario_UsuarioId",
                table: "TokensUsuario",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokensUsuario");

            migrationBuilder.DropColumn(
                name: "PendienteActivacion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PrivacidadAceptada",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TotpActivo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TotpSecreto",
                table: "Usuarios");
        }
    }
}
