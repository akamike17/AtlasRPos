using System.Security.Claims;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class RecetasController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<RecetasController> _logger;

    public RecetasController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<RecetasController> logger)
    {
        _db = db;
        _auditoria = auditoria;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Buscar(int? idProducto = null, string? termino = null)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return JsonError("No se pudo identificar tu empresa.");
        }

        try
        {
            IQueryable<RecetaProducto> query = _db.RecetasProducto
                .AsNoTracking()
                .Include(r => r.Insumo)
                    .ThenInclude(i => i.UnidadMedida)
                .Include(r => r.Producto)
                .Where(r => r.Insumo.IdEmpresa == idEmpresa.Value && r.Producto.IdEmpresa == idEmpresa.Value);

            if (idProducto is not null && idProducto > 0)
            {
                query = query.Where(r => r.IdProducto == idProducto);
            }

            if (!string.IsNullOrWhiteSpace(termino))
            {
                var t = termino.Trim();
                query = query.Where(r =>
                    r.Insumo.Nombre.Contains(t) ||
                    r.Producto.Nombre.Contains(t) ||
                    (r.Insumo.Codigo != null && r.Insumo.Codigo.Contains(t)) ||
                    (r.Producto.Codigo != null && r.Producto.Codigo.Contains(t)));
            }

            var lista = await query
                .OrderBy(r => r.Producto.Nombre)
                .ThenBy(r => r.Insumo.Nombre)
                .Select(r => new
                {
                    r.IdRecetaProducto,
                    r.IdProducto,
                    Producto = r.Producto.Nombre,
                    ProductoCodigo = r.Producto.Codigo,
                    r.IdInsumo,
                    Insumo = r.Insumo.Nombre,
                    InsumoCodigo = r.Insumo.Codigo,
                    Cantidad = r.Cantidad,
                    Unidad = r.Insumo.UnidadMedida != null ? r.Insumo.UnidadMedida.Nombre : string.Empty,
                    UnidadCodigo = r.Insumo.UnidadMedida != null ? r.Insumo.UnidadMedida.Codigo : string.Empty
                })
                .ToListAsync();

            return Json(lista);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Buscar recetas");
            return JsonError("Error al consultar recetas.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(int id)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return JsonError("No se pudo identificar tu empresa.");
        }

        var receta = await _db.RecetasProducto
            .AsNoTracking()
            .Include(r => r.Insumo)
                .ThenInclude(i => i.UnidadMedida)
            .Include(r => r.Producto)
            .FirstOrDefaultAsync(r => r.IdRecetaProducto == id && r.Insumo.IdEmpresa == idEmpresa.Value && r.Producto.IdEmpresa == idEmpresa.Value);

        if (receta is null)
        {
            return JsonError("La receta no existe.");
        }

        return Json(new
        {
            ok = true,
            receta.IdRecetaProducto,
            receta.IdProducto,
            Producto = receta.Producto.Nombre,
            receta.IdInsumo,
            Insumo = receta.Insumo.Nombre,
            InsumoCodigo = receta.Insumo.Codigo,
            receta.Cantidad,
            Unidad = receta.Insumo.UnidadMedida != null ? receta.Insumo.UnidadMedida.Nombre : string.Empty,
            UnidadCodigo = receta.Insumo.UnidadMedida != null ? receta.Insumo.UnidadMedida.Codigo : string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] RecetaForm modelo)
    {
        try
        {
            if (modelo is null) return JsonError("Datos inválidos.");
            if (!ModelState.IsValid) return JsonError(ErrorModelState());

            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var producto = await _db.Productos.FirstOrDefaultAsync(p => p.IdProducto == modelo.IdProducto && p.IdEmpresa == idEmpresa.Value && p.Activo);
            if (producto is null) return JsonError("El producto no existe o está inactivo.");

            var insumo = await _db.Insumos.FirstOrDefaultAsync(i => i.IdInsumo == modelo.IdInsumo && i.Activo && i.IdEmpresa == idEmpresa.Value);
            if (insumo is null) return JsonError("El insumo no existe o está inactivo.");

            if (modelo.Cantidad <= 0) return JsonError("La cantidad debe ser mayor a cero.");

            var existe = await _db.RecetasProducto.AnyAsync(r => r.IdProducto == modelo.IdProducto && r.IdInsumo == modelo.IdInsumo && r.Producto.IdEmpresa == idEmpresa.Value && r.Insumo.IdEmpresa == idEmpresa.Value);
            if (existe) return JsonError("Ya existe este insumo configurado en la receta del producto.");

            var receta = new RecetaProducto
            {
                IdProducto = modelo.IdProducto,
                IdInsumo = modelo.IdInsumo,
                Cantidad = modelo.Cantidad
            };
            _db.RecetasProducto.Add(receta);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "RecetaProducto",
                receta.IdRecetaProducto.ToString(),
                "RECETA_AGREGAR_INSUMO",
                null,
                new { receta.IdRecetaProducto, receta.IdProducto, Producto = producto.Nombre, receta.IdInsumo, Insumo = insumo.Nombre, receta.Cantidad });

            return JsonOk("Insumo agregado a la receta correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe este insumo en la receta del producto.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear receta.");
            return JsonError("Ocurrió un error al guardar la receta.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] RecetaForm modelo)
    {
        try
        {
            if (modelo is null) return JsonError("Datos inválidos.");
            if (!ModelState.IsValid) return JsonError(ErrorModelState());

            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var receta = await _db.RecetasProducto
                .Include(r => r.Insumo)
                .Include(r => r.Producto)
                .FirstOrDefaultAsync(r => r.IdRecetaProducto == modelo.IdRecetaProducto && r.Insumo.IdEmpresa == idEmpresa.Value && r.Producto.IdEmpresa == idEmpresa.Value);

            if (receta is null) return JsonError("La receta no existe.");

            if (modelo.Cantidad <= 0) return JsonError("La cantidad debe ser mayor a cero.");

            var anterior = new { receta.IdRecetaProducto, receta.IdProducto, receta.IdInsumo, receta.Cantidad };
            receta.Cantidad = modelo.Cantidad;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "RecetaProducto",
                receta.IdRecetaProducto.ToString(),
                "RECETA_MODIFICAR_CANTIDAD",
                anterior,
                new { receta.IdRecetaProducto, receta.IdProducto, receta.IdInsumo, receta.Cantidad });

            return JsonOk("Cantidad de la receta actualizada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar receta {Id}.", modelo.IdRecetaProducto);
            return JsonError("Ocurrió un error al actualizar la receta.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Borrar(int id)
    {
        try
        {
            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var receta = await _db.RecetasProducto
                .Include(r => r.Insumo)
                .Include(r => r.Producto)
                .FirstOrDefaultAsync(r => r.IdRecetaProducto == id && r.Insumo.IdEmpresa == idEmpresa.Value && r.Producto.IdEmpresa == idEmpresa.Value);

            if (receta is null) return JsonError("La receta no existe.");

            var datos = new { receta.IdRecetaProducto, receta.IdProducto, Producto = receta.Producto.Nombre, receta.IdInsumo, Insumo = receta.Insumo.Nombre, receta.Cantidad };

            _db.RecetasProducto.Remove(receta);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("RecetaProducto", id.ToString(), "RECETA_QUITAR_INSUMO", datos, null);

            return JsonOk("Insumo removido de la receta correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar receta {Id}.", id);
            return JsonError("Ocurrió un error al eliminar la receta.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCatalogos()
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return JsonError("No se pudo identificar tu empresa.");
        }

        var productos = await _db.Productos
            .AsNoTracking()
            .Where(p => p.IdEmpresa == idEmpresa.Value && p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new { p.IdProducto, p.Nombre, Categoria = p.CategoriaProducto.Nombre, p.Codigo })
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

        return Json(new { ok = true, productos, insumos });
    }

    private int? ObtenerClaimInt(string tipo)
    {
        var valor = User.FindFirstValue(tipo);
        return int.TryParse(valor, out var id) ? id : null;
    }

    private JsonResult JsonOk(string mensaje) => Json(new { ok = true, mensaje });
    private JsonResult JsonError(string mensaje) => Json(new { ok = false, mensaje });
    private string ErrorModelState() => ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Datos inválidos.";
}
