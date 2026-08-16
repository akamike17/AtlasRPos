using System.Data;
using System.Security.Claims;
using AtlasRestaurantPOS.Web.Constants;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using AtlasRestaurantPOS.Web.Services.Comanda;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize]
public class SalonController : Controller
{
    private const string ClaimIdCaja = "IdCaja";
    private const string ClaimIdSesionCaja = "IdSesionCaja";

    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IFolioComandaService _folioComanda;
    private readonly ILogger<SalonController> _logger;

    public SalonController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, IFolioComandaService folioComanda, ILogger<SalonController> logger)
    {
        _db = db;
        _auditoria = auditoria;
        _folioComanda = folioComanda;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return RedirectToAction("Seleccionar", "CajaOperacion");
        }

        if (!await ValidarSesionCajaAsync(idUsuario.Value, idCaja.Value, idSesion.Value))
        {
            return RedirectToAction("Seleccionar", "CajaOperacion");
        }

        var idSucursal = ObtenerClaimInt("IdSucursal") ?? 0;

        var mesas = await _db.Mesas
            .AsNoTracking()
            .Where(m => m.Activo && m.IdSucursal == idSucursal)
            .OrderBy(m => m.Nombre)
            .Select(m => new MesaSalonViewModel
            {
                IdMesa = m.IdMesa,
                Nombre = m.Nombre,
                Capacidad = m.Capacidad,
                Estado = m.Estado,
                IdComanda = m.Comandas
                    .Where(c => c.Estado == EstadosComanda.ABIERTA)
                    .OrderByDescending(c => c.IdComanda)
                    .Select(c => (long?)c.IdComanda)
                    .FirstOrDefault(),
                Folio = m.Comandas
                    .Where(c => c.Estado == EstadosComanda.ABIERTA)
                    .OrderByDescending(c => c.IdComanda)
                    .Select(c => c.Folio)
                    .FirstOrDefault(),
                Total = m.Comandas
                    .Where(c => c.Estado == EstadosComanda.ABIERTA)
                    .OrderByDescending(c => c.IdComanda)
                    .Select(c => (decimal?)c.Total)
                    .FirstOrDefault(),
                UsuarioResponsable = m.Comandas
                    .Where(c => c.Estado == EstadosComanda.ABIERTA)
                    .OrderByDescending(c => c.IdComanda)
                    .Select(c => c.Usuario.Nombre)
                    .FirstOrDefault()
            })
            .ToListAsync();

        foreach (var mesa in mesas)
        {
            if (mesa.IdComanda.HasValue)
            {
                var esPropia = await _db.Comandas
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.IdComanda == mesa.IdComanda.Value &&
                        c.IdCaja == idCaja &&
                        c.IdSesionCaja == idSesion &&
                        c.IdUsuario == idUsuario.Value);

                mesa.EsPropia = esPropia;
                mesa.EsAjena = !esPropia;
            }
        }

        var modelo = new SalonIndexViewModel { Mesas = mesas };
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AbrirMesa([FromBody] AbrirMesaViewModel modelo)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        if (modelo is null || modelo.IdMesa <= 0)
        {
            return JsonError("La mesa es obligatoria.");
        }

        var idSucursal = ObtenerClaimInt("IdSucursal");
        if (idSucursal is null)
        {
            return JsonError("No se pudo identificar tu sucursal.");
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                if (!await ValidarSesionCajaAsync(idUsuario.Value, idCaja.Value, idSesion.Value))
                {
                    await tx.RollbackAsync();
                    return JsonError("La sesión de caja no está disponible.");
                }

                var mesa = await _db.Mesas
                    .FromSqlRaw("SELECT * FROM Mesas WHERE IdMesa = {0} FOR UPDATE", modelo.IdMesa)
                    .FirstOrDefaultAsync();

                if (mesa is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La mesa no existe.");
                }

                if (!mesa.Activo)
                {
                    await tx.RollbackAsync();
                    return JsonError("La mesa está inactiva.");
                }

                if (mesa.IdSucursal != idSucursal)
                {
                    await tx.RollbackAsync();
                    return JsonError("La mesa no pertenece a tu sucursal.");
                }

                if (mesa.Estado != EstadosMesa.DISPONIBLE)
                {
                    await tx.RollbackAsync();
                    return JsonError("La mesa no está disponible.");
                }

                var tieneAbierta = await _db.Comandas
                    .AnyAsync(c => c.IdMesa == mesa.IdMesa && c.Estado == EstadosComanda.ABIERTA);

                if (tieneAbierta)
                {
                    await tx.RollbackAsync();
                    return JsonError("La mesa ya tiene una comanda abierta.");
                }

                var folio = await _folioComanda.GenerarAsync(idSucursal.Value);

                var comanda = new Comanda
                {
                    IdSucursal = idSucursal.Value,
                    IdCaja = idCaja.Value,
                    IdSesionCaja = idSesion,
                    IdMesa = mesa.IdMesa,
                    IdUsuario = idUsuario.Value,
                    FechaApertura = DateTime.Now,
                    Estado = EstadosComanda.ABIERTA,
                    Folio = folio,
                    Subtotal = 0m,
                    Impuestos = 0m,
                    Descuento = 0m,
                    Total = 0m
                };
                _db.Comandas.Add(comanda);
                await _db.SaveChangesAsync();

                var estadoAnterior = mesa.Estado;
                mesa.Estado = EstadosMesa.OCUPADA;
                await _db.SaveChangesAsync();

                await _auditoria.RegistrarAsync(
                    "Mesa",
                    mesa.IdMesa.ToString(),
                    "ABRIR_MESA",
                    new { mesa.IdMesa, EstadoAnterior = estadoAnterior },
                    new { mesa.IdMesa, comanda.IdComanda, comanda.Folio, EstadoNuevo = mesa.Estado });

                await tx.CommitAsync();

                return Json(new { ok = true, idComanda = comanda.IdComanda, folio = comanda.Folio, mensaje = "Mesa abierta." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al abrir mesa {IdMesa}.", modelo.IdMesa);
            return JsonError("Ocurrió un error al abrir la mesa.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Reanudar(int idMesa)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return RedirectToAction("Seleccionar", "CajaOperacion");
        }

        var idSucursal = ObtenerClaimInt("IdSucursal");
        if (idSucursal is null)
        {
            return RedirectToAction("Seleccionar", "CajaOperacion");
        }

        var comanda = await _db.Comandas
            .AsNoTracking()
            .Where(c =>
                c.IdMesa == idMesa &&
                c.Estado == EstadosComanda.ABIERTA &&
                c.IdSucursal == idSucursal &&
                c.IdCaja == idCaja &&
                c.IdSesionCaja == idSesion &&
                c.IdUsuario == idUsuario.Value)
            .Select(c => new { c.IdComanda, c.Folio })
            .FirstOrDefaultAsync();

        if (comanda is null)
        {
            TempData["SalonError"] = "La comanda de esta mesa no pertenece a tu sesión.";
            return RedirectToAction("Index");
        }

        await _auditoria.RegistrarAsync(
            "Comanda",
            comanda.IdComanda.ToString(),
            "REANUDAR_COMANDA_MESA",
            null,
            new { comanda.IdComanda, comanda.Folio, MesaId = idMesa });

        return RedirectToAction("Index", "Ventas", new { idComanda = comanda.IdComanda });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferirMesa([FromBody] TransferirMesaViewModel modelo)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        if (modelo is null || modelo.IdMesaOrigen <= 0 || modelo.IdMesaDestino <= 0)
        {
            return JsonError("Mesas inválidas.");
        }

        if (modelo.IdMesaOrigen == modelo.IdMesaDestino)
        {
            return JsonError("La mesa destino debe ser diferente de la mesa origen.");
        }

        var idSucursal = ObtenerClaimInt("IdSucursal");
        if (idSucursal is null)
        {
            return JsonError("No se pudo identificar tu sucursal.");
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                if (!await ValidarSesionCajaAsync(idUsuario.Value, idCaja.Value, idSesion.Value))
                {
                    await tx.RollbackAsync();
                    return JsonError("La sesión de caja no está disponible.");
                }

                // Locks deterministas por IdMesa (menor → mayor) para evitar deadlocks.
                var menor = Math.Min(modelo.IdMesaOrigen, modelo.IdMesaDestino);
                var mayor = Math.Max(modelo.IdMesaOrigen, modelo.IdMesaDestino);

                var mesaMenor = await _db.Mesas
                    .FromSqlRaw("SELECT * FROM Mesas WHERE IdMesa = {0} FOR UPDATE", menor)
                    .FirstOrDefaultAsync();
                var mesaMayor = await _db.Mesas
                    .FromSqlRaw("SELECT * FROM Mesas WHERE IdMesa = {0} FOR UPDATE", mayor)
                    .FirstOrDefaultAsync();

                if (mesaMenor is null || mesaMayor is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("Una de las mesas no existe.");
                }

                var origen = mesaMenor.IdMesa == modelo.IdMesaOrigen ? mesaMenor : mesaMayor;
                var destino = mesaMenor.IdMesa == modelo.IdMesaDestino ? mesaMenor : mesaMayor;

                if (!origen.Activo || !destino.Activo)
                {
                    await tx.RollbackAsync();
                    return JsonError("Una de las mesas está inactiva.");
                }

                if (origen.IdSucursal != idSucursal || destino.IdSucursal != idSucursal)
                {
                    await tx.RollbackAsync();
                    return JsonError("Las mesas no pertenecen a tu sucursal.");
                }

                if (destino.Estado != EstadosMesa.DISPONIBLE)
                {
                    await tx.RollbackAsync();
                    return JsonError("La mesa destino no está disponible.");
                }

                var destinoTieneAbierta = await _db.Comandas
                    .AnyAsync(c => c.IdMesa == destino.IdMesa && c.Estado == EstadosComanda.ABIERTA);

                if (destinoTieneAbierta)
                {
                    await tx.RollbackAsync();
                    return JsonError("La mesa destino ya tiene una comanda abierta.");
                }

                var comanda = await _db.Comandas
                    .FromSqlRaw("SELECT * FROM Comandas WHERE IdMesa = {0} AND Estado = {1} FOR UPDATE", origen.IdMesa, EstadosComanda.ABIERTA)
                    .FirstOrDefaultAsync();

                if (comanda is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La mesa origen no tiene una comanda abierta.");
                }

                if (comanda.IdSucursal != idSucursal ||
                    comanda.IdCaja != idCaja ||
                    comanda.IdSesionCaja != idSesion ||
                    comanda.IdUsuario != idUsuario)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no pertenece a tu sesión.");
                }

                var idOrigen = origen.IdMesa;
                var idDestino = destino.IdMesa;
                var estadoOrigenAnterior = origen.Estado;
                var estadoDestinoAnterior = destino.Estado;
                var idComanda = comanda.IdComanda;
                var folio = comanda.Folio;

                comanda.IdMesa = destino.IdMesa;
                origen.Estado = EstadosMesa.DISPONIBLE;
                destino.Estado = EstadosMesa.OCUPADA;
                await _db.SaveChangesAsync();

                await _auditoria.RegistrarAsync(
                    "Mesa",
                    idComanda.ToString(),
                    "TRANSFERIR_MESA",
                    new { IdComanda = idComanda, Folio = folio, MesaOrigen = idOrigen, MesaDestino = idDestino, EstadoOrigen = estadoOrigenAnterior, EstadoDestino = estadoDestinoAnterior },
                    new { IdComanda = idComanda, Folio = folio, MesaOrigen = idOrigen, MesaDestino = idDestino, EstadoOrigen = origen.Estado, EstadoDestino = destino.Estado });

                await tx.CommitAsync();

                return JsonOk("Mesa transferida correctamente.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al transferir mesa de {Origen} a {Destino}.", modelo.IdMesaOrigen, modelo.IdMesaDestino);
            return JsonError("Ocurrió un error al transferir la mesa.");
        }
    }

    private async Task<bool> ValidarSesionCajaAsync(int idUsuario, int idCaja, long idSesion)
    {
        return await _db.Cajas
            .AsNoTracking()
            .AnyAsync(c =>
                c.IdCaja == idCaja &&
                c.Activo &&
                c.Sucursal.Activo &&
                c.Sucursal.Empresa.Activo &&
                c.SesionesCaja.Any(sc =>
                    sc.IdSesionCaja == idSesion &&
                    sc.Estado == EstadosSesionCaja.ABIERTA &&
                    sc.IdUsuarioApertura == idUsuario));
    }

    private (int? IdUsuario, int? IdCaja, long? IdSesionCaja) ObtenerContexto()
    {
        var idUsuario = ObtenerIdUsuario();
        var idCaja = ObtenerClaimInt(ClaimIdCaja);
        var idSesion = ObtenerClaimLong(ClaimIdSesionCaja);
        return (idUsuario, idCaja, idSesion);
    }

    private int? ObtenerIdUsuario()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(valor, out var id) ? id : null;
    }

    private int? ObtenerClaimInt(string tipo)
    {
        var valor = User.FindFirstValue(tipo);
        return int.TryParse(valor, out var id) ? id : null;
    }

    private long? ObtenerClaimLong(string tipo)
    {
        var valor = User.FindFirstValue(tipo);
        return long.TryParse(valor, out var id) ? id : null;
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