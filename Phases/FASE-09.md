# FASE 9 — CANCELACIONES Y DEVOLUCIONES (Especificación)

> Documento de especificación de la Fase 9.
> NO implementa nada. NO modifica C#, Razor, JavaScript ni configuración.
> NO build. NO ejecución de aplicación. NO consulta a MySQL. NO migraciones. NO commit.
> Refleja el estado físico real inspeccionado en el repositorio al momento de escribir.

- Commit base esperado: `ed765b6 Atlas Restaurant POS - Fase 8 Mesas y Salon`
- Working tree al inicio: limpio.
- Solución: `AtlasRestaurantPOS.slnx`
- Proyecto web: `AtlasRestaurantPOS.Web` (.NET 8, MVC, EF Core 8, Pomelo MySQL)

---

## 1. Objetivo

Implementar el flujo operativo de **cancelación de comandas** y **devolución de pagos**,
cerrando el ciclo de corrección de ventas del POS:

```
Venta → error/arrepentimiento → cancelar comanda (liberando mesa si aplica) → devolver pagos → impacto correcto en caja y auditoría
```

Esto convierte al POS en un sistema de restaurante operable en condiciones reales,
donde las equivocaciones del cajero, los clientes que no pagan o las correcciones
de cobro deben quedar registradas, trazables y sin romper el arqueo de caja.

## 2. Problema que resuelve

Estado actual verificado (lectura directa de código):

- `Constants/EstadosComanda.cs` define `CANCELADA`, pero **ningún controlador/servicio/vista la usa**.
- `VentasController.RegistrarPago` solo permite pasar a `CERRADA`; no existe operación inversa.
- No existe ninguna entidad ni columna que registre devoluciones de pagos.
- `CajaOperacionController.CalcularTotalesAsync` suma pagos `EFECTIVO` sin distinguir devueltos:
  una devolución sin soporte físico inflaría el efectivo esperado del cierre.
- Sin cancelación: una comanda abierta por error (producto equivocado, cliente que se retira,
  comanda duplicada) queda "colgada" o debe cerrarse artificialmente; en mesas, la mesa queda
  OCUPADA sin forma legítima de liberarse (Salón solo libera mesa al cobrar).
- Sin devolución: un pago erróneo no puede revertirse; el dinero contado en caja no cuadra
  con lo esperado y no hay forma de corregirlo de manera trazable.

## 3. Estado físico actual relacionado

Hechos verificados por lectura directa; no inferencias.

### Modelos

`Models/Comanda.cs`:

- `IdComanda` (long, PK), `IdSucursal`, `IdCaja`, `IdSesionCaja?`, `IdMesa?`, `IdUsuario`
- `FechaApertura`, `FechaCierre?`, `Estado` (string), `Folio?`
- `Subtotal`, `Impuestos`, `Descuento`, `Total` (decimal(18,2))
- NO tiene: `FechaCancelacion`, `MotivoCancelacion`, `IdUsuarioCancelacion`.

`Models/Pago.cs`:

- `IdPago`, `IdComanda`, `IdMetodoPago?`, `IdCaja?`, `IdSesionCaja?`, `IdUsuario?`
- `MetodoPago` (string), `Importe`, `FechaPago`, `Referencia?`, `ProveedorExterno?`, `IdTransaccionExterna?`
- NO tiene: `Devuelto`, `FechaDevolucion`, `IdUsuarioDevolucion`, `MotivoDevolucion`.

`Models/MovimientoCaja.cs`:

- `IdMovimientoCaja`, `IdCaja`, `IdSesionCaja?`, `IdUsuario`, `Tipo` (string),
  `Importe`, `Concepto?`, `FechaMovimiento`, `ReferenciaExterna?`
- Tipos existentes en `Constants/TiposMovimientoCaja.cs`: `ENTRADA`, `SALIDA`, `VENTA`, `RETIRO`, `DEPOSITO`, `AJUSTE`.
- NO existe tipo `DEVOLUCION` (se puede añadir como constante, sin migración).

