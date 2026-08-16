namespace AtlasRestaurantPOS.Web.Models.ViewModels;

public class MesaForm
{
    public int IdMesa { get; set; }
    public int IdSucursal { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Capacidad { get; set; }
}