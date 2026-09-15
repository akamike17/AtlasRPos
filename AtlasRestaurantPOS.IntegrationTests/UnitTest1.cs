using System.Security.Claims;
using AtlasRestaurantPOS.Web.Constants;
using AtlasRestaurantPOS.Web.Controllers;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using AtlasRestaurantPOS.Web.Services.Comanda;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Xunit;

namespace AtlasRestaurantPOS.IntegrationTests;

[CollectionDefinition("MySql", DisableParallelization = true)]
public sealed class MySqlCollection : ICollectionFixture<MySqlFixture>
{
}

public sealed class MySqlFixture : IAsyncLifetime
{
    private string _adminConnectionString = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;
    public string DatabaseName { get; } = "atlas_rpos_test_" + Guid.NewGuid().ToString("N");

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("ATLAS_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("ATLAS_TEST_CONNECTION_STRING es obligatorio para las pruebas MySQL.");
        }

        var adminBuilder = new MySqlConnectionStringBuilder(configured)
        {
            Database = string.Empty
        };
        _adminConnectionString = adminBuilder.ConnectionString;

        try
        {
            await using var connection = new MySqlConnection(_adminConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE {DatabaseName} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            await command.ExecuteNonQueryAsync();

            adminBuilder.Database = DatabaseName;
            ConnectionString = adminBuilder.ConnectionString;

            await using var db = CreateContext();
            await db.Database.MigrateAsync();
        }
        catch
        {
            await DropDatabaseAsync();
            throw;
        }
    }

    public AtlasRestaurantDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AtlasRestaurantDbContext>()
            .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString))
            .Options;

        return new AtlasRestaurantDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("ATLAS_TEST_KEEP_DATABASE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        await DropDatabaseAsync();
    }

    private async Task DropDatabaseAsync()
    {
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            return;
        }

        await using var connection = new MySqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {DatabaseName}";
        await command.ExecuteNonQueryAsync();
    }
}

[Collection("MySql")]
public sealed class CierreQuirurgicoMySqlTests
{
    private readonly MySqlFixture _fixture;

