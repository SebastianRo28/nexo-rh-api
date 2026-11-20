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
        b.Entity<Configuracion>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(40);
            e.Property(x => x.ModificadoPor).HasMaxLength(120);
        });
        AsistenciaModelo(b);
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
        });
    }
}
