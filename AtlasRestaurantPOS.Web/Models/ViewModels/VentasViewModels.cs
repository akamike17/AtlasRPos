namespace AtlasRestaurantPOS.Web.Models.ViewModels;

public class VentasIndexViewModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Sucursal { get; set; } = string.Empty;
    public List<CategoriaVentasViewModel> Categorias { get; set; } = new();
    public List<MetodoPagoViewModel> MetodosPago { get; set; } = new();
    public long? IdComandaInicial { get; set; }
}

public class CategoriaVentasViewModel
{
    public int IdCategoriaProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<ProductoVentasViewModel> Productos { get; set; } = new();
}

public class ProductoVentasViewModel
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
}

public class MetodoPagoViewModel
{
    public int IdMetodoPago { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public bool RequiereReferencia { get; set; }
    public bool PermiteCambio { get; set; }
}

public class AgregarProductoViewModel
{
    public long IdComanda { get; set; }
    public int IdProducto { get; set; }
    public decimal Cantidad { get; set; }
    public string? Notas { get; set; }
}

public class ModificarCantidadViewModel
{
    public long IdComanda { get; set; }
    public long IdComandaDetalle { get; set; }
    public decimal Cantidad { get; set; }
    public string? Notas { get; set; }
}

public class QuitarDetalleViewModel
{
    public long IdComanda { get; set; }
    public long IdComandaDetalle { get; set; }
}

public class RegistrarPagoViewModel
{
    public long IdComanda { get; set; }
    public int IdMetodoPago { get; set; }
    public decimal Importe { get; set; }
    public string? Referencia { get; set; }
}

public class ComandaEstadoViewModel
{
    public long IdComanda { get; set; }
    public string Folio { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? NombreMesa { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Impuestos { get; set; }
    public decimal Total { get; set; }
    public decimal Saldo { get; set; }
    public bool ComandaCerrada { get; set; }
    public List<ComandaItemViewModel> Partidas { get; set; } = new();
    public List<PagoItemViewModel> Pagos { get; set; } = new();
}

public class ComandaItemViewModel
{
    public long IdComandaDetalle { get; set; }
    public int IdProducto { get; set; }
    public string Producto { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
    public string? Notas { get; set; }
}

public class PagoItemViewModel
{
    public long IdPago { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public DateTime FechaPago { get; set; }
    public string? Referencia { get; set; }
}