### Constantes

`Constants/EstadosComanda.cs` (ya existen las 4):

- `ABIERTA`, `EN_PROCESO`, `CERRADA`, `CANCELADA` ← `CANCELADA` está definida y **sin uso**.

### Controladores

`Controllers/VentasController.cs` (913 líneas):

- `NuevaComanda`, `Estado`, `AgregarProducto`, `ModificarCantidad`, `QuitarDetalle`, `RegistrarPago`.
- `ObtenerComandaOperativaAsync`: `SELECT ... FOR UPDATE` + validación de ownership
  (`IdSucursal + IdCaja + IdSesionCaja + IdUsuario`).
- `RegistrarPago`: bloquea comanda con `FOR UPDATE`, valida ownership y sesión, calcula saldo,
  aplica pago (con `PermiteCambio`), cierra comanda al saldo 0 y **libera la mesa** con
  `FOR UPDATE` + auditoría `LIBERAR_MESA`.

`Controllers/SalonController.cs` (453 líneas):

- `AbrirMesa`, `Reanudar`, `TransferirMesa` (locks deterministas menor → mayor).
- La mesa solo se libera desde `RegistrarPago` (cierre por cobro) o por transferencia.

`Controllers/CajaOperacionController.cs` (671 líneas):

- `CalcularTotalesAsync`: entradas = `ENTRADA`; retiros = `RETIRO|SALIDA`;
  ventasEfectivo = suma de `Pagos` con `MetodoPago == "EFECTIVO"` de la sesión.
  `EfectivoEsperado = FondoInicial + Entradas + VentasEfectivo - Retiros`.
- `Cerrar`: persiste `MontoCierre` y `Diferencia` en auditoría `CERRAR_CAJA`.

### Auditoría

`Services/Auditoria/AuditoriaService.cs` (`IAuditoriaService`) — registros con
`Entidad`, `IdEntidad`, `Accion`, `DatosAnteriores`, `DatosNuevos`, `Ip`, `Fecha`,
y contexto `Empresa/Sucursal/Caja/SesionCaja/Usuario` desde claims.

### Navegación y vistas

- `Views/Ventas/Index.cshtml`: pantalla única de venta (mostrador y mesa).
- `Views/Salon/Index.cshtml`: tarjetas de mesa; acciones Abrir / Abrir cuenta / Transferir.
- `Views/CajaOperacion/*`: selección, panel, movimientos, cierre.
- `_Layout.cshtml`: Home, Operación de Caja, Punto de Venta, Salón / Mesas, Administración (rol Administrador).

## 4. Alcance

- Cancelar una comanda ABIERTA de la propia sesión (mostrador o mesa), con motivo obligatorio.
- Liberar la mesa al cancelar una comanda de mesa (siempre que no tenga pagos; ver reglas).
- Devolver pagos de una comanda CERRADA o CANCELADA de la propia sesión (o con regla de empresa), con motivo y autorización.
- Registrar la devolución de forma física (campo en Pago) y su impacto en el efectivo esperado de caja.
- Registrar movimiento de caja de tipo `DEVOLUCION` (constante nueva, sin migración) cuando corresponda.
- Auditoría completa de cada acción (`CANCELAR_COMANDA`, `DEVOLVER_PAGO`, `LIBERAR_MESA_POR_CANCELACION`).
- Regresión completa de Ventas, Salón, Caja y Mesas.

## 5. Fuera de alcance

- No crear entidad `Salon` (pendiente de evaluación futura, igual que Fase 8).
- No implementar KDS/cocina, inventario/recetas, periféricos reales, integraciones/APIs,
  reportes avanzados ni permisos por acción (se documentan como alternativas postergadas).
- No corregir la deuda técnica registrada en `PROJECT_STATE.md` (patrón `Buscar?termino=<id>`,
  instancias repetidas de `bootstrap.Modal`, incidencia de password del admin).
