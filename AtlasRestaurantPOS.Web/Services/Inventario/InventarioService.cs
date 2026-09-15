using System.Data;
using AtlasRestaurantPOS.Web.Constants;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Services.Inventario;

public sealed class InventarioService : IInventarioService
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public InventarioService(AtlasRestaurantDbContext db, IAuditoriaService auditoria) { _db = db; _auditoria = auditoria; }

    public async Task<ResultadoMovimientoInventario> RegistrarMovimientoAsync(int idEmpresa, int idSucursal, int idInsumo, int idUsuario, string tipo, decimal cantidad, string concepto, decimal? costoUnitario = null, long? idComanda = null, long? idComandaDetalle = null, int? idCaja = null, long? idSesionCaja = null, CancellationToken cancellationToken = default)
    {
        tipo = (tipo ?? string.Empty).Trim().ToUpperInvariant();
        concepto = (concepto ?? string.Empty).Trim();
        var aumentos = new[] { TiposMovimientoInventario.ENTRADA, TiposMovimientoInventario.AJUSTE_POSITIVO, TiposMovimientoInventario.DEVOLUCION_INVENTARIO };
        var disminuciones = new[] { TiposMovimientoInventario.AJUSTE_NEGATIVO, TiposMovimientoInventario.MERMA, TiposMovimientoInventario.VENTA };
        if (!aumentos.Contains(tipo) && !disminuciones.Contains(tipo)) return new(false, "Tipo de movimiento no válido.");
        if (cantidad <= 0) return new(false, "La cantidad debe ser mayor a cero.");
        if (string.IsNullOrWhiteSpace(concepto) || concepto.Length < 3) return new(false, "El motivo / concepto es obligatorio (mínimo 3 caracteres).");
        if (concepto.Length > 500) concepto = concepto[..500];
        if (costoUnitario is < 0) return new(false, "El costo unitario no puede ser negativo.");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (!await _db.Sucursales.AsNoTracking().AnyAsync(s => s.IdSucursal == idSucursal && s.IdEmpresa == idEmpresa && s.Activo, cancellationToken)) return await RechazarAsync(tx, "La sucursal no pertenece a tu empresa o está inactiva.");
            var insumo = await _db.Insumos.AsNoTracking().FirstOrDefaultAsync(i => i.IdInsumo == idInsumo && i.IdEmpresa == idEmpresa && i.Activo, cancellationToken);
            if (insumo is null) return await RechazarAsync(tx, "El insumo no existe o está inactivo.");

            var existencia = await BloquearAsync(idSucursal, idInsumo, cancellationToken);
            if (existencia is null && aumentos.Contains(tipo))
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ExistenciasInsumo (IdSucursal, IdInsumo, CantidadActual, FechaModificacion) VALUES ({idSucursal}, {idInsumo}, {0m}, {DateTime.UtcNow}) ON DUPLICATE KEY UPDATE IdInsumo = IdInsumo", cancellationToken);
                existencia = await BloquearAsync(idSucursal, idInsumo, cancellationToken);
            }
            if (existencia is null) return await RechazarAsync(tx, "No existe existencia previa para ese insumo en la sucursal.");

            var anterior = existencia.CantidadActual;
            var nueva = aumentos.Contains(tipo) ? anterior + cantidad : anterior - cantidad;
            if (nueva < 0) return await RechazarAsync(tx, $"Operación rechazada: la existencia actual es {anterior:0.000} y no permite deducir {cantidad:0.000}.");
            existencia.CantidadActual = nueva;
            existencia.FechaModificacion = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var movimiento = new MovimientoInventario { IdSucursal = idSucursal, IdInsumo = idInsumo, IdUsuario = idUsuario, IdComanda = idComanda, IdComandaDetalle = idComandaDetalle, IdCaja = idCaja, IdSesionCaja = idSesionCaja, Tipo = tipo, Cantidad = cantidad, ExistenciaAnterior = anterior, ExistenciaNueva = nueva, CostoUnitario = costoUnitario, Concepto = concepto, FechaMovimiento = DateTime.UtcNow };
            _db.MovimientosInventario.Add(movimiento);
            await _db.SaveChangesAsync(cancellationToken);
            var accion = tipo switch { TiposMovimientoInventario.ENTRADA => "ENTRADA_INVENTARIO", TiposMovimientoInventario.AJUSTE_POSITIVO or TiposMovimientoInventario.AJUSTE_NEGATIVO => "AJUSTE_INVENTARIO", TiposMovimientoInventario.MERMA => "MERMA_INVENTARIO", TiposMovimientoInventario.DEVOLUCION_INVENTARIO => "DEVOLUCION_INVENTARIO", TiposMovimientoInventario.VENTA => "CONSUMO_VENTA", _ => "CREAR_MOVIMIENTO_INVENTARIO" };
            await _auditoria.RegistrarAsync("MovimientoInventario", movimiento.IdMovimientoInventario.ToString(), accion, new { movimiento.IdSucursal, movimiento.IdInsumo, movimiento.Tipo, ExistenciaAnterior = anterior }, new { movimiento.IdMovimientoInventario, movimiento.IdSucursal, movimiento.IdInsumo, Insumo = insumo.Nombre, movimiento.Tipo, movimiento.Cantidad, ExistenciaAnterior = anterior, ExistenciaNueva = nueva, movimiento.CostoUnitario, movimiento.Concepto, movimiento.IdComanda, movimiento.IdCaja, movimiento.IdSesionCaja, movimiento.IdUsuario });
            await tx.CommitAsync(cancellationToken);
            return new(true, "Movimiento de inventario registrado correctamente.", movimiento.IdMovimientoInventario);
        }
        catch { await tx.RollbackAsync(cancellationToken); throw; }
    }

    private Task<ExistenciaInsumo?> BloquearAsync(int idSucursal, int idInsumo, CancellationToken cancellationToken) => _db.ExistenciasInsumo.FromSqlInterpolated($"SELECT * FROM ExistenciasInsumo WHERE IdSucursal = {idSucursal} AND IdInsumo = {idInsumo} FOR UPDATE").FirstOrDefaultAsync(cancellationToken);
    private static async Task<ResultadoMovimientoInventario> RechazarAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, string mensaje) { await tx.RollbackAsync(); return new(false, mensaje); }
}
