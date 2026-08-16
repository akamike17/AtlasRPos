using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class CategoriasController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<CategoriasController> _logger;

    public CategoriasController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<CategoriasController> logger)
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
    public async Task<IActionResult> Buscar(string? termino = null)
    {
        var query = _db.CategoriasProducto.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(c =>
                c.Nombre.Contains(t) ||
                (c.Descripcion != null && c.Descripcion.Contains(t)));
        }

        var lista = await query
            .OrderBy(c => c.Nombre)
            .Select(c => new
            {
                c.IdCategoriaProducto,
                c.Nombre,
                c.Descripcion,
                c.Activo,
                CantidadProductos = c.Productos.Count()
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] CategoriaForm modelo)
    {
        try
        {
            if (modelo is null)
            {
                return JsonError("Datos inválidos.");
            }

            var error = ValidarFormulario(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.CategoriasProducto.AnyAsync(c => c.Nombre == nombre))
            {
                return JsonError("Ya existe una categoría con ese nombre.");
            }

            var categoria = new CategoriaProducto
            {
                Nombre = nombre,
                Descripcion = Normalizar(modelo.Descripcion),
                Activo = true
            };

            _db.CategoriasProducto.Add(categoria);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("CategoriaProducto", categoria.IdCategoriaProducto.ToString(), "CREAR_CATEGORIA", null, Snapshot(categoria));

            return JsonOk("Categoría creada correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe una categoría con ese nombre.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear categoría.");
            return JsonError("Ocurrió un error al guardar la categoría.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] CategoriaForm modelo)
    {
        try
        {
            if (modelo is null)
            {
                return JsonError("Datos inválidos.");
            }

            var error = ValidarFormulario(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var categoria = await _db.CategoriasProducto.FirstOrDefaultAsync(c => c.IdCategoriaProducto == modelo.IdCategoriaProducto);
            if (categoria is null)
            {
                return JsonError("La categoría no existe.");
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.CategoriasProducto.AnyAsync(c => c.Nombre == nombre && c.IdCategoriaProducto != categoria.IdCategoriaProducto))
            {
                return JsonError("Ya existe una categoría con ese nombre.");
            }

            var anterior = Snapshot(categoria);

            categoria.Nombre = nombre;
            categoria.Descripcion = Normalizar(modelo.Descripcion);

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("CategoriaProducto", categoria.IdCategoriaProducto.ToString(), "ACTUALIZAR_CATEGORIA", anterior, Snapshot(categoria));

            return JsonOk("Categoría actualizada correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe una categoría con ese nombre.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar categoría {Id}.", modelo.IdCategoriaProducto);
            return JsonError("Ocurrió un error al actualizar la categoría.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_CATEGORIA", "Categoría activada correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        try
        {
            var categoria = await _db.CategoriasProducto.FirstOrDefaultAsync(c => c.IdCategoriaProducto == id);
            if (categoria is null)
            {
                return JsonError("La categoría no existe.");
            }

            if (!categoria.Activo)
            {
                return JsonError("La categoría ya está inactiva.");
            }

            var tieneProductosActivos = await _db.Productos
                .AnyAsync(p => p.IdCategoriaProducto == id && p.Activo);

            if (tieneProductosActivos)
            {
                return JsonError("La categoría tiene productos activos. Inactívelos primero.");
            }

            var anterior = Snapshot(categoria);

            categoria.Activo = false;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("CategoriaProducto", categoria.IdCategoriaProducto.ToString(), "INACTIVAR_CATEGORIA", anterior, Snapshot(categoria));

            return JsonOk("Categoría inactivada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inactivar categoría {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var categoria = await _db.CategoriasProducto.FirstOrDefaultAsync(c => c.IdCategoriaProducto == id);
            if (categoria is null)
            {
                return JsonError("La categoría no existe.");
            }

            if (categoria.Activo == activo)
            {
                return JsonError(activo ? "La categoría ya está activa." : "La categoría ya está inactiva.");
            }

            var anterior = Snapshot(categoria);

            categoria.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("CategoriaProducto", categoria.IdCategoriaProducto.ToString(), accion, anterior, Snapshot(categoria));

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de categoría {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private string? ValidarFormulario(CategoriaForm modelo)
    {
        var nombre = (modelo.Nombre ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre es obligatorio.";
        }

        if (nombre.Length > 150)
        {
            return "El nombre no debe superar 150 caracteres.";
        }

        if (modelo.Nombre != nombre)
        {
            return "El nombre no debe contener espacios al inicio o al final.";
        }

        if (modelo.Descripcion != null && modelo.Descripcion.Trim().Length > 500)
        {
            return "La descripción no debe superar 500 caracteres.";
        }

        return null;
    }

    private static object Snapshot(CategoriaProducto c) => new
    {
        c.Nombre,
        c.Descripcion,
        c.Activo
    };

    private static string? Normalizar(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private JsonResult JsonOk(string mensaje)
    {
        return Json(new { ok = true, mensaje });
    }

    private JsonResult JsonError(string mensaje)
    {
        return Json(new { ok = false, mensaje });
    }
}