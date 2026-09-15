using System.Security.Claims;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Etiquetas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class EtiquetasController : Controller
{
    private readonly IEtiquetaService _etiquetas;

    public EtiquetasController(IEtiquetaService etiquetas) { _etiquetas = etiquetas; }

    [HttpGet]
    public async Task<IActionResult> Producto(int id, CancellationToken cancellationToken)
    {
        var empresa = ObtenerClaimInt("IdEmpresa");
        if (empresa is null) return Json(new { ok = false, mensaje = "No se pudo identificar tu empresa." });
        var etiqueta = await _etiquetas.ObtenerProductoAsync(empresa.Value, id, cancellationToken);
        return etiqueta is null ? Json(new { ok = false, mensaje = "El producto no existe." }) : Json(new { ok = true, etiqueta });
    }

    [HttpGet]
    public async Task<IActionResult> Insumo(int id, CancellationToken cancellationToken)
    {
        var empresa = ObtenerClaimInt("IdEmpresa");
        if (empresa is null) return Json(new { ok = false, mensaje = "No se pudo identificar tu empresa." });
        var etiqueta = await _etiquetas.ObtenerInsumoAsync(empresa.Value, id, cancellationToken);
        return etiqueta is null ? Json(new { ok = false, mensaje = "El insumo no existe." }) : Json(new { ok = true, etiqueta });
    }

    [HttpGet]
    public async Task<IActionResult> PreviewProducto(int id, CancellationToken cancellationToken)
    {
        var empresa = ObtenerClaimInt("IdEmpresa");
        if (empresa is null) return Unauthorized();
        var etiqueta = await _etiquetas.ObtenerProductoAsync(empresa.Value, id, cancellationToken);
        return etiqueta is null ? NotFound() : View("Preview", etiqueta);
    }

    [HttpGet]
    public async Task<IActionResult> PreviewInsumo(int id, CancellationToken cancellationToken)
    {
        var empresa = ObtenerClaimInt("IdEmpresa");
        if (empresa is null) return Unauthorized();
        var etiqueta = await _etiquetas.ObtenerInsumoAsync(empresa.Value, id, cancellationToken);
        return etiqueta is null ? NotFound() : View("Preview", etiqueta);
    }

    private int? ObtenerClaimInt(string tipo)
    {
        var valor = User.FindFirstValue(tipo);
        return int.TryParse(valor, out var id) ? id : null;
    }
}
