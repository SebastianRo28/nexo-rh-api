using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Data;

public class NexoDbContext(DbContextOptions<NexoDbContext> options) : DbContext(options)
{
    public DbSet<Colaborador> Colaboradores => Set<Colaborador>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<FamiliaCargo> FamiliasCargo => Set<FamiliaCargo>();
    public DbSet<Competencia> Competencias => Set<Competencia>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cambio> Cambios => Set<Cambio>();
    public DbSet<Perfil> Perfiles => Set<Perfil>();
    public DbSet<Configuracion> Configuraciones => Set<Configuracion>();
    public DbSet<FilaCatalogo> FilasCatalogo => Set<FilaCatalogo>();
    public DbSet<PlantillaWorkflow> PlantillasWorkflow => Set<PlantillaWorkflow>();
    public DbSet<SolicitudWorkflow> SolicitudesWorkflow => Set<SolicitudWorkflow>();
    public DbSet<Reconocimiento> Reconocimientos => Set<Reconocimiento>();
    public DbSet<Encuesta> Encuestas => Set<Encuesta>();
    public DbSet<ParticipacionEncuesta> ParticipacionesEncuesta => Set<ParticipacionEncuesta>();
    public DbSet<RespuestaEncuesta> RespuestasEncuesta => Set<RespuestaEncuesta>();
    public DbSet<Denuncia> Denuncias => Set<Denuncia>();
    public DbSet<MensajeDenuncia> MensajesDenuncia => Set<MensajeDenuncia>();
    public DbSet<Publicacion> Publicaciones => Set<Publicacion>();
    public DbSet<CumplimientoSst> CumplimientosSst => Set<CumplimientoSst>();
    public DbSet<RequisitoSst> RequisitosSst => Set<RequisitoSst>();
    public DbSet<AsignacionSst> AsignacionesSst => Set<AsignacionSst>();
    public DbSet<EvidenciaSst> EvidenciasSst => Set<EvidenciaSst>();
    public DbSet<ArchivoEvidenciaSst> ArchivosEvidenciaSst => Set<ArchivoEvidenciaSst>();
    public DbSet<CategoriaActivo> CategoriasActivo => Set<CategoriaActivo>();
    public DbSet<ProductoActivo> ProductosActivo => Set<ProductoActivo>();
    public DbSet<Activo> Activos => Set<Activo>();
    public DbSet<MovimientoActivo> MovimientosActivo => Set<MovimientoActivo>();
    public DbSet<OrdenActivo> OrdenesActivo => Set<OrdenActivo>();
    public DbSet<PlantillaDocumento> PlantillasDocumento => Set<PlantillaDocumento>();
    public DbSet<DocumentoColaborador> Documentos => Set<DocumentoColaborador>();
    public DbSet<BandaSalarial> Bandas => Set<BandaSalarial>();
    public DbSet<CompensacionColaborador> Compensaciones => Set<CompensacionColaborador>();
    public DbSet<CargoMercado> EstudioSalarial => Set<CargoMercado>();
    public DbSet<Posicion> Posiciones => Set<Posicion>();
    public DbSet<PlanHeadcount> PlanesHeadcount => Set<PlanHeadcount>();
    public DbSet<ReporteGuardado> Reportes => Set<ReporteGuardado>();
    public DbSet<EjecucionReporte> EjecucionesReporte => Set<EjecucionReporte>();
    public DbSet<CargaImportacion> Cargas => Set<CargaImportacion>();

    public DbSet<TipoItem> TiposItem => Set<TipoItem>();
    public DbSet<AsignacionItem> AsignacionesItem => Set<AsignacionItem>();
    public DbSet<ParametroRem> ParametrosRem => Set<ParametroRem>();
    public DbSet<ComisionAfp> ComisionesAfp => Set<ComisionAfp>();
    public DbSet<TramoRenta> TramosRenta => Set<TramoRenta>();
    public DbSet<PeriodoPlanilla> Periodos => Set<PeriodoPlanilla>();
    public DbSet<Boleta> Boletas => Set<Boleta>();

