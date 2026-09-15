namespace AtlasRestaurantPOS.Web.Models.ViewModels;

public sealed class EtiquetaViewModel
{
    public string Tipo { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public string? CodigoBarras { get; set; }
    public string? Unidad { get; set; }
    public string? UnidadCodigo { get; set; }
    public decimal? Precio { get; set; }
}
