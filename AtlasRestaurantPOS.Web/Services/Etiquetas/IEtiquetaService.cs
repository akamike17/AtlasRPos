using AtlasRestaurantPOS.Web.Models.ViewModels;
namespace AtlasRestaurantPOS.Web.Services.Etiquetas;
public interface IEtiquetaService
{
    Task<EtiquetaViewModel?> ObtenerProductoAsync(int idEmpresa, int idProducto, CancellationToken cancellationToken = default);
    Task<EtiquetaViewModel?> ObtenerInsumoAsync(int idEmpresa, int idInsumo, CancellationToken cancellationToken = default);
}
