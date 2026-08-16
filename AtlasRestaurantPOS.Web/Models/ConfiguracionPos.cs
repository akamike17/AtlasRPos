namespace AtlasRestaurantPOS.Web.Models;

public class ConfiguracionPos
{
    public int IdConfiguracionPos { get; set; }
    public int IdEmpresa { get; set; }
    public int? IdSucursal { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string? Valor { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sucursal? Sucursal { get; set; }
}