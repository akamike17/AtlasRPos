namespace AtlasRestaurantPOS.Web.Models.ViewModels;

public class SalonIndexViewModel
{
    public List<MesaSalonViewModel> Mesas { get; set; } = new();
}

public class MesaSalonViewModel
{
    public int IdMesa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Capacidad { get; set; }
    public string Estado { get; set; } = string.Empty;
    public long? IdComanda { get; set; }
    public string? Folio { get; set; }
    public decimal? Total { get; set; }
    public string? UsuarioResponsable { get; set; }
    public bool EsPropia { get; set; }
    public bool EsAjena { get; set; }
}

public class AbrirMesaViewModel
{
    public int IdMesa { get; set; }
}

public class TransferirMesaViewModel
{
    public int IdMesaOrigen { get; set; }
    public int IdMesaDestino { get; set; }
}