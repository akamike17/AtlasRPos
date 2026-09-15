namespace AtlasRestaurantPOS.Web.Services.Inventario;

public sealed record ResultadoMovimientoInventario(bool Ok, string Mensaje, long? IdMovimiento = null);

public interface IInventarioService
{
    Task<ResultadoMovimientoInventario> RegistrarMovimientoAsync(int idEmpresa, int idSucursal, int idInsumo, int idUsuario, string tipo, decimal cantidad, string concepto, decimal? costoUnitario = null, long? idComanda = null, long? idComandaDetalle = null, int? idCaja = null, long? idSesionCaja = null, CancellationToken cancellationToken = default);
}
