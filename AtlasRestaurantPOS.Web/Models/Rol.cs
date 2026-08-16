namespace AtlasRestaurantPOS.Web.Models;

public class Rol
{
    public int IdRol { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
