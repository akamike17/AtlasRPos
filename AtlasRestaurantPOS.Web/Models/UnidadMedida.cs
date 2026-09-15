using AtlasRestaurantPOS.Web.Models;

public class UnidadMedida
{
    public int IdUnidadMedida { get; set; }
    public int IdEmpresa { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<Insumo> Insumos { get; set; } = new List<Insumo>();
}
