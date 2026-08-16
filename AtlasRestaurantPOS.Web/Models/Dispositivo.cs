namespace AtlasRestaurantPOS.Web.Models;

public class Dispositivo
{
    public int IdDispositivo { get; set; }
    public int IdSucursal { get; set; }
    public int? IdCaja { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public string? Identificador { get; set; }
    public string? Conexion { get; set; }
    public string? Configuracion { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
    public Caja? Caja { get; set; }
}
