namespace AtlasRestaurantPOS.Web.Models;

public class SesionCaja
{
    public long IdSesionCaja { get; set; }
    public int IdCaja { get; set; }
    public int IdUsuarioApertura { get; set; }
    public int? IdUsuarioCierre { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal FondoInicial { get; set; }
    public decimal? MontoCierre { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observaciones { get; set; }

    public Caja Caja { get; set; } = null!;
    public Usuario UsuarioApertura { get; set; } = null!;
    public Usuario? UsuarioCierre { get; set; }
    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();
    public ICollection<MovimientoCaja> MovimientosCaja { get; set; } = new List<MovimientoCaja>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Auditoria> Auditorias { get; set; } = new List<Auditoria>();
}
