using System.Data;
using System.Security.Claims;
using AtlasRestaurantPOS.Web.Constants;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Models.ViewModels;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasRestaurantPOS.Web.Controllers;

[Authorize]
public class CajaOperacionController : Controller
{
    private const string EsquemaCookie = AccesoController.EsquemaCookie;
    private const string ClaimIdCaja = "IdCaja";
    private const string ClaimIdSesionCaja = "IdSesionCaja";
    private const string ClaimCajaNombre = "CajaNombre";
    private const string ClaimCajaCodigo = "CajaCodigo";
    private const string MetodoPagoEfectivo = "EFECTIVO";

    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<CajaOperacionController> _logger;

    public CajaOperacionController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, ILogger<CajaOperacionController> logger)
    {
        _db = db;
        _auditoria = auditoria;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Seleccionar()
    {
        var idUsuario = ObtenerIdUsuario();
        var idSucursal = ObtenerClaimInt("IdSucursal");
        if (idUsuario is null || idSucursal is null)
        {
            return RedirectToAction("Login", "Acceso");
        }

        if (TieneClaimsOperativos())
        {
            var idCajaActual = ObtenerClaimInt(ClaimIdCaja);
            var idSesionActual = ObtenerClaimLong(ClaimIdSesionCaja);
            var valida = idCajaActual is int && idSesionActual is long &&
                await _db.SesionesCaja.AnyAsync(sc =>
                    sc.IdCaja == idCajaActual &&
                    sc.IdSesionCaja == idSesionActual &&
                    sc.Estado == EstadosSesionCaja.ABIERTA &&
                    sc.IdUsuarioApertura == idUsuario);

            if (valida)
            {
                return RedirectToAction("Index");
            }

            await QuitarClaimsOperativosAsync();
        }

        var cajas = await _db.Cajas
            .AsNoTracking()
            .Where(c =>
                c.IdSucursal == idSucursal &&
                c.Activo &&
                c.Sucursal.Activo &&
                c.Sucursal.Empresa.Activo)
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        var sesiones = await _db.SesionesCaja
            .AsNoTracking()
            .Where(sc => cajas.Select(c => c.IdCaja).Contains(sc.IdCaja) && sc.Estado == EstadosSesionCaja.ABIERTA)
            .Include(sc => sc.UsuarioApertura)
            .ToListAsync();

        var sesionPorCaja = sesiones.ToDictionary(s => s.IdCaja);

        var modelo = new SeleccionCajaViewModel();
        foreach (var caja in cajas)
        {
            var opcion = new CajaOpcionViewModel
            {
                IdCaja = caja.IdCaja,
                Codigo = caja.Codigo,
                Nombre = caja.Nombre,
                Descripcion = caja.Descripcion
            };

            if (sesionPorCaja.TryGetValue(caja.IdCaja, out var s))
            {
                opcion.Estado = "ABIERTA";
                opcion.IdSesionCaja = s.IdSesionCaja;
                opcion.UsuarioApertura = s.UsuarioApertura.Nombre;
                opcion.FechaApertura = s.FechaApertura;
                opcion.EsPropia = s.IdUsuarioApertura == idUsuario;
            }
            else
            {
                opcion.Estado = "DISPONIBLE";
            }

            modelo.Cajas.Add(opcion);
        }

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var idUsuario = ObtenerIdUsuario();
        var idCaja = ObtenerClaimInt(ClaimIdCaja);
        var idSesion = ObtenerClaimLong(ClaimIdSesionCaja);
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return RedirectToAction("Seleccionar");
        }

        var sesion = await _db.SesionesCaja
            .Include(sc => sc.Caja).ThenInclude(c => c.Sucursal)
            .Include(sc => sc.UsuarioApertura)
            .FirstOrDefaultAsync(sc =>
                sc.IdSesionCaja == idSesion &&
                sc.IdCaja == idCaja &&
                sc.IdUsuarioApertura == idUsuario);

        if (sesion is null || sesion.Estado != EstadosSesionCaja.ABIERTA)
        {
            await QuitarClaimsOperativosAsync();
            return RedirectToAction("Seleccionar");
        }

        var (entradas, retiros, ventasEfectivo) = await CalcularTotalesAsync(idSesion.Value, idCaja.Value);
        var efectivoEsperado = sesion.FondoInicial + entradas + ventasEfectivo - retiros;

        var movimientos = await ObtenerMovimientosAsync(idSesion.Value, idCaja.Value);

        var modelo = new PanelCajaViewModel
        {
            IdCaja = sesion.IdCaja,
            Codigo = sesion.Caja.Codigo,
            Nombre = sesion.Caja.Nombre,
            Sucursal = sesion.Caja.Sucursal.Nombre,
            UsuarioApertura = sesion.UsuarioApertura.Nombre,
            FechaApertura = sesion.FechaApertura,
            FondoInicial = sesion.FondoInicial,
            Entradas = entradas,
            Retiros = retiros,
            VentasEfectivo = ventasEfectivo,
            EfectivoEsperado = efectivoEsperado,
            Movimientos = movimientos
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Abrir(AperturaCajaViewModel modelo)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return RedirectConError("Los datos de apertura son inválidos.");
            }

            var idUsuario = ObtenerIdUsuario();
            var idSucursal = ObtenerClaimInt("IdSucursal");
            if (idUsuario is null || idSucursal is null)
            {
                return RedirectToAction("Login", "Acceso");
            }

            if (modelo.FondoInicial < 0)
            {
                return RedirectConError("El fondo inicial no puede ser negativo.");
            }

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                var caja = await _db.Cajas
                    .FromSqlRaw("SELECT * FROM Cajas WHERE IdCaja = {0} FOR UPDATE", modelo.IdCaja)
                    .FirstOrDefaultAsync();

                if (caja is null) { await tx.RollbackAsync(); return RedirectConError("La caja no existe."); }
                if (!caja.Activo) { await tx.RollbackAsync(); return RedirectConError("La caja está inactiva."); }
                if (caja.IdSucursal != idSucursal) { await tx.RollbackAsync(); return RedirectConError("La caja no pertenece a tu sucursal."); }

                var sucursal = await _db.Sucursales
                    .Include(s => s.Empresa)
                    .FirstOrDefaultAsync(s => s.IdSucursal == caja.IdSucursal);

                if (sucursal is null || !sucursal.Activo)
                {
                    await tx.RollbackAsync();
                    return RedirectConError("La sucursal de la caja está inactiva.");
                }

                if (sucursal.Empresa is null || !sucursal.Empresa.Activo)
                {
                    await tx.RollbackAsync();
                    return RedirectConError("La empresa de la caja está inactiva.");
                }

                var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
                if (usuario is null || !usuario.Activo)
                {
                    await tx.RollbackAsync();
                    return RedirectConError("El usuario no está activo.");
                }

                var yaAbierta = await _db.SesionesCaja
                    .AnyAsync(sc => sc.IdCaja == caja.IdCaja && sc.Estado == EstadosSesionCaja.ABIERTA);
                if (yaAbierta)
                {
                    await tx.RollbackAsync();
                    return RedirectConError("La caja ya tiene una sesión abierta.");
                }

                var sesion = new SesionCaja
                {
                    IdCaja = caja.IdCaja,
                    IdUsuarioApertura = idUsuario.Value,
                    FechaApertura = DateTime.Now,
                    FondoInicial = modelo.FondoInicial,
                    Estado = EstadosSesionCaja.ABIERTA,
                    Observaciones = Normalizar(modelo.Observaciones)
                };
                _db.SesionesCaja.Add(sesion);
                await _db.SaveChangesAsync();

                await _auditoria.RegistrarAsync(
                    "SesionCaja",
                    sesion.IdSesionCaja.ToString(),
                    "ABRIR_CAJA",
                    null,
                    new
                    {
                        IdCaja = sesion.IdCaja,
                        IdSesionCaja = sesion.IdSesionCaja,
                        IdUsuarioApertura = sesion.IdUsuarioApertura,
                        sesion.FondoInicial,
                        sesion.Estado,
                        Observaciones = sesion.Observaciones
                    });

                await tx.CommitAsync();

                await AgregarClaimsOperativosAsync(caja.IdCaja, sesion.IdSesionCaja, caja.Nombre, caja.Codigo);

                return RedirectToAction("Index");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al abrir la caja {IdCaja}.", modelo.IdCaja);
            return RedirectConError("Ocurrió un error al abrir la caja.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reanudar(int idCaja)
    {
        try
        {
            var idUsuario = ObtenerIdUsuario();
            var idSucursal = ObtenerClaimInt("IdSucursal");
            if (idUsuario is null || idSucursal is null)
            {
                return RedirectToAction("Login", "Acceso");
            }

            var caja = await _db.Cajas
                .Include(c => c.Sucursal).ThenInclude(s => s.Empresa)
                .FirstOrDefaultAsync(c => c.IdCaja == idCaja);

            if (caja is null) return RedirectConError("La caja no existe.");
            if (!caja.Activo) return RedirectConError("La caja está inactiva.");
            if (caja.IdSucursal != idSucursal) return RedirectConError("La caja no pertenece a tu sucursal.");
            if (caja.Sucursal is null || !caja.Sucursal.Activo || caja.Sucursal.Empresa is null || !caja.Sucursal.Empresa.Activo)
            {
                return RedirectConError("La sucursal o empresa de la caja está inactiva.");
            }

            var sesion = await _db.SesionesCaja
                .FirstOrDefaultAsync(sc => sc.IdCaja == caja.IdCaja && sc.Estado == EstadosSesionCaja.ABIERTA);

            if (sesion is null) return RedirectConError("La caja no tiene una sesión abierta.");

            if (sesion.IdUsuarioApertura != idUsuario)
            {
                return RedirectConError("La caja está ocupada por otro usuario.");
            }

            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
            if (usuario is null || !usuario.Activo)
            {
                return RedirectConError("El usuario no está activo.");
            }

            await _auditoria.RegistrarAsync(
                "SesionCaja",
                sesion.IdSesionCaja.ToString(),
                "REANUDAR_SESION_CAJA",
                null,
                new { IdCaja = sesion.IdCaja, IdSesionCaja = sesion.IdSesionCaja, sesion.Estado });

            await AgregarClaimsOperativosAsync(caja.IdCaja, sesion.IdSesionCaja, caja.Nombre, caja.Codigo);

            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reanudar sesión de caja {IdCaja}.", idCaja);
            return RedirectConError("Ocurrió un error al reanudar la sesión.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrada([FromBody] MovimientoCajaOperacionViewModel modelo)
    {
        return await RegistrarMovimientoAsync(modelo, TiposMovimientoCaja.ENTRADA, "ENTRADA_CAJA", "Entrada registrada correctamente.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retiro([FromBody] MovimientoCajaOperacionViewModel modelo)
    {
        return await RegistrarMovimientoAsync(modelo, TiposMovimientoCaja.RETIRO, "RETIRO_CAJA", "Retiro registrado correctamente.");
    }

    private async Task<IActionResult> RegistrarMovimientoAsync(MovimientoCajaOperacionViewModel modelo, string tipo, string accionAuditoria, string mensajeOk)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return JsonError(ErrorModelState());
            }

            var idUsuario = ObtenerIdUsuario();
            var idCaja = ObtenerClaimInt(ClaimIdCaja);
            var idSesion = ObtenerClaimLong(ClaimIdSesionCaja);
            if (idUsuario is null || idCaja is null || idSesion is null)
            {
                return JsonError("No tienes una sesión de caja activa.");
            }

            var concepto = (modelo.Concepto ?? string.Empty).Trim();
            if (modelo.Importe <= 0) return JsonError("El importe debe ser mayor a cero.");
            if (string.IsNullOrWhiteSpace(concepto)) return JsonError("El concepto es obligatorio.");
            if (concepto.Length > 500) return JsonError("El concepto es demasiado largo.");

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                var sesion = await _db.SesionesCaja
                    .FromSqlRaw("SELECT * FROM SesionesCaja WHERE IdSesionCaja = {0} AND IdCaja = {1} FOR UPDATE", idSesion, idCaja)
                    .FirstOrDefaultAsync();

                if (sesion is null) { await tx.RollbackAsync(); return JsonError("La sesión de caja no existe."); }
                if (sesion.Estado != EstadosSesionCaja.ABIERTA) { await tx.RollbackAsync(); return JsonError("La sesión de caja no está abierta."); }
                if (sesion.IdUsuarioApertura != idUsuario) { await tx.RollbackAsync(); return JsonError("No tienes permiso sobre esta sesión."); }

                if (tipo == TiposMovimientoCaja.RETIRO)
                {
                    var (entradas, retiros, ventasEfectivo) = await CalcularTotalesAsync(idSesion.Value, idCaja.Value);
                    var disponible = sesion.FondoInicial + entradas + ventasEfectivo - retiros;
                    if (modelo.Importe > disponible)
                    {
                        await tx.RollbackAsync();
                        return JsonError($"El retiro excede el efectivo disponible de la sesión.");
                    }
                }

                var movimiento = new MovimientoCaja
                {
                    IdCaja = idCaja.Value,
                    IdSesionCaja = idSesion,
                    IdUsuario = idUsuario.Value,
                    Tipo = tipo,
                    Importe = modelo.Importe,
                    Concepto = concepto,
                    FechaMovimiento = DateTime.Now
                };
                _db.MovimientosCaja.Add(movimiento);
                await _db.SaveChangesAsync();

                await _auditoria.RegistrarAsync(
                    "MovimientoCaja",
                    movimiento.IdMovimientoCaja.ToString(),
                    accionAuditoria,
                    null,
                    new
                    {
                        movimiento.IdCaja,
                        IdSesionCaja = movimiento.IdSesionCaja,
                        movimiento.Tipo,
                        movimiento.Importe,
                        movimiento.Concepto
                    });

                await tx.CommitAsync();

                return JsonOk(mensajeOk);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar movimiento de caja (tipo {Tipo}).", tipo);
            return JsonError("Ocurrió un error al registrar el movimiento.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Movimientos()
    {
        var idUsuario = ObtenerIdUsuario();
        var idCaja = ObtenerClaimInt(ClaimIdCaja);
        var idSesion = ObtenerClaimLong(ClaimIdSesionCaja);
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        var pertenece = await _db.SesionesCaja
            .AsNoTracking()
            .AnyAsync(sc =>
                sc.IdSesionCaja == idSesion &&
                sc.IdCaja == idCaja &&
                sc.IdUsuarioApertura == idUsuario);

        if (!pertenece)
        {
            return JsonError("No tienes permiso sobre esta sesión.");
        }

        var lista = await ObtenerMovimientosAsync(idSesion.Value, idCaja.Value);

        return Json(new { ok = true, movimientos = lista });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(CierreCajaViewModel modelo)
    {
        try
        {
            var idUsuario = ObtenerIdUsuario();
            var idCaja = ObtenerClaimInt(ClaimIdCaja);
            var idSesion = ObtenerClaimLong(ClaimIdSesionCaja);
            if (idUsuario is null || idCaja is null || idSesion is null)
            {
                return RedirectConError("No tienes una sesión de caja activa.");
            }

            if (modelo.MontoCierre < 0)
            {
                return RedirectConError("El monto de cierre no puede ser negativo.");
            }

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                var sesion = await _db.SesionesCaja
                    .FromSqlRaw("SELECT * FROM SesionesCaja WHERE IdSesionCaja = {0} AND IdCaja = {1} FOR UPDATE", idSesion, idCaja)
                    .FirstOrDefaultAsync();

                if (sesion is null) { await tx.RollbackAsync(); return RedirectConError("La sesión de caja no existe."); }
                if (sesion.Estado == EstadosSesionCaja.CERRADA) { await tx.RollbackAsync(); return RedirectConError("La sesión de caja ya está cerrada."); }
                if (sesion.Estado != EstadosSesionCaja.ABIERTA) { await tx.RollbackAsync(); return RedirectConError("La sesión de caja no está abierta."); }
                if (sesion.IdUsuarioApertura != idUsuario) { await tx.RollbackAsync(); return RedirectConError("No tienes permiso sobre esta sesión."); }

                var (entradas, retiros, ventasEfectivo) = await CalcularTotalesAsync(idSesion.Value, idCaja.Value);
                var efectivoEsperado = sesion.FondoInicial + entradas + ventasEfectivo - retiros;
                var diferencia = modelo.MontoCierre - efectivoEsperado;

                sesion.IdUsuarioCierre = idUsuario;
                sesion.FechaCierre = DateTime.Now;
                sesion.MontoCierre = modelo.MontoCierre;
                sesion.Observaciones = ConcatenarObservaciones(sesion.Observaciones, Normalizar(modelo.Observaciones));
                sesion.Estado = EstadosSesionCaja.CERRADA;

                await _db.SaveChangesAsync();

                await _auditoria.RegistrarAsync(
                    "SesionCaja",
                    sesion.IdSesionCaja.ToString(),
                    "CERRAR_CAJA",
                    null,
                    new
                    {
                        sesion.IdCaja,
                        IdSesionCaja = sesion.IdSesionCaja,
                        sesion.MontoCierre,
                        EfectivoEsperado = efectivoEsperado,
                        Diferencia = diferencia,
                        IdUsuarioCierre = sesion.IdUsuarioCierre,
                        sesion.FechaCierre,
                        sesion.Estado
                    });

                await tx.CommitAsync();

                await QuitarClaimsOperativosAsync();

                TempData["CajaOperacionExito"] = "Caja cerrada correctamente.";
                return RedirectToAction("Seleccionar");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cerrar la caja {IdCaja}.", ObtenerClaimInt(ClaimIdCaja));
            return RedirectConError("Ocurrió un error al cerrar la caja.");
        }
    }

    private async Task<(decimal Entradas, decimal Retiros, decimal VentasEfectivo)> CalcularTotalesAsync(long idSesion, int idCaja)
    {
        var entradas = await _db.MovimientosCaja
            .Where(m => m.IdSesionCaja == idSesion && m.IdCaja == idCaja && m.Tipo == TiposMovimientoCaja.ENTRADA)
            .SumAsync(m => (decimal?)m.Importe) ?? 0m;

        var retiros = await _db.MovimientosCaja
            .Where(m =>
                m.IdSesionCaja == idSesion &&
                m.IdCaja == idCaja &&
                (m.Tipo == TiposMovimientoCaja.RETIRO || m.Tipo == TiposMovimientoCaja.SALIDA))
            .SumAsync(m => (decimal?)m.Importe) ?? 0m;

        var ventasEfectivo = await _db.Pagos
            .Where(p =>
                p.IdSesionCaja == idSesion &&
                p.IdCaja == idCaja &&
                p.MetodoPago == MetodoPagoEfectivo &&
                !p.Devuelto)
            .SumAsync(p => (decimal?)p.Importe) ?? 0m;

        return (entradas, retiros, ventasEfectivo);
    }

    private async Task<List<MovimientoItemViewModel>> ObtenerMovimientosAsync(long idSesion, int idCaja)
    {
        return await _db.MovimientosCaja
            .AsNoTracking()
            .Where(m => m.IdSesionCaja == idSesion && m.IdCaja == idCaja)
            .OrderByDescending(m => m.FechaMovimiento).ThenByDescending(m => m.IdMovimientoCaja)
            .Select(m => new MovimientoItemViewModel
            {
                IdMovimientoCaja = m.IdMovimientoCaja,
                Fecha = m.FechaMovimiento,
                Tipo = m.Tipo,
                Concepto = m.Concepto,
                Importe = m.Importe,
                Usuario = m.Usuario.Nombre
            })
            .ToListAsync();
    }

    private async Task AgregarClaimsOperativosAsync(int idCaja, long idSesion, string nombreCaja, string codigoCaja)
    {
        var claims = User.Claims
            .Where(c => c.Type is not ClaimIdCaja and not ClaimIdSesionCaja and not ClaimCajaNombre and not ClaimCajaCodigo)
            .ToList();
        claims.Add(new Claim(ClaimIdCaja, idCaja.ToString()));
        claims.Add(new Claim(ClaimIdSesionCaja, idSesion.ToString()));
        claims.Add(new Claim(ClaimCajaNombre, nombreCaja));
        claims.Add(new Claim(ClaimCajaCodigo, codigoCaja));
        await ReemitirIdentidadAsync(claims);
    }

    private async Task QuitarClaimsOperativosAsync()
    {
        var claims = User.Claims
            .Where(c => c.Type is not ClaimIdCaja and not ClaimIdSesionCaja and not ClaimCajaNombre and not ClaimCajaCodigo)
            .ToList();
        await ReemitirIdentidadAsync(claims);
    }

    private async Task ReemitirIdentidadAsync(IEnumerable<Claim> claims)
    {
        var props = new AuthenticationProperties { AllowRefresh = true, IsPersistent = false };
        var auth = await HttpContext.AuthenticateAsync(EsquemaCookie);
        if (auth.Succeeded && auth.Properties != null)
        {
            props.IsPersistent = auth.Properties.IsPersistent;
        }

        var identity = new ClaimsIdentity(claims, EsquemaCookie);
        await HttpContext.SignInAsync(EsquemaCookie, new ClaimsPrincipal(identity), props);
    }

    private bool TieneClaimsOperativos()
    {
        return ObtenerClaimInt(ClaimIdCaja) is int && ObtenerClaimLong(ClaimIdSesionCaja) is long;
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

    private IActionResult RedirectConError(string mensaje)
    {
        TempData["CajaOperacionError"] = mensaje;
        return RedirectToAction("Seleccionar");
    }

    private static string? Normalizar(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private static string? ConcatenarObservaciones(string? original, string? cierre)
    {
        if (string.IsNullOrWhiteSpace(cierre)) return original;
        if (string.IsNullOrWhiteSpace(original)) return cierre;
        return original + " | CIERRE: " + cierre;
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