    public CierreQuirurgicoMySqlTests(MySqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Cierre_multicaja_aisla_tenant_consumo_y_rollback_en_MySql_real()
    {
        await using (var seedContext = _fixture.CreateContext())
        {
            await SeedAsync(seedContext);
        }

        await using (var projectedContext = _fixture.CreateContext())
        {
            Assert.Equal(10m, await projectedContext.ExistenciasInsumo
                .Where(x => x.IdSucursal == 10 && x.IdInsumo == 501)
                .Select(x => x.CantidadActual)
                .SingleAsync());
            Assert.Equal(0, await projectedContext.MovimientosInventario.CountAsync());
        }

        await using (var tenantContext = _fixture.CreateContext())
        {
            var controller = CreateProductosController(tenantContext, 1002, 2, 20, null, null);
            var result = await controller.Buscar();
            var json = Assert.IsType<JsonResult>(result);
            var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(json.Value)
                .Cast<object>()
                .ToList();

            Assert.Single(rows);
            Assert.Equal(12, (int)rows[0].GetType().GetProperty("IdProducto")!.GetValue(rows[0])!);
        }

        await using (var ownTenantContext = _fixture.CreateContext())
        {
            var controller = CreateProductosController(ownTenantContext, 1001, 1, 10, null, null);
            var result = await controller.Buscar();
            var json = Assert.IsType<JsonResult>(result);
            var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(json.Value)
                .Cast<object>()
                .ToList();
            var productIds = rows
                .Select(row => (int)row.GetType().GetProperty("IdProducto")!.GetValue(row)!)
                .ToList();

            Assert.Contains(11, productIds);
            Assert.DoesNotContain(12, productIds);
        }

        await using (var codeContext = _fixture.CreateContext())
        {
            Assert.Equal(2, await codeContext.Productos.CountAsync(p => p.Codigo == "PIZZA-001"));
        }

        using var barrier = new Barrier(2);
        var attempts = await Task.WhenAll(
            Task.Run(() => CloseAsync(barrier, 1001, 101, 10001, 1001)),
            Task.Run(() => CloseAsync(barrier, 1002, 102, 10002, 1002)));

        Assert.Single(attempts, x => x.Ok);
        var failed = Assert.Single(attempts, x => !x.Ok);
        Assert.Contains("Stock insuficiente", failed.Message, StringComparison.OrdinalIgnoreCase);

        await using (var verifyContext = _fixture.CreateContext())
        {
            var stock = await verifyContext.ExistenciasInsumo
                .SingleAsync(x => x.IdSucursal == 10 && x.IdInsumo == 501);
            Assert.Equal(4m, stock.CantidadActual);
            Assert.Equal(1, await verifyContext.MovimientosInventario.CountAsync(x => x.IdComanda == 1001 || x.IdComanda == 1002));
            Assert.Equal(1, await verifyContext.Pagos.CountAsync(x => x.IdComanda == 1001 || x.IdComanda == 1002));
            Assert.Equal(1, await verifyContext.Comandas.CountAsync(x => (x.IdComanda == 1001 || x.IdComanda == 1002) && x.Estado == EstadosComanda.CERRADA));
            Assert.Equal(1, await verifyContext.Comandas.CountAsync(x => (x.IdComanda == 1001 || x.IdComanda == 1002) && x.Estado == EstadosComanda.ABIERTA));
        }

        var winner = Assert.Single(attempts, x => x.Ok);
        long paymentId;
        await using (var paymentContext = _fixture.CreateContext())
        {
            paymentId = await paymentContext.Pagos
                .Where(x => x.IdComanda == winner.IdComanda)
                .Select(x => x.IdPago)
                .SingleAsync();
        }

        await using (var refundContext = _fixture.CreateContext())
        {
            var controller = CreateVentasController(
                refundContext,
                winner.IdUsuario,
                1,
                10,
                winner.IdCaja,
                winner.IdSesion);

            var result = await controller.DevolverPago(new DevolverPagoViewModel
            {
                IdPago = paymentId,
                Motivo = "Cancelación probada"
            });

            Assert.True(ReadBool(result, "ok"));
        }

        await using (var rollbackContext = _fixture.CreateContext())
        {
            var controller = CreateVentasController(rollbackContext, 1001, 1, 10, 101, 10001);
            var result = await controller.RegistrarPago(new RegistrarPagoViewModel
            {
                IdComanda = 1003,
                IdMetodoPago = 701,
                Importe = 10m
            });

            Assert.False(ReadBool(result, "ok"));
        }

        await using (var finalContext = _fixture.CreateContext())
        {
            Assert.Equal(4m, await finalContext.ExistenciasInsumo
                .Where(x => x.IdSucursal == 10 && x.IdInsumo == 501)
                .Select(x => x.CantidadActual)
                .SingleAsync());

            Assert.Equal(0m, await finalContext.ExistenciasInsumo
                .Where(x => x.IdSucursal == 10 && x.IdInsumo == 502)
                .Select(x => x.CantidadActual)
                .SingleAsync());

            Assert.True(await finalContext.Pagos
                .Where(x => x.IdComanda == 1001 || x.IdComanda == 1002)
                .AllAsync(x => x.Devuelto));

            Assert.Equal(0, await finalContext.Pagos.CountAsync(x => x.IdComanda == 1003));
            Assert.Equal(0, await finalContext.MovimientosInventario.CountAsync(x => x.IdComanda == 1003));

            var movement = await finalContext.MovimientosInventario
                .SingleAsync(x => x.IdComanda == winner.IdComanda);
            var payment = await finalContext.Pagos
                .SingleAsync(x => x.IdComanda == winner.IdComanda);
            Assert.Equal(winner.IdCaja, movement.IdCaja);
            Assert.Equal(winner.IdSesion, movement.IdSesionCaja);
            Assert.Equal(winner.IdCaja, payment.IdCaja);
            Assert.Equal(winner.IdSesion, payment.IdSesionCaja);

            Assert.Equal(EstadosComanda.ABIERTA, await finalContext.Comandas
                .Where(x => x.IdComanda == 1003)
                .Select(x => x.Estado)
                .SingleAsync());
            Assert.Equal(EstadosMesa.OCUPADA, await finalContext.Mesas
                .Where(x => x.IdMesa == 901)
                .Select(x => x.Estado)
                .SingleAsync());
        }
    }

    private async Task<CloseAttempt> CloseAsync(Barrier barrier, long comandaId, int cajaId, long sesionId, int usuarioId)
    {
        await using var db = _fixture.CreateContext();
        var controller = CreateVentasController(db, usuarioId, 1, 10, cajaId, sesionId);
        barrier.SignalAndWait();

        var result = await controller.RegistrarPago(new RegistrarPagoViewModel
        {
            IdComanda = comandaId,
            IdMetodoPago = 701,
            Importe = 10m
        });

        return new CloseAttempt(
            comandaId,
            usuarioId,
            cajaId,
            sesionId,
            ReadBool(result, "ok"),
            ReadString(result, "mensaje"));
    }

    private static async Task SeedAsync(AtlasRestaurantDbContext db)
    {
        var now = DateTime.UtcNow;
        var passwordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>()
            .HashPassword(new Usuario(), "integration-only");

        db.AddRange(
            new Empresa { IdEmpresa = 1, Nombre = "Empresa A", Activo = true, FechaCreacion = now },
            new Empresa { IdEmpresa = 2, Nombre = "Empresa B", Activo = true, FechaCreacion = now },
            new Rol { IdRol = 1, Nombre = "Administrador", Activo = true },
            new Sucursal { IdSucursal = 10, IdEmpresa = 1, Nombre = "Sucursal A", Activo = true, FechaCreacion = now },
            new Sucursal { IdSucursal = 20, IdEmpresa = 2, Nombre = "Sucursal B", Activo = true, FechaCreacion = now },
            new Caja { IdCaja = 101, IdSucursal = 10, Nombre = "Caja01", Codigo = "C01", Activo = true, FechaCreacion = now },
            new Caja { IdCaja = 102, IdSucursal = 10, Nombre = "Caja02", Codigo = "C02", Activo = true, FechaCreacion = now },
            new Usuario { IdUsuario = 1001, IdEmpresa = 1, IdRol = 1, Nombre = "Cajero 01", UsuarioLogin = "cajero01", PasswordHash = passwordHash, Activo = true, FechaCreacion = now },
            new Usuario { IdUsuario = 1002, IdEmpresa = 1, IdRol = 1, Nombre = "Cajero 02", UsuarioLogin = "cajero02", PasswordHash = passwordHash, Activo = true, FechaCreacion = now },
            new UnidadMedida { IdUnidadMedida = 401, IdEmpresa = 1, Codigo = "KG", Nombre = "Kilogramo", Activo = true, FechaCreacion = now },
            new Insumo { IdInsumo = 501, IdEmpresa = 1, IdUnidadMedida = 401, Codigo = "INS-001", Nombre = "Harina", CostoReferencia = 1m, StockMinimo = 0m, Activo = true, FechaCreacion = now },
            new Insumo { IdInsumo = 502, IdEmpresa = 1, IdUnidadMedida = 401, Codigo = "INS-002", Nombre = "Salsa", CostoReferencia = 1m, StockMinimo = 0m, Activo = true, FechaCreacion = now },
            new CategoriaProducto { IdCategoriaProducto = 301, IdEmpresa = 1, Nombre = "Comida A", Activo = true },
            new CategoriaProducto { IdCategoriaProducto = 302, IdEmpresa = 2, Nombre = "Comida B", Activo = true },
            new Producto { IdProducto = 11, IdEmpresa = 1, IdCategoriaProducto = 301, Nombre = "Pizza A", Precio = 10m, Codigo = "PIZZA-001", CodigoBarras = "750001", Activo = true, FechaCreacion = now },
            new Producto { IdProducto = 12, IdEmpresa = 2, IdCategoriaProducto = 302, Nombre = "Pizza B", Precio = 10m, Codigo = "PIZZA-001", CodigoBarras = "750001", Activo = true, FechaCreacion = now },
            new Producto { IdProducto = 13, IdEmpresa = 1, IdCategoriaProducto = 301, Nombre = "Pizza sin stock", Precio = 10m, Codigo = "PIZZA-002", CodigoBarras = "750002", Activo = true, FechaCreacion = now },
            new RecetaProducto { IdRecetaProducto = 601, IdProducto = 11, IdInsumo = 501, Cantidad = 6m },
            new RecetaProducto { IdRecetaProducto = 602, IdProducto = 13, IdInsumo = 502, Cantidad = 6m },
            new ExistenciaInsumo { IdSucursal = 10, IdInsumo = 501, CantidadActual = 10m, FechaModificacion = now },
            new ExistenciaInsumo { IdSucursal = 10, IdInsumo = 502, CantidadActual = 0m, FechaModificacion = now },
            new MetodoPago { IdMetodoPago = 701, IdEmpresa = 1, Nombre = "Efectivo", Codigo = "EFECTIVO", PermiteCambio = true, Activo = true, FechaCreacion = now },
            new SesionCaja { IdSesionCaja = 10001, IdCaja = 101, IdUsuarioApertura = 1001, FechaApertura = now, FondoInicial = 0m, Estado = EstadosSesionCaja.ABIERTA },
            new SesionCaja { IdSesionCaja = 10002, IdCaja = 102, IdUsuarioApertura = 1002, FechaApertura = now, FondoInicial = 0m, Estado = EstadosSesionCaja.ABIERTA },
            new Mesa { IdMesa = 901, IdSucursal = 10, Nombre = "Mesa 01", Capacidad = 4, Estado = EstadosMesa.OCUPADA, Activo = true },
            new Comanda { IdComanda = 1001, IdSucursal = 10, IdCaja = 101, IdSesionCaja = 10001, IdMesa = null, IdUsuario = 1001, FechaApertura = now, Estado = EstadosComanda.ABIERTA, Folio = "A-1001", Subtotal = 10m, Total = 10m },
            new Comanda { IdComanda = 1002, IdSucursal = 10, IdCaja = 102, IdSesionCaja = 10002, IdMesa = null, IdUsuario = 1002, FechaApertura = now, Estado = EstadosComanda.ABIERTA, Folio = "A-1002", Subtotal = 10m, Total = 10m },
            new Comanda { IdComanda = 1003, IdSucursal = 10, IdCaja = 101, IdSesionCaja = 10001, IdMesa = 901, IdUsuario = 1001, FechaApertura = now, Estado = EstadosComanda.ABIERTA, Folio = "A-1003", Subtotal = 10m, Total = 10m },
            new ComandaDetalle { IdComandaDetalle = 2001, IdComanda = 1001, IdProducto = 11, Cantidad = 1m, PrecioUnitario = 10m, Importe = 10m },
            new ComandaDetalle { IdComandaDetalle = 2002, IdComanda = 1002, IdProducto = 11, Cantidad = 1m, PrecioUnitario = 10m, Importe = 10m },
            new ComandaDetalle { IdComandaDetalle = 2003, IdComanda = 1003, IdProducto = 13, Cantidad = 1m, PrecioUnitario = 10m, Importe = 10m });

        await db.SaveChangesAsync();
    }

    private static ProductosController CreateProductosController(AtlasRestaurantDbContext db, int usuario, int empresa, int sucursal, int? caja, long? sesion)
    {
        var httpContext = CreateHttpContext(usuario, empresa, sucursal, caja, sesion);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var controller = new ProductosController(
            db,
            new AuditoriaService(db, accessor),
            NullLogger<ProductosController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static VentasController CreateVentasController(AtlasRestaurantDbContext db, int usuario, int empresa, int sucursal, int caja, long sesion)
    {
        var httpContext = CreateHttpContext(usuario, empresa, sucursal, caja, sesion);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var controller = new VentasController(
            db,
            new AuditoriaService(db, accessor),
            new TestFolioComandaService(),
            NullLogger<VentasController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static DefaultHttpContext CreateHttpContext(int usuario, int empresa, int sucursal, int? caja, long? sesion)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.ToString()),
            new(ClaimTypes.Name, $"usuario-{usuario}"),
            new(ClaimTypes.Role, "Administrador"),
            new("IdEmpresa", empresa.ToString()),
            new("IdSucursal", sucursal.ToString()),
            new("IdRol", "1")
        };

        if (caja is not null)
        {
            claims.Add(new Claim("IdCaja", caja.Value.ToString()));
        }

        if (sesion is not null)
        {
            claims.Add(new Claim("IdSesionCaja", sesion.Value.ToString()));
        }

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Integration"))
        };
        return context;
    }

    private static bool ReadBool(IActionResult result, string propertyName)
    {
        var value = Assert.IsType<JsonResult>(result).Value;
        return (bool)(value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? false);
    }

    private static string ReadString(IActionResult result, string propertyName)
    {
        var value = Assert.IsType<JsonResult>(result).Value;
        return (string?)value?.GetType().GetProperty(propertyName)?.GetValue(value) ?? string.Empty;
    }

    private sealed record CloseAttempt(long IdComanda, int IdUsuario, int IdCaja, long IdSesion, bool Ok, string Message);

    private sealed class TestFolioComandaService : IFolioComandaService
    {
        public Task<string> GenerarAsync(int idSucursal) => Task.FromResult("TEST");
    }
}