- No refactorizaciones generales ni cambios de patrón (no Repository/UnitOfWork).
- No eliminar físicamente filas de `Comandas`, `Pagos` ni `MovimientosCaja` (solo marcas de estado).

## 6. Arquitectura

Reutilizar el patrón ya validado en el proyecto:

- ASP.NET Core MVC + Fetch API + Bootstrap, JavaScript embebido.
- Controladores `[Authorize]` con claims (`IdCaja`, `IdSesionCaja`, `IdSucursal`, `IdEmpresa`, `NameIdentifier`).
- Servicios con inyección de dependencias: `IAuditoriaService`; en esta fase NO se requiere
  servicio nuevo obligatorio (la lógica puede vivir en `VentasController`/`CajaOperacionController`),
  salvo que se opte por un `IDevolucionService` para centralizar reglas (decisión de implementación).
- Transacciones `IsolationLevel.ReadCommitted` + `SELECT ... FOR UPDATE` para bloqueos.
- Auditoría con `IAuditoriaService` existente (no crear sistema paralelo).

## 7. Modelos involucrados

- `Comanda` (estado `CANCELADA`, campos de cancelación).
- `Pago` (marca de devolución y trazabilidad de devolución).
- `MovimientoCaja` (tipo `DEVOLUCION` para efectivo devuelto).
- `Mesa` (liberación al cancelar comanda de mesa).
- `SesionCaja`, `Caja`, `Sucursal`, `Empresa`, `Usuario` (contexto de trazabilidad).
- `Auditoria` (registro de acciones).

## 8. Controladores / endpoints requeridos

`VentasController` (o controlador dedicado si así se decide en implementación):

- `POST /Ventas/CancelarComanda` — `[FromBody] { idComanda, motivo }`, antiforgery.
  - Valida ownership + estado ABIERTA + motivo obligatorio.
  - Bloquea comanda `FOR UPDATE`; si tiene pagos, rechaza o exige flujo de devolución previo (regla de negocio a definir; ver sección 13).
  - Cambia estado a `CANCELADA`, registra `FechaCancelacion`, `MotivoCancelacion`, `IdUsuarioCancelacion`.
  - Si `IdMesa` no nulo: mesa → `DISPONIBLE` con `FOR UPDATE` + auditoría.
  - Auditoría `CANCELAR_COMANDA`.
- `POST /Ventas/DevolverPago` — `[FromBody] { idPago, motivo }`, antiforgery.
  - Valida ownership del pago (sesión/caja/sucursal/usuario o regla de empresa) + motivo obligatorio.
  - Marca `Devuelto = true`, `FechaDevolucion`, `IdUsuarioDevolucion`, `MotivoDevolucion`.
  - Registra `MovimientoCaja` tipo `DEVOLUCION` (si el pago fue EFECTIVO; para otros métodos,
    registrar devolución sin movimiento de efectivo — regla a definir).
  - Auditoría `DEVOLVER_PAGO`.
- `GET /Ventas/ObtenerComandaParaGestion` (opcional, para consultar pagos devueltos en la vista).

`CajaOperacionController`:

- `CalcularTotalesAsync`: restar de `VentasEfectivo` los pagos `EFECTIVO` marcados `Devuelto = true`
  (cambio de código mínimo, sin migración).
- El panel de caja puede listar devoluciones como movimientos tipo `DEVOLUCION`.

## 9. Servicios requeridos

- `IAuditoriaService` (existente).
- `IFolioComandaService` (existente, no aplica a cancelaciones).
- Opcional: `IDevolucionService` para centralizar reglas de devolución si la implementación
  lo considera necesario; NO obligatorio.

## 10. ViewModels

`Models/ViewModels/VentasViewModels.cs` (añadir, top-level):

