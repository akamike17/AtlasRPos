using Microsoft.EntityFrameworkCore;
using AtlasRestaurantPOS.Web.Models;

namespace AtlasRestaurantPOS.Web.Data;

public class AtlasRestaurantDbContext : DbContext
{
    public AtlasRestaurantDbContext(DbContextOptions<AtlasRestaurantDbContext> options)
        : base(options)
    {
    }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<SesionCaja> SesionesCaja => Set<SesionCaja>();
    public DbSet<CategoriaProducto> CategoriasProducto => Set<CategoriaProducto>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<Comanda> Comandas => Set<Comanda>();
    public DbSet<ComandaDetalle> ComandaDetalles => Set<ComandaDetalle>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();
    public DbSet<IntegracionExterna> IntegracionesExternas => Set<IntegracionExterna>();
    public DbSet<ConfiguracionPos> ConfiguracionesPos => Set<ConfiguracionPos>();
    public DbSet<Impuesto> Impuestos => Set<Impuesto>();
    public DbSet<ProductoImpuesto> ProductosImpuestos => Set<ProductoImpuesto>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<FolioSecuencia> FoliosSecuencia => Set<FolioSecuencia>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("utf8mb4_unicode_ci");

        ConfigureEmpresa(modelBuilder);
        ConfigureSucursal(modelBuilder);
        ConfigureRol(modelBuilder);
        ConfigureUsuario(modelBuilder);
        ConfigureCaja(modelBuilder);
        ConfigureSesionCaja(modelBuilder);
        ConfigureCategoriaProducto(modelBuilder);
        ConfigureProducto(modelBuilder);
        ConfigureMesa(modelBuilder);
        ConfigureComanda(modelBuilder);
        ConfigureComandaDetalle(modelBuilder);
        ConfigurePago(modelBuilder);
        ConfigureMovimientoCaja(modelBuilder);
        ConfigureDispositivo(modelBuilder);
        ConfigureIntegracionExterna(modelBuilder);
        ConfigureConfiguracionPos(modelBuilder);
        ConfigureImpuesto(modelBuilder);
        ConfigureProductoImpuesto(modelBuilder);
        ConfigureMetodoPago(modelBuilder);
        ConfigureFolioSecuencia(modelBuilder);
        ConfigureAuditoria(modelBuilder);
    }

    private static void ConfigureEmpresa(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Empresa>(e =>
        {
            e.HasKey(x => x.IdEmpresa);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.RazonSocial).HasMaxLength(150);
            e.Property(x => x.Rfc).HasMaxLength(20);
            e.Property(x => x.Telefono).HasMaxLength(30);
            e.Property(x => x.Correo).HasMaxLength(200);

            e.HasMany(x => x.Sucursales)
                .WithOne(x => x.Empresa)
                .HasForeignKey(x => x.IdEmpresa)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Usuarios)
                .WithOne(x => x.Empresa)
                .HasForeignKey(x => x.IdEmpresa)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.IntegracionesExternas)
                .WithOne(x => x.Empresa)
                .HasForeignKey(x => x.IdEmpresa)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSucursal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sucursal>(e =>
        {
            e.HasKey(x => x.IdSucursal);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Direccion).HasMaxLength(500);
            e.Property(x => x.Telefono).HasMaxLength(30);

            e.HasMany(x => x.Cajas)
                .WithOne(x => x.Sucursal)
                .HasForeignKey(x => x.IdSucursal)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Mesas)
                .WithOne(x => x.Sucursal)
                .HasForeignKey(x => x.IdSucursal)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Comandas)
                .WithOne(x => x.Sucursal)
                .HasForeignKey(x => x.IdSucursal)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Dispositivos)
                .WithOne(x => x.Sucursal)
                .HasForeignKey(x => x.IdSucursal)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.IntegracionesExternas)
                .WithOne(x => x.Sucursal)
                .HasForeignKey(x => x.IdSucursal)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRol(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rol>(e =>
        {
            e.HasKey(x => x.IdRol);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            e.Property(x => x.Descripcion).HasMaxLength(500);

            e.HasIndex(x => x.Nombre).IsUnique();

            e.HasMany(x => x.Usuarios)
                .WithOne(x => x.Rol)
                .HasForeignKey(x => x.IdRol)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUsuario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.HasKey(x => x.IdUsuario);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.UsuarioLogin).IsRequired().HasMaxLength(100);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            e.Property(x => x.Correo).HasMaxLength(200);

            e.HasIndex(x => x.UsuarioLogin).IsUnique();

            e.HasMany(x => x.SesionesCajaApertura)
                .WithOne(x => x.UsuarioApertura)
                .HasForeignKey(x => x.IdUsuarioApertura)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.SesionesCajaCierre)
                .WithOne(x => x.UsuarioCierre)
                .HasForeignKey(x => x.IdUsuarioCierre)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Comandas)
                .WithOne(x => x.Usuario)
                .HasForeignKey(x => x.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.ComandasCanceladas)
                .WithOne(x => x.UsuarioCancelacion)
                .HasForeignKey(x => x.IdUsuarioCancelacion)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.MovimientosCaja)
                .WithOne(x => x.Usuario)
                .HasForeignKey(x => x.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.PagosDevueltos)
                .WithOne(x => x.UsuarioDevolucion)
                .HasForeignKey(x => x.IdUsuarioDevolucion)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Caja>(e =>
        {
            e.HasKey(x => x.IdCaja);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
            e.Property(x => x.Descripcion).HasMaxLength(500);

            e.HasIndex(x => new { x.IdSucursal, x.Codigo }).IsUnique();

            e.HasMany(x => x.SesionesCaja)
                .WithOne(x => x.Caja)
                .HasForeignKey(x => x.IdCaja)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Dispositivos)
                .WithOne(x => x.Caja)
                .HasForeignKey(x => x.IdCaja)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Comandas)
                .WithOne(x => x.Caja)
                .HasForeignKey(x => x.IdCaja)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.MovimientosCaja)
                .WithOne(x => x.Caja)
                .HasForeignKey(x => x.IdCaja)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSesionCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SesionCaja>(e =>
        {
            e.HasKey(x => x.IdSesionCaja);

            e.Property(x => x.FondoInicial).HasPrecision(18, 2);
            e.Property(x => x.MontoCierre).HasPrecision(18, 2);
            e.Property(x => x.Estado).IsRequired().HasMaxLength(50);
            e.Property(x => x.Observaciones).HasMaxLength(500);

            e.HasMany(x => x.Comandas)
                .WithOne(x => x.SesionCaja)
                .HasForeignKey(x => x.IdSesionCaja)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.MovimientosCaja)
                .WithOne(x => x.SesionCaja)
                .HasForeignKey(x => x.IdSesionCaja)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCategoriaProducto(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CategoriaProducto>(e =>
        {
            e.HasKey(x => x.IdCategoriaProducto);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Descripcion).HasMaxLength(500);

            e.HasIndex(x => x.Nombre).IsUnique();

            e.HasMany(x => x.Productos)
                .WithOne(x => x.CategoriaProducto)
                .HasForeignKey(x => x.IdCategoriaProducto)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProducto(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Producto>(e =>
        {
            e.HasKey(x => x.IdProducto);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.Precio).HasPrecision(18, 2);

            e.HasMany(x => x.ComandaDetalles)
                .WithOne(x => x.Producto)
                .HasForeignKey(x => x.IdProducto)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMesa(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mesa>(e =>
        {
            e.HasKey(x => x.IdMesa);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Estado).IsRequired().HasMaxLength(50);

            e.HasIndex(x => new { x.IdSucursal, x.Nombre }).IsUnique();

            e.HasMany(x => x.Comandas)
                .WithOne(x => x.Mesa)
                .HasForeignKey(x => x.IdMesa)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureComanda(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comanda>(e =>
        {
            e.HasKey(x => x.IdComanda);

            e.Property(x => x.Estado).IsRequired().HasMaxLength(50);
            e.Property(x => x.Folio).HasMaxLength(100);
            e.Property(x => x.MotivoCancelacion).HasMaxLength(500);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.Impuestos).HasPrecision(18, 2);
            e.Property(x => x.Descuento).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);

            e.HasIndex(x => new { x.IdSucursal, x.Folio }).IsUnique();

            e.HasMany(x => x.Detalles)
                .WithOne(x => x.Comanda)
                .HasForeignKey(x => x.IdComanda)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Pagos)
                .WithOne(x => x.Comanda)
                .HasForeignKey(x => x.IdComanda)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UsuarioCancelacion)
                .WithMany(x => x.ComandasCanceladas)
                .HasForeignKey(x => x.IdUsuarioCancelacion)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureComandaDetalle(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComandaDetalle>(e =>
        {
            e.HasKey(x => x.IdComandaDetalle);

            e.Property(x => x.Cantidad).HasPrecision(18, 3);
            e.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
            e.Property(x => x.Importe).HasPrecision(18, 2);
            e.Property(x => x.Notas).HasMaxLength(500);
        });
    }

    private static void ConfigurePago(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pago>(e =>
        {
            e.HasKey(x => x.IdPago);

            e.Property(x => x.MetodoPago).IsRequired().HasMaxLength(50);
            e.Property(x => x.Importe).HasPrecision(18, 2);
            e.Property(x => x.Referencia).HasMaxLength(200);
            e.Property(x => x.ProveedorExterno).HasMaxLength(100);
            e.Property(x => x.IdTransaccionExterna).HasMaxLength(200);
            e.Property(x => x.MotivoDevolucion).HasMaxLength(500);

            e.HasIndex(x => new { x.IdComanda, x.Devuelto });

            e.HasOne(x => x.MetodoPagoCatalogo)
                .WithMany(x => x.Pagos)
                .HasForeignKey(x => x.IdMetodoPago)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Caja)
                .WithMany(x => x.Pagos)
                .HasForeignKey(x => x.IdCaja)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.SesionCaja)
                .WithMany(x => x.Pagos)
                .HasForeignKey(x => x.IdSesionCaja)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Usuario)
                .WithMany(x => x.Pagos)
                .HasForeignKey(x => x.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UsuarioDevolucion)
                .WithMany(x => x.PagosDevueltos)
                .HasForeignKey(x => x.IdUsuarioDevolucion)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMovimientoCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovimientoCaja>(e =>
        {
            e.HasKey(x => x.IdMovimientoCaja);

            e.Property(x => x.Tipo).IsRequired().HasMaxLength(50);
            e.Property(x => x.Importe).HasPrecision(18, 2);
            e.Property(x => x.Concepto).HasMaxLength(500);
            e.Property(x => x.ReferenciaExterna).HasMaxLength(200);
        });
    }

    private static void ConfigureDispositivo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dispositivo>(e =>
        {
            e.HasKey(x => x.IdDispositivo);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Tipo).IsRequired().HasMaxLength(50);
            e.Property(x => x.Fabricante).HasMaxLength(100);
            e.Property(x => x.Modelo).HasMaxLength(100);
            e.Property(x => x.Identificador).HasMaxLength(200);
            e.Property(x => x.Conexion).HasMaxLength(500);
            e.Property(x => x.Configuracion).HasMaxLength(1000);
        });
    }

    private static void ConfigureIntegracionExterna(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntegracionExterna>(e =>
        {
            e.HasKey(x => x.IdIntegracionExterna);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Tipo).IsRequired().HasMaxLength(50);
            e.Property(x => x.Proveedor).HasMaxLength(100);
            e.Property(x => x.Endpoint).HasMaxLength(500);
            e.Property(x => x.Configuracion).HasMaxLength(1000);
        });
    }

    private static void ConfigureConfiguracionPos(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfiguracionPos>(e =>
        {
            e.HasKey(x => x.IdConfiguracionPos);

            e.Property(x => x.Clave).IsRequired().HasMaxLength(100);
            e.Property(x => x.Valor).HasColumnType("longtext");
            e.Property(x => x.Descripcion).HasMaxLength(500);

            e.HasIndex(x => new { x.IdEmpresa, x.IdSucursal, x.Clave });

            e.HasOne(x => x.Empresa)
                .WithMany(x => x.ConfiguracionesPos)
                .HasForeignKey(x => x.IdEmpresa)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Sucursal)
                .WithMany(x => x.ConfiguracionesPos)
                .HasForeignKey(x => x.IdSucursal)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureImpuesto(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Impuesto>(e =>
        {
            e.HasKey(x => x.IdImpuesto);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Tasa).HasPrecision(9, 4);

            e.HasIndex(x => new { x.IdEmpresa, x.Nombre }).IsUnique();

            e.HasOne(x => x.Empresa)
                .WithMany(x => x.Impuestos)
                .HasForeignKey(x => x.IdEmpresa)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.ProductosImpuestos)
                .WithOne(x => x.Impuesto)
                .HasForeignKey(x => x.IdImpuesto)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProductoImpuesto(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductoImpuesto>(e =>
        {
            e.HasKey(x => new { x.IdProducto, x.IdImpuesto });

            e.HasOne(x => x.Producto)
                .WithMany(x => x.ProductosImpuestos)
                .HasForeignKey(x => x.IdProducto)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Impuesto)
                .WithMany(x => x.ProductosImpuestos)
                .HasForeignKey(x => x.IdImpuesto)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMetodoPago(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetodoPago>(e =>
        {
            e.HasKey(x => x.IdMetodoPago);

            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            e.Property(x => x.Codigo).IsRequired().HasMaxLength(50);

            e.HasIndex(x => new { x.IdEmpresa, x.Codigo }).IsUnique();

            e.HasOne(x => x.Empresa)
                .WithMany(x => x.MetodosPago)
                .HasForeignKey(x => x.IdEmpresa)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFolioSecuencia(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FolioSecuencia>(e =>
        {
            e.HasKey(x => x.IdFolioSecuencia);

            e.Property(x => x.TipoDocumento).IsRequired().HasMaxLength(50);
            e.Property(x => x.Prefijo).HasMaxLength(20);

            e.HasIndex(x => new { x.IdSucursal, x.TipoDocumento }).IsUnique();

            e.HasOne(x => x.Sucursal)
                .WithMany(x => x.FoliosSecuencia)
                .HasForeignKey(x => x.IdSucursal)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditoria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Auditoria>(e =>
        {
            e.HasKey(x => x.IdAuditoria);

            e.Property(x => x.Entidad).IsRequired().HasMaxLength(100);
            e.Property(x => x.IdEntidad).HasMaxLength(100);
            e.Property(x => x.Accion).IsRequired().HasMaxLength(100);
            e.Property(x => x.DatosAnteriores).HasColumnType("longtext");
            e.Property(x => x.DatosNuevos).HasColumnType("longtext");
            e.Property(x => x.Ip).HasMaxLength(50);

            e.HasIndex(x => x.Fecha);
            e.HasIndex(x => new { x.IdUsuario, x.Fecha });
            e.HasIndex(x => new { x.Entidad, x.IdEntidad });

            e.HasOne(x => x.Empresa)
                .WithMany(x => x.Auditorias)
                .HasForeignKey(x => x.IdEmpresa)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Sucursal)
                .WithMany(x => x.Auditorias)
                .HasForeignKey(x => x.IdSucursal)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Caja)
                .WithMany(x => x.Auditorias)
                .HasForeignKey(x => x.IdCaja)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.SesionCaja)
                .WithMany(x => x.Auditorias)
                .HasForeignKey(x => x.IdSesionCaja)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Usuario)
                .WithMany(x => x.Auditorias)
                .HasForeignKey(x => x.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
