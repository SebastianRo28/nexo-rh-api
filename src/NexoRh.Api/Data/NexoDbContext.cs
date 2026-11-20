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
