namespace AtlasRestaurantPOS.Web.Models;

public class Caja
{
    public int IdCaja { get; set; }
    public int IdSucursal { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<SesionCaja> SesionesCaja { get; set; } = new List<SesionCaja>();
    public ICollection<Dispositivo> Dispositivos { get; set; } = new List<Dispositivo>();
    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();
    public ICollection<MovimientoCaja> MovimientosCaja { get; set; } = new List<MovimientoCaja>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Auditoria> Auditorias { get; set; } = new List<Auditoria>();
}
