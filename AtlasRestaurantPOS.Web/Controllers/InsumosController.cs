using System.Security.Claims;
using AtlasRestaurantPOS.Web.Constants;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using AtlasRestaurantPOS.Web.Services.CodigoInterno;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class InsumosController : Controller
{
    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ICodigoInternoService _codigoInterno;
    private readonly ILogger<InsumosController> _logger;

    public InsumosController(
        AtlasRestaurantDbContext db,
        IAuditoriaService auditoria,
        ICodigoInternoService codigoInterno,
        ILogger<InsumosController> logger)
    {
        _db = db;
        _auditoria = auditoria;
        _codigoInterno = codigoInterno;
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

        var query = _db.Insumos
            .AsNoTracking()
            .Where(i => i.IdEmpresa == idEmpresa);

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(i =>
                i.Nombre.Contains(t) ||
                (i.Codigo != null && i.Codigo.Contains(t)) ||
                (i.CodigoBarras != null && i.CodigoBarras.Contains(t)));
        }

        var lista = await query
            .OrderBy(i => i.Nombre)
            .Select(i => new
            {
                i.IdInsumo,
                i.IdEmpresa,
                Empresa = i.Empresa.Nombre,
                i.IdUnidadMedida,
                UnidadMedida = i.UnidadMedida.Nombre,
                i.Codigo,
                i.CodigoBarras,
                i.Nombre,
                i.CostoReferencia,
                i.StockMinimo,
                i.Activo,
                i.FechaCreacion
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

        var insumo = await _db.Insumos
            .AsNoTracking()
            .Where(i => i.IdInsumo == id && i.IdEmpresa == idEmpresa)
            .Include(i => i.UnidadMedida)
            .Include(i => i.Empresa)
            .Select(i => new
            {
                i.IdInsumo,
                i.IdEmpresa,
                Empresa = i.Empresa.Nombre,
                i.IdUnidadMedida,
                UnidadMedida = i.UnidadMedida.Nombre,
                i.Codigo,
                i.CodigoBarras,
                i.Nombre,
                i.CostoReferencia,
                i.StockMinimo,
                i.Activo,
                i.FechaCreacion
            })
            .FirstOrDefaultAsync();

        if (insumo is null)
        {
            return JsonError("El insumo no existe.");
        }

        return Json(insumo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] InsumoForm modelo)
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

            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null || idEmpresa <= 0)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var error = await ValidarFormularioAsync(modelo, idEmpresa.Value);
            if (error != null)
            {
                return JsonError(error);
            }

            var nombre = modelo.Nombre.Trim();
            var codigo = string.IsNullOrWhiteSpace(modelo.Codigo) ? null : modelo.Codigo.Trim();
            var codigoBarras = string.IsNullOrWhiteSpace(modelo.CodigoBarras) ? null : modelo.CodigoBarras.Trim();

            string? codigoGenerado = null;
            if (modelo.GenerarCodigo)
            {
                codigoGenerado = await _codigoInterno.GenerarAsync(idEmpresa.Value, TiposEntidadCodigo.INSUMO);
            }

            if (await _db.Insumos.AnyAsync(i => i.IdEmpresa == idEmpresa && i.Codigo == codigo && i.Codigo != null))
            {
                return JsonError("Ya existe un insumo con ese código en tu empresa.");
            }

            if (codigoBarras != null && await _db.Insumos.AnyAsync(i => i.IdEmpresa == idEmpresa && i.CodigoBarras == codigoBarras))
            {
                return JsonError("Ya existe un insumo con ese código de barras en tu empresa.");
            }

            var insumo = new Insumo
            {
                IdEmpresa = idEmpresa.Value,
                IdUnidadMedida = modelo.IdUnidadMedida,
                Codigo = codigoGenerado ?? codigo,
                CodigoBarras = codigoBarras,
                Nombre = nombre,
                CostoReferencia = modelo.CostoReferencia,
                StockMinimo = modelo.StockMinimo,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.Insumos.Add(insumo);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Insumo", insumo.IdInsumo.ToString(), "CREAR_INSUMO", null, Snapshot(insumo));

            return JsonOk("Insumo creado correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe un insumo con ese código o código de barras en tu empresa.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear insumo.");
            return JsonError("Ocurrió un error al guardar el insumo.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] InsumoForm modelo)
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

            var idEmpresa = ObtenerClaimInt("IdEmpresa");
            if (idEmpresa is null || idEmpresa <= 0)
            {
                return JsonError("No se pudo identificar tu empresa.");
            }

            var error = await ValidarFormularioAsync(modelo, idEmpresa.Value);
            if (error != null)
            {
                return JsonError(error);
            }

            var insumo = await _db.Insumos.FirstOrDefaultAsync(i => i.IdInsumo == modelo.IdInsumo && i.IdEmpresa == idEmpresa);
            if (insumo is null)
            {
                return JsonError("El insumo no existe.");
            }

            var nombre = modelo.Nombre.Trim();
            var codigo = string.IsNullOrWhiteSpace(modelo.Codigo) ? null : modelo.Codigo.Trim();
            var codigoBarras = string.IsNullOrWhiteSpace(modelo.CodigoBarras) ? null : modelo.CodigoBarras.Trim();

            if (await _db.Insumos.AnyAsync(i => i.IdEmpresa == idEmpresa && i.Codigo == codigo && i.IdInsumo != insumo.IdInsumo))
            {
                return JsonError("Ya existe un insumo con ese código en tu empresa.");
            }

            if (codigoBarras != null && await _db.Insumos.AnyAsync(i => i.IdEmpresa == idEmpresa && i.CodigoBarras == codigoBarras && i.IdInsumo != insumo.IdInsumo))
            {
                return JsonError("Ya existe un insumo con ese código de barras en tu empresa.");
            }

            var anterior = Snapshot(insumo);

            insumo.IdUnidadMedida = modelo.IdUnidadMedida;
            insumo.Codigo = codigo;
            insumo.CodigoBarras = codigoBarras;
            insumo.Nombre = nombre;
            insumo.CostoReferencia = modelo.CostoReferencia;
            insumo.StockMinimo = modelo.StockMinimo;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Insumo", insumo.IdInsumo.ToString(), "ACTUALIZAR_INSUMO", anterior, Snapshot(insumo));

            return JsonOk("Insumo actualizado correctamente.");
        }
        catch (DbUpdateException)
        {
            return JsonError("Ya existe un insumo con ese código o código de barras en tu empresa.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar insumo {Id}.", modelo.IdInsumo);
            return JsonError("Ocurrió un error al actualizar el insumo.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_INSUMO", "Insumo activado correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        return await CambiarEstatus(id, false, "INACTIVAR_INSUMO", "Insumo inactivado correctamente.");
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

            var insumo = await _db.Insumos.FirstOrDefaultAsync(i => i.IdInsumo == id && i.IdEmpresa == idEmpresa);
            if (insumo is null)
            {
                return JsonError("El insumo no existe.");
            }

            if (insumo.Activo == activo)
            {
                return JsonError(activo ? "El insumo ya está activo." : "El insumo ya está inactivo.");
            }

            var anterior = Snapshot(insumo);

            insumo.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Insumo", insumo.IdInsumo.ToString(), accion, anterior, Snapshot(insumo));

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de insumo {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(InsumoForm modelo, int idEmpresa)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.IdEmpresa == idEmpresa);
        if (empresa is null)
        {
            return "La empresa no existe.";
        }

        if (!empresa.Activo)
        {
            return "La empresa está inactiva.";
        }

        if (modelo.IdUnidadMedida <= 0)
        {
            return "La unidad de medida es obligatoria.";
        }

        var unidad = await _db.UnidadesMedida
            .FirstOrDefaultAsync(u => u.IdUnidadMedida == modelo.IdUnidadMedida);

        if (unidad is null)
        {
            return "La unidad de medida seleccionada no existe.";
        }

        if (unidad.IdEmpresa != idEmpresa)
        {
            return "La unidad de medida no pertenece a tu empresa.";
        }

        if (!unidad.Activo)
        {
            return "La unidad de medida seleccionada está inactiva.";
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

        if (modelo.Codigo != null)
        {
            var codigo = modelo.Codigo.Trim();
            if (codigo.Length > 50)
            {
                return "El código no debe superar 50 caracteres.";
            }

            if (modelo.Codigo != codigo && modelo.Codigo != modelo.Codigo.Trim())
            {
                return "El código no debe contener espacios al inicio o al final.";
            }
        }

        if (modelo.CodigoBarras != null)
        {
            var cb = modelo.CodigoBarras.Trim();
            if (cb.Length > 50)
            {
                return "El código de barras no debe superar 50 caracteres.";
            }
        }

        if (modelo.CostoReferencia < 0)
        {
            return "El costo de referencia no puede ser negativo.";
        }

        if (modelo.StockMinimo < 0)
        {
            return "El stock mínimo no puede ser negativo.";
        }

        return null;
    }

    private static object Snapshot(Insumo i) => new
    {
        IdEmpresa = i.IdEmpresa,
        i.IdUnidadMedida,
        i.Codigo,
        i.CodigoBarras,
        i.Nombre,
        i.CostoReferencia,
        i.StockMinimo,
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

    private string ErrorModelState()
    {
        return ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Datos inválidos.";
    }
}
