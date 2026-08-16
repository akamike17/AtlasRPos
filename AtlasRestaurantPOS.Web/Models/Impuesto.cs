namespace AtlasRestaurantPOS.Web.Models;

public class Impuesto
{
    public int IdImpuesto { get; set; }
    public int IdEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Tasa { get; set; }
    public bool IncluidoEnPrecio { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<ProductoImpuesto> ProductosImpuestos { get; set; } = new List<ProductoImpuesto>();
}