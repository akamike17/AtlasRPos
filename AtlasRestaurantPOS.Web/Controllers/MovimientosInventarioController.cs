using System.Data;
using System.Security.Claims;
using AtlasRestaurantPOS.Web.Constants;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using AtlasRestaurantPOS.Web.Services.Identificacion;
using AtlasRestaurantPOS.Web.Services.Inventario;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class MovimientosInventarioController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<MovimientosInventarioController> _logger;
    private readonly IInventarioService _inventario;
    private readonly IIdentificacionService _identificacion;

    public MovimientosInventarioController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<MovimientosInventarioController> logger, IInventarioService inventario, IIdentificacionService identificacion)
    {
        _db = db;
        _auditoria = auditoria;
        _logger = logger;
        _inventario = inventario;
        _identificacion = identificacion;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Buscar(
        int? idSucursal = null,
        int? idInsumo = null,
        string? tipo = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        string? termino = null)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return JsonError("No se pudo identificar tu empresa.");
        }

        try
        {
            IQueryable<MovimientoInventario> query = _db.MovimientosInventario
                .AsNoTracking()
                .Include(m => m.Sucursal)
                .Include(m => m.Insumo)
                    .ThenInclude(i => i.UnidadMedida)
                .Include(m => m.Usuario)
                .Where(m => m.Insumo.IdEmpresa == idEmpresa.Value && m.Sucursal.IdEmpresa == idEmpresa.Value);

            if (idSucursal is not null && idSucursal > 0)
            {
                query = query.Where(m => m.IdSucursal == idSucursal);
            }

            if (idInsumo is not null && idInsumo > 0)
            {
                query = query.Where(m => m.IdInsumo == idInsumo);
            }

            if (!string.IsNullOrWhiteSpace(tipo))
            {
                var t = tipo.Trim().ToUpperInvariant();
                query = query.Where(m => m.Tipo == t);
            }

            if (fechaInicio.HasValue)
            {
                var inicio = fechaInicio.Value.Date;
                query = query.Where(m => m.FechaMovimiento >= inicio);
            }

            if (fechaFin.HasValue)
            {
                var fin = fechaFin.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(m => m.FechaMovimiento <= fin);
            }

            if (!string.IsNullOrWhiteSpace(termino))
            {
                var term = termino.Trim();
                query = query.Where(m =>
                    m.Insumo.Nombre.Contains(term) ||
                    (m.Insumo.Codigo != null && m.Insumo.Codigo.Contains(term)) ||
                    (m.Insumo.CodigoBarras != null && m.Insumo.CodigoBarras.Contains(term)) ||
                    m.Concepto.Contains(term) ||
                    m.Usuario.Nombre.Contains(term));
            }

            var lista = await query
                .OrderByDescending(m => m.FechaMovimiento)
                .ThenByDescending(m => m.IdMovimientoInventario)
                .Take(500)
                .Select(m => new
                {
                    m.IdMovimientoInventario,
                    m.FechaMovimiento,
                    m.IdSucursal,
                    Sucursal = m.Sucursal.Nombre,
                    m.IdInsumo,
                    Insumo = m.Insumo.Nombre,
                    InsumoCodigo = m.Insumo.Codigo,
                    Unidad = m.Insumo.UnidadMedida != null ? m.Insumo.UnidadMedida.Nombre : string.Empty,
                    UnidadCodigo = m.Insumo.UnidadMedida != null ? m.Insumo.UnidadMedida.Codigo : string.Empty,
                    m.Tipo,
                    m.Cantidad,
                    m.ExistenciaAnterior,
                    m.ExistenciaNueva,
                    m.CostoUnitario,
                    m.Concepto,
                    Usuario = m.Usuario.Nombre,
                    m.IdComanda
                })
                .ToListAsync();

            return Json(lista);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Buscar movimientos de inventario");
            return JsonError("Error al consultar movimientos de inventario.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] MovimientoForm modelo, CancellationToken cancellationToken)
    {
        if (modelo is null) return JsonError("Datos inválidos.");
        if (!ModelState.IsValid) return JsonError(ErrorModelState());

        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        var idSucursal = ObtenerClaimInt("IdSucursal");
        var idUsuario = ObtenerIdUsuario();
        if (idEmpresa is null || idSucursal is null || idUsuario is null) return JsonError("No se pudieron identificar tus datos operativos.");

        var tipo = (modelo.Tipo ?? string.Empty).Trim().ToUpperInvariant();
        var permitidos = new[] { TiposMovimientoInventario.ENTRADA, TiposMovimientoInventario.AJUSTE_POSITIVO, TiposMovimientoInventario.AJUSTE_NEGATIVO, TiposMovimientoInventario.MERMA, TiposMovimientoInventario.DEVOLUCION_INVENTARIO };
        if (!permitidos.Contains(tipo)) return JsonError("Tipo de movimiento no válido.");

        try
        {
            var resultado = await _inventario.RegistrarMovimientoAsync(idEmpresa.Value, idSucursal.Value, modelo.IdInsumo, idUsuario.Value, tipo, modelo.Cantidad, modelo.Concepto ?? string.Empty, modelo.CostoUnitario, cancellationToken: cancellationToken);
            return Json(new { ok = resultado.Ok, mensaje = resultado.Mensaje, idMovimiento = resultado.IdMovimiento });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear movimiento de inventario.");
            return JsonError("Ocurrió un error al registrar el movimiento.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> IdentificarInsumo(string codigo, CancellationToken cancellationToken)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null) return JsonError("No se pudo identificar tu empresa.");
        if (string.IsNullOrWhiteSpace(codigo)) return JsonError("El código es obligatorio.");

        var insumo = await _identificacion.BuscarInsumoAsync(idEmpresa.Value, codigo, cancellationToken);
        return insumo is null ? JsonError("No existe un insumo activo con ese código.") : Json(new { ok = true, insumo });
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCatalogos()
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return JsonError("No se pudo identificar tu empresa.");
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
            .Select(i => new
            {
                i.IdInsumo,
                i.Nombre,
                i.Codigo,
                Unidad = i.UnidadMedida != null ? i.UnidadMedida.Nombre : string.Empty,
                UnidadCodigo = i.UnidadMedida != null ? i.UnidadMedida.Codigo : string.Empty
            })
            .ToListAsync();

        return Json(new { ok = true, sucursales, insumos });
    }

    private int? ObtenerClaimInt(string tipo)
    {
        var valor = User.FindFirstValue(tipo);
        return int.TryParse(valor, out var id) ? id : null;
    }

    private int? ObtenerIdUsuario()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(v, out var id) ? id : null;
    }

    private JsonResult JsonOk(string mensaje) => Json(new { ok = true, mensaje });
    private JsonResult JsonError(string mensaje) => Json(new { ok = false, mensaje });
    private string ErrorModelState() => ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Datos inválidos.";
}
