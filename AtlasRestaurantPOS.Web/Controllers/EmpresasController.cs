using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class EmpresasController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<EmpresasController> _logger;

    public EmpresasController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<EmpresasController> logger)
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
        var query = _db.Empresas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(e =>
                e.Nombre.Contains(t) ||
                (e.RazonSocial != null && e.RazonSocial.Contains(t)) ||
                (e.Rfc != null && e.Rfc.Contains(t)));
        }

        var lista = await query
            .OrderBy(e => e.Nombre)
            .Select(e => new
            {
                e.IdEmpresa,
                e.Nombre,
                e.RazonSocial,
                e.Rfc,
                e.Telefono,
                e.Correo,
                e.Activo,
                e.FechaCreacion
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] EmpresaForm modelo)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return JsonError(ErrorModelState());
            }

            var error = ValidarFormulario(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.Empresas.AnyAsync(e => e.Nombre == nombre))
            {
                return JsonError("Ya existe una empresa con ese nombre.");
            }

            var empresa = new Empresa
            {
                Nombre = nombre,
                RazonSocial = Normalizar(modelo.RazonSocial),
                Rfc = Normalizar(modelo.Rfc),
                Telefono = Normalizar(modelo.Telefono),
                Correo = Normalizar(modelo.Correo),
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.Empresas.Add(empresa);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Empresa", empresa.IdEmpresa.ToString(), "CREAR_EMPRESA", null, Snapshot(empresa));

            return JsonOk("Empresa creada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear empresa.");
            return JsonError("Ocurrió un error al guardar la empresa.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] EmpresaForm modelo)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return JsonError(ErrorModelState());
            }

            var error = ValidarFormulario(modelo);
            if (error != null)
            {
                return JsonError(error);
            }

            var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.IdEmpresa == modelo.IdEmpresa);
            if (empresa is null)
            {
                return JsonError("La empresa no existe.");
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.Empresas.AnyAsync(e => e.Nombre == nombre && e.IdEmpresa != empresa.IdEmpresa))
            {
                return JsonError("Ya existe una empresa con ese nombre.");
            }

            var anterior = Snapshot(empresa);

            empresa.Nombre = nombre;
            empresa.RazonSocial = Normalizar(modelo.RazonSocial);
            empresa.Rfc = Normalizar(modelo.Rfc);
            empresa.Telefono = Normalizar(modelo.Telefono);
            empresa.Correo = Normalizar(modelo.Correo);

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Empresa", empresa.IdEmpresa.ToString(), "ACTUALIZAR_EMPRESA", anterior, Snapshot(empresa));

            return JsonOk("Empresa actualizada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar empresa {Id}.", modelo.IdEmpresa);
            return JsonError("Ocurrió un error al actualizar la empresa.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_EMPRESA", "Empresa activada correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        return await CambiarEstatus(id, false, "INACTIVAR_EMPRESA", "Empresa inactivada correctamente.");
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.IdEmpresa == id);
            if (empresa is null)
            {
                return JsonError("La empresa no existe.");
            }

            if (empresa.Activo == activo)
            {
                return JsonError(activo ? "La empresa ya está activa." : "La empresa ya está inactiva.");
            }

            empresa.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Empresa", empresa.IdEmpresa.ToString(), accion, null, new { empresa.Activo });

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de empresa {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private string? ValidarFormulario(EmpresaForm modelo)
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

        return null;
    }

    private static object Snapshot(Empresa e) => new
    {
        e.Nombre,
        e.RazonSocial,
        e.Rfc,
        e.Telefono,
        e.Correo,
        e.Activo
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