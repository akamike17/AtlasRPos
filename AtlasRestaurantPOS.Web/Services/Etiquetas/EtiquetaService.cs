using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Services.Etiquetas;

public sealed class EtiquetaService : IEtiquetaService
{
    private readonly AtlasRestaurantDbContext _db;
    public EtiquetaService(AtlasRestaurantDbContext db) { _db = db; }

    public async Task<EtiquetaViewModel?> ObtenerProductoAsync(int idEmpresa, int idProducto, CancellationToken cancellationToken = default) =>
        await _db.Productos.AsNoTracking()
            .Where(p => p.IdEmpresa == idEmpresa && p.IdProducto == idProducto)
            .Select(p => new EtiquetaViewModel
            {
                Tipo = "Producto", Id = p.IdProducto, Nombre = p.Nombre,
                Codigo = p.Codigo, CodigoBarras = p.CodigoBarras, Precio = p.Precio
            }).FirstOrDefaultAsync(cancellationToken);

    public async Task<EtiquetaViewModel?> ObtenerInsumoAsync(int idEmpresa, int idInsumo, CancellationToken cancellationToken = default) =>
        await _db.Insumos.AsNoTracking()
            .Where(i => i.IdEmpresa == idEmpresa && i.IdInsumo == idInsumo)
            .Select(i => new EtiquetaViewModel
            {
                Tipo = "Insumo", Id = i.IdInsumo, Nombre = i.Nombre,
                Codigo = i.Codigo, CodigoBarras = i.CodigoBarras,
                Unidad = i.UnidadMedida != null ? i.UnidadMedida.Nombre : string.Empty,
                UnidadCodigo = i.UnidadMedida != null ? i.UnidadMedida.Codigo : string.Empty
            }).FirstOrDefaultAsync(cancellationToken);
}
