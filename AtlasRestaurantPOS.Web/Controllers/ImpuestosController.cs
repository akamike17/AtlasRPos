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
public class ImpuestosController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<ImpuestosController> _logger;

    public ImpuestosController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<ImpuestosController> logger)
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
        if (idEmpresa is null)
        {
            return JsonError("No se pudo identificar tu empresa.");
        }

        var query = _db.Impuestos.AsNoTracking().Where(i => i.IdEmpresa == idEmpresa);

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(i => i.Nombre.Contains(t));
        }

        var lista = await query
            .OrderBy(i => i.Nombre)
            .Select(i => new
            {
                i.IdImpuesto,
                i.IdEmpresa,
                Empresa = i.Empresa.Nombre,
                i.Nombre,
                i.Tasa,
                i.IncluidoEnPrecio,
                i.Activo,
                i.FechaCreacion
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] ImpuestoForm modelo)
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

            var idEmpresa = ObtenerClaimInt("IdEmpresa")!.Value;
            var nombre = modelo.Nombre.Trim();

            if (await _db.Impuestos.AnyAsync(i => i.IdEmpresa == idEmpresa && i.Nombre == nombre))
            {
                return JsonError("Ya existe un impuesto con ese nombre en tu empresa.");
            }

            var impuesto = new Impuesto
            {
                IdEmpresa = idEmpresa,
                Nombre = nombre,
                Tasa = modelo.Tasa,
                IncluidoEnPrecio = modelo.IncluidoEnPrecio,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.Impuestos.Add(impuesto);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Impuesto", impuesto.IdImpuesto.ToString(), "CREAR_IMPUESTO", null, Snapshot(impuesto));

            return JsonOk("Impuesto creado correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe un impuesto con ese nombre en tu empresa.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear impuesto.");
            return JsonError("Ocurrió un error al guardar el impuesto.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] ImpuestoForm modelo)
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

            var idEmpresa = ObtenerClaimInt("IdEmpresa")!.Value;

            var impuesto = await _db.Impuestos.FirstOrDefaultAsync(i => i.IdImpuesto == modelo.IdImpuesto && i.IdEmpresa == idEmpresa);
            if (impuesto is null)
            {
                return JsonError("El impuesto no existe.");
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.Impuestos.AnyAsync(i => i.IdEmpresa == idEmpresa && i.Nombre == nombre && i.IdImpuesto != impuesto.IdImpuesto))
            {
                return JsonError("Ya existe un impuesto con ese nombre en tu empresa.");
            }

            var anterior = Snapshot(impuesto);

            impuesto.Nombre = nombre;
            impuesto.Tasa = modelo.Tasa;
            impuesto.IncluidoEnPrecio = modelo.IncluidoEnPrecio;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Impuesto", impuesto.IdImpuesto.ToString(), "ACTUALIZAR_IMPUESTO", anterior, Snapshot(impuesto));

            return JsonOk("Impuesto actualizado correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe un impuesto con ese nombre en tu empresa.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar impuesto {Id}.", modelo.IdImpuesto);
            return JsonError("Ocurrió un error al actualizar el impuesto.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_IMPUESTO", "Impuesto activado correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        return await CambiarEstatus(id, false, "INACTIVAR_IMPUESTO", "Impuesto inactivado correctamente.");
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var impuesto = await _db.Impuestos.FirstOrDefaultAsync(i => i.IdImpuesto == id && i.IdEmpresa == idEmpresa);
            if (impuesto is null)
            {
                return JsonError("El impuesto no existe.");
            }

            if (impuesto.Activo == activo)
            {
                return JsonError(activo ? "El impuesto ya está activo." : "El impuesto ya está inactivo.");
            }

            var anterior = Snapshot(impuesto);

            impuesto.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Impuesto", impuesto.IdImpuesto.ToString(), accion, anterior, Snapshot(impuesto));

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de impuesto {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(ImpuestoForm modelo)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null || idEmpresa <= 0)
        {
            return "No se pudo identificar tu empresa.";
        }

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.IdEmpresa == idEmpresa);
        if (empresa is null)
        {
            return "La empresa no existe.";
        }

        if (!empresa.Activo)
        {
            return "La empresa está inactiva.";
        }

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

        if (modelo.Tasa < 0 || modelo.Tasa > 100)
        {
            return "La tasa debe estar entre 0 y 100.";
        }

        return null;
    }

    private static object Snapshot(Impuesto i) => new
    {
        IdEmpresa = i.IdEmpresa,
        i.Nombre,
        i.Tasa,
        i.IncluidoEnPrecio,
        i.Activo
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
}