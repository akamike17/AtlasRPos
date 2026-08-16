namespace AtlasRestaurantPOS.Web.Models;

public class Comanda
{
    public long IdComanda { get; set; }
    public int IdSucursal { get; set; }
    public int IdCaja { get; set; }
    public long? IdSesionCaja { get; set; }
    public int? IdMesa { get; set; }
    public int IdUsuario { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Folio { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public string? MotivoCancelacion { get; set; }
    public int? IdUsuarioCancelacion { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Impuestos { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
    public Caja Caja { get; set; } = null!;
    public SesionCaja? SesionCaja { get; set; }
    public Mesa? Mesa { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public Usuario? UsuarioCancelacion { get; set; }
    public ICollection<ComandaDetalle> Detalles { get; set; } = new List<ComandaDetalle>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
