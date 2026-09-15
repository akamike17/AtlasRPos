using AtlasRestaurantPOS.Web.Models;

public class Insumo
{
    public int IdInsumo { get; set; }
    public int IdEmpresa { get; set; }
    public int IdUnidadMedida { get; set; }
    public string? Codigo { get; set; }
    public string? CodigoBarras { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal CostoReferencia { get; set; }
    public decimal StockMinimo { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public UnidadMedida UnidadMedida { get; set; } = null!;
    public ICollection<ExistenciaInsumo> Existencias { get; set; } = new List<ExistenciaInsumo>();
    public ICollection<RecetaProducto> Recetas { get; set; } = new List<RecetaProducto>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}
