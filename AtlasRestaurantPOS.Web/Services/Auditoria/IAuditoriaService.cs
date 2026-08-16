namespace AtlasRestaurantPOS.Web.Services.Auditoria;

public interface IAuditoriaService
{
    Task RegistrarAsync(
        string entidad,
        string? idEntidad,
        string accion,
        object? datosAnteriores = null,
        object? datosNuevos = null);
}