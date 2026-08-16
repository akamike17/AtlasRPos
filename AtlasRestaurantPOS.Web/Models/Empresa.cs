namespace AtlasRestaurantPOS.Web.Models;

public class Empresa
{
    public int IdEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public string? Rfc { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<IntegracionExterna> IntegracionesExternas { get; set; } = new List<IntegracionExterna>();
    public ICollection<ConfiguracionPos> ConfiguracionesPos { get; set; } = new List<ConfiguracionPos>();
    public ICollection<Impuesto> Impuestos { get; set; } = new List<Impuesto>();
    public ICollection<MetodoPago> MetodosPago { get; set; } = new List<MetodoPago>();
    public ICollection<Auditoria> Auditorias { get; set; } = new List<Auditoria>();
}
