namespace AtlasRestaurantPOS.Web.Models;

public class Auditoria
{
    public long IdAuditoria { get; set; }
    public int? IdEmpresa { get; set; }
    public int? IdSucursal { get; set; }
    public int? IdCaja { get; set; }
    public long? IdSesionCaja { get; set; }
    public int? IdUsuario { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public string? IdEntidad { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? DatosAnteriores { get; set; }
    public string? DatosNuevos { get; set; }
    public string? Ip { get; set; }
    public DateTime Fecha { get; set; }

    public Empresa? Empresa { get; set; }
    public Sucursal? Sucursal { get; set; }
    public Caja? Caja { get; set; }
    public SesionCaja? SesionCaja { get; set; }
    public Usuario? Usuario { get; set; }
}