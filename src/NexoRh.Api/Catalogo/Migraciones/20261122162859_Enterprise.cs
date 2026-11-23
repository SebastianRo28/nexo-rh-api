using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Catalogo.Migraciones
{
    /// <inheritdoc />
    public partial class Enterprise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CadenaConexion",
                table: "Tenants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnMantenimiento",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificadoEn",
                table: "Dominios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorVerificacion",
                table: "Dominios",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoCertificado",
                table: "Dominios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Activo");

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaRevision",
                table: "Dominios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificadoEn",
                table: "Dominios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DkimDominio",
                table: "Correos",
                type: "nvarchar(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remitente",
                table: "Correos",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CorreosDominio",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dominio = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    Remitente = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Selector = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                    ClavePublica = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ClavePrivada = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SpfOk = table.Column<bool>(type: "bit", nullable: false),
                    DkimOk = table.Column<bool>(type: "bit", nullable: false),
                    DmarcOk = table.Column<bool>(type: "bit", nullable: false),
                    VerificadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaRevision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorreosDominio", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_CorreosDominio_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Planes",
                keyColumn: "Codigo",
                keyValue: "ENTERPRISE",
                column: "Funciones",
                value: "SSO_SOCIAL,SAML,CUSTOM_DOMAIN,CUSTOM_EMAIL,HIDE_POWERED_BY,DEDICATED_DB");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorreosDominio");

            migrationBuilder.DropColumn(
                name: "EnMantenimiento",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CertificadoEn",
                table: "Dominios");

            migrationBuilder.DropColumn(
                name: "ErrorVerificacion",
                table: "Dominios");

            migrationBuilder.DropColumn(
                name: "EstadoCertificado",
                table: "Dominios");

            migrationBuilder.DropColumn(
                name: "UltimaRevision",
                table: "Dominios");

            migrationBuilder.DropColumn(
                name: "VerificadoEn",
                table: "Dominios");

            migrationBuilder.DropColumn(
                name: "DkimDominio",
                table: "Correos");

            migrationBuilder.DropColumn(
                name: "Remitente",
                table: "Correos");

            migrationBuilder.AlterColumn<string>(
                name: "CadenaConexion",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Planes",
                keyColumn: "Codigo",
                keyValue: "ENTERPRISE",
                column: "Funciones",
                value: "SSO_SOCIAL,SAML,CUSTOM_DOMAIN,HIDE_POWERED_BY,DEDICATED_DB");
        }
    }
}
