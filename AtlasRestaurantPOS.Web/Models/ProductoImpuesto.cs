namespace AtlasRestaurantPOS.Web.Models;

public class ProductoImpuesto
{
    public int IdProducto { get; set; }
    public int IdImpuesto { get; set; }

    public Producto Producto { get; set; } = null!;
    public Impuesto Impuesto { get; set; } = null!;
}