- `CancelarComandaViewModel { long IdComanda; string Motivo; }`
- `DevolverPagoViewModel { long IdPago; string Motivo; }`
- Ampliar `ComandaEstadoViewModel` con `Pagados`/`Devueltos` o lista de pagos con `Devuelto`
  para que la vista muestre pagos devueltos.
- No usar entidades EF directamente como payload.

## 11. Vistas / UI

- `Views/Ventas/Index.cshtml`: botón "Cancelar comanda" (solo estado ABIERTA y sin pagos,
  o con confirmación que advierta de la regla), modal con motivo obligatorio;
  lista de pagos con indicador "Devuelto" y botón "Devolver" por pago (con confirmación y motivo).
- `Views/Salon/Index.cshtml`: sin cambios funcionales obligatorios; la mesa se libera al cancelar
  (la tarjeta volverá a DISPONIBLE al recargar).
- `Views/CajaOperacion/Index.cshtml`: listar movimientos `DEVOLUCION` en el panel (los movimientos
  ya se listan por tipo; el tipo nuevo aparece automáticamente).

## 12. Navegación

- Sin cambios en `_Layout.cshtml` obligatorios: las acciones viven dentro de Ventas.
- La cancelación de mesa puede invocarse desde Salón (botón en tarjeta OCUPADA propia) si se
  decide en implementación; opcional.

## 13. Reglas de negocio

- Solo se cancela una comanda `ABIERTA` de la propia sesión (`IdCaja + IdSesionCaja + IdUsuario + IdSucursal`).
- Motivo de cancelación obligatorio (mín. 5 caracteres, máx. 500).
- **Regla a decidir**: cancelación de comanda con pagos ya registrados.
  Opción A (recomendada): rechazar cancelación si hay pagos; primero devolver pagos y luego cancelar.
  Opción B: cancelar y devolver en la misma operación. La especificación recomienda A por simplicidad
  y trazabilidad; requiere aprobación del usuario.
- Devolución solo sobre pagos NO devueltos aún, de comanda CERRADA o CANCELADA (no devolver pagos
  de comanda ABIERTA), con motivo obligatorio.
- Devolución de pago `EFECTIVO`: genera `MovimientoCaja` tipo `DEVOLUCION` (reduce efectivo esperado).
- Devolución de otros métodos (TARJETA, etc.): marca `Devuelto` sin movimiento de efectivo
  (la reversión bancaria es externa); regla a confirmar con el usuario.
- No se eliminan físicamente filas; todo es marca de estado + auditoría.
- El total de la comanda CANCELADA permanece histórico (no se alteran `Subtotal/Impuestos/Total`).

## 14. Ownership / multisucursal / multicaja

- Toda operación valida claims server-side: `IdEmpresa → IdSucursal → IdCaja → IdSesionCaja → IdUsuario`.
- NO confiar en IDs del navegador (mismo criterio que Fases 5–8).
- La devolución queda registrada con la sesión/caja del usuario que la ejecuta.

## 15. Autorización

- `VentasController` ya es `[Authorize]`; las nuevas acciones heredan la misma protección.
- Para devoluciones podría exigirse rol (p. ej. solo Administrador o rol con permiso),
  decisión a aprobar; por defecto se propone: cancelar = usuario con sesión activa;
  devolver = usuario con sesión activa (regla de empresa opcional).
- Navegación sin sesión → 302; Fetch sin sesión → 401; Fetch sin permiso → 403.

## 16. Antiforgery

- Todo POST nuevo con `[ValidateAntiForgeryToken]` y token enviado desde la vista
  (`RequestVerificationToken`), igual que el resto del proyecto.

## 17. Concurrencia y transacciones

- `CancelarComanda`: transacción `ReadCommitted`; bloqueo `SELECT ... FOR UPDATE` de la comanda;
  si aplica liberación de mesa, bloqueo `FOR UPDATE` de la mesa; commit/rollback explícito.
