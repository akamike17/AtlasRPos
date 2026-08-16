using System.Security.Claims;
using System.Text.Json;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;

namespace AtlasRestaurantPOS.Web.Services.Auditoria;

public class AuditoriaService : IAuditoriaService
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditoriaService(AtlasRestaurantDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RegistrarAsync(
        string entidad,
        string? idEntidad,
        string accion,
        object? datosAnteriores = null,
        object? datosNuevos = null)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        int? idUsuario = null;
        int? idEmpresa = null;
        int? idSucursal = null;
        int? idCaja = null;
        long? idSesionCaja = null;

        if (user?.Identity?.IsAuthenticated == true)
        {
            if (int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) idUsuario = uid;
            if (int.TryParse(user.FindFirstValue("IdEmpresa"), out var eid)) idEmpresa = eid;
            if (int.TryParse(user.FindFirstValue("IdSucursal"), out var sid)) idSucursal = sid;
            if (int.TryParse(user.FindFirstValue("IdCaja"), out var cid)) idCaja = cid;
            if (long.TryParse(user.FindFirstValue("IdSesionCaja"), out var sesid)) idSesionCaja = sesid;
        }

        var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        _db.Auditorias.Add(new Models.Auditoria
        {
            IdEmpresa = idEmpresa,
            IdSucursal = idSucursal,
            IdCaja = idCaja,
            IdSesionCaja = idSesionCaja,
            IdUsuario = idUsuario,
            Entidad = entidad,
            IdEntidad = idEntidad,
            Accion = accion,
            DatosAnteriores = SerializarSnapshot(datosAnteriores),
            DatosNuevos = SerializarSnapshot(datosNuevos),
            Ip = ip,
            Fecha = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    private static string? SerializarSnapshot(object? datos)
    {
        if (datos is null) return null;
        if (datos is string texto) return texto;
        return JsonSerializer.Serialize(datos);
    }
}