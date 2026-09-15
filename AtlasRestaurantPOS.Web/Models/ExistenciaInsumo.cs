using AtlasRestaurantPOS.Web.Models;

public class ExistenciaInsumo
{
    public int IdSucursal { get; set; }
    public int IdInsumo { get; set; }
    public decimal CantidadActual { get; set; }
    public DateTime FechaModificacion { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
}
