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
public class ProductosController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<ProductosController> _logger;

    public ProductosController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<ProductosController> logger)
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
        var query = _db.Productos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(p =>
                p.Nombre.Contains(t) ||
                (p.Descripcion != null && p.Descripcion.Contains(t)) ||
                (p.Codigo != null && p.Codigo.Contains(t)) ||
                (p.CodigoBarras != null && p.CodigoBarras.Contains(t)) ||
                p.CategoriaProducto.Nombre.Contains(t));
        }

        var lista = await query
            .OrderBy(p => p.Nombre)
            .Select(p => new
            {
                p.IdProducto,
                p.IdCategoriaProducto,
                Categoria = p.CategoriaProducto.Nombre,
                p.Codigo,
                p.CodigoBarras,
                p.Nombre,
                p.Descripcion,
                p.Precio,
                p.Activo,
                p.FechaCreacion,
                CantidadImpuestos = p.ProductosImpuestos.Count(),
                CantidadRecetas = p.Recetas.Count()
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] ProductoForm modelo)
    {
        try
        {
            if (modelo is null)
            {
                return JsonError("Datos inválidos.");
            }

            var error = await ValidarFormularioAsync(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var nombre = modelo.Nombre.Trim();
            var codigo = string.IsNullOrWhiteSpace(modelo.Codigo) ? null : modelo.Codigo.Trim();
            var codigoBarras = string.IsNullOrWhiteSpace(modelo.CodigoBarras) ? null : modelo.CodigoBarras.Trim();

            if (await _db.Productos.AnyAsync(p => p.IdCategoriaProducto == modelo.IdCategoriaProducto && p.Nombre == nombre))
            {
                return JsonError("Ya existe un producto con ese nombre en la categoría seleccionada.");
            }

            if (codigo != null && await _db.Productos.AnyAsync(p => p.Codigo == codigo))
            {
                return JsonError("Ya existe un producto con ese código interno.");
            }

            if (codigoBarras != null && await _db.Productos.AnyAsync(p => p.CodigoBarras == codigoBarras))
            {
                return JsonError("Ya existe un producto con ese código de barras.");
            }

            var producto = new Producto
            {
                IdCategoriaProducto = modelo.IdCategoriaProducto,
                Codigo = codigo,
                CodigoBarras = codigoBarras,
                Nombre = nombre,
                Descripcion = Normalizar(modelo.Descripcion),
                Precio = Redondear(modelo.Precio),
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.Productos.Add(producto);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Producto", producto.IdProducto.ToString(), "CREAR_PRODUCTO", null, Snapshot(producto));

            return JsonOk("Producto creado correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe un producto con ese código o nombre.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear producto.");
            return JsonError("Ocurrió un error al guardar el producto.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] ProductoForm modelo)
    {
        try
        {
            if (modelo is null)
            {
                return JsonError("Datos inválidos.");
            }

            var error = await ValidarFormularioAsync(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var producto = await _db.Productos.FirstOrDefaultAsync(p => p.IdProducto == modelo.IdProducto);
            if (producto is null)
            {
                return JsonError("El producto no existe.");
            }

            var nombre = modelo.Nombre.Trim();
            var codigo = string.IsNullOrWhiteSpace(modelo.Codigo) ? null : modelo.Codigo.Trim();
            var codigoBarras = string.IsNullOrWhiteSpace(modelo.CodigoBarras) ? null : modelo.CodigoBarras.Trim();

            if (await _db.Productos.AnyAsync(p =>
                p.IdCategoriaProducto == modelo.IdCategoriaProducto &&
                p.Nombre == nombre &&
                p.IdProducto != producto.IdProducto))
            {
                return JsonError("Ya existe un producto con ese nombre en la categoría seleccionada.");
            }

            if (codigo != null && await _db.Productos.AnyAsync(p => p.Codigo == codigo && p.IdProducto != producto.IdProducto))
            {
                return JsonError("Ya existe un producto con ese código interno.");
            }

            if (codigoBarras != null && await _db.Productos.AnyAsync(p => p.CodigoBarras == codigoBarras && p.IdProducto != producto.IdProducto))
            {
                return JsonError("Ya existe un producto con ese código de barras.");
            }

            var anterior = Snapshot(producto);

            producto.IdCategoriaProducto = modelo.IdCategoriaProducto;
            producto.Codigo = codigo;
            producto.CodigoBarras = codigoBarras;
            producto.Nombre = nombre;
            producto.Descripcion = Normalizar(modelo.Descripcion);
            producto.Precio = Redondear(modelo.Precio);

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Producto", producto.IdProducto.ToString(), "ACTUALIZAR_PRODUCTO", anterior, Snapshot(producto));

            return JsonOk("Producto actualizado correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe un producto con ese código o nombre.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar producto {Id}.", modelo.IdProducto);
            return JsonError("Ocurrió un error al actualizar el producto.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_PRODUCTO", "Producto activado correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        return await CambiarEstatus(id, false, "INACTIVAR_PRODUCTO", "Producto inactivado correctamente.");
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var producto = await _db.Productos.FirstOrDefaultAsync(p => p.IdProducto == id);
            if (producto is null)
            {
                return JsonError("El producto no existe.");
            }

            if (producto.Activo == activo)
            {
                return JsonError(activo ? "El producto ya está activo." : "El producto ya está inactivo.");
            }

            var anterior = Snapshot(producto);

            producto.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Producto", producto.IdProducto.ToString(), accion, anterior, Snapshot(producto));

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de producto {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerImpuestos(int idProducto)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return JsonError("No se pudo identificar tu empresa.");
        }

        var producto = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.IdProducto == idProducto);
        if (producto is null)
        {
            return JsonError("El producto no existe.");
        }

        var asignados = await _db.ProductosImpuestos
            .AsNoTracking()
            .Where(pi => pi.IdProducto == idProducto && pi.Impuesto.IdEmpresa == idEmpresa && pi.Impuesto.Activo)
            .OrderBy(pi => pi.Impuesto.Nombre)
            .Select(pi => new
            {
                pi.IdImpuesto,
                pi.Impuesto.Nombre,
                pi.Impuesto.Tasa,
                pi.Impuesto.IncluidoEnPrecio
            })
            .ToListAsync();

        var disponibles = await _db.Impuestos
            .AsNoTracking()
            .Where(i => i.IdEmpresa == idEmpresa && i.Activo && !i.ProductosImpuestos.Any(pi => pi.IdProducto == idProducto))
            .OrderBy(i => i.Nombre)
            .Select(i => new
            {
                i.IdImpuesto,
                i.Nombre,
                i.Tasa,
                i.IncluidoEnPrecio
            })
            .ToListAsync();

        return Json(new { asignados, disponibles });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarImpuesto([FromBody] AsignarImpuestoViewModel modelo)
    {
        try
        {
            if (modelo is null)
            {
                return JsonError("Datos inválidos.");
            }

            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var producto = await _db.Productos.FirstOrDefaultAsync(p => p.IdProducto == modelo.IdProducto);
            if (producto is null)
            {
                return JsonError("El producto no existe.");
            }

            var impuesto = await _db.Impuestos.FirstOrDefaultAsync(i => i.IdImpuesto == modelo.IdImpuesto);
            if (impuesto is null)
            {
                return JsonError("El impuesto no existe.");
            }

            if (impuesto.IdEmpresa != idEmpresa)
            {
                return JsonError("El impuesto no pertenece a tu empresa.");
            }

            if (!impuesto.Activo)
            {
                return JsonError("El impuesto está inactivo.");
            }

            var yaAsignado = await _db.ProductosImpuestos.AnyAsync(pi => pi.IdProducto == modelo.IdProducto && pi.IdImpuesto == modelo.IdImpuesto);
            if (yaAsignado)
            {
                return JsonError("Este impuesto ya está asignado al producto.");
            }

            _db.ProductosImpuestos.Add(new ProductoImpuesto
            {
                IdProducto = modelo.IdProducto,
                IdImpuesto = modelo.IdImpuesto
            });

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "ProductoImpuesto",
                $"{modelo.IdProducto}:{modelo.IdImpuesto}",
                "ASIGNAR_IMPUESTO",
                null,
                new { IdProducto = modelo.IdProducto, IdImpuesto = modelo.IdImpuesto, Impuesto = impuesto.Nombre });

            return JsonOk("Impuesto asignado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al asignar impuesto {IdImpuesto} a producto {IdProducto}.", modelo.IdImpuesto, modelo.IdProducto);
            return JsonError("Ocurrió un error al asignar el impuesto.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarImpuesto([FromBody] QuitarImpuestoViewModel modelo)
    {
        try
        {
            if (modelo is null)
            {
                return JsonError("Datos inválidos.");
            }

            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var rel = await _db.ProductosImpuestos
                .Include(pi => pi.Impuesto)
                .FirstOrDefaultAsync(pi => pi.IdProducto == modelo.IdProducto && pi.IdImpuesto == modelo.IdImpuesto);

            if (rel is null)
            {
                return JsonError("La asignación de impuesto no existe.");
            }

            if (rel.Impuesto.IdEmpresa != idEmpresa)
            {
                return JsonError("El impuesto no pertenece a tu empresa.");
            }

            _db.ProductosImpuestos.Remove(rel);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "ProductoImpuesto",
                $"{modelo.IdProducto}:{modelo.IdImpuesto}",
                "QUITAR_IMPUESTO",
                new { IdProducto = modelo.IdProducto, IdImpuesto = modelo.IdImpuesto },
                null);

            return JsonOk("Impuesto quitado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al quitar impuesto {IdImpuesto} de producto {IdProducto}.", modelo.IdImpuesto, modelo.IdProducto);
            return JsonError("Ocurrió un error al quitar el impuesto.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(ProductoForm modelo)
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

        if (modelo.Codigo != null && modelo.Codigo.Trim().Length > 50)
        {
            return "El código interno no debe superar 50 caracteres.";
        }

        if (modelo.CodigoBarras != null && modelo.CodigoBarras.Trim().Length > 50)
        {
            return "El código de barras no debe superar 50 caracteres.";
        }

        if (modelo.Descripcion != null && modelo.Descripcion.Trim().Length > 500)
        {
            return "La descripción no debe superar 500 caracteres.";
        }

        if (modelo.Precio < 0)
        {
            return "El precio no puede ser negativo.";
        }

        if (modelo.IdCategoriaProducto <= 0)
        {
            return "La categoría es obligatoria.";
        }

        var categoria = await _db.CategoriasProducto.FirstOrDefaultAsync(c => c.IdCategoriaProducto == modelo.IdCategoriaProducto);
        if (categoria is null)
        {
            return "La categoría seleccionada no existe.";
        }

        if (!categoria.Activo)
        {
            return "La categoría seleccionada está inactiva.";
        }

        return null;
    }

    private static object Snapshot(Producto p) => new
    {
        p.IdCategoriaProducto,
        p.Codigo,
        p.CodigoBarras,
        p.Nombre,
        p.Descripcion,
        p.Precio,
        p.Activo
    };

    private int? ObtenerClaimInt(string tipo)
    {
        var valor = User.FindFirstValue(tipo);
        return int.TryParse(valor, out var id) ? id : null;
    }

    private static decimal Redondear(decimal valor)
    {
        return Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }

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