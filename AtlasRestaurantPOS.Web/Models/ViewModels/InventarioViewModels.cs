namespace AtlasRestaurantPOS.Web.Models.ViewModels;

public class UnidadMedidaForm
{
    public int IdUnidadMedida { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public class InsumoForm
{
    public int IdInsumo { get; set; }
    public int IdUnidadMedida { get; set; }
    public string? Codigo { get; set; }
    public string? CodigoBarras { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal CostoReferencia { get; set; }
    public decimal StockMinimo { get; set; }
    public bool GenerarCodigo { get; set; }
}

public class MovimientoForm
{
    public string Tipo { get; set; } = string.Empty; // ENTRADA, AJUSTE_POSITIVO, AJUSTE_NEGATIVO, MERMA, DEVOLUCION_INVENTARIO
    public int IdInsumo { get; set; }
    public decimal Cantidad { get; set; }
    public string? Concepto { get; set; }
}
