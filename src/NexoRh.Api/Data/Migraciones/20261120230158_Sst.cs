using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class Sst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CumplimientosSst",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Normativa = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CumplimientosSst", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequisitosSst",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CumplimientoId = table.Column<int>(type: "int", nullable: false),
                    AplicaA = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    VigenciaMeses = table.Column<int>(type: "int", nullable: true),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequisitosSst", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequisitosSst_CumplimientosSst_CumplimientoId",
                        column: x => x.CumplimientoId,
                        principalTable: "CumplimientosSst",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesSst",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequisitoId = table.Column<int>(type: "int", nullable: false),
                    ColaboradorId = table.Column<int>(type: "int", nullable: true),
                    Empresa = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Finalizada = table.Column<bool>(type: "bit", nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesSst", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesSst_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AsignacionesSst_RequisitosSst_RequisitoId",
                        column: x => x.RequisitoId,
                        principalTable: "RequisitosSst",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenciasSst",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsignacionId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Vence = table.Column<DateOnly>(type: "date", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RegistradaPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RegistradaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivoNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ArchivoTipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ArchivoTamano = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenciasSst", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenciasSst_AsignacionesSst_AsignacionId",
                        column: x => x.AsignacionId,
                        principalTable: "AsignacionesSst",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosEvidenciaSst",
                columns: table => new
                {
                    EvidenciaId = table.Column<int>(type: "int", nullable: false),
                    Contenido = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosEvidenciaSst", x => x.EvidenciaId);
                    table.ForeignKey(
                        name: "FK_ArchivosEvidenciaSst_EvidenciasSst_EvidenciaId",
                        column: x => x.EvidenciaId,
                        principalTable: "EvidenciasSst",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesSst_ColaboradorId",
                table: "AsignacionesSst",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesSst_RequisitoId",
                table: "AsignacionesSst",
                column: "RequisitoId");

            migrationBuilder.CreateIndex(
                name: "IX_CumplimientosSst_Nombre",
                table: "CumplimientosSst",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasSst_AsignacionId",
                table: "EvidenciasSst",
                column: "AsignacionId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosSst_CumplimientoId",
                table: "RequisitosSst",
                column: "CumplimientoId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosSst_Nombre",
                table: "RequisitosSst",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosEvidenciaSst");

            migrationBuilder.DropTable(
                name: "EvidenciasSst");

            migrationBuilder.DropTable(
                name: "AsignacionesSst");

            migrationBuilder.DropTable(
                name: "RequisitosSst");

            migrationBuilder.DropTable(
                name: "CumplimientosSst");
        }
    }
}
