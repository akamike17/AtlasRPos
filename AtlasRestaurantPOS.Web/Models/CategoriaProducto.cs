namespace AtlasRestaurantPOS.Web.Models;

public class CategoriaProducto
{
    public int IdCategoriaProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
