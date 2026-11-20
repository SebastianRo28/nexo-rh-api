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
    }
}
