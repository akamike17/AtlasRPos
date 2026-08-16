using System;
using System.Threading.Tasks;

namespace AtlasRestaurantPOS.Web.Services.Comanda;

public interface IFolioComandaService
{
    Task<string> GenerarAsync(int idSucursal);
}