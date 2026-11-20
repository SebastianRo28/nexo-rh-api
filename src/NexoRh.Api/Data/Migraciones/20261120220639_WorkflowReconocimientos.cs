using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class WorkflowReconocimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlantillasWorkflow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Operaciones = table.Column<int>(type: "int", nullable: false),
                    ActualizadaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillasWorkflow", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reconocimientos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Para = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ParaIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false),
                    AutorId = table.Column<int>(type: "int", nullable: true),
                    Valor = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reconocimientos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesWorkflow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "18700, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlantillaId = table.Column<int>(type: "int", nullable: true),
                    Proceso = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SolicitanteUsuarioId = table.Column<int>(type: "int", nullable: true),
                    Solicitante = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false),
                    AfectadoId = table.Column<int>(type: "int", nullable: true),
                    Afectado = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false),
                    Documento = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Progreso = table.Column<int>(type: "int", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ResueltaPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Creada = table.Column<DateOnly>(type: "date", nullable: false),
                    Actualizada = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesWorkflow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesWorkflow_Colaboradores_AfectadoId",
                        column: x => x.AfectadoId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SolicitudesWorkflow_PlantillasWorkflow_PlantillaId",
                        column: x => x.PlantillaId,
                        principalTable: "PlantillasWorkflow",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasWorkflow_Nombre",
                table: "PlantillasWorkflow",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reconocimientos_Fecha",
                table: "Reconocimientos",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesWorkflow_AfectadoId",
                table: "SolicitudesWorkflow",
                column: "AfectadoId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesWorkflow_PlantillaId",
                table: "SolicitudesWorkflow",
                column: "PlantillaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reconocimientos");

            migrationBuilder.DropTable(
                name: "SolicitudesWorkflow");

            migrationBuilder.DropTable(
                name: "PlantillasWorkflow");
        }
    }
}
