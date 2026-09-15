using System.Security.Claims;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class UnidadesMedidaController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<UnidadesMedidaController> _logger;

    public UnidadesMedidaController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<UnidadesMedidaController> logger)
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

        var query = _db.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.IdEmpresa == idEmpresa);

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(u => u.Codigo.Contains(t) || u.Nombre.Contains(t));
        }

        var lista = await query
            .OrderBy(u => u.Nombre)
            .Select(u => new
            {
                u.IdUnidadMedida,
                u.IdEmpresa,
                Empresa = u.Empresa.Nombre,
                u.Codigo,
                u.Nombre,
                u.Activo,
                u.FechaCreacion
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(int id)
    {
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idEmpresa is null)
        {
            return JsonError("No se pudo identificar tu empresa.");
        }

        var unidad = await _db.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.IdUnidadMedida == id && u.IdEmpresa == idEmpresa)
            .Select(u => new
            {
                u.IdUnidadMedida,
                u.IdEmpresa,
                Empresa = u.Empresa.Nombre,
                u.Codigo,
                u.Nombre,
                u.Activo,
                u.FechaCreacion
            })
            .FirstOrDefaultAsync();

        if (unidad is null)
        {
            return JsonError("La unidad de medida no existe.");
        }

        return Json(unidad);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] UnidadMedidaForm modelo)
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

            var idEmpresa = ObtenerClaimInt("IdEmpresa")!.Value;
            var codigo = modelo.Codigo.Trim();
            var nombre = modelo.Nombre.Trim();

            if (await _db.UnidadesMedida.AnyAsync(u => u.IdEmpresa == idEmpresa && u.Codigo == codigo))
            {
                return JsonError("Ya existe una unidad de medida con ese código en tu empresa.");
            }

            var unidad = new UnidadMedida
            {
                IdEmpresa = idEmpresa,
                Codigo = codigo,
                Nombre = nombre,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.UnidadesMedida.Add(unidad);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("UnidadMedida", unidad.IdUnidadMedida.ToString(), "CREAR_UNIDAD_MEDIDA", null, Snapshot(unidad));

            return JsonOk("Unidad de medida creada correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe una unidad de medida con ese código en tu empresa.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear unidad de medida.");
            return JsonError("Ocurrió un error al guardar la unidad de medida.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] UnidadMedidaForm modelo)
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

            var idEmpresa = ObtenerClaimInt("IdEmpresa")!.Value;

            var unidad = await _db.UnidadesMedida.FirstOrDefaultAsync(u => u.IdUnidadMedida == modelo.IdUnidadMedida && u.IdEmpresa == idEmpresa);
            if (unidad is null)
            {
                return JsonError("La unidad de medida no existe.");
            }

            var codigo = modelo.Codigo.Trim();
            var nombre = modelo.Nombre.Trim();

            if (await _db.UnidadesMedida.AnyAsync(u => u.IdEmpresa == idEmpresa && u.Codigo == codigo && u.IdUnidadMedida != unidad.IdUnidadMedida))
            {
                return JsonError("Ya existe una unidad de medida con ese código en tu empresa.");
            }

            var anterior = Snapshot(unidad);

            unidad.Codigo = codigo;
            unidad.Nombre = nombre;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("UnidadMedida", unidad.IdUnidadMedida.ToString(), "ACTUALIZAR_UNIDAD_MEDIDA", anterior, Snapshot(unidad));

            return JsonOk("Unidad de medida actualizada correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe una unidad de medida con ese código en tu empresa.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar unidad de medida {Id}.", modelo.IdUnidadMedida);
            return JsonError("Ocurrió un error al actualizar la unidad de medida.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_UNIDAD_MEDIDA", "Unidad de medida activada correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        try
        {
            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var unidad = await _db.UnidadesMedida.FirstOrDefaultAsync(u => u.IdUnidadMedida == id && u.IdEmpresa == idEmpresa);
            if (unidad is null)
            {
                return JsonError("La unidad de medida no existe.");
            }

            if (!unidad.Activo)
            {
                return JsonError("La unidad de medida ya está inactiva.");
            }

            var tieneInsumosActivos = await _db.Insumos
                .AnyAsync(i => i.IdUnidadMedida == id && i.IdEmpresa == idEmpresa.Value && i.Activo);

            if (tieneInsumosActivos)
            {
                return JsonError("No se puede inactivar la unidad porque tiene insumos activos asociados.");
            }

            var anterior = Snapshot(unidad);

            unidad.Activo = false;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("UnidadMedida", unidad.IdUnidadMedida.ToString(), "INACTIVAR_UNIDAD_MEDIDA", anterior, Snapshot(unidad));

            return JsonOk("Unidad de medida inactivada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inactivar unidad de medida {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
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

            var unidad = await _db.UnidadesMedida.FirstOrDefaultAsync(u => u.IdUnidadMedida == id && u.IdEmpresa == idEmpresa);
            if (unidad is null)
            {
                return JsonError("La unidad de medida no existe.");
            }

            if (unidad.Activo == activo)
            {
                return JsonError(activo ? "La unidad de medida ya está activa." : "La unidad de medida ya está inactiva.");
            }

            var anterior = Snapshot(unidad);

            unidad.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("UnidadMedida", unidad.IdUnidadMedida.ToString(), accion, anterior, Snapshot(unidad));

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de unidad de medida {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(UnidadMedidaForm modelo)
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

        if (modelo.IdUnidadMedida < 0)
        {
            return "Id de unidad de medida inválido.";
        }

        if (modelo.IdUnidadMedida > 0)
        {
            var existe = await _db.UnidadesMedida
                .AsNoTracking()
                .AnyAsync(u => u.IdUnidadMedida == modelo.IdUnidadMedida && u.IdEmpresa == idEmpresa);
            if (!existe)
            {
                return "La unidad de medida no existe.";
            }
        }

        var codigo = (modelo.Codigo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return "El código es obligatorio.";
        }

        if (codigo.Length > 20)
        {
            return "El código no debe superar 20 caracteres.";
        }

        if (modelo.Codigo != codigo)
        {
            return "El código no debe contener espacios al inicio o al final.";
        }

        var nombre = (modelo.Nombre ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre es obligatorio.";
        }

        if (nombre.Length > 100)
        {
            return "El nombre no debe superar 100 caracteres.";
        }

        if (modelo.Nombre != nombre)
        {
            return "El nombre no debe contener espacios al inicio o al final.";
        }

        return null;
    }

    private static object Snapshot(UnidadMedida u) => new
    {
        IdEmpresa = u.IdEmpresa,
        u.Codigo,
        u.Nombre,
        u.Activo
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
