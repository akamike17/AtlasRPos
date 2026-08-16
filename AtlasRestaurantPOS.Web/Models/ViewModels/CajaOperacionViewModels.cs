namespace AtlasRestaurantPOS.Web.Models.ViewModels;

public class AperturaCajaViewModel
{
    public int IdCaja { get; set; }
    public decimal FondoInicial { get; set; }
    public string? Observaciones { get; set; }
}

public class CierreCajaViewModel
{
    public decimal MontoCierre { get; set; }
    public string? Observaciones { get; set; }
}

public class MovimientoCajaOperacionViewModel
{
    public decimal Importe { get; set; }
    public string Concepto { get; set; } = string.Empty;
}

public class CajaOpcionViewModel
{
    public int IdCaja { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = "DISPONIBLE";
    public long? IdSesionCaja { get; set; }
    public string? UsuarioApertura { get; set; }
    public DateTime? FechaApertura { get; set; }
    public bool EsPropia { get; set; }
}

public class SeleccionCajaViewModel
{
    public List<CajaOpcionViewModel> Cajas { get; set; } = new();
}

public class MovimientoItemViewModel
{
    public long IdMovimientoCaja { get; set; }
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string? Concepto { get; set; }
    public decimal Importe { get; set; }
    public string Usuario { get; set; } = string.Empty;
}

public class PanelCajaViewModel
{
    public int IdCaja { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Sucursal { get; set; } = string.Empty;
    public string UsuarioApertura { get; set; } = string.Empty;
    public DateTime FechaApertura { get; set; }
    public decimal FondoInicial { get; set; }
    public decimal Entradas { get; set; }
    public decimal Retiros { get; set; }
    public decimal VentasEfectivo { get; set; }
    public decimal EfectivoEsperado { get; set; }
    public List<MovimientoItemViewModel> Movimientos { get; set; } = new();
}