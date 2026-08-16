namespace AtlasRestaurantPOS.Web.Models;

public class Mesa
{
    public int IdMesa { get; set; }
    public int IdSucursal { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Capacidad { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Activo { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();
}
