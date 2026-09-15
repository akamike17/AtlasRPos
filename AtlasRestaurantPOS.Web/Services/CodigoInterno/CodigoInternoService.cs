using System.Data;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Services.CodigoInterno;

public class CodigoInternoService : ICodigoInternoService
{
    private const int LongitudCodigoPredeterminada = 6;
    private readonly AtlasRestaurantDbContext _db;

    public CodigoInternoService(AtlasRestaurantDbContext db) { _db = db; }

    public async Task<string> GenerarAsync(int idEmpresa, string tipoEntidad)
    {
        if (idEmpresa <= 0 || string.IsNullOrWhiteSpace(tipoEntidad)) throw new ArgumentException("La empresa y el tipo de entidad son obligatorios.");
        tipoEntidad = tipoEntidad.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            // Inserción idempotente; después se bloquea la fila real. Evita la carrera del primer código.
            await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SecuenciasCodigo (IdEmpresa, TipoEntidad, Prefijo, Longitud, UltimoNumero, FechaModificacion) VALUES ({idEmpresa}, {tipoEntidad}, {null}, {LongitudCodigoPredeterminada}, {0L}, {DateTime.UtcNow}) ON DUPLICATE KEY UPDATE IdEmpresa = IdEmpresa");

            var sec = await _db.SecuenciasCodigo
                .FromSqlInterpolated($"SELECT * FROM SecuenciasCodigo WHERE IdEmpresa = {idEmpresa} AND TipoEntidad = {tipoEntidad} FOR UPDATE")
                .FirstAsync();

            sec.UltimoNumero += 1;
            sec.FechaModificacion = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var longitud = sec.Longitud > 0 ? sec.Longitud : LongitudCodigoPredeterminada;
            return (sec.Prefijo ?? string.Empty) + sec.UltimoNumero.ToString(new string('0', longitud));
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
