using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class Administracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bandas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Cargo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Objetivo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PctMin = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PctMax = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bandas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoriasActivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasActivo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Compensaciones",
                columns: table => new
                {
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    UltimaModificacion = table.Column<DateOnly>(type: "date", nullable: true),
                    Evaluacion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompTeorica = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActualizadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compensaciones", x => x.ColaboradorId);
                    table.ForeignKey(
                        name: "FK_Compensaciones_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Archivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    FirmaColaborador = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    FirmaEmpresa = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Oculto = table.Column<bool>(type: "bit", nullable: false),
                    EliminadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EliminadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentos_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EstudioSalarial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Interno = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Industria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Tamano = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    P25 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    P50 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    P75 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    P90 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstudioSalarial", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanesHeadcount",
                columns: table => new
                {
                    Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesHeadcount", x => new { x.Tipo, x.Nombre });
                });

            migrationBuilder.CreateTable(
                name: "PlantillasDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Carpeta = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillasDocumento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Posiciones",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Division = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SubArea = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Ingreso = table.Column<DateOnly>(type: "date", nullable: false),
                    Termino = table.Column<DateOnly>(type: "date", nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posiciones", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "ProductosActivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosActivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductosActivo_CategoriasActivo_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasActivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Activos",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AsignadoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activos", x => x.Codigo);
                    table.ForeignKey(
                        name: "FK_Activos_Colaboradores_AsignadoId",
                        column: x => x.AsignadoId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Activos_ProductosActivo_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "ProductosActivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrdenesActivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "100, 1"),
                    Origen = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ParaId = table.Column<int>(type: "int", nullable: true),
                    Para = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Solicitante = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false),
                    Solicitada = table.Column<DateOnly>(type: "date", nullable: false),
                    AsignarEl = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ActivoCodigo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenesActivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdenesActivo_Colaboradores_ParaId",
                        column: x => x.ParaId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrdenesActivo_ProductosActivo_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "ProductosActivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosActivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivoCodigo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Asignado = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Admin = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosActivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosActivo_Activos_ActivoCodigo",
                        column: x => x.ActivoCodigo,
                        principalTable: "Activos",
                        principalColumn: "Codigo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activos_AsignadoId",
                table: "Activos",
                column: "AsignadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Activos_ProductoId",
                table: "Activos",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Bandas_Cargo",
                table: "Bandas",
                column: "Cargo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasActivo_Nombre",
                table: "CategoriasActivo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_ColaboradorId",
                table: "Documentos",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosActivo_ActivoCodigo",
                table: "MovimientosActivo",
                column: "ActivoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesActivo_ParaId",
                table: "OrdenesActivo",
                column: "ParaId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesActivo_ProductoId",
                table: "OrdenesActivo",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasDocumento_Nombre",
                table: "PlantillasDocumento",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductosActivo_CategoriaId",
                table: "ProductosActivo",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosActivo_Nombre",
                table: "ProductosActivo",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bandas");

            migrationBuilder.DropTable(
                name: "Compensaciones");

            migrationBuilder.DropTable(
                name: "Documentos");

            migrationBuilder.DropTable(
                name: "EstudioSalarial");

            migrationBuilder.DropTable(
                name: "MovimientosActivo");

            migrationBuilder.DropTable(
                name: "OrdenesActivo");

            migrationBuilder.DropTable(
                name: "PlanesHeadcount");

            migrationBuilder.DropTable(
                name: "PlantillasDocumento");

            migrationBuilder.DropTable(
                name: "Posiciones");

            migrationBuilder.DropTable(
                name: "Activos");

            migrationBuilder.DropTable(
                name: "ProductosActivo");

            migrationBuilder.DropTable(
                name: "CategoriasActivo");
        }
    }
}