- `DevolverPago`: transacción `ReadCommitted`; bloqueo `FOR UPDATE` del pago y de la comanda
  (para recalcular consistencia); insert de `MovimientoCaja` en la misma transacción.
- No usar `lock`/static/flags en memoria.
- Si dos devoluciones simultáneas sobre el mismo pago: la segunda debe fallar (pago ya devuelto).

## 18. Auditoría

- `CANCELAR_COMANDA` (Comanda): snapshot con `IdComanda, Folio, EstadoAnterior, EstadoNuevo, Motivo, IdMesa?`.
- `LIBERAR_MESA_POR_CANCELACION` (Mesa) si aplica: `IdMesa, IdComanda, Folio, EstadoAnterior → DISPONIBLE`.
- `DEVOLVER_PAGO` (Pago): snapshot con `IdPago, IdComanda, Importe, MetodoPago, Devuelto, Motivo`.
- `DEVOLUCION_CAJA` (MovimientoCaja): `IdMovimientoCaja, Importe, Concepto`.
- NUNCA auditar: Password, PasswordHash, tokens, secretos, ConnectionStrings.

## 19. Manejo de errores

- Cuerpos `[FromBody]` nulos → `JsonError` (sin NullReference), patrón FIX7.
- `ModelState` validado; motivos normalizados (Trim).
- Errores de negocio → `{ ok = false, mensaje }` (HTTP 200 con JSON), igual que el resto.
- Excepciones inesperadas → `LogError` + `JsonError` genérico, sin fuga de detalles.
- Fetch 401/403 manejados en el frontend (redirect/toast), patrón existente.

## 20. Impacto sobre Ventas

- `RegistrarPago` NO cambia su lógica de cobro.
- Nuevas acciones `CancelarComanda`/`DevolverPago` conviven con el flujo actual.
- `CalcularTotalesAsync` del panel de caja debe restar pagos devueltos (cambio de código mínimo).
- El estado `CANCELADA` debe verse en la vista de gestión (si existe) y no permitir más pagos.

## 21. Impacto sobre Caja

- `EfectivoEsperado` ya no inflado: los pagos EFECTIVO devueltos se restan.
- Movimientos `DEVOLUCION` aparecen en el panel y en el cierre (diferencia correcta).
- La lógica de retiro excedente (`Retiro`) no cambia.

## 22. Impacto sobre Mesas / Salón

- Cancelar una comanda de mesa libera la mesa (`DISPONIBLE`) de forma legítima.
- Salón muestra la mesa DISPONIBLE tras recargar; no queda mesa "colgada" en OCUPADA.
- No se altera `TransferirMesa` ni `AbrirMesa`.

## 23. Compatibilidad / regresiones

- Todos los flujos existentes (Ventas mostrador, Ventas mesa, Salón, Caja, CRUD Mesas,
  Catálogo comercial) deben seguir funcionando sin cambios de comportamiento.
- Batería de regresión sobre Fases 6–8 al finalizar.

## 24. Schema / migraciones

Con el schema actual NO es suficiente para una implementación trazable:

### Qué falta (documentado, NO creado en esta tarea)

1. `Pagos`: columnas nuevas para devolución:
   - `Devuelto` (bool, NOT NULL, default false)
   - `FechaDevolucion` (datetime(6), NULL)
   - `IdUsuarioDevolucion` (int, NULL, FK → Usuarios Restrict)
   - `MotivoDevolucion` (varchar(500), NULL)
2. `Comandas`: columnas nuevas para cancelación (alternativa: registrar solo vía auditoría):
   - `FechaCancelacion` (datetime(6), NULL)
   - `MotivoCancelacion` (varchar(500), NULL)
   - `IdUsuarioCancelacion` (int, NULL, FK → Usuarios Restrict)
3. Constante nueva SIN migración: `TiposMovimientoCaja.DEVOLUCION = "DEVOLUCION"`.

### Migración probable

`AgregarDevoluciones` (única, al implementar):