    public DbSet<Recinto> Recintos => Set<Recinto>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<AsignacionTurno> AsignacionesTurno => Set<AsignacionTurno>();
    public DbSet<CambioTurno> CambiosTurno => Set<CambioTurno>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<MarcaFallida> MarcasFallidas => Set<MarcaFallida>();
    public DbSet<NotaSupervisor> NotasSupervisor => Set<NotaSupervisor>();
    public DbSet<Vacacion> Vacaciones => Set<Vacacion>();
    public DbSet<Ausencia> Ausencias => Set<Ausencia>();
    public DbSet<LicenciaMedica> Licencias => Set<LicenciaMedica>();
    public DbSet<SolicitudAsistencia> SolicitudesAsistencia => Set<SolicitudAsistencia>();
    public DbSet<RecargoDomingo> Recargos => Set<RecargoDomingo>();
    public DbSet<AprobacionHhee> AprobacionesHhee => Set<AprobacionHhee>();
    public DbSet<Feriado> Feriados => Set<Feriado>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Colaborador>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Apellidos).HasMaxLength(80).IsRequired();
            e.Property(x => x.Nombres).HasMaxLength(80).IsRequired();
            e.Property(x => x.TipoDocumento).HasMaxLength(20).IsRequired();
            e.Property(x => x.Documento).HasMaxLength(15).IsRequired();
            e.HasIndex(x => x.Documento).IsUnique();
            e.Property(x => x.Cargo).HasMaxLength(80).IsRequired();
            e.Property(x => x.Division).HasMaxLength(80).IsRequired();
            e.Property(x => x.Area).HasMaxLength(80).IsRequired();
            e.Property(x => x.SubArea).HasMaxLength(80).IsRequired();
            e.Property(x => x.SueldoNeto).HasPrecision(12, 2);
            e.Property(x => x.SueldoBase).HasPrecision(12, 2);
            e.Property(x => x.EmailCorporativo).HasMaxLength(120);
            e.Property(x => x.Telefono).HasMaxLength(30);
            e.Property(x => x.Direccion).HasMaxLength(200);
            e.Property(x => x.Cuspp).HasMaxLength(20);
            e.Property(x => x.Aviso).HasMaxLength(300);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.JefeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Cargo>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Codigo).HasMaxLength(30);
            e.Property(x => x.Familia).HasMaxLength(40).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(2000);
        });
        b.Entity<FamiliaCargo>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
        });
        b.Entity<Competencia>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(300);
        });
        b.Entity<Usuario>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Correo).IsUnique();
            e.Property(x => x.Perfil).HasMaxLength(60);
            e.Property(x => x.Sello).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.ColaboradorId).IsUnique().HasFilter("[ColaboradorId] IS NOT NULL");
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Cambio>(e =>
        {
            e.Property(x => x.Usuario).HasMaxLength(120);
            e.Property(x => x.Origen).HasMaxLength(20);
            e.Property(x => x.Colaborador).HasMaxLength(170);
            e.Property(x => x.Documento).HasMaxLength(20);
            e.Property(x => x.Entidad).HasMaxLength(120);
            e.Property(x => x.Accion).HasMaxLength(20);
            e.Property(x => x.Propiedad).HasMaxLength(200);
            e.Property(x => x.Anterior).HasMaxLength(500);
            e.Property(x => x.Actual).HasMaxLength(500);
            e.HasIndex(x => x.Fecha);
        });
        b.Entity<Perfil>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Descripcion).HasMaxLength(300);
            e.Property(x => x.ModificadoPor).HasMaxLength(120);
        });
        b.Entity<Usuario>().HasOne<Perfil>().WithMany().HasForeignKey(x => x.PerfilId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FilaCatalogo>(e =>
        {
            e.Property(x => x.Clave).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Clave);
            e.Property(x => x.ModificadoPor).HasMaxLength(120);
        });
        b.Entity<Configuracion>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(40);
            e.Property(x => x.ModificadoPor).HasMaxLength(120);
        });
        AsistenciaModelo(b);
        RemuneracionesModelo(b);
        WorkflowModelo(b);
        ComunicacionModelo(b);
        SstModelo(b);
        AdministracionModelo(b);
        ReporteriaModelo(b);
    }

    private static void ReporteriaModelo(ModelBuilder b)
    {
        b.Entity<ReporteGuardado>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120);
            e.Property(x => x.Plantilla).HasMaxLength(80);
            e.Property(x => x.Categoria).HasMaxLength(60);
            e.Property(x => x.CreadoPor).HasMaxLength(120);
        });
        b.Entity<EjecucionReporte>(e =>
        {
            e.Property(x => x.Reporte).HasMaxLength(120);
            e.Property(x => x.Plantilla).HasMaxLength(80);
            e.Property(x => x.Usuario).HasMaxLength(120);
            e.HasIndex(x => x.Fecha);
        });
        b.Entity<CargaImportacion>(e =>
        {
            e.Property(x => x.Importador).HasMaxLength(80);
            e.Property(x => x.Archivo).HasMaxLength(200);
            e.Property(x => x.Usuario).HasMaxLength(120);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.OwnsMany(x => x.Errores, l => l.ToJson());
        });
    }

    private static void AdministracionModelo(ModelBuilder b)
    {
        b.Entity<CategoriaActivo>(e => { e.Property(x => x.Nombre).HasMaxLength(60).IsRequired(); e.HasIndex(x => x.Nombre).IsUnique(); });
        b.Entity<ProductoActivo>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Marca).HasMaxLength(60);
            e.HasOne<CategoriaActivo>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Activo>(e =>
        {
            e.HasKey(x => x.Codigo);
            e.Property(x => x.Codigo).HasMaxLength(12);
            e.Property(x => x.Estado).HasMaxLength(15);
            e.HasOne<ProductoActivo>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.AsignadoId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<MovimientoActivo>(e =>
        {
            e.Property(x => x.ActivoCodigo).HasMaxLength(12);
            e.Property(x => x.Asignado).HasMaxLength(170);
            e.Property(x => x.Estado).HasMaxLength(15);
            e.Property(x => x.Comentario).HasMaxLength(300);
            e.Property(x => x.Admin).HasMaxLength(120);
            e.HasIndex(x => x.ActivoCodigo);
            e.HasOne<Activo>().WithMany().HasForeignKey(x => x.ActivoCodigo).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<OrdenActivo>(e =>
        {
            e.Property(x => x.Id).UseIdentityColumn(100, 1);
            e.Property(x => x.Origen).HasMaxLength(30);
            e.Property(x => x.Para).HasMaxLength(170);
            e.Property(x => x.Area).HasMaxLength(80);
            e.Property(x => x.Cargo).HasMaxLength(80);
            e.Property(x => x.Solicitante).HasMaxLength(170);
            e.Property(x => x.Estado).HasMaxLength(12);
            e.Property(x => x.ActivoCodigo).HasMaxLength(12);
            e.Property(x => x.Comentario).HasMaxLength(300);
            e.HasOne<ProductoActivo>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ParaId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<PlantillaDocumento>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Tipo).HasMaxLength(40);
            e.Property(x => x.Carpeta).HasMaxLength(80);
        });
        b.Entity<DocumentoColaborador>(e =>
        {
            e.Property(x => x.Archivo).HasMaxLength(200).IsRequired();
            e.Property(x => x.Revision).HasMaxLength(2);
            e.Property(x => x.FirmaColaborador).HasMaxLength(2);
            e.Property(x => x.FirmaEmpresa).HasMaxLength(2);
            e.Property(x => x.EliminadoPor).HasMaxLength(120);
            e.HasIndex(x => x.ColaboradorId);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<BandaSalarial>(e =>
        {
            e.Property(x => x.Cargo).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Cargo).IsUnique();
            e.Property(x => x.Objetivo).HasPrecision(12, 2);
            e.Property(x => x.PctMin).HasPrecision(5, 2);
            e.Property(x => x.PctMax).HasPrecision(5, 2);
        });
        b.Entity<CompensacionColaborador>(e =>
        {
            e.HasKey(x => x.ColaboradorId);
            e.Property(x => x.Evaluacion).HasMaxLength(40);
            e.Property(x => x.CompTeorica).HasPrecision(12, 2);
            e.Property(x => x.Comentario).HasMaxLength(300);
            e.HasOne<Colaborador>().WithOne().HasForeignKey<CompensacionColaborador>(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<CargoMercado>(e =>
        {
            e.Property(x => x.Cargo).HasMaxLength(100).IsRequired();
            e.Property(x => x.Interno).HasMaxLength(80);
            e.Property(x => x.Descripcion).HasMaxLength(300);
            e.Property(x => x.Industria).HasMaxLength(60);
            e.Property(x => x.Tamano).HasMaxLength(40);
            foreach (var p in new[] { nameof(CargoMercado.P25), nameof(CargoMercado.P50), nameof(CargoMercado.P75), nameof(CargoMercado.P90) }) e.Property(p).HasPrecision(12, 2);
        });
        b.Entity<Posicion>(e =>
        {
            e.HasKey(x => x.Codigo);
            e.Property(x => x.Codigo).HasMaxLength(12);
            e.Property(x => x.Cargo).HasMaxLength(80);
            e.Property(x => x.Division).HasMaxLength(80);
            e.Property(x => x.Area).HasMaxLength(80);
            e.Property(x => x.SubArea).HasMaxLength(80);
        });
        b.Entity<PlanHeadcount>(e =>
        {
            e.HasKey(x => new { x.Tipo, x.Nombre });
            e.Property(x => x.Tipo).HasMaxLength(10);
            e.Property(x => x.Nombre).HasMaxLength(80);
        });
    }

    private static void SstModelo(ModelBuilder b)
    {
        b.Entity<CumplimientoSst>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Normativa).HasMaxLength(80);
            e.Property(x => x.Categoria).HasMaxLength(80);
            e.Property(x => x.Descripcion).HasMaxLength(500);
        });
        b.Entity<RequisitoSst>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.AplicaA).HasMaxLength(15);
            e.HasOne<CumplimientoSst>().WithMany().HasForeignKey(x => x.CumplimientoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AsignacionSst>(e =>
        {
            e.Property(x => x.Empresa).HasMaxLength(120);
            e.HasIndex(x => x.RequisitoId);
            e.HasOne<RequisitoSst>().WithMany().HasForeignKey(x => x.RequisitoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<EvidenciaSst>(e =>
        {
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.RegistradaPor).HasMaxLength(120);
            e.Property(x => x.ArchivoNombre).HasMaxLength(200);
            e.Property(x => x.ArchivoTipo).HasMaxLength(60);
            e.HasIndex(x => x.AsignacionId);
            e.HasOne<AsignacionSst>().WithMany().HasForeignKey(x => x.AsignacionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ArchivoEvidenciaSst>(e =>
        {
            e.HasKey(x => x.EvidenciaId);
            e.HasOne<EvidenciaSst>().WithOne().HasForeignKey<ArchivoEvidenciaSst>(x => x.EvidenciaId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ComunicacionModelo(ModelBuilder b)
    {
        b.Entity<Encuesta>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Tipo).HasMaxLength(10);
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.CreadaPor).HasMaxLength(120);
            e.OwnsMany(x => x.Preguntas, p => { p.ToJson(); });
        });
        b.Entity<ParticipacionEncuesta>(e =>
        {
            e.HasKey(x => new { x.EncuestaId, x.ColaboradorId });
            e.HasOne<Encuesta>().WithMany().HasForeignKey(x => x.EncuestaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RespuestaEncuesta>(e =>
        {
            e.Property(x => x.Valores).HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, string>>(
                    (a, c) => System.Text.Json.JsonSerializer.Serialize(a, (System.Text.Json.JsonSerializerOptions?)null) == System.Text.Json.JsonSerializer.Serialize(c, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
                    v => new Dictionary<string, string>(v)));
            e.HasIndex(x => x.EncuestaId);
            e.HasOne<Encuesta>().WithMany().HasForeignKey(x => x.EncuestaId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Denuncia>(e =>
        {
            e.Property(x => x.Codigo).HasMaxLength(12).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Tipo).HasMaxLength(60);
            e.Property(x => x.Relacion).HasMaxLength(30);
            e.Property(x => x.Descripcion).HasMaxLength(4000);
            e.Property(x => x.DenuncianteNombre).HasMaxLength(170);
            e.Property(x => x.Estado).HasMaxLength(12);
            e.Property(x => x.Resolucion).HasMaxLength(1000);
            e.HasMany(x => x.Mensajes).WithOne().HasForeignKey(m => m.DenunciaId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MensajeDenuncia>(e =>
        {
            e.Property(x => x.Autor).HasMaxLength(12);
            e.Property(x => x.Texto).HasMaxLength(2000);
        });
        b.Entity<Publicacion>(e =>
        {
            e.Property(x => x.Titulo).HasMaxLength(150).IsRequired();
            e.Property(x => x.Texto).HasMaxLength(4000);
            e.Property(x => x.Autor).HasMaxLength(80);
        });
    }

    private static void WorkflowModelo(ModelBuilder b)
    {
        b.Entity<PlantillaWorkflow>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Tipo).HasMaxLength(20);
            e.Property(x => x.Estado).HasMaxLength(12);
            e.Property(x => x.ModificadoPor).HasMaxLength(120);
        });
        b.Entity<SolicitudWorkflow>(e =>
        {
            // Los números de solicitud siguen la numeración del sistema anterior.
            e.Property(x => x.Id).UseIdentityColumn(18_700, 1);
            e.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
            e.Property(x => x.Tipo).HasMaxLength(20);
            e.Property(x => x.Proceso).HasMaxLength(20);
            e.Property(x => x.Solicitante).HasMaxLength(170);
            e.Property(x => x.Afectado).HasMaxLength(170);
            e.Property(x => x.Documento).HasMaxLength(15);
            e.Property(x => x.Detalle).HasMaxLength(300);
            e.Property(x => x.Monto).HasPrecision(12, 2);
            e.Property(x => x.Estado).HasMaxLength(12);
            e.Property(x => x.Comentario).HasMaxLength(300);
            e.Property(x => x.ResueltaPor).HasMaxLength(120);
            e.HasIndex(x => x.AfectadoId);
            e.HasOne<PlantillaWorkflow>().WithMany().HasForeignKey(x => x.PlantillaId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.AfectadoId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Reconocimiento>(e =>
        {
            e.Property(x => x.Para).HasMaxLength(500);
            e.Property(x => x.Autor).HasMaxLength(170);
            e.Property(x => x.Valor).HasMaxLength(60);
            e.Property(x => x.Mensaje).HasMaxLength(300);
            e.HasIndex(x => x.Fecha);
        });
    }

    private static void RemuneracionesModelo(ModelBuilder b)
    {
        b.Entity<TipoItem>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            e.Property(x => x.Clase).HasMaxLength(20);
            e.Property(x => x.Plame).HasMaxLength(4);
        });
        b.Entity<AsignacionItem>(e =>
        {
            e.Property(x => x.Monto).HasPrecision(12, 2);
            e.Property(x => x.Observacion).HasMaxLength(300);
            e.HasIndex(x => x.ColaboradorId);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TipoItem>().WithMany().HasForeignKey(x => x.TipoItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ParametroRem>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(30);
            e.Property(x => x.Nombre).HasMaxLength(80);
            e.Property(x => x.Valor).HasPrecision(12, 4);
            e.Property(x => x.Unidad).HasMaxLength(5);
            e.Property(x => x.VigenteDesde).HasMaxLength(10);
            e.Property(x => x.Fuente).HasMaxLength(120);
        });
        b.Entity<ComisionAfp>(e =>
        {
            e.HasKey(x => x.Afp);
            e.Property(x => x.Afp).HasMaxLength(20);
            e.Property(x => x.ComisionFlujoPct).HasPrecision(6, 4);
            e.Property(x => x.ComisionSaldoPct).HasPrecision(6, 4);
        });
        b.Entity<TramoRenta>(e =>
        {
            e.Property(x => x.Desde).HasPrecision(6, 2);
            e.Property(x => x.Hasta).HasPrecision(6, 2);
            e.Property(x => x.TasaPct).HasPrecision(5, 2);
        });
        b.Entity<PeriodoPlanilla>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ProcesadoPor).HasMaxLength(120);
            e.Property(x => x.CerradoPor).HasMaxLength(120);
        });
        b.Entity<Boleta>(e =>
        {
            e.HasIndex(x => new { x.Periodo, x.ColaboradorId }).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(170);
            e.Property(x => x.Documento).HasMaxLength(15);
            e.Property(x => x.Cargo).HasMaxLength(80);
            e.Property(x => x.Regimen).HasMaxLength(60);
            foreach (var p in new[] { nameof(Boleta.Ingresos), nameof(Boleta.Descuentos), nameof(Boleta.Aportes), nameof(Boleta.Neto), nameof(Boleta.BaseQuinta), nameof(Boleta.RetencionQuinta) })
                e.Property(p).HasPrecision(12, 2);
            e.OwnsMany(x => x.Lineas, l => l.ToJson());
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PeriodoPlanilla>().WithMany().HasForeignKey(x => x.Periodo).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void AsistenciaModelo(ModelBuilder b)
    {
        b.Entity<Colaborador>(e =>
        {
            e.Property(x => x.Jornal).HasPrecision(10, 2);
            e.Property(x => x.SaldoVacacionesInicial).HasPrecision(6, 2);
            e.HasOne<Recinto>().WithMany().HasForeignKey(x => x.RecintoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Recinto>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Empresa).HasMaxLength(120);
            e.Property(x => x.Ruc).HasMaxLength(11);
            e.Property(x => x.CodigoInterno).HasMaxLength(20);
            e.Property(x => x.Pais).HasMaxLength(40);
            e.Property(x => x.Departamento).HasMaxLength(40);
            e.Property(x => x.Provincia).HasMaxLength(40);
            e.Property(x => x.Distrito).HasMaxLength(60);
            e.Property(x => x.Direccion).HasMaxLength(200);
            e.Property(x => x.Clave).HasMaxLength(20);
            e.Property(x => x.Info).HasMaxLength(500);
        });
        b.Entity<Turno>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
            e.Property(x => x.Sigla).HasMaxLength(5).IsRequired();
            e.HasIndex(x => x.Sigla).IsUnique();
            e.Property(x => x.Tipo).HasMaxLength(15);
            e.Property(x => x.Jornada).HasMaxLength(10);
            e.Property(x => x.Entrada).HasMaxLength(5);
            e.Property(x => x.Salida).HasMaxLength(5);
            e.Property(x => x.Color).HasMaxLength(9);
        });
        b.Entity<AsignacionTurno>(e =>
        {
            e.Property(x => x.CreadoPor).HasMaxLength(120);
            e.HasIndex(x => x.ColaboradorId);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Turno>().WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CambioTurno>(e =>
        {
            e.Property(x => x.Codigo).HasMaxLength(5);
            e.Property(x => x.CreadoPor).HasMaxLength(120);
            e.HasIndex(x => new { x.ColaboradorId, x.Fecha }).IsUnique();
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Turno>().WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Marca>(e =>
        {
            e.Property(x => x.Hora).HasMaxLength(5);
            e.Property(x => x.Tipo).HasMaxLength(10);
            e.Property(x => x.Metodo).HasMaxLength(10);
            e.Property(x => x.Observacion).HasMaxLength(300);
            e.Property(x => x.CreadoPor).HasMaxLength(120);
            e.Property(x => x.Motivo).HasMaxLength(300);
            e.HasIndex(x => new { x.ColaboradorId, x.Fecha });
            e.HasIndex(x => x.Fecha);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Recinto>().WithMany().HasForeignKey(x => x.RecintoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<MarcaFallida>(e =>
        {
            e.Property(x => x.Hora).HasMaxLength(5);
            e.Property(x => x.Tipo).HasMaxLength(10);
            e.Property(x => x.Metodo).HasMaxLength(10);
            e.Property(x => x.Motivo).HasMaxLength(40);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Recinto>().WithMany().HasForeignKey(x => x.RecintoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<NotaSupervisor>(e =>
        {
            e.Property(x => x.Texto).HasMaxLength(1000);
            e.Property(x => x.Autor).HasMaxLength(170);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Vacacion>(e =>
        {
            e.Property(x => x.Origen).HasMaxLength(20);
            e.Property(x => x.Observacion).HasMaxLength(300);
            e.HasIndex(x => x.ColaboradorId);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Ausencia>(e =>
        {
            e.Property(x => x.Tipo).HasMaxLength(40);
            e.Property(x => x.Observacion).HasMaxLength(300);
            e.HasIndex(x => x.ColaboradorId);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LicenciaMedica>(e =>
        {
            e.Property(x => x.Folio).HasMaxLength(30);
            e.Property(x => x.Emisor).HasMaxLength(120);
            e.Property(x => x.Diagnostico).HasMaxLength(200);
            e.Property(x => x.Estado).HasMaxLength(12);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Ausencia>().WithMany().HasForeignKey(x => x.AusenciaId).OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<SolicitudAsistencia>(e =>
        {
            e.Property(x => x.Tipo).HasMaxLength(20);
            e.Property(x => x.Motivo).HasMaxLength(300);
            e.Property(x => x.Estado).HasMaxLength(12);
            e.Property(x => x.Aprobador).HasMaxLength(120);
            e.Property(x => x.Comentario).HasMaxLength(300);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RecargoDomingo>(e =>
        {
            e.Property(x => x.Horas).HasPrecision(5, 2);
            e.Property(x => x.Observacion).HasMaxLength(300);
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AprobacionHhee>(e =>
        {
            e.Property(x => x.Estado).HasMaxLength(20);
            e.Property(x => x.Observacion).HasMaxLength(300);
            e.Property(x => x.AprobadoPor).HasMaxLength(120);
            e.HasIndex(x => new { x.ColaboradorId, x.Fecha }).IsUnique();
            e.OwnsMany(x => x.Lineas, l => l.ToJson());
            e.HasOne<Colaborador>().WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Feriado>(e =>
        {
            e.HasKey(x => x.Fecha);
            e.Property(x => x.Nombre).HasMaxLength(80);
            e.Property(x => x.Alcance).HasMaxLength(40);
        });
    }
}
