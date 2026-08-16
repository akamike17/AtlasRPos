namespace AtlasRestaurantPOS.Web.Models;

public class MetodoPago
{
    public int IdMetodoPago { get; set; }
    public int IdEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public bool RequiereReferencia { get; set; }
    public bool PermiteCambio { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}