- `AddColumn` en `Pagos`: `Devuelto`, `FechaDevolucion`, `IdUsuarioDevolucion`, `MotivoDevolucion`.
- `AddColumn` en `Comandas`: `FechaCancelacion`, `MotivoCancelacion`, `IdUsuarioCancelacion`.
- `AddForeignKey` de `Pagos.IdUsuarioDevolucion → Usuarios` (Restrict) y
  `Comandas.IdUsuarioCancelacion → Usuarios` (Restrict).
- Índice opcional: `IX_Pagos_IdComanda_Devuelto` para consultas de devoluciones.

### Impacto si NO se hace migración

- Sin `Pagos.Devuelto` no se puede distinguir un pago devuelto de uno vigente:
  el efectivo esperado del cierre seguiría contando dinero devuelto → arqueo incorrecto.
- Sin `Comandas.FechaCancelacion/Motivo` la trazabilidad de cancelaciones dependería
  únicamente de auditoría (aceptable, pero menos consultable).

## 25. Seguridad

- Sin secretos en código, vistas, logs, auditoría ni claims.
- Sin `DELETE` físico: solo marcas de estado.
- Validación estricta de ownership en cada operación.
- Sin endpoints que expongan datos sensibles.
- Sin operaciones destructivas ni `TRUNCATE`.

## 26. Batería de pruebas funcionales (grupos A–K)

Cada prueba debe tener resultado verificable (PASS/FAIL) en la evidencia.

- **A. Autorización y sesión**: Fetch anónimo → 401; Fetch usuario sin caja → error JSON;
  navegación sin sesión → redirect. POST sin token antiforgery → 400.
- **B. Cancelar comanda mostrador (sin pagos)**: abrir comanda, agregar producto, cancelar
  con motivo → estado `CANCELADA`, `FechaCancelacion`/`Motivo`/`IdUsuarioCancelacion` físicos,
  auditoría `CANCELAR_COMANDA`. Intentar pagar comanda CANCELADA → rechazado.
- **C. Cancelar con motivo vacío/corto** → rechazado con mensaje.
- **D. Cancelar comanda AJENA / de otra caja / otra sesión** → rechazado (ownership).
- **E. Cancelar comanda CERRADA** → rechazado (solo ABIERTA).
- **F. Cancelar comanda de mesa**: mesa OCUPADA → cancelar → comanda CANCELADA y
  mesa `DISPONIBLE` (físico en `Mesas.Estado` + auditoría `LIBERAR_MESA_POR_CANCELACION`).
- **G. Devolución de pago**: comanda CERRADA con pago EFECTIVO → devolver con motivo →
  `Pagos.Devuelto=true` + `MovimientoCaja` tipo `DEVOLUCION` + auditoría `DEVOLVER_PAGO`;
  `CalcularTotalesAsync` reduce ventasEfectivo.
- **H. Devolución duplicada**: devolver el mismo pago dos veces → segunda rechazada.
- **I. Devolución de pago ajeno / sesión ajena** → rechazado.
- **J. Devolución sin motivo / motivo corto** → rechazado.
- **K. Regresión**: flujo normal de venta (agregar, modificar, quitar, pagar, cerrar),
  apertura/transferencia de mesa, cierre de caja con devolución → diferencia correcta;
  auditoría sin secretos; snapshots válidos.

## 27. Pruebas de concurrencia (cuando correspondan)

- **Cancelación concurrente de la misma comanda** (2 solicitudes simultáneas):
  exactamente 1 éxito; la segunda falla (comanda ya no ABIERTA). Verificación física: 1 fila
  con `CANCELADA`; sin excepciones 500.
- **Devolución concurrente del mismo pago** (2 solicitudes simultáneas):
  exactamente 1 éxito; la segunda falla (pago ya devuelto). Verificación física: 1 movimiento
  `DEVOLUCION`; 1 marca `Devuelto=true`.
