using System;
using System.Threading.Tasks;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Services.Comanda;

public class FolioComandaService : IFolioComandaService
{
    private const string TipoDocumentoComanda = "COMANDA";
    private const int LongitudFolioPredeterminada = 6;

    private readonly AtlasRestaurantDbContext _db;

    public FolioComandaService(AtlasRestaurantDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerarAsync(int idSucursal)
    {
        var sucursal = await _db.Sucursales
            .FromSqlRaw("SELECT * FROM Sucursales WHERE IdSucursal = {0} FOR UPDATE", idSucursal)
            .FirstOrDefaultAsync();

        if (sucursal is null)
        {
            throw new InvalidOperationException($"La sucursal {idSucursal} no existe.");
        }

        var sec = await _db.FoliosSecuencia
            .FromSqlRaw("SELECT * FROM FoliosSecuencia WHERE IdSucursal = {0} AND TipoDocumento = {1} FOR UPDATE", idSucursal, TipoDocumentoComanda)
            .FirstOrDefaultAsync();

        if (sec is null)
        {
            sec = new FolioSecuencia
            {
                IdSucursal = idSucursal,
                TipoDocumento = TipoDocumentoComanda,
                UltimoNumero = 0,
                Longitud = LongitudFolioPredeterminada,
                FechaModificacion = DateTime.Now
            };
            _db.FoliosSecuencia.Add(sec);
            await _db.SaveChangesAsync();
        }

        sec.UltimoNumero += 1;
        sec.FechaModificacion = DateTime.Now;
        await _db.SaveChangesAsync();

        var patron = new string('0', sec.Longitud);
        return (sec.Prefijo ?? string.Empty) + sec.UltimoNumero.ToString(patron);
    }
}