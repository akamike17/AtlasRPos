namespace AtlasRestaurantPOS.Web.Models;

public class Pago
{
    public long IdPago { get; set; }
    public long IdComanda { get; set; }
    public int? IdMetodoPago { get; set; }
    public int? IdCaja { get; set; }
    public long? IdSesionCaja { get; set; }
    public int? IdUsuario { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public DateTime FechaPago { get; set; }
    public string? Referencia { get; set; }
    public string? ProveedorExterno { get; set; }
    public string? IdTransaccionExterna { get; set; }

    public Comanda Comanda { get; set; } = null!;
    public MetodoPago? MetodoPagoCatalogo { get; set; }
    public Caja? Caja { get; set; }
    public SesionCaja? SesionCaja { get; set; }
    public Usuario? Usuario { get; set; }
}
