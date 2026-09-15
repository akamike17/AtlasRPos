using AtlasRestaurantPOS.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Services.Identificacion;

public sealed class IdentificacionService : IIdentificacionService
{
    private readonly AtlasRestaurantDbContext _db;
    public IdentificacionService(AtlasRestaurantDbContext db) { _db = db; }

    public async Task<ProductoIdentificado?> BuscarProductoAsync(int idEmpresa, string codigo, CancellationToken cancellationToken = default)
    {
        codigo = (codigo ?? string.Empty).Trim();
        if (idEmpresa <= 0 || string.IsNullOrWhiteSpace(codigo)) return null;
        return await _db.Productos.AsNoTracking()
            .Where(p => p.IdEmpresa == idEmpresa && p.Activo && p.CategoriaProducto.Activo &&
                ((p.Codigo != null && p.Codigo == codigo) || (p.CodigoBarras != null && p.CodigoBarras == codigo)))
            .Select(p => new ProductoIdentificado(p.IdProducto, p.Nombre, p.Precio, p.Codigo, p.CodigoBarras))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<InsumoIdentificado?> BuscarInsumoAsync(int idEmpresa, string codigo, CancellationToken cancellationToken = default)
    {
        codigo = (codigo ?? string.Empty).Trim();
        if (idEmpresa <= 0 || string.IsNullOrWhiteSpace(codigo)) return null;
        return await _db.Insumos.AsNoTracking()
            .Where(i => i.IdEmpresa == idEmpresa && i.Activo &&
                ((i.Codigo != null && i.Codigo == codigo) || (i.CodigoBarras != null && i.CodigoBarras == codigo)))
            .Select(i => new InsumoIdentificado(i.IdInsumo, i.Nombre, i.Codigo, i.CodigoBarras,
                i.UnidadMedida != null ? i.UnidadMedida.Nombre : string.Empty,
                i.UnidadMedida != null ? i.UnidadMedida.Codigo : string.Empty))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
