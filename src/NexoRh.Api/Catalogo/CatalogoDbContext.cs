using Microsoft.EntityFrameworkCore;

namespace NexoRh.Api.Catalogo;

/// <summary>BD catálogo de la plataforma: tenants y sus dominios (luego planes, módulos, marca, uso…).</summary>
public class CatalogoDbContext(DbContextOptions<CatalogoDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDominio> Dominios => Set<TenantDominio>();
    public DbSet<TenantAjustes> Ajustes => Set<TenantAjustes>();
    public DbSet<CorreoSaliente> Correos => Set<CorreoSaliente>();
    public DbSet<TenantMarca> Marcas => Set<TenantMarca>();
    public DbSet<ArchivoMarca> ArchivosMarca => Set<ArchivoMarca>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<ModuloComercial> ModulosComerciales => Set<ModuloComercial>();
    public DbSet<TenantModulo> TenantModulos => Set<TenantModulo>();
    public DbSet<TenantFlag> Flags => Set<TenantFlag>();
    public DbSet<SolicitudInformacion> Solicitudes => Set<SolicitudInformacion>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<UsuarioPlataforma> UsuariosPlataforma => Set<UsuarioPlataforma>();
    public DbSet<AuditoriaPlataforma> AuditoriaPlataforma => Set<AuditoriaPlataforma>();
    public DbSet<TokenPlataforma> TokensPlataforma => Set<TokenPlataforma>();
    public DbSet<SesionSoporte> SesionesSoporte => Set<SesionSoporte>();
    public DbSet<NotaTenant> Notas => Set<NotaTenant>();
    public DbSet<Anuncio> Anuncios => Set<Anuncio>();
    public DbSet<DefinicionFlag> DefinicionesFlag => Set<DefinicionFlag>();
    public DbSet<CatalogoGlobal> CatalogosGlobales => Set<CatalogoGlobal>();
    public DbSet<VersionCatalogo> VersionesCatalogo => Set<VersionCatalogo>();
    public DbSet<UsoMensual> Usos => Set<UsoMensual>();
    public DbSet<TenantCorreoDominio> CorreosDominio => Set<TenantCorreoDominio>();

    /// <summary>
    /// Las fechas del catálogo se guardan en UTC: al leerlas se marcan como UTC para que la API las envíe con “Z” y los
    /// tokens (soporte) venzan a la hora correcta.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        c.Properties<DateTime>().HaveConversion<FechaUtc>();
        c.Properties<DateTime?>().HaveConversion<FechaUtcNula>();
    }

    private class FechaUtc() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    private class FechaUtcNula() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e =>
        {
            e.Property(x => x.Slug).HasMaxLength(30);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(120);
            e.Property(x => x.RazonSocial).HasMaxLength(160);
            e.Property(x => x.Ruc).HasMaxLength(11);
            e.Property(x => x.Pais).HasMaxLength(2);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.Property(x => x.CadenaConexion).HasMaxLength(2000);
            e.Property(x => x.PlanCodigo).HasMaxLength(20).HasDefaultValue(Modulos.PlanPorDefecto);
            e.Property(x => x.DireccionFiscal).HasMaxLength(200);
            e.Property(x => x.CorreoFacturacion).HasMaxLength(254);
            e.Property(x => x.Industria).HasMaxLength(60);
            e.Property(x => x.EjecutivoCuenta).HasMaxLength(120);
            e.Property(x => x.PrecioPactado).HasPrecision(10, 2);
            e.Property(x => x.MotivoSuspension).HasMaxLength(300);
            e.Property(x => x.CertificadoEliminacion).HasMaxLength(8000);
        });
        b.Entity<TenantDominio>(e =>
        {
            e.Property(x => x.Host).HasMaxLength(253);
            e.HasIndex(x => x.Host).IsUnique();
            e.Property(x => x.Tipo).HasMaxLength(20);
            e.Property(x => x.EstadoCertificado).HasMaxLength(20).HasDefaultValue(EstadosCertificado.Activo);
            e.Property(x => x.ErrorVerificacion).HasMaxLength(300);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TenantCorreoDominio>(e =>
        {
            e.HasKey(x => x.TenantId);
            e.Property(x => x.Dominio).HasMaxLength(253);
            e.Property(x => x.Remitente).HasMaxLength(64);
            e.Property(x => x.Selector).HasMaxLength(63);
            e.Property(x => x.ClavePublica).HasMaxLength(1000);
            e.Property(x => x.ClavePrivada).HasMaxLength(4000);
            e.Ignore(x => x.Verificado);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TenantAjustes>(e =>
        {
            e.HasKey(x => x.TenantId);
            e.OwnsOne(x => x.Acceso, a => a.ToJson());
            e.Property(x => x.PrivacidadUrl).HasMaxLength(500);
            e.HasOne<Tenant>().WithOne().HasForeignKey<TenantAjustes>(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TenantMarca>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Version }).IsUnique();
            e.Property(x => x.PublicadoPor).HasMaxLength(160);
            e.Property(x => x.Nota).HasMaxLength(200);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ArchivoMarca>(e =>
        {
            e.Property(x => x.Tipo).HasMaxLength(20);
            e.Property(x => x.ContentType).HasMaxLength(40);
            e.HasIndex(x => x.TenantId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Plan>(e =>
        {
            e.HasKey(x => x.Codigo);
            e.Property(x => x.Codigo).HasMaxLength(20);
            e.Property(x => x.Nombre).HasMaxLength(60);
            e.Property(x => x.PrecioPorColaborador).HasPrecision(10, 2);
            e.Property(x => x.ModulosIncluidos).HasMaxLength(300);
            e.Property(x => x.Funciones).HasMaxLength(300);
            e.HasData(Modulos.Planes);
        });
        b.Entity<ModuloComercial>(e =>
        {
            e.ToTable("Modulos");
            e.HasKey(x => x.Codigo);
            e.Property(x => x.Codigo).HasMaxLength(20);
            e.Property(x => x.Nombre).HasMaxLength(80);
            e.Property(x => x.Descripcion).HasMaxLength(300);
            e.Property(x => x.Beneficios).HasMaxLength(600);
            e.Property(x => x.PrecioPorColaborador).HasPrecision(10, 2);
            e.HasData(Modulos.Catalogo);
        });
        b.Entity<TenantModulo>(e =>
        {
            e.Property(x => x.Codigo).HasMaxLength(20);
            e.Property(x => x.PrecioPactado).HasPrecision(10, 2);
            e.HasIndex(x => new { x.TenantId, x.Codigo });
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ModuloComercial>().WithMany().HasForeignKey(x => x.Codigo).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TenantFlag>(e =>
        {
            e.Property(x => x.Flag).HasMaxLength(40);
            e.HasIndex(x => new { x.TenantId, x.Flag }).IsUnique();
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<SolicitudInformacion>(e =>
        {
            e.Property(x => x.Modulo).HasMaxLength(80);
            e.Property(x => x.Usuario).HasMaxLength(160);
            e.Property(x => x.Correo).HasMaxLength(254);
            e.Property(x => x.Mensaje).HasMaxLength(500);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.HasIndex(x => x.CreadaEn);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Factura>(e =>
        {
            e.Property(x => x.Numero).HasMaxLength(20);
            e.Property(x => x.Periodo).HasMaxLength(7);
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.Igv).HasPrecision(12, 2);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.Property(x => x.SunatEstado).HasMaxLength(40);
            e.HasIndex(x => new { x.TenantId, x.Periodo });
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<UsuarioPlataforma>(e =>
        {
            e.Property(x => x.Correo).HasMaxLength(254);
            e.HasIndex(x => x.Correo).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(120);
            e.Property(x => x.Rol).HasMaxLength(20);
            e.Property(x => x.TotpSecreto).HasMaxLength(400);
            e.Property(x => x.Sello).HasMaxLength(32);
        });
        b.Entity<AuditoriaPlataforma>(e =>
        {
            e.Property(x => x.Actor).HasMaxLength(160);
            e.Property(x => x.Accion).HasMaxLength(60);
            e.Property(x => x.Detalle).HasMaxLength(1000);
            e.Property(x => x.Ip).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(300);
            e.HasIndex(x => x.En);
            e.HasIndex(x => x.TenantId);
        });
        b.Entity<TokenPlataforma>(e =>
        {
            e.Property(x => x.Hash).HasMaxLength(64);
            e.HasIndex(x => x.Hash).IsUnique();
            e.HasOne<UsuarioPlataforma>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<SesionSoporte>(e =>
        {
            e.Property(x => x.Agente).HasMaxLength(120);
            e.Property(x => x.Motivo).HasMaxLength(500);
            e.Property(x => x.Ticket).HasMaxLength(40);
            e.Property(x => x.Modo).HasMaxLength(12);
            e.Property(x => x.Justificacion).HasMaxLength(500);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<UsuarioPlataforma>().WithMany().HasForeignKey(x => x.UsuarioPlataformaId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.Vigente);
        });
        b.Entity<NotaTenant>(e =>
        {
            e.Property(x => x.Autor).HasMaxLength(160);
            e.Property(x => x.Texto).HasMaxLength(2000);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Anuncio>(e =>
        {
            e.Property(x => x.Titulo).HasMaxLength(80);
            e.Property(x => x.Cuerpo).HasMaxLength(2000);
            e.Property(x => x.Tipo).HasMaxLength(20);
            e.Property(x => x.Planes).HasMaxLength(100);
            e.Property(x => x.Modulos).HasMaxLength(300);
            e.Property(x => x.Canal).HasMaxLength(20);
            e.Property(x => x.CreadoPor).HasMaxLength(160);
        });
        b.Entity<DefinicionFlag>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(40);
            e.Property(x => x.Descripcion).HasMaxLength(200);
            e.Property(x => x.Modulo).HasMaxLength(20);
            e.HasData(
                new DefinicionFlag { Clave = "firma_masiva_v2", Descripcion = "Firma masiva v2", Modulo = Modulos.Docs, CreadoEn = new DateTime(2026, 11, 1) },
                new DefinicionFlag { Clave = "nueva_ui_sst", Descripcion = "Nueva UI de SST", Modulo = Modulos.Sst, CreadoEn = new DateTime(2026, 11, 1) },
                new DefinicionFlag { Clave = "export_plame_beta", Descripcion = "Exportador PLAME (beta)", Modulo = Modulos.Payroll, CreadoEn = new DateTime(2026, 11, 1) },
                new DefinicionFlag { Clave = "portal_reconocimientos", Descripcion = "Reconocimientos en el portal", Modulo = Modulos.Comms, CreadoEn = new DateTime(2026, 11, 1) },
                new DefinicionFlag { Clave = "nexo_ai_widgets", Descripcion = "Widgets generados con Nexo AI", CreadoEn = new DateTime(2026, 11, 1) });
        });
        b.Entity<CatalogoGlobal>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(40);
            e.Property(x => x.Nombre).HasMaxLength(120);
            e.Property(x => x.Columnas).HasMaxLength(300);
            e.HasData(
                new CatalogoGlobal { Clave = "ubigeo", Nombre = "Ubigeo INEI", Columnas = "codigo,departamento,provincia,distrito" },
                new CatalogoGlobal { Clave = "ocupaciones", Nombre = "Ocupaciones T-Registro (SUNAT)", Columnas = "codigo,descripcion" },
                new CatalogoGlobal { Clave = "bancos", Nombre = "Bancos y formatos de pago", Columnas = "codigo,nombre,formato" },
                new CatalogoGlobal { Clave = "afp", Nombre = "AFP: comisiones y primas", Columnas = "afp,comision_flujo,comision_saldo,prima_seguro,aporte" },
                new CatalogoGlobal { Clave = "feriados", Nombre = "Feriados nacionales", Columnas = "fecha,descripcion" },
                new CatalogoGlobal { Clave = "tipos_documento", Nombre = "Tipos de documento de identidad", Columnas = "codigo,descripcion" },
                new CatalogoGlobal { Clave = "parametros_tributarios", Nombre = "Parámetros tributarios (UIT, tasas)", Columnas = "clave,valor,vigencia" },
                new CatalogoGlobal { Clave = "monedas", Nombre = "Monedas", Columnas = "codigo,nombre,simbolo" });
        });
        b.Entity<VersionCatalogo>(e =>
        {
            e.Property(x => x.Clave).HasMaxLength(40);
            e.Property(x => x.Version).HasMaxLength(20);
            e.Property(x => x.Notas).HasMaxLength(500);
            e.Property(x => x.PublicadaPor).HasMaxLength(160);
            e.HasIndex(x => new { x.Clave, x.Version }).IsUnique();
            e.HasOne<CatalogoGlobal>().WithMany().HasForeignKey(x => x.Clave).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<UsoMensual>(e =>
        {
            e.Property(x => x.Periodo).HasMaxLength(7);
            e.Property(x => x.AlmacenamientoMb).HasPrecision(12, 2);
            e.HasIndex(x => new { x.TenantId, x.Periodo }).IsUnique();
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<CorreoSaliente>(e =>
        {
            e.Property(x => x.Para).HasMaxLength(254);
            e.Property(x => x.Asunto).HasMaxLength(200);
            e.Property(x => x.Enlace).HasMaxLength(1000);
            e.Property(x => x.Remitente).HasMaxLength(320);
            e.Property(x => x.DkimDominio).HasMaxLength(253);
            e.Property(x => x.Proveedor).HasMaxLength(20);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.HasIndex(x => x.CreadoEn);
        });
    }
}
