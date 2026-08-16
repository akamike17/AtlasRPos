namespace AtlasRestaurantPOS.Web.Models;

public class FolioSecuencia
{
    public int IdFolioSecuencia { get; set; }
    public int IdSucursal { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public long UltimoNumero { get; set; }
    public string? Prefijo { get; set; }
    public int Longitud { get; set; }
    public DateTime FechaModificacion { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
}