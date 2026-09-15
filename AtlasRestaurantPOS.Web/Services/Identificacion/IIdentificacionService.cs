namespace AtlasRestaurantPOS.Web.Services.Identificacion;

public sealed record ProductoIdentificado(int IdProducto, string Nombre, decimal Precio, string? Codigo, string? CodigoBarras);
public sealed record InsumoIdentificado(int IdInsumo, string Nombre, string? Codigo, string? CodigoBarras, string Unidad, string UnidadCodigo);
public interface IIdentificacionService
{
    Task<ProductoIdentificado?> BuscarProductoAsync(int idEmpresa, string codigo, CancellationToken cancellationToken = default);
    Task<InsumoIdentificado?> BuscarInsumoAsync(int idEmpresa, string codigo, CancellationToken cancellationToken = default);
}
