using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Services.CodigoInterno;

public class CodigoInternoService : ICodigoInternoService
{
    private const int LongitudCodigoPredeterminada = 6;

    private readonly AtlasRestaurantDbContext _db;

    public CodigoInternoService(AtlasRestaurantDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerarAsync(int idEmpresa, string tipoEntidad)
    {
        using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

        // Lock de la fila de secuencia (patrón FOR UPDATE, replicado de FolioComandaService)
        var sec = await _db.SecuenciasCodigo
            .FromSqlRaw(
                "SELECT * FROM SecuenciasCodigo WHERE IdEmpresa = {0} AND TipoEntidad = {1} FOR UPDATE",
                idEmpresa, tipoEntidad)
            .FirstOrDefaultAsync();

        if (sec is null)
        {
            sec = new SecuenciasCodigo
            {
                IdEmpresa = idEmpresa,
                TipoEntidad = tipoEntidad,
                Prefijo = null,
                Longitud = LongitudCodigoPredeterminada,
                UltimoNumero = 0,
                FechaModificacion = DateTime.UtcNow
            };
            _db.SecuenciasCodigo.Add(sec);
            await _db.SaveChangesAsync();
        }

        sec.UltimoNumero += 1;
        sec.FechaModificacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        var longitud = sec.Longitud > 0 ? sec.Longitud : LongitudCodigoPredeterminada;
        var patron = new string('0', longitud);
        return (sec.Prefijo ?? string.Empty) + sec.UltimoNumero.ToString(patron);
    }

}
