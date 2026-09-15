using System.Security.Claims;
using AtlasRestaurantPOS.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ExistenciasController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly ILogger<ExistenciasController> _logger;

    public ExistenciasController(AtlasRestaurantDbContext db, ILogger<ExistenciasController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Buscar(int? idSucursal = null, int? idInsumo = null, string? termino = null, bool soloBajoStock = false)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return Json(new { ok = false, mensaje = "No se pudo identificar tu empresa." });
        }

        try
        {
            IQueryable<ExistenciaInsumo> query = _db.ExistenciasInsumo
                .AsNoTracking()
                .Include(e => e.Sucursal)
                .Include(e => e.Insumo)
                    .ThenInclude(i => i.UnidadMedida)
                .Where(e => e.Insumo.IdEmpresa == idEmpresa.Value && e.Sucursal.IdEmpresa == idEmpresa.Value);

            if (idSucursal is not null && idSucursal > 0)
                query = query.Where(e => e.IdSucursal == idSucursal);

            if (idInsumo is not null && idInsumo > 0)
                query = query.Where(e => e.IdInsumo == idInsumo);

            if (!string.IsNullOrWhiteSpace(termino))
            {
                var t = termino.Trim();
                query = query.Where(e =>
                    e.Insumo.Nombre.Contains(t) ||
                    (e.Insumo.Codigo != null && e.Insumo.Codigo.Contains(t)) ||
                    (e.Insumo.CodigoBarras != null && e.Insumo.CodigoBarras.Contains(t)));
            }

            if (soloBajoStock)
            {
                query = query.Where(e => e.CantidadActual <= e.Insumo.StockMinimo);
            }

            var lista = await query
                .OrderBy(e => e.Sucursal.Nombre)
                .ThenBy(e => e.Insumo.Nombre)
                .Select(e => new
                {
                    e.IdSucursal,
                    Sucursal = e.Sucursal.Nombre,
                    e.IdInsumo,
                    Insumo = e.Insumo.Nombre,
                    Codigo = e.Insumo.Codigo,
                    CodigoBarras = e.Insumo.CodigoBarras,
                    e.CantidadActual,
                    StockMinimo = e.Insumo.StockMinimo,
                    Unidad = e.Insumo.UnidadMedida != null ? e.Insumo.UnidadMedida.Nombre : string.Empty,
                    UnidadCodigo = e.Insumo.UnidadMedida != null ? e.Insumo.UnidadMedida.Codigo : string.Empty,
                    BajoStock = e.CantidadActual <= e.Insumo.StockMinimo,
                    FechaModificacion = e.FechaModificacion
                })
                .ToListAsync();

            return Json(lista);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Buscar existencias");
            return Json(new { ok = false, mensaje = "Error al consultar existencias." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(int idSucursal, int idInsumo)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return Json(new { ok = false, mensaje = "No se pudo identificar tu empresa." });
        }

        var e = await _db.ExistenciasInsumo
            .AsNoTracking()
            .Include(x => x.Sucursal)
            .Include(x => x.Insumo)
                .ThenInclude(i => i.UnidadMedida)
            .FirstOrDefaultAsync(x => x.IdSucursal == idSucursal && x.IdInsumo == idInsumo && x.Insumo.IdEmpresa == idEmpresa.Value && x.Sucursal.IdEmpresa == idEmpresa.Value);

        if (e is null)
        {
            return Json(new { ok = false, mensaje = "No existe la existencia." });
        }

        return Json(new
        {
            ok = true,
            e.IdSucursal,
            Sucursal = e.Sucursal.Nombre,
            e.IdInsumo,
            Insumo = e.Insumo.Nombre,
            Codigo = e.Insumo.Codigo,
            CodigoBarras = e.Insumo.CodigoBarras,
            e.CantidadActual,
            StockMinimo = e.Insumo.StockMinimo,
            Unidad = e.Insumo.UnidadMedida != null ? e.Insumo.UnidadMedida.Nombre : string.Empty,
            UnidadCodigo = e.Insumo.UnidadMedida != null ? e.Insumo.UnidadMedida.Codigo : string.Empty,
            BajoStock = e.CantidadActual <= e.Insumo.StockMinimo,
            FechaModificacion = e.FechaModificacion
        });
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCatalogos()
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return Json(new { ok = false, mensaje = "No se pudo identificar tu empresa." });
        }

        var sucursales = await _db.Sucursales
            .AsNoTracking()
            .Where(s => s.IdEmpresa == idEmpresa.Value && s.Activo)
            .OrderBy(s => s.Nombre)
            .Select(s => new { s.IdSucursal, s.Nombre })
            .ToListAsync();

        var insumos = await _db.Insumos
            .AsNoTracking()
            .Where(i => i.IdEmpresa == idEmpresa.Value && i.Activo)
            .OrderBy(i => i.Nombre)
            .Select(i => new { i.IdInsumo, i.Nombre, i.Codigo, i.CodigoBarras })
            .ToListAsync();

        return Json(new { ok = true, sucursales, insumos });
    }

    private int? ObtenerClaimInt(string tipo)
    {
        var valor = User.FindFirstValue(tipo);
        return int.TryParse(valor, out var id) ? id : null;
    }
}
