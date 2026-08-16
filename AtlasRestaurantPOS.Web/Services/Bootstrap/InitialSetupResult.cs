namespace AtlasRestaurantPOS.Web.Services.Bootstrap;

public class InitialSetupResult
{
    public bool ConfigCompleta { get; set; }
    public bool EmpresaCreada { get; set; }
    public bool SucursalCreada { get; set; }
    public bool RolCreado { get; set; }
    public bool UsuarioCreado { get; set; }
    public bool Idempotente { get; set; }
    public string? Detalle { get; set; }
}