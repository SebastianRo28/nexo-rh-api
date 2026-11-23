using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoRh.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class MultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activos_Colaboradores_AsignadoId",
                table: "Activos");

            migrationBuilder.DropForeignKey(
                name: "FK_AprobacionesHhee_Colaboradores_ColaboradorId",
                table: "AprobacionesHhee");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesItem_Colaboradores_ColaboradorId",
                table: "AsignacionesItem");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesItem_TiposItem_TipoItemId",
                table: "AsignacionesItem");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesSst_Colaboradores_ColaboradorId",
                table: "AsignacionesSst");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesTurno_Colaboradores_ColaboradorId",
                table: "AsignacionesTurno");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesTurno_Turnos_TurnoId",
                table: "AsignacionesTurno");

            migrationBuilder.DropForeignKey(
                name: "FK_Ausencias_Colaboradores_ColaboradorId",
                table: "Ausencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Boletas_Colaboradores_ColaboradorId",
                table: "Boletas");

            migrationBuilder.DropForeignKey(
                name: "FK_Boletas_Periodos_Periodo",
                table: "Boletas");

            migrationBuilder.DropForeignKey(
                name: "FK_CambiosTurno_Colaboradores_ColaboradorId",
                table: "CambiosTurno");

            migrationBuilder.DropForeignKey(
                name: "FK_CambiosTurno_Turnos_TurnoId",
                table: "CambiosTurno");

            migrationBuilder.DropForeignKey(
                name: "FK_Colaboradores_Colaboradores_JefeId",
                table: "Colaboradores");

            migrationBuilder.DropForeignKey(
                name: "FK_Colaboradores_Recintos_RecintoId",
                table: "Colaboradores");

            migrationBuilder.DropForeignKey(
                name: "FK_Compensaciones_Colaboradores_ColaboradorId",
                table: "Compensaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Documentos_Colaboradores_ColaboradorId",
                table: "Documentos");

            migrationBuilder.DropForeignKey(
                name: "FK_Licencias_Colaboradores_ColaboradorId",
                table: "Licencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Marcas_Colaboradores_ColaboradorId",
                table: "Marcas");

            migrationBuilder.DropForeignKey(
                name: "FK_Marcas_Recintos_RecintoId",
                table: "Marcas");

            migrationBuilder.DropForeignKey(
                name: "FK_MarcasFallidas_Colaboradores_ColaboradorId",
                table: "MarcasFallidas");

            migrationBuilder.DropForeignKey(
                name: "FK_MarcasFallidas_Recintos_RecintoId",
                table: "MarcasFallidas");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosActivo_Activos_ActivoCodigo",
                table: "MovimientosActivo");

            migrationBuilder.DropForeignKey(
                name: "FK_NotasSupervisor_Colaboradores_ColaboradorId",
                table: "NotasSupervisor");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesActivo_Colaboradores_ParaId",
                table: "OrdenesActivo");

            migrationBuilder.DropForeignKey(
                name: "FK_ParticipacionesEncuesta_Colaboradores_ColaboradorId",
                table: "ParticipacionesEncuesta");

            migrationBuilder.DropForeignKey(
                name: "FK_Recargos_Colaboradores_ColaboradorId",
                table: "Recargos");

            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesAsistencia_Colaboradores_ColaboradorId",
                table: "SolicitudesAsistencia");

            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesWorkflow_Colaboradores_AfectadoId",
                table: "SolicitudesWorkflow");

            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesWorkflow_PlantillasWorkflow_PlantillaId",
                table: "SolicitudesWorkflow");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Colaboradores_ColaboradorId",
                table: "Usuarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Vacaciones_Colaboradores_ColaboradorId",
                table: "Vacaciones");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_ColaboradorId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Turnos",
                table: "Turnos");

            migrationBuilder.DropIndex(
                name: "IX_Turnos_Sigla",
                table: "Turnos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TiposItem",
                table: "TiposItem");

            migrationBuilder.DropIndex(
                name: "IX_TiposItem_Codigo",
                table: "TiposItem");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesWorkflow_PlantillaId",
                table: "SolicitudesWorkflow");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesAsistencia_ColaboradorId",
                table: "SolicitudesAsistencia");

            migrationBuilder.DropIndex(
                name: "IX_RequisitosSst_Nombre",
                table: "RequisitosSst");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Recintos",
                table: "Recintos");

            migrationBuilder.DropIndex(
                name: "IX_Recintos_Nombre",
                table: "Recintos");

            migrationBuilder.DropIndex(
                name: "IX_Recargos_ColaboradorId",
                table: "Recargos");

            migrationBuilder.DropIndex(
                name: "IX_ProductosActivo_Nombre",
                table: "ProductosActivo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Posiciones",
                table: "Posiciones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlantillasWorkflow",
                table: "PlantillasWorkflow");

            migrationBuilder.DropIndex(
                name: "IX_PlantillasWorkflow_Nombre",
                table: "PlantillasWorkflow");

            migrationBuilder.DropIndex(
                name: "IX_PlantillasDocumento_Nombre",
                table: "PlantillasDocumento");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanesHeadcount",
                table: "PlanesHeadcount");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Periodos",
                table: "Periodos");

            migrationBuilder.DropIndex(
                name: "IX_Perfiles_Nombre",
                table: "Perfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ParticipacionesEncuesta",
                table: "ParticipacionesEncuesta");

            migrationBuilder.DropIndex(
                name: "IX_ParticipacionesEncuesta_ColaboradorId",
                table: "ParticipacionesEncuesta");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ParametrosRem",
                table: "ParametrosRem");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesActivo_ParaId",
                table: "OrdenesActivo");

            migrationBuilder.DropIndex(
                name: "IX_NotasSupervisor_ColaboradorId",
                table: "NotasSupervisor");

            migrationBuilder.DropIndex(
                name: "IX_MarcasFallidas_ColaboradorId",
                table: "MarcasFallidas");

            migrationBuilder.DropIndex(
                name: "IX_MarcasFallidas_RecintoId",
                table: "MarcasFallidas");

            migrationBuilder.DropIndex(
                name: "IX_Marcas_RecintoId",
                table: "Marcas");

            migrationBuilder.DropIndex(
                name: "IX_Licencias_ColaboradorId",
                table: "Licencias");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Feriados",
                table: "Feriados");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FamiliasCargo",
                table: "FamiliasCargo");

            migrationBuilder.DropIndex(
                name: "IX_FamiliasCargo_Nombre",
                table: "FamiliasCargo");

            migrationBuilder.DropIndex(
                name: "IX_Denuncias_Codigo",
                table: "Denuncias");

            migrationBuilder.DropIndex(
                name: "IX_CumplimientosSst_Nombre",
                table: "CumplimientosSst");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Configuraciones",
                table: "Configuraciones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Competencias",
                table: "Competencias");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Compensaciones",
                table: "Compensaciones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ComisionesAfp",
                table: "ComisionesAfp");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Colaboradores",
                table: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_Colaboradores_Documento",
                table: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_Colaboradores_JefeId",
                table: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_Colaboradores_RecintoId",
                table: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_CategoriasActivo_Nombre",
                table: "CategoriasActivo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Cargos",
                table: "Cargos");

            migrationBuilder.DropIndex(
                name: "IX_Cargos_Nombre",
                table: "Cargos");

            migrationBuilder.DropIndex(
                name: "IX_CambiosTurno_ColaboradorId_Fecha",
                table: "CambiosTurno");

            migrationBuilder.DropIndex(
                name: "IX_CambiosTurno_TurnoId",
                table: "CambiosTurno");

            migrationBuilder.DropIndex(
                name: "IX_Boletas_ColaboradorId",
                table: "Boletas");

            migrationBuilder.DropIndex(
                name: "IX_Boletas_Periodo_ColaboradorId",
                table: "Boletas");

            migrationBuilder.DropIndex(
                name: "IX_Bandas_Cargo",
                table: "Bandas");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesTurno_TurnoId",
                table: "AsignacionesTurno");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesSst_ColaboradorId",
                table: "AsignacionesSst");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesItem_TipoItemId",
                table: "AsignacionesItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ArchivosEvidenciaSst",
                table: "ArchivosEvidenciaSst");

            migrationBuilder.DropIndex(
                name: "IX_AprobacionesHhee_ColaboradorId_Fecha",
                table: "AprobacionesHhee");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Activos",
                table: "Activos");

            migrationBuilder.DropIndex(
                name: "IX_Activos_AsignadoId",
                table: "Activos");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Vacaciones",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Usuarios",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Turnos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TramosRenta",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TiposItem",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "SolicitudesWorkflow",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "SolicitudesAsistencia",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RespuestasEncuesta",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RequisitosSst",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Reportes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Reconocimientos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Recintos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Recargos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Publicaciones",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductosActivo",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Posiciones",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PlantillasWorkflow",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PlantillasDocumento",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PlanesHeadcount",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Periodos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Perfiles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ParticipacionesEncuesta",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ParametrosRem",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OrdenesActivo",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "NotasSupervisor",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MovimientosActivo",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MensajesDenuncia",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MarcasFallidas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Marcas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Licencias",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "FilasCatalogo",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Feriados",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "FamiliasCargo",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EvidenciasSst",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EstudioSalarial",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Encuestas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EjecucionesReporte",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Documentos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Denuncias",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CumplimientosSst",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Configuraciones",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Competencias",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Compensaciones",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ComisionesAfp",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Colaboradores",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CategoriasActivo",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Cargos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Cargas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CambiosTurno",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Cambios",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Boletas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Bandas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Ausencias",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AsignacionesTurno",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AsignacionesSst",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AsignacionesItem",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ArchivosEvidenciaSst",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AprobacionesHhee",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Activos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("8c1f5a2e-4b7d-4e19-9a0c-2f6d1e3b7a44"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Turnos",
                table: "Turnos",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_TiposItem",
                table: "TiposItem",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Recintos",
                table: "Recintos",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Posiciones",
                table: "Posiciones",
                columns: new[] { "TenantId", "Codigo" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlantillasWorkflow",
                table: "PlantillasWorkflow",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanesHeadcount",
                table: "PlanesHeadcount",
                columns: new[] { "TenantId", "Tipo", "Nombre" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Periodos",
                table: "Periodos",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ParticipacionesEncuesta",
                table: "ParticipacionesEncuesta",
                columns: new[] { "TenantId", "EncuestaId", "ColaboradorId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ParametrosRem",
                table: "ParametrosRem",
                columns: new[] { "TenantId", "Clave" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Feriados",
                table: "Feriados",
                columns: new[] { "TenantId", "Fecha" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_FamiliasCargo",
                table: "FamiliasCargo",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Configuraciones",
                table: "Configuraciones",
                columns: new[] { "TenantId", "Clave" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Competencias",
                table: "Competencias",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Compensaciones",
                table: "Compensaciones",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ComisionesAfp",
                table: "ComisionesAfp",
                columns: new[] { "TenantId", "Afp" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Colaboradores",
                table: "Colaboradores",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cargos",
                table: "Cargos",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ArchivosEvidenciaSst",
                table: "ArchivosEvidenciaSst",
                columns: new[] { "TenantId", "EvidenciaId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Activos",
                table: "Activos",
                columns: new[] { "TenantId", "Codigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacaciones_TenantId_ColaboradorId",
                table: "Vacaciones",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_TenantId_ColaboradorId",
                table: "Usuarios",
                columns: new[] { "TenantId", "ColaboradorId" },
                unique: true,
                filter: "[ColaboradorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_TenantId_Correo",
                table: "Usuarios",
                columns: new[] { "TenantId", "Correo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_TenantId_Sigla",
                table: "Turnos",
                columns: new[] { "TenantId", "Sigla" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TramosRenta_TenantId",
                table: "TramosRenta",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposItem_TenantId_Codigo",
                table: "TiposItem",
                columns: new[] { "TenantId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesWorkflow_TenantId_AfectadoId",
                table: "SolicitudesWorkflow",
                columns: new[] { "TenantId", "AfectadoId" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesWorkflow_TenantId_PlantillaId",
                table: "SolicitudesWorkflow",
                columns: new[] { "TenantId", "PlantillaId" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAsistencia_TenantId_ColaboradorId",
                table: "SolicitudesAsistencia",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasEncuesta_TenantId",
                table: "RespuestasEncuesta",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosSst_TenantId_Nombre",
                table: "RequisitosSst",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reportes_TenantId",
                table: "Reportes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Reconocimientos_TenantId",
                table: "Reconocimientos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Recintos_TenantId_Nombre",
                table: "Recintos",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recargos_TenantId_ColaboradorId",
                table: "Recargos",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_TenantId",
                table: "Publicaciones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosActivo_TenantId_Nombre",
                table: "ProductosActivo",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasWorkflow_TenantId_Nombre",
                table: "PlantillasWorkflow",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasDocumento_TenantId_Nombre",
                table: "PlantillasDocumento",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Perfiles_TenantId_Nombre",
                table: "Perfiles",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipacionesEncuesta_EncuestaId",
                table: "ParticipacionesEncuesta",
                column: "EncuestaId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipacionesEncuesta_TenantId_ColaboradorId",
                table: "ParticipacionesEncuesta",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesActivo_TenantId_ParaId",
                table: "OrdenesActivo",
                columns: new[] { "TenantId", "ParaId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasSupervisor_TenantId_ColaboradorId",
                table: "NotasSupervisor",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosActivo_TenantId_ActivoCodigo",
                table: "MovimientosActivo",
                columns: new[] { "TenantId", "ActivoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesDenuncia_TenantId",
                table: "MensajesDenuncia",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MarcasFallidas_TenantId_ColaboradorId",
                table: "MarcasFallidas",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarcasFallidas_TenantId_RecintoId",
                table: "MarcasFallidas",
                columns: new[] { "TenantId", "RecintoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_TenantId_ColaboradorId",
                table: "Marcas",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_TenantId_RecintoId",
                table: "Marcas",
                columns: new[] { "TenantId", "RecintoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Licencias_TenantId_ColaboradorId",
                table: "Licencias",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_FilasCatalogo_TenantId",
                table: "FilasCatalogo",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FamiliasCargo_TenantId_Nombre",
                table: "FamiliasCargo",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasSst_TenantId",
                table: "EvidenciasSst",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EstudioSalarial_TenantId",
                table: "EstudioSalarial",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Encuestas_TenantId",
                table: "Encuestas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesReporte_TenantId",
                table: "EjecucionesReporte",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_TenantId_ColaboradorId",
                table: "Documentos",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_TenantId_Codigo",
                table: "Denuncias",
                columns: new[] { "TenantId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CumplimientosSst_TenantId_Nombre",
                table: "CumplimientosSst",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_TenantId_Documento",
                table: "Colaboradores",
                columns: new[] { "TenantId", "Documento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_TenantId_JefeId",
                table: "Colaboradores",
                columns: new[] { "TenantId", "JefeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_TenantId_RecintoId",
                table: "Colaboradores",
                columns: new[] { "TenantId", "RecintoId" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasActivo_TenantId_Nombre",
                table: "CategoriasActivo",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_TenantId_Nombre",
                table: "Cargos",
                columns: new[] { "TenantId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cargas_TenantId",
                table: "Cargas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosTurno_TenantId_ColaboradorId_Fecha",
                table: "CambiosTurno",
                columns: new[] { "TenantId", "ColaboradorId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CambiosTurno_TenantId_TurnoId",
                table: "CambiosTurno",
                columns: new[] { "TenantId", "TurnoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Cambios_TenantId",
                table: "Cambios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Boletas_TenantId_ColaboradorId",
                table: "Boletas",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Boletas_TenantId_Periodo_ColaboradorId",
                table: "Boletas",
                columns: new[] { "TenantId", "Periodo", "ColaboradorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bandas_TenantId_Cargo",
                table: "Bandas",
                columns: new[] { "TenantId", "Cargo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ausencias_TenantId_ColaboradorId",
                table: "Ausencias",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesTurno_TenantId_ColaboradorId",
                table: "AsignacionesTurno",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesTurno_TenantId_TurnoId",
                table: "AsignacionesTurno",
                columns: new[] { "TenantId", "TurnoId" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesSst_TenantId_ColaboradorId",
                table: "AsignacionesSst",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesItem_TenantId_ColaboradorId",
                table: "AsignacionesItem",
                columns: new[] { "TenantId", "ColaboradorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesItem_TenantId_TipoItemId",
                table: "AsignacionesItem",
                columns: new[] { "TenantId", "TipoItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosEvidenciaSst_EvidenciaId",
                table: "ArchivosEvidenciaSst",
                column: "EvidenciaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosEvidenciaSst_TenantId_EvidenciaId",
                table: "ArchivosEvidenciaSst",
                columns: new[] { "TenantId", "EvidenciaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AprobacionesHhee_TenantId_ColaboradorId_Fecha",
                table: "AprobacionesHhee",
                columns: new[] { "TenantId", "ColaboradorId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activos_TenantId_AsignadoId",
                table: "Activos",
                columns: new[] { "TenantId", "AsignadoId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Activos_Colaboradores_TenantId_AsignadoId",
                table: "Activos",
                columns: new[] { "TenantId", "AsignadoId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_AprobacionesHhee_Colaboradores_TenantId_ColaboradorId",
                table: "AprobacionesHhee",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesItem_Colaboradores_TenantId_ColaboradorId",
                table: "AsignacionesItem",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesItem_TiposItem_TenantId_TipoItemId",
                table: "AsignacionesItem",
                columns: new[] { "TenantId", "TipoItemId" },
                principalTable: "TiposItem",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesSst_Colaboradores_TenantId_ColaboradorId",
                table: "AsignacionesSst",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesTurno_Colaboradores_TenantId_ColaboradorId",
                table: "AsignacionesTurno",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesTurno_Turnos_TenantId_TurnoId",
                table: "AsignacionesTurno",
                columns: new[] { "TenantId", "TurnoId" },
                principalTable: "Turnos",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ausencias_Colaboradores_TenantId_ColaboradorId",
                table: "Ausencias",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boletas_Colaboradores_TenantId_ColaboradorId",
                table: "Boletas",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boletas_Periodos_TenantId_Periodo",
                table: "Boletas",
                columns: new[] { "TenantId", "Periodo" },
                principalTable: "Periodos",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CambiosTurno_Colaboradores_TenantId_ColaboradorId",
                table: "CambiosTurno",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CambiosTurno_Turnos_TenantId_TurnoId",
                table: "CambiosTurno",
                columns: new[] { "TenantId", "TurnoId" },
                principalTable: "Turnos",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Colaboradores_Colaboradores_TenantId_JefeId",
                table: "Colaboradores",
                columns: new[] { "TenantId", "JefeId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Colaboradores_Recintos_TenantId_RecintoId",
                table: "Colaboradores",
                columns: new[] { "TenantId", "RecintoId" },
                principalTable: "Recintos",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Compensaciones_Colaboradores_TenantId_ColaboradorId",
                table: "Compensaciones",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Documentos_Colaboradores_TenantId_ColaboradorId",
                table: "Documentos",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Licencias_Colaboradores_TenantId_ColaboradorId",
                table: "Licencias",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Marcas_Colaboradores_TenantId_ColaboradorId",
                table: "Marcas",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Marcas_Recintos_TenantId_RecintoId",
                table: "Marcas",
                columns: new[] { "TenantId", "RecintoId" },
                principalTable: "Recintos",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MarcasFallidas_Colaboradores_TenantId_ColaboradorId",
                table: "MarcasFallidas",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MarcasFallidas_Recintos_TenantId_RecintoId",
                table: "MarcasFallidas",
                columns: new[] { "TenantId", "RecintoId" },
                principalTable: "Recintos",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosActivo_Activos_TenantId_ActivoCodigo",
                table: "MovimientosActivo",
                columns: new[] { "TenantId", "ActivoCodigo" },
                principalTable: "Activos",
                principalColumns: new[] { "TenantId", "Codigo" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotasSupervisor_Colaboradores_TenantId_ColaboradorId",
                table: "NotasSupervisor",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesActivo_Colaboradores_TenantId_ParaId",
                table: "OrdenesActivo",
                columns: new[] { "TenantId", "ParaId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_ParticipacionesEncuesta_Colaboradores_TenantId_ColaboradorId",
                table: "ParticipacionesEncuesta",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recargos_Colaboradores_TenantId_ColaboradorId",
                table: "Recargos",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesAsistencia_Colaboradores_TenantId_ColaboradorId",
                table: "SolicitudesAsistencia",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesWorkflow_Colaboradores_TenantId_AfectadoId",
                table: "SolicitudesWorkflow",
                columns: new[] { "TenantId", "AfectadoId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesWorkflow_PlantillasWorkflow_TenantId_PlantillaId",
                table: "SolicitudesWorkflow",
                columns: new[] { "TenantId", "PlantillaId" },
                principalTable: "PlantillasWorkflow",
                principalColumns: new[] { "TenantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Colaboradores_TenantId_ColaboradorId",
                table: "Usuarios",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Vacaciones_Colaboradores_TenantId_ColaboradorId",
                table: "Vacaciones",
                columns: new[] { "TenantId", "ColaboradorId" },
                principalTable: "Colaboradores",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            // Las filas existentes quedaron en el tenant de ejemplo (Comercial Andina). Sin valor por defecto en adelante:
            // cada fila nueva lleva el TenantId del request.
            migrationBuilder.Sql(@"DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(d.name) + N'; '
FROM sys.default_constraints d
JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
JOIN sys.tables t ON t.object_id = d.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE c.name = N'TenantId';
EXEC sp_executesql @sql;");

            // Row-Level Security: segunda barrera de aislamiento. El back fija SESSION_CONTEXT('tid') al abrir cada conexión.
            migrationBuilder.Sql("CREATE SCHEMA rls;");
            migrationBuilder.Sql(@"CREATE FUNCTION rls.fn_tenant(@TenantId uniqueidentifier)
RETURNS TABLE WITH SCHEMABINDING
AS RETURN
    SELECT 1 AS permitido
    WHERE @TenantId = CAST(SESSION_CONTEXT(N'tid') AS uniqueidentifier)
       OR CAST(SESSION_CONTEXT(N'plataforma') AS bit) = 1;");
            // Recrea la política con TODAS las tablas que tienen TenantId (la API la ejecuta tras cada migración).
            migrationBuilder.Sql(@"CREATE PROCEDURE rls.ActualizarPolitica
AS
BEGIN
    SET NOCOUNT ON;
    IF OBJECT_ID(N'rls.PoliticaTenant', N'SP') IS NOT NULL DROP SECURITY POLICY rls.PoliticaTenant;
    DECLARE @sql nvarchar(max);
    SELECT @sql = STRING_AGG(CAST(N'ADD FILTER PREDICATE rls.fn_tenant(TenantId) ON dbo.' + QUOTENAME(t.name)
                 + N', ADD BLOCK PREDICATE rls.fn_tenant(TenantId) ON dbo.' + QUOTENAME(t.name) AS nvarchar(max)), N', ')
    FROM sys.tables t
    JOIN sys.columns c ON c.object_id = t.object_id AND c.name = N'TenantId'
    WHERE t.schema_id = SCHEMA_ID(N'dbo');
    IF @sql IS NULL RETURN;
    SET @sql = N'CREATE SECURITY POLICY rls.PoliticaTenant ' + @sql + N' WITH (STATE = ON, SCHEMABINDING = OFF);';
    EXEC sp_executesql @sql;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID(N'rls.PoliticaTenant', N'SP') IS NOT NULL DROP SECURITY POLICY rls.PoliticaTenant;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS rls.ActualizarPolitica;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS rls.fn_tenant;");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS rls;");

            migrationBuilder.DropForeignKey(
                name: "FK_Activos_Colaboradores_TenantId_AsignadoId",
                table: "Activos");

            migrationBuilder.DropForeignKey(
                name: "FK_AprobacionesHhee_Colaboradores_TenantId_ColaboradorId",
                table: "AprobacionesHhee");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesItem_Colaboradores_TenantId_ColaboradorId",
                table: "AsignacionesItem");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesItem_TiposItem_TenantId_TipoItemId",
                table: "AsignacionesItem");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesSst_Colaboradores_TenantId_ColaboradorId",
                table: "AsignacionesSst");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesTurno_Colaboradores_TenantId_ColaboradorId",
                table: "AsignacionesTurno");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesTurno_Turnos_TenantId_TurnoId",
                table: "AsignacionesTurno");

            migrationBuilder.DropForeignKey(
                name: "FK_Ausencias_Colaboradores_TenantId_ColaboradorId",
                table: "Ausencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Boletas_Colaboradores_TenantId_ColaboradorId",
                table: "Boletas");

            migrationBuilder.DropForeignKey(
                name: "FK_Boletas_Periodos_TenantId_Periodo",
                table: "Boletas");

            migrationBuilder.DropForeignKey(
                name: "FK_CambiosTurno_Colaboradores_TenantId_ColaboradorId",
                table: "CambiosTurno");

            migrationBuilder.DropForeignKey(
                name: "FK_CambiosTurno_Turnos_TenantId_TurnoId",
                table: "CambiosTurno");

            migrationBuilder.DropForeignKey(
                name: "FK_Colaboradores_Colaboradores_TenantId_JefeId",
                table: "Colaboradores");

            migrationBuilder.DropForeignKey(
                name: "FK_Colaboradores_Recintos_TenantId_RecintoId",
                table: "Colaboradores");

            migrationBuilder.DropForeignKey(
                name: "FK_Compensaciones_Colaboradores_TenantId_ColaboradorId",
                table: "Compensaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Documentos_Colaboradores_TenantId_ColaboradorId",
                table: "Documentos");

            migrationBuilder.DropForeignKey(
                name: "FK_Licencias_Colaboradores_TenantId_ColaboradorId",
                table: "Licencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Marcas_Colaboradores_TenantId_ColaboradorId",
                table: "Marcas");

            migrationBuilder.DropForeignKey(
                name: "FK_Marcas_Recintos_TenantId_RecintoId",
                table: "Marcas");

            migrationBuilder.DropForeignKey(
                name: "FK_MarcasFallidas_Colaboradores_TenantId_ColaboradorId",
                table: "MarcasFallidas");

            migrationBuilder.DropForeignKey(
                name: "FK_MarcasFallidas_Recintos_TenantId_RecintoId",
                table: "MarcasFallidas");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosActivo_Activos_TenantId_ActivoCodigo",
                table: "MovimientosActivo");

            migrationBuilder.DropForeignKey(
                name: "FK_NotasSupervisor_Colaboradores_TenantId_ColaboradorId",
                table: "NotasSupervisor");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesActivo_Colaboradores_TenantId_ParaId",
                table: "OrdenesActivo");

            migrationBuilder.DropForeignKey(
                name: "FK_ParticipacionesEncuesta_Colaboradores_TenantId_ColaboradorId",
                table: "ParticipacionesEncuesta");

            migrationBuilder.DropForeignKey(
                name: "FK_Recargos_Colaboradores_TenantId_ColaboradorId",
                table: "Recargos");

            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesAsistencia_Colaboradores_TenantId_ColaboradorId",
                table: "SolicitudesAsistencia");

            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesWorkflow_Colaboradores_TenantId_AfectadoId",
                table: "SolicitudesWorkflow");

            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesWorkflow_PlantillasWorkflow_TenantId_PlantillaId",
                table: "SolicitudesWorkflow");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Colaboradores_TenantId_ColaboradorId",
                table: "Usuarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Vacaciones_Colaboradores_TenantId_ColaboradorId",
                table: "Vacaciones");

            migrationBuilder.DropIndex(
                name: "IX_Vacaciones_TenantId_ColaboradorId",
                table: "Vacaciones");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_TenantId_ColaboradorId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_TenantId_Correo",
                table: "Usuarios");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Turnos",
                table: "Turnos");

            migrationBuilder.DropIndex(
                name: "IX_Turnos_TenantId_Sigla",
                table: "Turnos");

            migrationBuilder.DropIndex(
                name: "IX_TramosRenta_TenantId",
                table: "TramosRenta");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TiposItem",
                table: "TiposItem");

            migrationBuilder.DropIndex(
                name: "IX_TiposItem_TenantId_Codigo",
                table: "TiposItem");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesWorkflow_TenantId_AfectadoId",
                table: "SolicitudesWorkflow");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesWorkflow_TenantId_PlantillaId",
                table: "SolicitudesWorkflow");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesAsistencia_TenantId_ColaboradorId",
                table: "SolicitudesAsistencia");

            migrationBuilder.DropIndex(
                name: "IX_RespuestasEncuesta_TenantId",
                table: "RespuestasEncuesta");

            migrationBuilder.DropIndex(
                name: "IX_RequisitosSst_TenantId_Nombre",
                table: "RequisitosSst");

            migrationBuilder.DropIndex(
                name: "IX_Reportes_TenantId",
                table: "Reportes");

            migrationBuilder.DropIndex(
                name: "IX_Reconocimientos_TenantId",
                table: "Reconocimientos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Recintos",
                table: "Recintos");

            migrationBuilder.DropIndex(
                name: "IX_Recintos_TenantId_Nombre",
                table: "Recintos");

            migrationBuilder.DropIndex(
                name: "IX_Recargos_TenantId_ColaboradorId",
                table: "Recargos");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_TenantId",
                table: "Publicaciones");

            migrationBuilder.DropIndex(
                name: "IX_ProductosActivo_TenantId_Nombre",
                table: "ProductosActivo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Posiciones",
                table: "Posiciones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlantillasWorkflow",
                table: "PlantillasWorkflow");

            migrationBuilder.DropIndex(
                name: "IX_PlantillasWorkflow_TenantId_Nombre",
                table: "PlantillasWorkflow");

            migrationBuilder.DropIndex(
                name: "IX_PlantillasDocumento_TenantId_Nombre",
                table: "PlantillasDocumento");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanesHeadcount",
                table: "PlanesHeadcount");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Periodos",
                table: "Periodos");

            migrationBuilder.DropIndex(
                name: "IX_Perfiles_TenantId_Nombre",
                table: "Perfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ParticipacionesEncuesta",
                table: "ParticipacionesEncuesta");

            migrationBuilder.DropIndex(
                name: "IX_ParticipacionesEncuesta_EncuestaId",
                table: "ParticipacionesEncuesta");

            migrationBuilder.DropIndex(
                name: "IX_ParticipacionesEncuesta_TenantId_ColaboradorId",
                table: "ParticipacionesEncuesta");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ParametrosRem",
                table: "ParametrosRem");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesActivo_TenantId_ParaId",
                table: "OrdenesActivo");

            migrationBuilder.DropIndex(
                name: "IX_NotasSupervisor_TenantId_ColaboradorId",
                table: "NotasSupervisor");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosActivo_TenantId_ActivoCodigo",
                table: "MovimientosActivo");

            migrationBuilder.DropIndex(
                name: "IX_MensajesDenuncia_TenantId",
                table: "MensajesDenuncia");

            migrationBuilder.DropIndex(
                name: "IX_MarcasFallidas_TenantId_ColaboradorId",
                table: "MarcasFallidas");

            migrationBuilder.DropIndex(
                name: "IX_MarcasFallidas_TenantId_RecintoId",
                table: "MarcasFallidas");

            migrationBuilder.DropIndex(
                name: "IX_Marcas_TenantId_ColaboradorId",
                table: "Marcas");

            migrationBuilder.DropIndex(
                name: "IX_Marcas_TenantId_RecintoId",
                table: "Marcas");

            migrationBuilder.DropIndex(
                name: "IX_Licencias_TenantId_ColaboradorId",
                table: "Licencias");

            migrationBuilder.DropIndex(
                name: "IX_FilasCatalogo_TenantId",
                table: "FilasCatalogo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Feriados",
                table: "Feriados");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FamiliasCargo",
                table: "FamiliasCargo");

            migrationBuilder.DropIndex(
                name: "IX_FamiliasCargo_TenantId_Nombre",
                table: "FamiliasCargo");

            migrationBuilder.DropIndex(
                name: "IX_EvidenciasSst_TenantId",
                table: "EvidenciasSst");

            migrationBuilder.DropIndex(
                name: "IX_EstudioSalarial_TenantId",
                table: "EstudioSalarial");

            migrationBuilder.DropIndex(
                name: "IX_Encuestas_TenantId",
                table: "Encuestas");

            migrationBuilder.DropIndex(
                name: "IX_EjecucionesReporte_TenantId",
                table: "EjecucionesReporte");

            migrationBuilder.DropIndex(
                name: "IX_Documentos_TenantId_ColaboradorId",
                table: "Documentos");

            migrationBuilder.DropIndex(
                name: "IX_Denuncias_TenantId_Codigo",
                table: "Denuncias");

            migrationBuilder.DropIndex(
                name: "IX_CumplimientosSst_TenantId_Nombre",
                table: "CumplimientosSst");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Configuraciones",
                table: "Configuraciones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Competencias",
                table: "Competencias");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Compensaciones",
                table: "Compensaciones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ComisionesAfp",
                table: "ComisionesAfp");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Colaboradores",
                table: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_Colaboradores_TenantId_Documento",
                table: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_Colaboradores_TenantId_JefeId",
                table: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_Colaboradores_TenantId_RecintoId",
                table: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_CategoriasActivo_TenantId_Nombre",
                table: "CategoriasActivo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Cargos",
                table: "Cargos");

            migrationBuilder.DropIndex(
                name: "IX_Cargos_TenantId_Nombre",
                table: "Cargos");

            migrationBuilder.DropIndex(
                name: "IX_Cargas_TenantId",
                table: "Cargas");

            migrationBuilder.DropIndex(
                name: "IX_CambiosTurno_TenantId_ColaboradorId_Fecha",
                table: "CambiosTurno");

            migrationBuilder.DropIndex(
                name: "IX_CambiosTurno_TenantId_TurnoId",
                table: "CambiosTurno");

            migrationBuilder.DropIndex(
                name: "IX_Cambios_TenantId",
                table: "Cambios");

            migrationBuilder.DropIndex(
                name: "IX_Boletas_TenantId_ColaboradorId",
                table: "Boletas");

            migrationBuilder.DropIndex(
                name: "IX_Boletas_TenantId_Periodo_ColaboradorId",
                table: "Boletas");

            migrationBuilder.DropIndex(
                name: "IX_Bandas_TenantId_Cargo",
                table: "Bandas");

            migrationBuilder.DropIndex(
                name: "IX_Ausencias_TenantId_ColaboradorId",
                table: "Ausencias");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesTurno_TenantId_ColaboradorId",
                table: "AsignacionesTurno");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesTurno_TenantId_TurnoId",
                table: "AsignacionesTurno");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesSst_TenantId_ColaboradorId",
                table: "AsignacionesSst");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesItem_TenantId_ColaboradorId",
                table: "AsignacionesItem");

            migrationBuilder.DropIndex(
                name: "IX_AsignacionesItem_TenantId_TipoItemId",
                table: "AsignacionesItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ArchivosEvidenciaSst",
                table: "ArchivosEvidenciaSst");

            migrationBuilder.DropIndex(
                name: "IX_ArchivosEvidenciaSst_EvidenciaId",
                table: "ArchivosEvidenciaSst");

            migrationBuilder.DropIndex(
                name: "IX_ArchivosEvidenciaSst_TenantId_EvidenciaId",
                table: "ArchivosEvidenciaSst");

            migrationBuilder.DropIndex(
                name: "IX_AprobacionesHhee_TenantId_ColaboradorId_Fecha",
                table: "AprobacionesHhee");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Activos",
                table: "Activos");

            migrationBuilder.DropIndex(
                name: "IX_Activos_TenantId_AsignadoId",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Vacaciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TramosRenta");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TiposItem");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SolicitudesWorkflow");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SolicitudesAsistencia");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RespuestasEncuesta");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RequisitosSst");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Reportes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Reconocimientos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Recintos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Recargos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductosActivo");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Posiciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PlantillasWorkflow");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PlantillasDocumento");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PlanesHeadcount");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Perfiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ParticipacionesEncuesta");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ParametrosRem");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OrdenesActivo");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NotasSupervisor");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MovimientosActivo");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MensajesDenuncia");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MarcasFallidas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Marcas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Licencias");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FilasCatalogo");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Feriados");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FamiliasCargo");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EvidenciasSst");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EstudioSalarial");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Encuestas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EjecucionesReporte");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Denuncias");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CumplimientosSst");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Configuraciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Competencias");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Compensaciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ComisionesAfp");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CategoriasActivo");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Cargos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Cargas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CambiosTurno");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Cambios");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Boletas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Bandas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Ausencias");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AsignacionesTurno");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AsignacionesSst");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AsignacionesItem");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ArchivosEvidenciaSst");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AprobacionesHhee");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Activos");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Turnos",
                table: "Turnos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TiposItem",
                table: "TiposItem",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Recintos",
                table: "Recintos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Posiciones",
                table: "Posiciones",
                column: "Codigo");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlantillasWorkflow",
                table: "PlantillasWorkflow",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanesHeadcount",
                table: "PlanesHeadcount",
                columns: new[] { "Tipo", "Nombre" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Periodos",
                table: "Periodos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ParticipacionesEncuesta",
                table: "ParticipacionesEncuesta",
                columns: new[] { "EncuestaId", "ColaboradorId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ParametrosRem",
                table: "ParametrosRem",
                column: "Clave");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Feriados",
                table: "Feriados",
                column: "Fecha");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FamiliasCargo",
                table: "FamiliasCargo",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Configuraciones",
                table: "Configuraciones",
                column: "Clave");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Competencias",
                table: "Competencias",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Compensaciones",
                table: "Compensaciones",
                column: "ColaboradorId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ComisionesAfp",
                table: "ComisionesAfp",
                column: "Afp");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Colaboradores",
                table: "Colaboradores",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cargos",
                table: "Cargos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ArchivosEvidenciaSst",
                table: "ArchivosEvidenciaSst",
                column: "EvidenciaId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Activos",
                table: "Activos",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ColaboradorId",
                table: "Usuarios",
                column: "ColaboradorId",
                unique: true,
                filter: "[ColaboradorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_Sigla",
                table: "Turnos",
                column: "Sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposItem_Codigo",
                table: "TiposItem",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesWorkflow_PlantillaId",
                table: "SolicitudesWorkflow",
                column: "PlantillaId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAsistencia_ColaboradorId",
                table: "SolicitudesAsistencia",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosSst_Nombre",
                table: "RequisitosSst",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recintos_Nombre",
                table: "Recintos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recargos_ColaboradorId",
                table: "Recargos",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosActivo_Nombre",
                table: "ProductosActivo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasWorkflow_Nombre",
                table: "PlantillasWorkflow",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasDocumento_Nombre",
                table: "PlantillasDocumento",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Perfiles_Nombre",
                table: "Perfiles",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipacionesEncuesta_ColaboradorId",
                table: "ParticipacionesEncuesta",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesActivo_ParaId",
                table: "OrdenesActivo",
                column: "ParaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasSupervisor_ColaboradorId",
                table: "NotasSupervisor",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_MarcasFallidas_ColaboradorId",
                table: "MarcasFallidas",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_MarcasFallidas_RecintoId",
                table: "MarcasFallidas",
                column: "RecintoId");

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_RecintoId",
                table: "Marcas",
                column: "RecintoId");

            migrationBuilder.CreateIndex(
                name: "IX_Licencias_ColaboradorId",
                table: "Licencias",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_FamiliasCargo_Nombre",
                table: "FamiliasCargo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_Codigo",
                table: "Denuncias",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CumplimientosSst_Nombre",
                table: "CumplimientosSst",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_Documento",
                table: "Colaboradores",
                column: "Documento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_JefeId",
                table: "Colaboradores",
                column: "JefeId");

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_RecintoId",
                table: "Colaboradores",
                column: "RecintoId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasActivo_Nombre",
                table: "CategoriasActivo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_Nombre",
                table: "Cargos",
                column: "Nombre",
                unique: true);

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
                name: "IX_Boletas_ColaboradorId",
                table: "Boletas",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Boletas_Periodo_ColaboradorId",
                table: "Boletas",
                columns: new[] { "Periodo", "ColaboradorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bandas_Cargo",
                table: "Bandas",
                column: "Cargo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesTurno_TurnoId",
                table: "AsignacionesTurno",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesSst_ColaboradorId",
                table: "AsignacionesSst",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesItem_TipoItemId",
                table: "AsignacionesItem",
                column: "TipoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AprobacionesHhee_ColaboradorId_Fecha",
                table: "AprobacionesHhee",
                columns: new[] { "ColaboradorId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activos_AsignadoId",
                table: "Activos",
                column: "AsignadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activos_Colaboradores_AsignadoId",
                table: "Activos",
                column: "AsignadoId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AprobacionesHhee_Colaboradores_ColaboradorId",
                table: "AprobacionesHhee",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesItem_Colaboradores_ColaboradorId",
                table: "AsignacionesItem",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesItem_TiposItem_TipoItemId",
                table: "AsignacionesItem",
                column: "TipoItemId",
                principalTable: "TiposItem",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesSst_Colaboradores_ColaboradorId",
                table: "AsignacionesSst",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesTurno_Colaboradores_ColaboradorId",
                table: "AsignacionesTurno",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesTurno_Turnos_TurnoId",
                table: "AsignacionesTurno",
                column: "TurnoId",
                principalTable: "Turnos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ausencias_Colaboradores_ColaboradorId",
                table: "Ausencias",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boletas_Colaboradores_ColaboradorId",
                table: "Boletas",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boletas_Periodos_Periodo",
                table: "Boletas",
                column: "Periodo",
                principalTable: "Periodos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CambiosTurno_Colaboradores_ColaboradorId",
                table: "CambiosTurno",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CambiosTurno_Turnos_TurnoId",
                table: "CambiosTurno",
                column: "TurnoId",
                principalTable: "Turnos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Colaboradores_Colaboradores_JefeId",
                table: "Colaboradores",
                column: "JefeId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Colaboradores_Recintos_RecintoId",
                table: "Colaboradores",
                column: "RecintoId",
                principalTable: "Recintos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Compensaciones_Colaboradores_ColaboradorId",
                table: "Compensaciones",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Documentos_Colaboradores_ColaboradorId",
                table: "Documentos",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Licencias_Colaboradores_ColaboradorId",
                table: "Licencias",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Marcas_Colaboradores_ColaboradorId",
                table: "Marcas",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Marcas_Recintos_RecintoId",
                table: "Marcas",
                column: "RecintoId",
                principalTable: "Recintos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MarcasFallidas_Colaboradores_ColaboradorId",
                table: "MarcasFallidas",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MarcasFallidas_Recintos_RecintoId",
                table: "MarcasFallidas",
                column: "RecintoId",
                principalTable: "Recintos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosActivo_Activos_ActivoCodigo",
                table: "MovimientosActivo",
                column: "ActivoCodigo",
                principalTable: "Activos",
                principalColumn: "Codigo",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotasSupervisor_Colaboradores_ColaboradorId",
                table: "NotasSupervisor",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesActivo_Colaboradores_ParaId",
                table: "OrdenesActivo",
                column: "ParaId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ParticipacionesEncuesta_Colaboradores_ColaboradorId",
                table: "ParticipacionesEncuesta",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recargos_Colaboradores_ColaboradorId",
                table: "Recargos",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesAsistencia_Colaboradores_ColaboradorId",
                table: "SolicitudesAsistencia",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesWorkflow_Colaboradores_AfectadoId",
                table: "SolicitudesWorkflow",
                column: "AfectadoId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesWorkflow_PlantillasWorkflow_PlantillaId",
                table: "SolicitudesWorkflow",
                column: "PlantillaId",
                principalTable: "PlantillasWorkflow",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Colaboradores_ColaboradorId",
                table: "Usuarios",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Vacaciones_Colaboradores_ColaboradorId",
                table: "Vacaciones",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
