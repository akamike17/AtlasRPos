namespace AtlasRestaurantPOS.Web.Models;

public class Usuario
{
    public int IdUsuario { get; set; }
    public int IdEmpresa { get; set; }
    public int IdRol { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UsuarioLogin { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Rol Rol { get; set; } = null!;
    public ICollection<SesionCaja> SesionesCajaApertura { get; set; } = new List<SesionCaja>();
    public ICollection<SesionCaja> SesionesCajaCierre { get; set; } = new List<SesionCaja>();
    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();
    public ICollection<Comanda> ComandasCanceladas { get; set; } = new List<Comanda>();
    public ICollection<MovimientoCaja> MovimientosCaja { get; set; } = new List<MovimientoCaja>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Pago> PagosDevueltos { get; set; } = new List<Pago>();
    public ICollection<Auditoria> Auditorias { get; set; } = new List<Auditoria>();
}
