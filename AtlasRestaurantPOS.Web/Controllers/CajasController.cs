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
public class CajasController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<CajasController> _logger;

    public CajasController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<CajasController> logger)
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
        var query = _db.Cajas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(c =>
                c.Codigo.Contains(t) ||
                c.Nombre.Contains(t) ||
                c.Sucursal.Nombre.Contains(t) ||
                c.Sucursal.Empresa.Nombre.Contains(t));
        }

        var lista = await query
            .OrderBy(c => c.Nombre)
            .Select(c => new
            {
                c.IdCaja,
                IdSucursal = c.IdSucursal,
                Sucursal = c.Sucursal.Nombre,
                IdEmpresa = c.Sucursal.IdEmpresa,
                Empresa = c.Sucursal.Empresa.Nombre,
                c.Codigo,
                c.Nombre,
                c.Descripcion,
                c.Activo,
                c.FechaCreacion
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] CajaForm modelo)
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

            var codigo = modelo.Codigo.Trim();
            var nombre = modelo.Nombre.Trim();

            if (await _db.Cajas.AnyAsync(c => c.IdSucursal == modelo.IdSucursal && c.Codigo == codigo))
            {
                return JsonError("Ya existe una caja con ese código en la sucursal seleccionada.");
            }

            var caja = new Caja
            {
                IdSucursal = modelo.IdSucursal,
                Codigo = codigo,
                Nombre = nombre,
                Descripcion = Normalizar(modelo.Descripcion),
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.Cajas.Add(caja);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Caja", caja.IdCaja.ToString(), "CREAR_CAJA", null, Snapshot(caja));

            return JsonOk("Caja creada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear caja.");
            return JsonError("Ocurrió un error al guardar la caja.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] CajaForm modelo)
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

            var caja = await _db.Cajas.FirstOrDefaultAsync(c => c.IdCaja == modelo.IdCaja);
            if (caja is null)
            {
                return JsonError("La caja no existe.");
            }

            var codigo = modelo.Codigo.Trim();
            var nombre = modelo.Nombre.Trim();

            if (await _db.Cajas.AnyAsync(c => c.IdSucursal == modelo.IdSucursal && c.Codigo == codigo && c.IdCaja != caja.IdCaja))
            {
                return JsonError("Ya existe una caja con ese código en la sucursal seleccionada.");
            }

            var anterior = Snapshot(caja);

            caja.IdSucursal = modelo.IdSucursal;
            caja.Codigo = codigo;
            caja.Nombre = nombre;
            caja.Descripcion = Normalizar(modelo.Descripcion);

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Caja", caja.IdCaja.ToString(), "ACTUALIZAR_CAJA", anterior, Snapshot(caja));

            return JsonOk("Caja actualizada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar caja {Id}.", modelo.IdCaja);
            return JsonError("Ocurrió un error al actualizar la caja.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_CAJA", "Caja activada correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        try
        {
            var caja = await _db.Cajas.FirstOrDefaultAsync(c => c.IdCaja == id);
            if (caja is null)
            {
                return JsonError("La caja no existe.");
            }

            if (!caja.Activo)
            {
                return JsonError("La caja ya está inactiva.");
            }

            var tieneSesionAbierta = await _db.SesionesCaja
                .AnyAsync(sc => sc.IdCaja == caja.IdCaja && sc.Estado == EstadosSesionCaja.ABIERTA);

            if (tieneSesionAbierta)
            {
                return JsonError("No se puede inactivar la caja porque tiene una sesión abierta.");
            }

            caja.Activo = false;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Caja", caja.IdCaja.ToString(), "INACTIVAR_CAJA", null, new { caja.Activo });

            return JsonOk("Caja inactivada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inactivar caja {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var caja = await _db.Cajas.FirstOrDefaultAsync(c => c.IdCaja == id);
            if (caja is null)
            {
                return JsonError("La caja no existe.");
            }

            if (caja.Activo == activo)
            {
                return JsonError(activo ? "La caja ya está activa." : "La caja ya está inactiva.");
            }

            caja.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Caja", caja.IdCaja.ToString(), accion, null, new { caja.Activo });

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de caja {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(CajaForm modelo)
    {
        var codigo = (modelo.Codigo ?? string.Empty).Trim();
        var nombre = (modelo.Nombre ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return "El código es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre es obligatorio.";
        }

        if (modelo.Codigo != codigo || modelo.Nombre != nombre)
        {
            return "Los campos no deben contener espacios al inicio o al final.";
        }

        if (modelo.IdSucursal <= 0)
        {
            return "La sucursal es obligatoria.";
        }

        var sucursal = await _db.Sucursales
            .Include(s => s.Empresa)
            .FirstOrDefaultAsync(s => s.IdSucursal == modelo.IdSucursal);

        if (sucursal is null)
        {
            return "La sucursal seleccionada no existe.";
        }

        if (!sucursal.Activo)
        {
            return "La sucursal seleccionada está inactiva.";
        }

        if (!sucursal.Empresa.Activo)
        {
            return "La empresa de la sucursal seleccionada está inactiva.";
        }

        return null;
    }

    private static object Snapshot(Caja c) => new
    {
        IdSucursal = c.IdSucursal,
        c.Codigo,
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

    private string ErrorModelState()
    {
        return ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Datos inválidos.";
    }
}