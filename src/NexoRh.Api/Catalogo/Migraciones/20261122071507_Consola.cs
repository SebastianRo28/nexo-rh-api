using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NexoRh.Api.Catalogo.Migraciones
{
    /// <inheritdoc />
    public partial class Consola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EjecutivoCuenta",
                table: "Tenants",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Industria",
                table: "Tenants",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoSuspension",
                table: "Tenants",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioPactado",
                table: "Tenants",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Anuncios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Cuerpo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Planes = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Modulos = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Desde = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Hasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Canal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Publicado = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anuncios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaPlataforma",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    En = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaPlataforma", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogosGlobales",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Columnas = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogosGlobales", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "DefinicionesFlag",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefinicionesFlag", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "Notas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosPlataforma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotpSecreto = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TotpActivo = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoAcceso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Sello = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosPlataforma", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VersionesCatalogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Clave = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    Registros = table.Column<int>(type: "int", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublicadaPor = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PublicadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionesCatalogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionesCatalogo_CatalogosGlobales_Clave",
                        column: x => x.Clave,
                        principalTable: "CatalogosGlobales",
                        principalColumn: "Clave",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SesionesSoporte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioPlataformaId = table.Column<int>(type: "int", nullable: false),
                    Agente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Ticket = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Modo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Justificacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Autorizada = table.Column<bool>(type: "bit", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Canjeada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Expira = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fin = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesSoporte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionesSoporte_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SesionesSoporte_UsuariosPlataforma_UsuarioPlataformaId",
                        column: x => x.UsuarioPlataformaId,
                        principalTable: "UsuariosPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TokensPlataforma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Expira = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Usado = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensPlataforma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensPlataforma_UsuariosPlataforma_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "UsuariosPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CatalogosGlobales",
                columns: new[] { "Clave", "Columnas", "Nombre" },
                values: new object[,]
                {
                    { "afp", "afp,comision_flujo,comision_saldo,prima_seguro,aporte", "AFP: comisiones y primas" },
                    { "bancos", "codigo,nombre,formato", "Bancos y formatos de pago" },
                    { "feriados", "fecha,descripcion", "Feriados nacionales" },
                    { "monedas", "codigo,nombre,simbolo", "Monedas" },
                    { "ocupaciones", "codigo,descripcion", "Ocupaciones T-Registro (SUNAT)" },
                    { "parametros_tributarios", "clave,valor,vigencia", "Parámetros tributarios (UIT, tasas)" },
                    { "tipos_documento", "codigo,descripcion", "Tipos de documento de identidad" },
                    { "ubigeo", "codigo,departamento,provincia,distrito", "Ubigeo INEI" }
                });

            migrationBuilder.InsertData(
                table: "DefinicionesFlag",
                columns: new[] { "Clave", "CreadoEn", "Descripcion", "Modulo" },
                values: new object[,]
                {
                    { "export_plame_beta", new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Exportador PLAME (beta)", "PAYROLL" },
                    { "firma_masiva_v2", new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Firma masiva v2", "DOCS" },
                    { "nexo_ai_widgets", new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Widgets generados con Nexo AI", null },
                    { "nueva_ui_sst", new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nueva UI de SST", "SST" },
                    { "portal_reconocimientos", new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Reconocimientos en el portal", "COMMS" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaPlataforma_En",
                table: "AuditoriaPlataforma",
                column: "En");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaPlataforma_TenantId",
                table: "AuditoriaPlataforma",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notas_TenantId",
                table: "Notas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesSoporte_TenantId",
                table: "SesionesSoporte",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesSoporte_TokenHash",
                table: "SesionesSoporte",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesSoporte_UsuarioPlataformaId",
                table: "SesionesSoporte",
                column: "UsuarioPlataformaId");

            migrationBuilder.CreateIndex(
                name: "IX_TokensPlataforma_Hash",
                table: "TokensPlataforma",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensPlataforma_UsuarioId",
                table: "TokensPlataforma",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosPlataforma_Correo",
                table: "UsuariosPlataforma",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersionesCatalogo_Clave_Version",
                table: "VersionesCatalogo",
                columns: new[] { "Clave", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Anuncios");

            migrationBuilder.DropTable(
                name: "AuditoriaPlataforma");

            migrationBuilder.DropTable(
                name: "DefinicionesFlag");

            migrationBuilder.DropTable(
                name: "Notas");

            migrationBuilder.DropTable(
                name: "SesionesSoporte");

            migrationBuilder.DropTable(
                name: "TokensPlataforma");

            migrationBuilder.DropTable(
                name: "VersionesCatalogo");

            migrationBuilder.DropTable(
                name: "UsuariosPlataforma");

            migrationBuilder.DropTable(
                name: "CatalogosGlobales");

            migrationBuilder.DropColumn(
                name: "EjecutivoCuenta",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Industria",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MotivoSuspension",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PrecioPactado",
                table: "Tenants");
        }
    }
}
