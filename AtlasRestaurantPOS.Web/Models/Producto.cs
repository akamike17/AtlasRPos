using AtlasRestaurantPOS.Web.Models;

namespace AtlasRestaurantPOS.Web.Models;

public class Producto
{
    public int IdProducto { get; set; }
    public int? IdEmpresa { get; set; }
    public int IdCategoriaProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public string? Codigo { get; set; }
    public string? CodigoBarras { get; set; }

    public Empresa? Empresa { get; set; }
    public CategoriaProducto CategoriaProducto { get; set; } = null!;
    public ICollection<ComandaDetalle> ComandaDetalles { get; set; } = new List<ComandaDetalle>();
    public ICollection<ProductoImpuesto> ProductosImpuestos { get; set; } = new List<ProductoImpuesto>();
    public ICollection<RecetaProducto> Recetas { get; set; } = new List<RecetaProducto>();
}
