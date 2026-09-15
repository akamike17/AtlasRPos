namespace AtlasRestaurantPOS.Web.Services.CodigoInterno;

public interface ICodigoInternoService
{
    Task<string> GenerarAsync(int idEmpresa, string tipoEntidad);
}