- **Cancelación concurrente + apertura concurrente de la misma mesa**:
  la mesa no puede quedar en estado incoherente (una de las dos operaciones gana; la otra falla).
- Patrón: transacción + `SELECT ... FOR UPDATE` (validado en Fases 5–8); NO `lock` en memoria.

## 28. Evidencia persistente requerida

- `C:\Users\Admin\AppData\Local\Temp\opencode\fase9\resultados.txt` con cada prueba
  (grupo, nombre, PASS/FAIL, detalle).
- Volcado físico de DB (sin secretos) con: comandas CANCELADAS, pagos devueltos,
  movimientos `DEVOLUCION`, estados de mesas, auditorías de las acciones nuevas.
- Servidor iniciado con `AgentInstructions/SERVER_RUNNER.md` (PID + READY + cleanup);
  puerto 5250; usuarios de prueba de evidencia previa (p. ej. `usuario_fix_1`,
  `usuario_f7_noadmin`) o los que la implementación requiera; sin secretos en evidencia.

## 29. Cleanup

- Detener el servidor de prueba (PID exacto) y verificar puerto 5250 libre.
- Sin procesos vivos; sin código temporal en el repo; sin cookies/logs en el repo.
- Cerrar sesiones de caja de prueba al finalizar (302 OK).
- `git status` final limpio (o únicamente los cambios legítimos de la fase).

## 30. Criterio exacto para declarar FASE 9 COMPLETADA

Se declara COMPLETADA Y VERIFICADA únicamente cuando TODAS las siguientes condiciones
se cumplen físicamente:

1. Build `dotnet build .\AtlasRestaurantPOS.slnx` → **0 warnings / 0 errors**.
2. Batería A–K completa → **0 FAIL** (todas PASS, con detalle verificable).
3. Verificación física DB:
   - existe al menos 1 comanda con `Estado = CANCELADA` y campos de cancelación llenos;
   - existe al menos 1 pago con `Devuelto = true` y campos de devolución llenos;
   - existe al menos 1 `MovimientoCaja` tipo `DEVOLUCION` vinculado a la devolución;
   - la mesa de una comanda cancelada quedó `DISPONIBLE`;
   - `CalcularTotalesAsync` excluye pagos devueltos (arqueo correcto con devolución).
4. Auditorías presentes: `CANCELAR_COMANDA`, `DEVOLVER_PAGO`,
   `LIBERAR_MESA_POR_CANCELACION` (si aplica); **0 auditorías con secretos**.
5. Concurrencia verificada: cancelación y devolución concurrentes → exactamente 1 éxito cada una.
6. Regresión Fases 6–8 sin fallos (Ventas mostrador/mesa, Salón, Caja, CRUD Mesas).
7. Evidencia persistente en `fase9\resultados.txt` y volcado DB sin secretos.
8. Cleanup completo (servidor detenido, puerto libre, sesiones de caja cerradas).
9. Migración (si se autoriza) inspeccionada: `Up`/`Down`, FK, índices, nulabilidad,
   precisión decimal, sin operaciones destructivas.
10. Incidencias y decisiones pendientes registradas en `PROJECT_STATE.md` al cerrar.

---

## Comparación de alternativas y justificación

Se evaluaron los siguientes bloques para Fase 9:

