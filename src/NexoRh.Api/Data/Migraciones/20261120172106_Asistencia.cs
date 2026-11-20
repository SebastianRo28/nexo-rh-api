using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class Asistencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Jornal",
                table: "Colaboradores",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecintoId",
                table: "Colaboradores",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoVacacionesInicial",
                table: "Colaboradores",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "UsaApp",
                table: "Colaboradores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AprobacionesHhee",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AprobadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Enviado = table.Column<bool>(type: "bit", nullable: false),
                    Lineas = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AprobacionesHhee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AprobacionesHhee_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ausencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ausencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ausencias_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Feriados",
                columns: table => new
                {
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feriados", x => x.Fecha);
                });

            migrationBuilder.CreateTable(
                name: "NotasSupervisor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasSupervisor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasSupervisor_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recargos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Horas = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DescansoSustitutorio = table.Column<bool>(type: "bit", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recargos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recargos_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recintos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Empresa = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Ruc = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    CodigoInterno = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Departamento = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Provincia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Distrito = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Info = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    JefaturaId = table.Column<int>(type: "int", nullable: true),
                    AdministrativoId = table.Column<int>(type: "int", nullable: true),
                    Lat = table.Column<double>(type: "float", nullable: false),
                    Lng = table.Column<double>(type: "float", nullable: false),
                    Radio = table.Column<int>(type: "int", nullable: false),
                    Emails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Usuarios = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recintos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesAsistencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Creada = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Aprobador = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesAsistencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesAsistencia_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Sigla = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Jornada = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Entrada = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Salida = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Colacion = table.Column<int>(type: "int", nullable: false),
                    Dias = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tolerancia = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    CicloTrabajo = table.Column<int>(type: "int", nullable: true),
                    CicloDescanso = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vacaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vacaciones_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Licencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Folio = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Emisor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Diagnostico = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Recibida = table.Column<DateOnly>(type: "date", nullable: false),
                    AusenciaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Licencias_Ausencias_AusenciaId",
                        column: x => x.AusenciaId,
                        principalTable: "Ausencias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Licencias_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Marcas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    RecintoId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Hora = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Metodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Lat = table.Column<double>(type: "float", nullable: false),
                    Lng = table.Column<double>(type: "float", nullable: false),
                    DentroRadio = table.Column<bool>(type: "bit", nullable: false),
                    Foto = table.Column<bool>(type: "bit", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RegistradaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Marcas_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Marcas_Recintos_RecintoId",
                        column: x => x.RecintoId,
                        principalTable: "Recintos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarcasFallidas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    RecintoId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Hora = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Metodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Distancia = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarcasFallidas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarcasFallidas_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarcasFallidas_Recintos_RecintoId",
                        column: x => x.RecintoId,
                        principalTable: "Recintos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesTurno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    TurnoId = table.Column<int>(type: "int", nullable: false),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    CicloInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    CreadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesTurno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesTurno_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AsignacionesTurno_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CambiosTurno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaboradorId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    TurnoId = table.Column<int>(type: "int", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CreadoPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosTurno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosTurno_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CambiosTurno_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_RecintoId",
                table: "Colaboradores",
                column: "RecintoId");

            migrationBuilder.CreateIndex(
                name: "IX_AprobacionesHhee_ColaboradorId_Fecha",
                table: "AprobacionesHhee",
                columns: new[] { "ColaboradorId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesTurno_ColaboradorId",
                table: "AsignacionesTurno",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesTurno_TurnoId",
                table: "AsignacionesTurno",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ausencias_ColaboradorId",
                table: "Ausencias",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosTurno_ColaboradorId_Fecha",
                table: "CambiosTurno",
                columns: new[] { "ColaboradorId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CambiosTurno_TurnoId",
                table: "CambiosTurno",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Licencias_AusenciaId",
                table: "Licencias",
                column: "AusenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Licencias_ColaboradorId",
                table: "Licencias",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_ColaboradorId_Fecha",
                table: "Marcas",
                columns: new[] { "ColaboradorId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_Fecha",
                table: "Marcas",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_RecintoId",
                table: "Marcas",
                column: "RecintoId");

            migrationBuilder.CreateIndex(
                name: "IX_MarcasFallidas_ColaboradorId",
                table: "MarcasFallidas",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_MarcasFallidas_RecintoId",
                table: "MarcasFallidas",
                column: "RecintoId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasSupervisor_ColaboradorId",
                table: "NotasSupervisor",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Recargos_ColaboradorId",
                table: "Recargos",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Recintos_Nombre",
                table: "Recintos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAsistencia_ColaboradorId",
                table: "SolicitudesAsistencia",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_Sigla",
                table: "Turnos",
                column: "Sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacaciones_ColaboradorId",
                table: "Vacaciones",
                column: "ColaboradorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Colaboradores_Recintos_RecintoId",
                table: "Colaboradores",
                column: "RecintoId",
                principalTable: "Recintos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Colaboradores_Recintos_RecintoId",
                table: "Colaboradores");

            migrationBuilder.DropTable(
                name: "AprobacionesHhee");

            migrationBuilder.DropTable(
                name: "AsignacionesTurno");

            migrationBuilder.DropTable(
                name: "CambiosTurno");

            migrationBuilder.DropTable(
                name: "Feriados");

            migrationBuilder.DropTable(
                name: "Licencias");

            migrationBuilder.DropTable(
                name: "Marcas");

            migrationBuilder.DropTable(
                name: "MarcasFallidas");

            migrationBuilder.DropTable(
                name: "NotasSupervisor");

            migrationBuilder.DropTable(
                name: "Recargos");

            migrationBuilder.DropTable(
                name: "SolicitudesAsistencia");

            migrationBuilder.DropTable(
                name: "Vacaciones");

            migrationBuilder.DropTable(
                name: "Turnos");

            migrationBuilder.DropTable(
                name: "Ausencias");

            migrationBuilder.DropTable(
                name: "Recintos");

            migrationBuilder.DropIndex(
                name: "IX_Colaboradores_RecintoId",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "Jornal",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "RecintoId",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "SaldoVacacionesInicial",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "UsaApp",
                table: "Colaboradores");
        }
    }
}
