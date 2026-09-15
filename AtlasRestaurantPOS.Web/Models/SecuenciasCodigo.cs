using AtlasRestaurantPOS.Web.Models;

public class SecuenciasCodigo
{
    public int IdSecuenciaCodigo { get; set; }
    public int IdEmpresa { get; set; }
    public string TipoEntidad { get; set; } = string.Empty;
    public string? Prefijo { get; set; }
    public int Longitud { get; set; }
    public long UltimoNumero { get; set; }
    public DateTime FechaModificacion { get; set; }
}
