using System.Security.Claims;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class UsuariosController : Controller
{
    private const string NombreRolAdministrador = "Administrador";

    private readonly AtlasRestaurantDbContext _db;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<UsuariosController> _logger;

    public UsuariosController(
        AtlasRestaurantDbContext db,
        IPasswordHasher<Usuario> passwordHasher,
        IAuditoriaService auditoria,
        ILogger<UsuariosController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
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
        var query = _db.Usuarios.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(u =>
                u.Nombre.Contains(t) ||
                u.UsuarioLogin.Contains(t) ||
                u.Empresa.Nombre.Contains(t) ||
                u.Rol.Nombre.Contains(t) ||
                (u.Correo != null && u.Correo.Contains(t)));
        }

        var lista = await query
            .OrderBy(u => u.Nombre)
            .Select(u => new
            {
                u.IdUsuario,
                u.Nombre,
                u.UsuarioLogin,
                IdEmpresa = u.IdEmpresa,
                Empresa = u.Empresa.Nombre,
                IdRol = u.IdRol,
                Rol = u.Rol.Nombre,
                u.Correo,
                u.Activo,
                u.FechaCreacion
            })
            .ToListAsync();

        return Json(lista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] UsuarioForm modelo)
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

            if (string.IsNullOrWhiteSpace(modelo.Password))
            {
                return JsonError("La contraseña es obligatoria.");
            }

            var login = modelo.UsuarioLogin.Trim();

            if (await _db.Usuarios.AnyAsync(u => u.UsuarioLogin == login))
            {
                return JsonError("Ya existe un usuario con ese nombre de usuario.");
            }

            var hash = _passwordHasher.HashPassword(new Usuario(), modelo.Password);

            var usuario = new Usuario
            {
                IdEmpresa = modelo.IdEmpresa,
                IdRol = modelo.IdRol,
                Nombre = modelo.Nombre.Trim(),
                UsuarioLogin = login,
                Correo = Normalizar(modelo.Correo),
                PasswordHash = hash,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Usuario", usuario.IdUsuario.ToString(), "CREAR_USUARIO", null, Snapshot(usuario));

            return JsonOk("Usuario creado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear usuario.");
            return JsonError("Ocurrió un error al guardar el usuario.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] UsuarioForm modelo)
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

            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == modelo.IdUsuario);
            if (usuario is null)
            {
                return JsonError("El usuario no existe.");
            }

            var login = modelo.UsuarioLogin.Trim();

            if (await _db.Usuarios.AnyAsync(u => u.UsuarioLogin == login && u.IdUsuario != usuario.IdUsuario))
            {
                return JsonError("Ya existe un usuario con ese nombre de usuario.");
            }

            var anterior = Snapshot(usuario);

            usuario.IdEmpresa = modelo.IdEmpresa;
            usuario.IdRol = modelo.IdRol;
            usuario.Nombre = modelo.Nombre.Trim();
            usuario.UsuarioLogin = login;
            usuario.Correo = Normalizar(modelo.Correo);

            if (!string.IsNullOrWhiteSpace(modelo.Password))
            {
                usuario.PasswordHash = _passwordHasher.HashPassword(usuario, modelo.Password);
            }

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Usuario", usuario.IdUsuario.ToString(), "ACTUALIZAR_USUARIO", anterior, Snapshot(usuario));

            return JsonOk("Usuario actualizado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar usuario {Id}.", modelo.IdUsuario);
            return JsonError("Ocurrió un error al actualizar el usuario.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        return await CambiarEstatus(id, true, "ACTIVAR_USUARIO", "Usuario activado correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id)
    {
        try
        {
            var usuario = await _db.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.IdUsuario == id);

            if (usuario is null)
            {
                return JsonError("El usuario no existe.");
            }

            if (!usuario.Activo)
            {
                return JsonError("El usuario ya está inactivo.");
            }

            var idActual = ObtenerIdUsuarioActual();
            if (usuario.IdUsuario == idActual)
            {
                return JsonError("No puedes inactivarte a ti mismo.");
            }

            if (usuario.Rol.Nombre == NombreRolAdministrador)
            {
                var hayOtroAdminActivo = await _db.Usuarios
                    .AnyAsync(u => u.IdUsuario != usuario.IdUsuario && u.Activo && u.Rol.Nombre == NombreRolAdministrador && u.Rol.Activo);

                if (!hayOtroAdminActivo)
                {
                    return JsonError("No se puede inactivar el último usuario administrador activo.");
                }
            }

            usuario.Activo = false;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Usuario", usuario.IdUsuario.ToString(), "INACTIVAR_USUARIO", null, new { usuario.Activo });

            return JsonOk("Usuario inactivado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inactivar usuario {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<IActionResult> CambiarEstatus(int id, bool activo, string accion, string mensajeOk)
    {
        try
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id);
            if (usuario is null)
            {
                return JsonError("El usuario no existe.");
            }

            if (usuario.Activo == activo)
            {
                return JsonError(activo ? "El usuario ya está activo." : "El usuario ya está inactivo.");
            }

            usuario.Activo = activo;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Usuario", usuario.IdUsuario.ToString(), accion, null, new { usuario.Activo });

            return JsonOk(mensajeOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estatus de usuario {Id}.", id);
            return JsonError("Ocurrió un error al cambiar el estatus.");
        }
    }

    private async Task<string?> ValidarFormularioAsync(UsuarioForm modelo)
    {
        var nombre = (modelo.Nombre ?? string.Empty).Trim();
        var login = (modelo.UsuarioLogin ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(login))
        {
            return "El nombre de usuario es obligatorio.";
        }

        if (modelo.Nombre != nombre || modelo.UsuarioLogin != login)
        {
            return "Los campos no deben contener espacios al inicio o al final.";
        }

        if (modelo.IdEmpresa <= 0)
        {
            return "La empresa es obligatoria.";
        }

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.IdEmpresa == modelo.IdEmpresa);
        if (empresa is null)
        {
            return "La empresa seleccionada no existe.";
        }

        if (!empresa.Activo)
        {
            return "La empresa seleccionada está inactiva.";
        }

        if (modelo.IdRol <= 0)
        {
            return "El rol es obligatorio.";
        }

        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.IdRol == modelo.IdRol);
        if (rol is null)
        {
            return "El rol seleccionado no existe.";
        }

        if (!rol.Activo)
        {
            return "El rol seleccionado está inactivo.";
        }

        return null;
    }

    private int ObtenerIdUsuarioActual()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(valor, out var id) ? id : 0;
    }

    private static object Snapshot(Usuario u) => new
    {
        u.Nombre,
        u.UsuarioLogin,
        IdEmpresa = u.IdEmpresa,
        IdRol = u.IdRol,
        u.Correo,
        u.Activo
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