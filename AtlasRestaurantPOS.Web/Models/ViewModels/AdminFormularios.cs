namespace AtlasRestaurantPOS.Web.Models.ViewModels;

public class EmpresaForm
{
    public int IdEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public string? Rfc { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
}

public class SucursalForm
{
    public int IdSucursal { get; set; }
    public int IdEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
}

public class RolForm
{
    public int IdRol { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class UsuarioForm
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UsuarioLogin { get; set; } = string.Empty;
    public int IdEmpresa { get; set; }
    public int IdRol { get; set; }
    public string? Correo { get; set; }
    public string? Password { get; set; }
}

public class CajaForm
{
    public int IdCaja { get; set; }
    public int IdSucursal { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}