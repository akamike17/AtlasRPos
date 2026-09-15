using AtlasRestaurantPOS.Web.Models;

public class RecetaProducto
{
    public int IdRecetaProducto { get; set; }
    public int IdProducto { get; set; }
    public int IdInsumo { get; set; }
    public decimal Cantidad { get; set; }

    public Producto Producto { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
}
