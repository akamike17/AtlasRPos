using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Services.Bootstrap;

public class InitialSetupService : IInitialSetupService
{
    private const string NombreRolAdministrador = "Administrador";

    private readonly AtlasRestaurantDbContext _db;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InitialSetupService> _logger;

    public InitialSetupService(
        AtlasRestaurantDbContext db,
        IPasswordHasher<Usuario> passwordHasher,
        IConfiguration configuration,
        ILogger<InitialSetupService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<InitialSetupResult> ExecuteInitialSetupAsync()
    {
        var result = new InitialSetupResult();

        var adminUsername = _configuration["InitialSetup:AdminUsername"];
        var adminPassword = _configuration["InitialSetup:AdminPassword"];
        var adminName = _configuration["InitialSetup:AdminName"];
        var companyName = _configuration["InitialSetup:CompanyName"];
        var branchName = _configuration["InitialSetup:BranchName"];

        var requeridos = new Dictionary<string, string?>
        {
            ["InitialSetup:AdminUsername"] = adminUsername,
            ["InitialSetup:AdminPassword"] = adminPassword,
            ["InitialSetup:AdminName"] = adminName,
            ["InitialSetup:CompanyName"] = companyName,
            ["InitialSetup:BranchName"] = branchName
        };

        var faltantes = requeridos
            .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        if (faltantes.Count > 0)
        {
            result.ConfigCompleta = false;
            result.Detalle = "Faltan claves de configuración: " + string.Join(", ", faltantes);
            _logger.LogWarning("InitialSetup no ejecutado: claves de configuración faltantes.");
            return result;
        }

        result.ConfigCompleta = true;

        var empresa = await _db.Empresas
            .FirstOrDefaultAsync(e => e.Nombre == companyName);

        var rol = await _db.Roles
            .FirstOrDefaultAsync(r => r.Nombre == NombreRolAdministrador);

        if (empresa is null)
        {
            empresa = new Empresa
            {
                Nombre = companyName!,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            _db.Empresas.Add(empresa);
            result.EmpresaCreada = true;
        }

        if (rol is null)
        {
            rol = new Rol
            {
                Nombre = NombreRolAdministrador,
                Activo = true
            };
            _db.Roles.Add(rol);
            result.RolCreado = true;
        }

        await _db.SaveChangesAsync();

        Sucursal? sucursal = null;
        if (empresa is not null)
        {
            sucursal = await _db.Sucursales
                .FirstOrDefaultAsync(s => s.IdEmpresa == empresa.IdEmpresa && s.Nombre == branchName);

            if (sucursal is null)
            {
                sucursal = new Sucursal
                {
                    IdEmpresa = empresa.IdEmpresa,
                    Nombre = branchName!,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };
                _db.Sucursales.Add(sucursal);
                result.SucursalCreada = true;
            }
        }

        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.UsuarioLogin == adminUsername);

        if (usuario is null)
        {
            if (empresa is null || rol is null || sucursal is null)
            {
                result.Detalle = "No se pudo crear el usuario inicial: faltan datos base.";
                await _db.SaveChangesAsync();
                return result;
            }

            var hash = _passwordHasher.HashPassword(new Usuario(), adminPassword!);

            usuario = new Usuario
            {
                IdEmpresa = empresa.IdEmpresa,
                IdRol = rol.IdRol,
                Nombre = adminName!,
                UsuarioLogin = adminUsername!,
                PasswordHash = hash,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            _db.Usuarios.Add(usuario);
            result.UsuarioCreado = true;

            await _db.SaveChangesAsync();

            _db.Auditorias.Add(new Models.Auditoria
            {
                IdEmpresa = empresa.IdEmpresa,
                IdSucursal = sucursal.IdSucursal,
                IdUsuario = usuario.IdUsuario,
                Entidad = "Usuario",
                IdEntidad = usuario.IdUsuario.ToString(),
                Accion = "CREACION",
                DatosAnteriores = null,
                DatosNuevos = null,
                Fecha = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation("InitialSetup: usuario administrador inicial creado (Id={IdUsuario}).", usuario.IdUsuario);
        }

        result.Idempotente = !(result.EmpresaCreada || result.SucursalCreada || result.RolCreado || result.UsuarioCreado);

        return result;
    }
}