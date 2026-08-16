namespace AtlasRestaurantPOS.Web.Models;

public class IntegracionExterna
{
    public int IdIntegracionExterna { get; set; }
    public int? IdEmpresa { get; set; }
    public int? IdSucursal { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Proveedor { get; set; }
    public string? Endpoint { get; set; }
    public string? Configuracion { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa? Empresa { get; set; }
    public Sucursal? Sucursal { get; set; }
}
