namespace AtlasRestaurantPOS.Web.Models;

public class ComandaDetalle
{
    public long IdComandaDetalle { get; set; }
    public long IdComanda { get; set; }
    public int IdProducto { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
    public string? Notas { get; set; }

    public Comanda Comanda { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
