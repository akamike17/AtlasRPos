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
public class VentasController : Controller
{
    private const string ClaimIdCaja = "IdCaja";
    private const string ClaimIdSesionCaja = "IdSesionCaja";

    private readonly AtlasRestaurantDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IFolioComandaService _folioComanda;
    private readonly ILogger<VentasController> _logger;

    public VentasController(AtlasRestaurantDbContext db, IAuditoriaService auditoria, IFolioComandaService folioComanda, ILogger<VentasController> logger)
    {
        _db = db;
        _auditoria = auditoria;
        _folioComanda = folioComanda;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(long? idComanda = null)
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
        var idEmpresa = ObtenerClaimInt("IdEmpresa") ?? 0;

        var caja = await _db.Cajas
            .AsNoTracking()
            .Where(c => c.IdCaja == idCaja && c.IdSucursal == idSucursal)
            .Select(c => new { c.Codigo, c.Nombre, Sucursal = c.Sucursal.Nombre })
            .FirstOrDefaultAsync();

        if (caja is null)
        {
            return RedirectToAction("Seleccionar", "CajaOperacion");
        }

        var categorias = await _db.CategoriasProducto
            .AsNoTracking()
            .Where(cat => cat.Activo && cat.Productos.Any(p => p.Activo))
            .OrderBy(cat => cat.Nombre)
            .Select(cat => new CategoriaVentasViewModel
            {
                IdCategoriaProducto = cat.IdCategoriaProducto,
                Nombre = cat.Nombre,
                Productos = cat.Productos
                    .Where(p => p.Activo)
                    .OrderBy(p => p.Nombre)
                    .Select(p => new ProductoVentasViewModel
                    {
                        IdProducto = p.IdProducto,
                        Nombre = p.Nombre,
                        Precio = p.Precio
                    })
                    .ToList()
            })
            .ToListAsync();

        var metodos = await _db.MetodosPago
            .AsNoTracking()
            .Where(m => m.IdEmpresa == idEmpresa && m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new MetodoPagoViewModel
            {
                IdMetodoPago = m.IdMetodoPago,
                Nombre = m.Nombre,
                Codigo = m.Codigo,
                RequiereReferencia = m.RequiereReferencia,
                PermiteCambio = m.PermiteCambio
            })
            .ToListAsync();

        var modelo = new VentasIndexViewModel
        {
            Codigo = caja.Codigo,
            Nombre = caja.Nombre,
            Sucursal = caja.Sucursal,
            Categorias = categorias,
            MetodosPago = metodos
        };

        if (idComanda.HasValue)
        {
            var comandaValida = await _db.Comandas
                .AsNoTracking()
                .AnyAsync(c =>
                    c.IdComanda == idComanda.Value &&
                    c.IdSucursal == idSucursal &&
                    c.IdCaja == idCaja &&
                    c.IdSesionCaja == idSesion &&
                    c.IdUsuario == idUsuario.Value &&
                    c.Estado == EstadosComanda.ABIERTA);

            if (comandaValida)
            {
                modelo.IdComandaInicial = idComanda.Value;
            }
        }

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevaComanda()
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
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

                var folio = await _folioComanda.GenerarAsync(idSucursal.Value);

                var comanda = new Comanda
                {
                    IdSucursal = idSucursal.Value,
                    IdCaja = idCaja.Value,
                    IdSesionCaja = idSesion,
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

                await _auditoria.RegistrarAsync(
                    "Comanda",
                    comanda.IdComanda.ToString(),
                    "CREAR_COMANDA",
                    null,
                    new
                    {
                        comanda.IdComanda,
                        comanda.Folio,
                        comanda.IdSucursal,
                        comanda.IdCaja,
                        comanda.IdSesionCaja,
                        comanda.Estado
                    });

                await tx.CommitAsync();

                return Json(new { ok = true, idComanda = comanda.IdComanda, folio = comanda.Folio, mensaje = "Comanda creada." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear comanda.");
            return JsonError("Ocurrió un error al crear la comanda.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Estado(long idComanda)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        var idSucursal = ObtenerClaimInt("IdSucursal");
        if (idSucursal is null)
        {
            return JsonError("No se pudo identificar tu sucursal.");
        }

        var valida = await _db.Comandas
            .AsNoTracking()
            .AnyAsync(c =>
                c.IdComanda == idComanda &&
                c.IdSucursal == idSucursal &&
                c.IdCaja == idCaja &&
                c.IdSesionCaja == idSesion &&
                c.IdUsuario == idUsuario.Value);

        if (!valida)
        {
            return JsonError("La comanda no existe o no pertenece a tu sesión.");
        }

        var estado = await ConstruirEstadoAsync(idComanda, idCaja.Value, idSesion.Value);
        return Json(new { ok = true, estado });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarProducto([FromBody] AgregarProductoViewModel modelo)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        if (modelo is null)
        {
            return JsonError("Datos inválidos.");
        }

        if (!ModelState.IsValid)
        {
            return JsonError(ErrorModelState());
        }

        if (modelo.Cantidad <= 0)
        {
            return JsonError("La cantidad debe ser mayor a cero.");
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                var comanda = await ObtenerComandaOperativaAsync(modelo.IdComanda, idUsuario.Value, idCaja.Value, idSesion.Value);
                if (comanda is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no existe o no pertenece a tu sesión.");
                }
                if (comanda.Estado != EstadosComanda.ABIERTA)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no está abierta.");
                }

                var producto = await _db.Productos
                    .AsNoTracking()
                    .Where(p => p.IdProducto == modelo.IdProducto && p.Activo && p.CategoriaProducto.Activo)
                    .FirstOrDefaultAsync();

                if (producto is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("El producto no existe o está inactivo.");
                }

                var detalle = new ComandaDetalle
                {
                    IdComanda = comanda.IdComanda,
                    IdProducto = producto.IdProducto,
                    Cantidad = modelo.Cantidad,
                    PrecioUnitario = producto.Precio,
                    Importe = Redondear(modelo.Cantidad * producto.Precio),
                    Notas = Normalizar(modelo.Notas)
                };
                _db.ComandaDetalles.Add(detalle);
                await _db.SaveChangesAsync();

                await RecalcularComandaAsync(comanda.IdComanda);

                await _auditoria.RegistrarAsync(
                    "ComandaDetalle",
                    detalle.IdComandaDetalle.ToString(),
                    "AGREGAR_PRODUCTO",
                    null,
                    new
                    {
                        detalle.IdComanda,
                        detalle.IdComandaDetalle,
                        detalle.IdProducto,
                        detalle.Cantidad,
                        detalle.PrecioUnitario,
                        detalle.Importe,
                        detalle.Notas
                    });

                await tx.CommitAsync();

                var estado = await ConstruirEstadoAsync(comanda.IdComanda, idCaja.Value, idSesion.Value);
                return Json(new { ok = true, estado, mensaje = "Producto agregado." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar producto a comanda {IdComanda}.", modelo.IdComanda);
            return JsonError("Ocurrió un error al agregar el producto.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModificarCantidad([FromBody] ModificarCantidadViewModel modelo)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        if (modelo is null)
        {
            return JsonError("Datos inválidos.");
        }

        if (!ModelState.IsValid)
        {
            return JsonError(ErrorModelState());
        }

        if (modelo.Cantidad <= 0)
        {
            return JsonError("La cantidad debe ser mayor a cero.");
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                var comanda = await ObtenerComandaOperativaAsync(modelo.IdComanda, idUsuario.Value, idCaja.Value, idSesion.Value);
                if (comanda is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no existe o no pertenece a tu sesión.");
                }
                if (comanda.Estado != EstadosComanda.ABIERTA)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no está abierta.");
                }

                var detalle = await _db.ComandaDetalles
                    .FirstOrDefaultAsync(d => d.IdComandaDetalle == modelo.IdComandaDetalle && d.IdComanda == modelo.IdComanda);

                if (detalle is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La partida no existe en esta comanda.");
                }

                var anterior = new { detalle.Cantidad, detalle.Notas };

                detalle.Cantidad = modelo.Cantidad;
                detalle.Importe = Redondear(detalle.Cantidad * detalle.PrecioUnitario);
                if (modelo.Notas is not null)
                {
                    detalle.Notas = Normalizar(modelo.Notas);
                }

                await _db.SaveChangesAsync();
                await RecalcularComandaAsync(comanda.IdComanda);

                await _auditoria.RegistrarAsync(
                    "ComandaDetalle",
                    detalle.IdComandaDetalle.ToString(),
                    "MODIFICAR_DETALLE",
                    anterior,
                    new { detalle.IdComanda, detalle.IdComandaDetalle, detalle.Cantidad, detalle.Importe, detalle.Notas });

                await tx.CommitAsync();

                var estado = await ConstruirEstadoAsync(comanda.IdComanda, idCaja.Value, idSesion.Value);
                return Json(new { ok = true, estado, mensaje = "Partida actualizada." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al modificar partida de comanda {IdComanda}.", modelo.IdComanda);
            return JsonError("Ocurrió un error al modificar la partida.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarDetalle([FromBody] QuitarDetalleViewModel modelo)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        if (modelo is null)
        {
            return JsonError("Datos inválidos.");
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                var comanda = await ObtenerComandaOperativaAsync(modelo.IdComanda, idUsuario.Value, idCaja.Value, idSesion.Value);
                if (comanda is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no existe o no pertenece a tu sesión.");
                }
                if (comanda.Estado != EstadosComanda.ABIERTA)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no está abierta.");
                }

                var detalle = await _db.ComandaDetalles
                    .FirstOrDefaultAsync(d => d.IdComandaDetalle == modelo.IdComandaDetalle && d.IdComanda == modelo.IdComanda);

                if (detalle is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La partida no existe en esta comanda.");
                }

                var idDetalle = detalle.IdComandaDetalle;
                var idProducto = detalle.IdProducto;
                _db.ComandaDetalles.Remove(detalle);
                await _db.SaveChangesAsync();

                await RecalcularComandaAsync(comanda.IdComanda);

                await _auditoria.RegistrarAsync(
                    "ComandaDetalle",
                    idDetalle.ToString(),
                    "QUITAR_PRODUCTO",
                    new { IdComandaDetalle = idDetalle, IdProducto = idProducto },
                    null);

                await tx.CommitAsync();

                var estado = await ConstruirEstadoAsync(comanda.IdComanda, idCaja.Value, idSesion.Value);
                return Json(new { ok = true, estado, mensaje = "Partida eliminada." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al quitar partida de comanda {IdComanda}.", modelo.IdComanda);
            return JsonError("Ocurrió un error al quitar la partida.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPago([FromBody] RegistrarPagoViewModel modelo)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        if (modelo is null)
        {
            return JsonError("Datos inválidos.");
        }

        if (!ModelState.IsValid)
        {
            return JsonError(ErrorModelState());
        }

        if (modelo.Importe <= 0)
        {
            return JsonError("El importe debe ser mayor a cero.");
        }

        var idSucursal = ObtenerClaimInt("IdSucursal");
        var idEmpresa = ObtenerClaimInt("IdEmpresa");
        if (idSucursal is null || idEmpresa is null)
        {
            return JsonError("No se pudieron identificar tus datos operativos.");
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                var comanda = await _db.Comandas
                    .FromSqlRaw("SELECT * FROM Comandas WHERE IdComanda = {0} FOR UPDATE", modelo.IdComanda)
                    .FirstOrDefaultAsync();

                if (comanda is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no existe.");
                }

                if (comanda.IdCaja != idCaja || comanda.IdSesionCaja != idSesion || comanda.IdSucursal != idSucursal || comanda.IdUsuario != idUsuario)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no pertenece a tu sesión.");
                }

                if (comanda.Estado != EstadosComanda.ABIERTA)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda ya está cerrada.");
                }

                if (!await ValidarSesionCajaAsync(idUsuario.Value, idCaja.Value, idSesion.Value))
                {
                    await tx.RollbackAsync();
                    return JsonError("La sesión de caja no está disponible.");
                }

                var pagado = await _db.Pagos
                    .Where(p => p.IdComanda == comanda.IdComanda && !p.Devuelto)
                    .SumAsync(p => (decimal?)p.Importe) ?? 0m;

                var saldo = Redondear(comanda.Total - pagado);
                if (saldo <= 0)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda ya está cubierta.");
                }

                var metodo = await _db.MetodosPago
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.IdMetodoPago == modelo.IdMetodoPago && m.Activo && m.IdEmpresa == idEmpresa);

                if (metodo is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("El método de pago no existe o no está activo.");
                }

                if (metodo.RequiereReferencia && string.IsNullOrWhiteSpace(modelo.Referencia))
                {
                    await tx.RollbackAsync();
                    return JsonError("La referencia es obligatoria para este método de pago.");
                }

                decimal importeAplicar;
                decimal cambio = 0m;

                if (metodo.PermiteCambio)
                {
                    importeAplicar = Math.Min(modelo.Importe, saldo);
                    cambio = Redondear(modelo.Importe - importeAplicar);
                }
                else
                {
                    if (modelo.Importe > saldo)
                    {
                        await tx.RollbackAsync();
                        return JsonError("El pago excede el saldo pendiente.");
                    }
                    importeAplicar = modelo.Importe;
                }

                var pago = new Pago
                {
                    IdComanda = comanda.IdComanda,
                    IdMetodoPago = metodo.IdMetodoPago,
                    IdCaja = idCaja,
                    IdSesionCaja = idSesion,
                    IdUsuario = idUsuario,
                    MetodoPago = metodo.Codigo,
                    Importe = importeAplicar,
                    FechaPago = DateTime.Now,
                    Referencia = Normalizar(modelo.Referencia)
                };
                _db.Pagos.Add(pago);
                await _db.SaveChangesAsync();

                var nuevoSaldo = Redondear(saldo - importeAplicar);
                var comandaCerrada = nuevoSaldo <= 0;

                if (comandaCerrada)
                {
                    comanda.Estado = EstadosComanda.CERRADA;
                    comanda.FechaCierre = DateTime.Now;
                    await _db.SaveChangesAsync();

                    if (comanda.IdMesa is not null)
                    {
                        var mesa = await _db.Mesas
                            .FromSqlRaw("SELECT * FROM Mesas WHERE IdMesa = {0} FOR UPDATE", comanda.IdMesa.Value)
                            .FirstOrDefaultAsync();

                        if (mesa is not null)
                        {
                            var estadoAnteriorMesa = mesa.Estado;
                            mesa.Estado = EstadosMesa.DISPONIBLE;
                            await _db.SaveChangesAsync();

                            await _auditoria.RegistrarAsync(
                                "Mesa",
                                mesa.IdMesa.ToString(),
                                "LIBERAR_MESA",
                                new { mesa.IdMesa, comanda.IdComanda, comanda.Folio, EstadoAnterior = estadoAnteriorMesa },
                                new { mesa.IdMesa, comanda.IdComanda, comanda.Folio, EstadoNuevo = mesa.Estado });
                        }
                    }
                }

                await _auditoria.RegistrarAsync(
                    "Pago",
                    pago.IdPago.ToString(),
                    "REGISTRAR_PAGO",
                    null,
                    new
                    {
                        pago.IdComanda,
                        pago.IdPago,
                        pago.IdMetodoPago,
                        pago.MetodoPago,
                        pago.Importe,
                        pago.Referencia,
                        pago.IdCaja,
                        pago.IdSesionCaja,
                        pago.IdUsuario
                    });

                if (comandaCerrada)
                {
                    await _auditoria.RegistrarAsync(
                        "Comanda",
                        comanda.IdComanda.ToString(),
                        "CERRAR_COMANDA",
                        null,
                        new
                        {
                            comanda.IdComanda,
                            comanda.Folio,
                            comanda.Total,
                            comanda.FechaCierre,
                            comanda.Estado
                        });
                }

                await tx.CommitAsync();

                var estado = await ConstruirEstadoAsync(comanda.IdComanda, idCaja.Value, idSesion.Value);
                return Json(new
                {
                    ok = true,
                    cambio,
                    saldo = nuevoSaldo,
                    comandaCerrada,
                    estado,
                    mensaje = comandaCerrada ? "Comanda cobrada." : "Pago registrado."
                });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar pago en comanda {IdComanda}.", modelo.IdComanda);
            return JsonError("Ocurrió un error al registrar el pago.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> CancelarComanda([FromBody] CancelarComandaViewModel modelo)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        if (modelo is null)
        {
            return JsonError("Datos inválidos.");
        }

        if (!ModelState.IsValid)
        {
            return JsonError(ErrorModelState());
        }

        var motivo = Normalizar(modelo.Motivo);
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return JsonError("El motivo de cancelación es obligatorio.");
        }
        if (motivo.Length < 5)
        {
            return JsonError("El motivo de cancelación debe tener al menos 5 caracteres.");
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

                var comanda = await ObtenerComandaOperativaAsync(modelo.IdComanda, idUsuario.Value, idCaja.Value, idSesion.Value);
                if (comanda is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda no existe o no pertenece a tu sesión.");
                }

                if (comanda.Estado != EstadosComanda.ABIERTA)
                {
                    await tx.RollbackAsync();
                    return JsonError("Solo se puede cancelar una comanda abierta.");
                }

                var tienePagosNoDevueltos = await _db.Pagos
                    .AnyAsync(p => p.IdComanda == comanda.IdComanda && !p.Devuelto);

                if (tienePagosNoDevueltos)
                {
                    await tx.RollbackAsync();
                    return JsonError("La comanda tiene pagos sin devolver. Devuelve los pagos antes de cancelarla.");
                }

                var estadoAnterior = comanda.Estado;
                comanda.Estado = EstadosComanda.CANCELADA;
                comanda.FechaCancelacion = DateTime.Now;
                comanda.MotivoCancelacion = motivo;
                comanda.IdUsuarioCancelacion = idUsuario.Value;
                await _db.SaveChangesAsync();

                await _auditoria.RegistrarAsync(
                    "Comanda",
                    comanda.IdComanda.ToString(),
                    "CANCELAR_COMANDA",
                    new
                    {
                        comanda.IdComanda,
                        comanda.Folio,
                        comanda.Estado,
                        comanda.IdMesa
                    },
                    new
                    {
                        comanda.IdComanda,
                        comanda.Folio,
                        comanda.Estado,
                        comanda.IdMesa,
                        comanda.FechaCancelacion,
                        comanda.MotivoCancelacion,
                        comanda.IdUsuarioCancelacion
                    });

                if (comanda.IdMesa is not null)
                {
                    var mesa = await _db.Mesas
                        .FromSqlRaw("SELECT * FROM Mesas WHERE IdMesa = {0} FOR UPDATE", comanda.IdMesa.Value)
                        .FirstOrDefaultAsync();

                    if (mesa is not null)
                    {
                        var estadoAnteriorMesa = mesa.Estado;
                        mesa.Estado = EstadosMesa.DISPONIBLE;
                        await _db.SaveChangesAsync();

                        await _auditoria.RegistrarAsync(
                            "Mesa",
                            mesa.IdMesa.ToString(),
                            "LIBERAR_MESA_POR_CANCELACION",
                            new { mesa.IdMesa, comanda.IdComanda, comanda.Folio, EstadoAnterior = estadoAnteriorMesa },
                            new { mesa.IdMesa, comanda.IdComanda, comanda.Folio, EstadoNuevo = mesa.Estado });
                    }
                }

                await tx.CommitAsync();

                var estado = await ConstruirEstadoAsync(comanda.IdComanda, idCaja.Value, idSesion.Value);
                return Json(new { ok = true, estado, mensaje = "Comanda cancelada correctamente." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cancelar comanda {IdComanda}.", modelo.IdComanda);
            return JsonError("Ocurrió un error al cancelar la comanda.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DevolverPago([FromBody] DevolverPagoViewModel modelo)
    {
        var (idUsuario, idCaja, idSesion) = ObtenerContexto();
        if (idUsuario is null || idCaja is null || idSesion is null)
        {
            return JsonError("No tienes una sesión de caja activa.");
        }

        if (modelo is null)
        {
            return JsonError("Datos inválidos.");
        }

        if (!ModelState.IsValid)
        {
            return JsonError(ErrorModelState());
        }

        var motivo = Normalizar(modelo.Motivo);
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return JsonError("El motivo de devolución es obligatorio.");
        }
        if (motivo.Length < 5)
        {
            return JsonError("El motivo de devolución debe tener al menos 5 caracteres.");
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

                var pago = await _db.Pagos
                    .FromSqlRaw("SELECT * FROM Pagos WHERE IdPago = {0} FOR UPDATE", modelo.IdPago)
                    .FirstOrDefaultAsync();

                if (pago is null)
                {
                    await tx.RollbackAsync();
                    return JsonError("El pago no existe.");
                }

                if (pago.IdCaja != idCaja || pago.IdSesionCaja != idSesion)
                {
                    await tx.RollbackAsync();
                    return JsonError("El pago no pertenece a tu sesión de caja.");
                }

                var comandaPago = await _db.Comandas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.IdComanda == pago.IdComanda);

                if (comandaPago is null || comandaPago.IdSucursal != idSucursal)
                {
                    await tx.RollbackAsync();
                    return JsonError("El pago no pertenece a tu sucursal.");
                }

                if (pago.Devuelto)
                {
                    await tx.RollbackAsync();
                    return JsonError("El pago ya fue devuelto.");
                }

                pago.Devuelto = true;
                pago.FechaDevolucion = DateTime.Now;
                pago.IdUsuarioDevolucion = idUsuario.Value;
                pago.MotivoDevolucion = motivo;
                await _db.SaveChangesAsync();

                if (pago.MetodoPago == "EFECTIVO")
                {
                    var movimiento = new MovimientoCaja
                    {
                        IdCaja = idCaja.Value,
                        IdSesionCaja = idSesion,
                        IdUsuario = idUsuario.Value,
                        Tipo = TiposMovimientoCaja.DEVOLUCION,
                        Importe = pago.Importe,
                        Concepto = $"Devolución de pago {pago.IdPago} de comanda {pago.IdComanda}.",
                        FechaMovimiento = DateTime.Now
                    };
                    _db.MovimientosCaja.Add(movimiento);
                    await _db.SaveChangesAsync();

                    await _auditoria.RegistrarAsync(
                        "MovimientoCaja",
                        movimiento.IdMovimientoCaja.ToString(),
                        "DEVOLUCION_CAJA",
                        null,
                        new
                        {
                            movimiento.IdCaja,
                            IdSesionCaja = movimiento.IdSesionCaja,
                            movimiento.Tipo,
                            movimiento.Importe,
                            movimiento.Concepto,
                            IdPago = pago.IdPago
                        });
                }

                await _auditoria.RegistrarAsync(
                    "Pago",
                    pago.IdPago.ToString(),
                    "DEVOLVER_PAGO",
                    new
                    {
                        pago.IdPago,
                        pago.IdComanda,
                        pago.MetodoPago,
                        pago.Importe,
                        Devuelto = false
                    },
                    new
                    {
                        pago.IdPago,
                        pago.IdComanda,
                        pago.MetodoPago,
                        pago.Importe,
                        pago.Devuelto,
                        pago.FechaDevolucion,
                        pago.IdUsuarioDevolucion,
                        pago.MotivoDevolucion
                    });

                await tx.CommitAsync();

                var estado = await ConstruirEstadoAsync(pago.IdComanda, idCaja.Value, idSesion.Value);
                return Json(new { ok = true, estado, mensaje = "Pago devuelto correctamente." });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al devolver pago {IdPago}.", modelo.IdPago);
            return JsonError("Ocurrió un error al devolver el pago.");
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

    private async Task<Comanda?> ObtenerComandaOperativaAsync(long idComanda, int idUsuario, int idCaja, long idSesion)
    {
        var idSucursal = ObtenerClaimInt("IdSucursal");
        if (idSucursal is null)
        {
            return null;
        }

        var comanda = await _db.Comandas
            .FromSqlRaw("SELECT * FROM Comandas WHERE IdComanda = {0} FOR UPDATE", idComanda)
            .FirstOrDefaultAsync();

        if (comanda is null)
        {
            return null;
        }

        if (comanda.IdSucursal != idSucursal ||
            comanda.IdCaja != idCaja ||
            comanda.IdSesionCaja != idSesion ||
            comanda.IdUsuario != idUsuario)
        {
            return null;
        }

        return comanda;
    }

    private async Task RecalcularComandaAsync(long idComanda)
    {
        var comanda = await _db.Comandas
            .Include(c => c.Detalles)
                .ThenInclude(d => d.Producto)
                    .ThenInclude(p => p.ProductosImpuestos)
                        .ThenInclude(pi => pi.Impuesto)
            .FirstOrDefaultAsync(c => c.IdComanda == idComanda);

        if (comanda is null)
        {
            return;
        }

        decimal subtotal = 0m;
        decimal impuestos = 0m;

        foreach (var d in comanda.Detalles)
        {
            var baseDetalle = Redondear(d.Cantidad * d.PrecioUnitario);
            decimal impuestoIncluido = 0m;
            decimal impuestoExtra = 0m;

            var impuestosProducto = (d.Producto?.ProductosImpuestos ?? Enumerable.Empty<ProductoImpuesto>())
                .Where(pi => pi.Impuesto != null && pi.Impuesto.Activo)
                .Select(pi => pi.Impuesto);

            foreach (var imp in impuestosProducto)
            {
                if (imp.IncluidoEnPrecio)
                {
                    impuestoIncluido += Redondear(baseDetalle * (imp.Tasa / (100m + imp.Tasa)));
                }
                else
                {
                    impuestoExtra += Redondear(baseDetalle * (imp.Tasa / 100m));
                }
            }

            d.Importe = baseDetalle;
            subtotal += Redondear(baseDetalle - impuestoIncluido);
            impuestos += Redondear(impuestoIncluido + impuestoExtra);
        }

        comanda.Subtotal = Redondear(subtotal);
        comanda.Impuestos = Redondear(impuestos);
        comanda.Total = Redondear(comanda.Subtotal + comanda.Impuestos);

        await _db.SaveChangesAsync();
    }

    private async Task<ComandaEstadoViewModel> ConstruirEstadoAsync(long idComanda, int idCaja, long idSesion)
    {
        var comanda = await _db.Comandas
            .AsNoTracking()
            .Where(c => c.IdComanda == idComanda && c.IdCaja == idCaja && c.IdSesionCaja == idSesion)
            .Select(c => new ComandaEstadoViewModel
            {
                IdComanda = c.IdComanda,
                Folio = c.Folio ?? string.Empty,
                Estado = c.Estado,
                NombreMesa = c.Mesa != null ? c.Mesa.Nombre : null,
                Subtotal = c.Subtotal,
                Impuestos = c.Impuestos,
                Total = c.Total,
                ComandaCerrada = c.Estado == EstadosComanda.CERRADA,
                Partidas = c.Detalles
                    .OrderBy(d => d.IdComandaDetalle)
                    .Select(d => new ComandaItemViewModel
                    {
                        IdComandaDetalle = d.IdComandaDetalle,
                        IdProducto = d.IdProducto,
                        Producto = d.Producto.Nombre,
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioUnitario,
                        Importe = d.Importe,
                        Notas = d.Notas
                    })
                    .ToList(),
                Pagos = c.Pagos
                    .OrderBy(p => p.FechaPago)
                    .Select(p => new PagoItemViewModel
                    {
                        IdPago = p.IdPago,
                        MetodoPago = p.MetodoPago,
                        Importe = p.Importe,
                        FechaPago = p.FechaPago,
                        Referencia = p.Referencia,
                        Devuelto = p.Devuelto,
                        FechaDevolucion = p.FechaDevolucion,
                        MotivoDevolucion = p.MotivoDevolucion
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (comanda is null)
        {
            return new ComandaEstadoViewModel { IdComanda = idComanda };
        }

        var pagado = comanda.Pagos.Where(p => !p.Devuelto).Sum(p => p.Importe);
        comanda.Saldo = Redondear(comanda.Total - pagado);
        comanda.EsAdministrador = User.IsInRole("Administrador");
        comanda.PuedeCancelar = comanda.Estado == EstadosComanda.ABIERTA;
        return comanda;
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

    private static decimal Redondear(decimal valor)
    {
        return Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }

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