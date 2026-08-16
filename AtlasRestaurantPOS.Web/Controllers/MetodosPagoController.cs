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
public class MetodosPagoController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<MetodosPagoController> _logger;

    public MetodosPagoController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<MetodosPagoController> logger)
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

        var query = _db.MetodosPago.AsNoTracking().Where(m => m.IdEmpresa == idEmpresa);

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(m => m.Nombre.Contains(t) || m.Codigo.Contains(t));
        }

        var lista = await query
            .OrderBy(m => m.Nombre)
            .Select(m => new
            {
                m.IdMetodoPago,
                m.IdEmpresa,
                Empresa = m.Empresa.Nombre,
                m.Nombre,
                m.Codigo,
                m.RequiereReferencia,
                m.PermiteCambio,
                m.Activo,
                m.FechaCreacion
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] MetodoPagoForm modelo)
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
            var codigo = modelo.Codigo.Trim();

            if (await _db.MetodosPago.AnyAsync(m => m.IdEmpresa == idEmpresa && m.Codigo == codigo))
            {
                return JsonError("Ya existe un método de pago con ese código en tu empresa.");
            }

            var metodo = new MetodoPago
            {
                IdEmpresa = idEmpresa,
                Nombre = nombre,
                Codigo = codigo,
                RequiereReferencia = modelo.RequiereReferencia,
                PermiteCambio = modelo.PermiteCambio,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.MetodosPago.Add(metodo);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("MetodoPago", metodo.IdMetodoPago.ToString(), "CREAR_METODO_PAGO", null, Snapshot(metodo));

            return JsonOk("Método de pago creado correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe un método de pago con ese código en tu empresa.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear método de pago.");
            return JsonError("Ocurrió un error al guardar el método de pago.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] MetodoPagoForm modelo)
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

            var metodo = await _db.MetodosPago.FirstOrDefaultAsync(m => m.IdMetodoPago == modelo.IdMetodoPago && m.IdEmpresa == idEmpresa);
            if (metodo is null)
            {
                return JsonError("El método de pago no existe.");
            }

            var nombre = modelo.Nombre.Trim();
            var codigo = modelo.Codigo.Trim();

            if (await _db.MetodosPago.AnyAsync(m => m.IdEmpresa == idEmpresa && m.Codigo == codigo && m.IdMetodoPago != metodo.IdMetodoPago))
            {
                return JsonError("Ya existe un método de pago con ese código en tu empresa.");
            }

            var anterior = Snapshot(metodo);

            metodo.Nombre = nombre;
            metodo.Codigo = codigo;
            metodo.RequiereReferencia = modelo.RequiereReferencia;
            metodo.PermiteCambio = modelo.PermiteCambio;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("MetodoPago", metodo.IdMetodoPago.ToString(), "ACTUALIZAR_METODO_PAGO", anterior, Snapshot(metodo));

            return JsonOk("Método de pago actualizado correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe un método de pago con ese código en tu empresa.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar método de pago {Id}.", modelo.IdMetodoPago);
            return JsonError("Ocurrió un error al actualizar el método de pago.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_METODO_PAGO", "Método de pago activado correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        return await CambiarEstatus(id, false, "INACTIVAR_METODO_PAGO", "Método de pago inactivado correctamente.");
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

            var metodo = await _db.MetodosPago.FirstOrDefaultAsync(m => m.IdMetodoPago == id && m.IdEmpresa == idEmpresa);
            if (metodo is null)
            {
                return JsonError("El método de pago no existe.");
            }

            if (metodo.Activo == activo)
            {
                return JsonError(activo ? "El método de pago ya está activo." : "El método de pago ya está inactivo.");
            }

            var anterior = Snapshot(metodo);

            metodo.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("MetodoPago", metodo.IdMetodoPago.ToString(), accion, anterior, Snapshot(metodo));

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de método de pago {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(MetodoPagoForm modelo)
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
        var codigo = (modelo.Codigo ?? string.Empty).Trim();

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

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return "El código es obligatorio.";
        }

        if (codigo.Length > 50)
        {
            return "El código no debe superar 50 caracteres.";
        }

        if (modelo.Codigo != codigo)
        {
            return "El código no debe contener espacios al inicio o al final.";
        }

        return null;
    }

    private static object Snapshot(MetodoPago m) => new
    {
        IdEmpresa = m.IdEmpresa,
        m.Nombre,
        m.Codigo,
        m.RequiereReferencia,
        m.PermiteCambio,
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
}