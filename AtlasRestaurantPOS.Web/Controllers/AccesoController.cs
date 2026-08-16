using System.Security.Claims;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

public class AccesoController : Controller
{
    public const string EsquemaCookie = "AtlasRestaurantCookie";

    private readonly AtlasRestaurantDbContext _db;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public AccesoController(AtlasRestaurantDbContext db, IPasswordHasher<Usuario> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var usuario = await _db.Usuarios
            .Include(u => u.Empresa)
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioLogin == model.Usuario);

        if (usuario is null ||
            !usuario.Activo ||
            usuario.Rol is null || !usuario.Rol.Activo ||
            usuario.Empresa is null || !usuario.Empresa.Activo)
        {
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            return View(model);
        }

        var verificacion = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, model.Password);
        if (verificacion == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            return View(model);
        }

        var sucursal = await _db.Sucursales
            .FirstOrDefaultAsync(s => s.IdEmpresa == usuario.IdEmpresa && s.Activo);

        if (sucursal is null)
        {
            ModelState.AddModelError(string.Empty, "No existe una sucursal activa para esta cuenta.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Role, usuario.Rol.Nombre),
            new("IdEmpresa", usuario.IdEmpresa.ToString()),
            new("IdSucursal", sucursal.IdSucursal.ToString()),
            new("IdRol", usuario.IdRol.ToString()),
            new("UsuarioLogin", usuario.UsuarioLogin)
        };

        var identity = new ClaimsIdentity(claims, EsquemaCookie);
        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            IsPersistent = model.Recordarme,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(EsquemaCookie, principal, properties);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Seleccionar", "CajaOperacion");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(EsquemaCookie);
        return RedirectToAction("Login", "Acceso");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Denegado()
    {
        return View();
    }
}