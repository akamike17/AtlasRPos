using AtlasRestaurantPOS.Web.Models;

public class MovimientoInventario
{
    public long IdMovimientoInventario { get; set; }
    public int IdSucursal { get; set; }
    public int IdInsumo { get; set; }
    public int IdUsuario { get; set; }
    public long? IdComanda { get; set; }
    public long? IdComandaDetalle { get; set; }
    public int? IdCaja { get; set; }
    public long? IdSesionCaja { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal ExistenciaAnterior { get; set; }
    public decimal ExistenciaNueva { get; set; }
    public decimal? CostoUnitario { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public DateTime FechaMovimiento { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Comanda? Comanda { get; set; }
    public ComandaDetalle? ComandaDetalle { get; set; }
    public Caja? Caja { get; set; }
    public SesionCaja? SesionCaja { get; set; }
}
