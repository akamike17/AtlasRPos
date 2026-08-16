namespace AtlasRestaurantPOS.Web.Models;

public class Sucursal
{
    public int IdSucursal { get; set; }
    public int IdEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<Caja> Cajas { get; set; } = new List<Caja>();
    public ICollection<Mesa> Mesas { get; set; } = new List<Mesa>();
    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();
    public ICollection<Dispositivo> Dispositivos { get; set; } = new List<Dispositivo>();
    public ICollection<IntegracionExterna> IntegracionesExternas { get; set; } = new List<IntegracionExterna>();
    public ICollection<ConfiguracionPos> ConfiguracionesPos { get; set; } = new List<ConfiguracionPos>();
    public ICollection<FolioSecuencia> FoliosSecuencia { get; set; } = new List<FolioSecuencia>();
    public ICollection<Auditoria> Auditorias { get; set; } = new List<Auditoria>();
}