| Alternativa | Veredicto | Justificación |
|---|---|---|
| **Cancelaciones y devoluciones** | **SELECCIONADA** | Cierra el ciclo de corrección de ventas; `CANCELADA` ya existe sin uso; sin esto una comanda errónea queda colgada, una mesa no se puede liberar sin cobro y un pago erróneo rompe el arqueo de caja. Es el siguiente bloque operativo con mayor coherencia. |
| Cocina / KDS | Postergada | Requiere entidades nuevas grandes (estaciones, estados de preparación) y depende de dispositivos/periféricos; AGENTS.md exige dominio desacoplado del hardware. Bloque grande; no es el siguiente paso mínimo. |
| Inventario / recetas | Postergada | Requiere entidades nuevas (insumos, recetas, movimientos de stock) y migración amplia; debe ir después de consolidar ventas y cancelaciones (el stock debe descontar cancelaciones también). |
| Reportes / dashboard | Postergada | Los reportes serían incompletos/incorrectos sin cancelaciones y devoluciones (ventas brutas vs. netas). Mejor después de esta fase. |
| Configuración POS / Dispositivos / Integraciones | Postergada | Son módulos de configuración (clave-valor, CRUD lógico), no un flujo operativo; la tarea indica no elegir una fase solo porque la entidad existe. |
| Permisos avanzados por acción | Postergada | Requiere schema nuevo (tabla de permisos) y diseño de autorización; puede combinarse con fases futuras de administración. |

## Decisiones que requieren aprobación del usuario

1. **Cancelación de comanda con pagos**: ¿rechazar hasta devolver pagos (Opción A, recomendada)
   o permitir cancelar + devolver en una sola operación (Opción B)?
2. **Devolución de métodos distintos a EFECTIVO**: ¿marcar devuelto sin movimiento de caja
   (recomendado, reversión externa) o exigir también un movimiento de caja?
3. **Autorización de devoluciones**: ¿cualquier usuario con sesión activa o restricción por rol?
4. **Migración**: ¿autorizar la migración `AgregarDevoluciones` al implementar la fase,
   o aceptar trazabilidad solo vía auditoría (sin columnas nuevas)?
5. **Campo de cancelación en Comandas**: ¿columnas físicas o solo auditoría?

## Archivos inspeccionados (verificación física)

- `Controllers/VentasController.cs`, `Controllers/SalonController.cs`, `Controllers/CajaOperacionController.cs`
- `Models/Comanda.cs`, `Models/Pago.cs`, `Models/MovimientoCaja.cs`, `Models/SesionCaja.cs`,
  `Models/ComandaDetalle.cs`, `Models/Producto.cs`, `Models/Empresa.cs`, `Models/Impuesto.cs`,
  `Models/Auditoria.cs`, `Models/ConfiguracionPos.cs`, `Models/Dispositivo.cs`, `Models/IntegracionExterna.cs`
- `Models/ViewModels/VentasViewModels.cs`, `Models/ViewModels/CajaOperacionViewModels.cs`
- `Constants/EstadosComanda.cs`, `Constants/TiposMovimientoCaja.cs`, `Constants/TiposDispositivo.cs`, `Constants/TiposIntegracion.cs`
- `Data/AtlasRestaurantDbContext.cs`, `Migrations/20260815192554_InitialRestaurantCore.cs`,
  `Migrations/20260815193340_OperationalInfrastructure.cs`
- `Services/Bootstrap/InitialSetupService.cs`, `Services/Auditoria/AuditoriaService.cs`,
  `Services/Comanda/FolioComandaService.cs`
- `Views/Home/Index.cshtml`, `Views/Shared/_Layout.cshtml`, `Views/Ventas/Index.cshtml`, `Views/Salon/Index.cshtml`
- `Phases/PROJECT_STATE.md`, `Phases/FASE-08.md`

## Discrepancias / limitaciones

- `EstadosComanda.CANCELADA` definida y sin uso en todo el código (verificado por grep):
  el modelo anticipó la cancelación, pero no hay implementación.
- No existe ninguna referencia a devoluciones en el código (grep `DEVOLUCION|Devolucion|Devolución`
  solo encuentra botones "Cancelar" de modales y la constante `CANCELADA`).
- `CalcularTotalesAsync` no contempla devoluciones: el cambio es de código (no de schema).
- La migración necesaria se documenta pero NO se crea en esta tarea (prohibido por la especificación).
- La incidencia del password del admin (`PROJECT_STATE.md`) permanece NO bloqueante y NO se toca.