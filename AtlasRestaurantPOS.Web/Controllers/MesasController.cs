using System.Security.Claims;
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
public class MesasController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<MesasController> _logger;

    public MesasController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<MesasController> logger)
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
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null) return JsonError("No se pudo identificar tu empresa.");
        var query = _db.Mesas.AsNoTracking().Where(m => m.Sucursal.IdEmpresa == idEmpresa.Value);

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(m =>
                m.Nombre.Contains(t) ||
                m.Sucursal.Nombre.Contains(t) ||
                m.Sucursal.Empresa.Nombre.Contains(t));
        }

        var lista = await query
            .OrderBy(m => m.Nombre)
            .Select(m => new
            {
                m.IdMesa,
                m.IdSucursal,
                Sucursal = m.Sucursal.Nombre,
                IdEmpresa = m.Sucursal.IdEmpresa,
                Empresa = m.Sucursal.Empresa.Nombre,
                m.Nombre,
                m.Capacidad,
                m.Estado,
                m.Activo
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(int id)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null) return JsonError("No se pudo identificar tu empresa.");
        var mesa = await _db.Mesas
            .AsNoTracking()
            .Where(m => m.IdMesa == id && m.Sucursal.IdEmpresa == idEmpresa.Value)
            .Select(m => new
            {
                m.IdMesa,
                m.IdSucursal,
                Sucursal = m.Sucursal.Nombre,
                IdEmpresa = m.Sucursal.IdEmpresa,
                Empresa = m.Sucursal.Empresa.Nombre,
                m.Nombre,
                m.Capacidad,
                m.Estado,
                m.Activo
            })
            .FirstOrDefaultAsync();

        if (mesa is null)
        {
            return JsonError("La mesa no existe.");
        }

        return Json(mesa);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] MesaForm modelo)
    {
        try
        {
            if (modelo is null)
            {
                return JsonError("Datos inválidos.");
            }

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

            if (await _db.Mesas.AnyAsync(m => m.IdSucursal == modelo.IdSucursal && m.Nombre == nombre))
            {
                return JsonError("Ya existe una mesa con ese nombre en la sucursal seleccionada.");
            }

            var mesa = new Mesa
            {
                IdSucursal = modelo.IdSucursal,
                Nombre = nombre,
                Capacidad = modelo.Capacidad,
                Estado = EstadosMesa.DISPONIBLE,
                Activo = true
            };

            _db.Mesas.Add(mesa);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Mesa", mesa.IdMesa.ToString(), "CREAR_MESA", null, Snapshot(mesa));

            return JsonOk("Mesa creada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear mesa.");
            return JsonError("Ocurrió un error al guardar la mesa.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] MesaForm modelo)
    {
        try
        {
            if (modelo is null)
            {
                return JsonError("Datos inválidos.");
            }

            if (!ModelState.IsValid)
            {
                return JsonError(ErrorModelState());
            }

            var error = await ValidarFormularioAsync(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var mesa = await _db.Mesas.FirstOrDefaultAsync(m => m.IdMesa == modelo.IdMesa && m.Sucursal.IdEmpresa == (ObtenerClaimInt("IdEmpresa") ?? 0));
            if (mesa is null)
            {
                return JsonError("La mesa no existe.");
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.Mesas.AnyAsync(m => m.IdSucursal == modelo.IdSucursal && m.Nombre == nombre && m.IdMesa != mesa.IdMesa))
            {
                return JsonError("Ya existe una mesa con ese nombre en la sucursal seleccionada.");
            }

            var anterior = Snapshot(mesa);

            mesa.IdSucursal = modelo.IdSucursal;
            mesa.Nombre = nombre;
            mesa.Capacidad = modelo.Capacidad;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Mesa", mesa.IdMesa.ToString(), "ACTUALIZAR_MESA", anterior, Snapshot(mesa));

            return JsonOk("Mesa actualizada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar mesa {Id}.", modelo.IdMesa);
            return JsonError("Ocurrió un error al actualizar la mesa.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_MESA", "Mesa activada correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        try
        {
            var mesa = await _db.Mesas.FirstOrDefaultAsync(m => m.IdMesa == id && m.Sucursal.IdEmpresa == (ObtenerClaimInt("IdEmpresa") ?? 0));
            if (mesa is null)
            {
                return JsonError("La mesa no existe.");
            }

            if (!mesa.Activo)
            {
                return JsonError("La mesa ya está inactiva.");
            }

            var tieneComandaAbierta = await _db.Comandas
                .AnyAsync(c => c.IdMesa == mesa.IdMesa && c.Estado == EstadosComanda.ABIERTA);

            if (tieneComandaAbierta)
            {
                return JsonError("No se puede inactivar la mesa porque tiene una comanda abierta.");
            }

            mesa.Activo = false;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Mesa", mesa.IdMesa.ToString(), "INACTIVAR_MESA", null, new { mesa.Activo });

            return JsonOk("Mesa inactivada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inactivar mesa {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var mesa = await _db.Mesas.FirstOrDefaultAsync(m => m.IdMesa == id && m.Sucursal.IdEmpresa == (ObtenerClaimInt("IdEmpresa") ?? 0));
            if (mesa is null)
            {
                return JsonError("La mesa no existe.");
            }

            if (mesa.Activo == activo)
            {
                return JsonError(activo ? "La mesa ya está activa." : "La mesa ya está inactiva.");
            }

            mesa.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Mesa", mesa.IdMesa.ToString(), accion, null, new { mesa.Activo });

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de mesa {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(MesaForm modelo)
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

        if (modelo.Capacidad <= 0)
        {
            return "La capacidad debe ser mayor a cero.";
        }

        if (modelo.IdSucursal <= 0)
        {
            return "La sucursal es obligatoria.";
        }

        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return "No se pudo identificar tu empresa.";
        }

        var sucursal = await _db.Sucursales
            .Include(s => s.Empresa)
            .FirstOrDefaultAsync(s => s.IdSucursal == modelo.IdSucursal && s.IdEmpresa == idEmpresa.Value);

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

    private static object Snapshot(Mesa m) => new
    {
        IdSucursal = m.IdSucursal,
        m.Nombre,
        m.Capacidad,
        m.Estado,
        m.Activo
    };

    private int? ObtenerClaimInt(string tipo)
    {
        var valor = User.FindFirstValue(tipo);
        return int.TryParse(valor, out var id) ? id : null;
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