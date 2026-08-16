namespace AtlasRestaurantPOS.Web.Models.ViewModels;

public class CategoriaForm
{
    public int IdCategoriaProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class ProductoForm
{
    public int IdProducto { get; set; }
    public int IdCategoriaProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
}

public class ImpuestoForm
{
    public int IdImpuesto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Tasa { get; set; }
    public bool IncluidoEnPrecio { get; set; }
}

public class MetodoPagoForm
{
    public int IdMetodoPago { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public bool RequiereReferencia { get; set; }
    public bool PermiteCambio { get; set; }
}

public class AsignarImpuestoViewModel
{
    public int IdProducto { get; set; }
    public int IdImpuesto { get; set; }
}

public class QuitarImpuestoViewModel
{
    public int IdProducto { get; set; }
    public int IdImpuesto { get; set; }
}