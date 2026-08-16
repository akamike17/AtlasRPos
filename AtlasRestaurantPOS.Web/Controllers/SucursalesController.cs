using AtlasRestaurantPOS.Web.Constants;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class SucursalesController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<SucursalesController> _logger;

    public SucursalesController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<SucursalesController> logger)
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
        var query = _db.Sucursales.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(s => s.Nombre.Contains(t) || s.Empresa.Nombre.Contains(t));
        }

        var lista = await query
            .OrderBy(s => s.Nombre)
            .Select(s => new
            {
                s.IdSucursal,
                s.IdEmpresa,
                Empresa = s.Empresa.Nombre,
                s.Nombre,
                s.Direccion,
                s.Telefono,
                s.Activo,
                s.FechaCreacion
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] SucursalForm modelo)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return JsonError(ErrorModelState());
            }

            var error = await ValidarFormularioAsync(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.Sucursales.AnyAsync(s => s.IdEmpresa == modelo.IdEmpresa && s.Nombre == nombre))
            {
                return JsonError("Ya existe una sucursal con ese nombre en la empresa seleccionada.");
            }

            var sucursal = new Sucursal
            {
                IdEmpresa = modelo.IdEmpresa,
                Nombre = nombre,
                Direccion = Normalizar(modelo.Direccion),
                Telefono = Normalizar(modelo.Telefono),
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.Sucursales.Add(sucursal);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Sucursal", sucursal.IdSucursal.ToString(), "CREAR_SUCURSAL", null, Snapshot(sucursal));

            return JsonOk("Sucursal creada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear sucursal.");
            return JsonError("Ocurrió un error al guardar la sucursal.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] SucursalForm modelo)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return JsonError(ErrorModelState());
            }

            var error = await ValidarFormularioAsync(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var sucursal = await _db.Sucursales.FirstOrDefaultAsync(s => s.IdSucursal == modelo.IdSucursal);
            if (sucursal is null)
            {
                return JsonError("La sucursal no existe.");
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.Sucursales.AnyAsync(s => s.IdEmpresa == modelo.IdEmpresa && s.Nombre == nombre && s.IdSucursal != sucursal.IdSucursal))
            {
                return JsonError("Ya existe una sucursal con ese nombre en la empresa seleccionada.");
            }

            var anterior = Snapshot(sucursal);

            sucursal.IdEmpresa = modelo.IdEmpresa;
            sucursal.Nombre = nombre;
            sucursal.Direccion = Normalizar(modelo.Direccion);
            sucursal.Telefono = Normalizar(modelo.Telefono);

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Sucursal", sucursal.IdSucursal.ToString(), "ACTUALIZAR_SUCURSAL", anterior, Snapshot(sucursal));

            return JsonOk("Sucursal actualizada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar sucursal {Id}.", modelo.IdSucursal);
            return JsonError("Ocurrió un error al actualizar la sucursal.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_SUCURSAL", "Sucursal activada correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        try
        {
            var sucursal = await _db.Sucursales.FirstOrDefaultAsync(s => s.IdSucursal == id);
            if (sucursal is null)
            {
                return JsonError("La sucursal no existe.");
            }

            if (!sucursal.Activo)
            {
                return JsonError("La sucursal ya está inactiva.");
            }

            var tieneSesionAbierta = await _db.SesionesCaja
                .AnyAsync(sc => sc.Estado == EstadosSesionCaja.ABIERTA && sc.Caja.IdSucursal == sucursal.IdSucursal);

            if (tieneSesionAbierta)
            {
                return JsonError("No se puede inactivar la sucursal porque tiene una caja con sesión abierta.");
            }

            sucursal.Activo = false;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Sucursal", sucursal.IdSucursal.ToString(), "INACTIVAR_SUCURSAL", null, new { sucursal.Activo });

            return JsonOk("Sucursal inactivada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inactivar sucursal {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var sucursal = await _db.Sucursales.FirstOrDefaultAsync(s => s.IdSucursal == id);
            if (sucursal is null)
            {
                return JsonError("La sucursal no existe.");
            }

            if (sucursal.Activo == activo)
            {
                return JsonError(activo ? "La sucursal ya está activa." : "La sucursal ya está inactiva.");
            }

            sucursal.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Sucursal", sucursal.IdSucursal.ToString(), accion, null, new { sucursal.Activo });

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de sucursal {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(SucursalForm modelo)
    {
        var nombre = (modelo.Nombre ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre es obligatorio.";
        }

        if (modelo.Nombre != nombre)
        {
            return "El nombre no debe contener espacios al inicio o al final.";
        }

        if (modelo.IdEmpresa <= 0)
        {
            return "La empresa es obligatoria.";
        }

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.IdEmpresa == modelo.IdEmpresa);
        if (empresa is null)
        {
            return "La empresa seleccionada no existe.";
        }

        if (!empresa.Activo)
        {
            return "La empresa seleccionada está inactiva.";
        }

        return null;
    }

    private static object Snapshot(Sucursal s) => new
    {
        IdEmpresa = s.IdEmpresa,
        s.Nombre,
        s.Direccion,
        s.Telefono,
        s.Activo
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

    private string ErrorModelState()
    {
        return ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Datos inválidos.";
    }
}