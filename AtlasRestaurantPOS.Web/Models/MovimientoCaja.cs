namespace AtlasRestaurantPOS.Web.Models;

public class MovimientoCaja
{
    public long IdMovimientoCaja { get; set; }
    public int IdCaja { get; set; }
    public long? IdSesionCaja { get; set; }
    public int IdUsuario { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public string? Concepto { get; set; }
    public DateTime FechaMovimiento { get; set; }
    public string? ReferenciaExterna { get; set; }

    public Caja Caja { get; set; } = null!;
    public SesionCaja? SesionCaja { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
