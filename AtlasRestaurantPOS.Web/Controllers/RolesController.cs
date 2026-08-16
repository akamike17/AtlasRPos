using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class RolesController : Controller
{
    private const string NombreRolAdministrador = "Administrador";

    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<RolesController> _logger;

    public RolesController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<RolesController> logger)
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
        var query = _db.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(r => r.Nombre.Contains(t) || (r.Descripcion != null && r.Descripcion.Contains(t)));
        }

        var lista = await query
            .OrderBy(r => r.Nombre)
            .Select(r => new
            {
                r.IdRol,
                r.Nombre,
                r.Descripcion,
                r.Activo
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] RolForm modelo)
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

            if (await _db.Roles.AnyAsync(r => r.Nombre == nombre))
            {
                return JsonError("Ya existe un rol con ese nombre.");
            }

            var rol = new Rol
            {
                Nombre = nombre,
                Descripcion = Normalizar(modelo.Descripcion),
                Activo = true
            };

            _db.Roles.Add(rol);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Rol", rol.IdRol.ToString(), "CREAR_ROL", null, Snapshot(rol));

            return JsonOk("Rol creado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear rol.");
            return JsonError("Ocurrió un error al guardar el rol.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] RolForm modelo)
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

            var rol = await _db.Roles.FirstOrDefaultAsync(r => r.IdRol == modelo.IdRol);
            if (rol is null)
            {
                return JsonError("El rol no existe.");
            }

            var nombre = modelo.Nombre.Trim();

            if (await _db.Roles.AnyAsync(r => r.Nombre == nombre && r.IdRol != rol.IdRol))
            {
                return JsonError("Ya existe un rol con ese nombre.");
            }

            var anterior = Snapshot(rol);

            rol.Nombre = nombre;
            rol.Descripcion = Normalizar(modelo.Descripcion);

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Rol", rol.IdRol.ToString(), "ACTUALIZAR_ROL", anterior, Snapshot(rol));

            return JsonOk("Rol actualizado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar rol {Id}.", modelo.IdRol);
            return JsonError("Ocurrió un error al actualizar el rol.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_ROL", "Rol activado correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        try
        {
            var rol = await _db.Roles.FirstOrDefaultAsync(r => r.IdRol == id);
            if (rol is null)
            {
                return JsonError("El rol no existe.");
            }

            if (!rol.Activo)
            {
                return JsonError("El rol ya está inactivo.");
            }

            if (rol.Nombre == NombreRolAdministrador)
            {
                var hayOtroAdminActivo = await _db.Roles
                    .AnyAsync(r => r.Nombre == NombreRolAdministrador && r.IdRol != rol.IdRol && r.Activo);

                if (!hayOtroAdminActivo)
                {
                    return JsonError("No se puede inactivar el rol Administrador porque dejaría al sistema sin acceso administrativo.");
                }
            }

            rol.Activo = false;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Rol", rol.IdRol.ToString(), "INACTIVAR_ROL", null, new { rol.Activo });

            return JsonOk("Rol inactivado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inactivar rol {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var rol = await _db.Roles.FirstOrDefaultAsync(r => r.IdRol == id);
            if (rol is null)
            {
                return JsonError("El rol no existe.");
            }

            if (rol.Activo == activo)
            {
                return JsonError(activo ? "El rol ya está activo." : "El rol ya está inactivo.");
            }

            rol.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Rol", rol.IdRol.ToString(), accion, null, new { rol.Activo });

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de rol {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private string? ValidarFormulario(RolForm modelo)
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

    private static object Snapshot(Rol r) => new
    {
        r.Nombre,
        r.Descripcion,
        r.Activo
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