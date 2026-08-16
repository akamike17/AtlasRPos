# FASE 8 — MESAS Y SALÓN (Especificación)

> Documento de especificación de la Fase 8.
> NO implementa nada. NO modifica C#, Razor, JavaScript ni configuración.
> NO build. NO ejecución de aplicación. NO consulta a MySQL. NO migraciones. NO commit.
> Refleja el estado físico real inspeccionado en el repositorio al momento de escribir.

- Commit base esperado: `655bbca Atlas Restaurant POS - Fase 7 Catalogo comercial`
- Working tree al inicio: limpio.
- Solución: `AtlasRestaurantPOS.slnx`
- Proyecto web: `AtlasRestaurantPOS.Web` (.NET 8, MVC, EF Core 8, Pomelo MySQL)

---

## Objetivo funcional

Implementar operación real de restaurante basada en mesas:

`Caja abierta → Salón/Mesas → abrir mesa → crear comanda asociada → operar venta → cobrar → liberar mesa`

Además:

- administración de mesas;
- visualización operacional de mesas (Salón);
- reanudación de comanda de mesa;
- transferencia segura de una comanda entre mesas;
- protección concurrente para que dos usuarios no ocupen la misma mesa;
- sincronización Mesa ↔ Comanda;
- auditoría;
- regresión completa de Ventas y Caja.

## Alcance importante

El modelo actual tiene entidad `Mesa`, pero NO existe entidad `Salon`.

Por lo tanto:

- NO crear entidad `Salon`.
- NO crear migración solo para agrupar mesas visualmente por salón.
- En esta fase, “Salón” significa la pantalla operacional de las mesas de la Sucursal actual.
- Una entidad física `Salon` podrá evaluarse posteriormente si el producto la requiere.

---

## Estado físico real inspeccionado (base de esta especificación)

Hechos verificados por lectura directa de archivos; no inferencias.

### Modelos

`Models/Mesa.cs`:

- `IdMesa` (int, PK)
- `IdSucursal` (int, FK → Sucursal, Restrict)
- `Nombre` (string, requerido, máx 150)
- `Capacidad` (int)
- `Estado` (string, requerido, máx 50)
- `Activo` (bool)
- Navegación: `Sucursal`, `Comandas`.

`Models/Comanda.cs`:

- `IdComanda` (long, PK)
- `IdSucursal` (int, FK → Sucursal, Restrict)
- `IdCaja` (int, FK → Caja, Restrict)
- `IdSesionCaja` (long?, FK → SesionCaja, Restrict)
- `IdMesa` (int?, FK → Mesa, Restrict)
- `IdUsuario` (int, FK → Usuario, Restrict)
- `FechaApertura`, `FechaCierre?`, `Estado`, `Folio?`
- `Subtotal`, `Impuestos`, `Descuento`, `Total` (decimal(18,2))

`Models/Sucursal.cs`:

- `IdSucursal`, `IdEmpresa`, `Nombre`, `Direccion?`, `Telefono?`, `Activo`, `FechaCreacion`
- Navegación: `Empresa`, `Cajas`, `Mesas`, `Comandas`, `FoliosSecuencia`, entre otras.

`Models/Caja.cs`:

- `IdCaja`, `IdSucursal`, `Nombre`, `Codigo`, `Descripcion?`, `Activo`, `FechaCreacion`
- Índice único `(IdSucursal, Codigo)`.

`Models/SesionCaja.cs`:

- `IdSesionCaja` (long), `IdCaja`, `IdUsuarioApertura`, `IdUsuarioCierre?`, `FechaApertura`, `FechaCierre?`, `FondoInicial`, `MontoCierre?`, `Estado`, `Observaciones?`.

### Constantes

`Constants/EstadosMesa.cs` (existen las 4):

- `DISPONIBLE`
- `OCUPADA`
- `RESERVADA`
- `FUERA_SERVICIO`

`Constants/EstadosComanda.cs` (existen las 4):

- `ABIERTA`
- `EN_PROCESO`
- `CERRADA`
- `CANCELADA`

### DbContext (Data/AtlasRestaurantDbContext.cs)

- `DbSet<Mesa> Mesas` ya existe.
- `ConfigureMesa`: PK `IdMesa`; `Nombre` requerido máx 150; `Estado` requerido máx 50; **índice único `(IdSucursal, Nombre)`**; `Comandas` con FK `IdMesa` y `DeleteBehavior.Restrict`.
- `ConfigureComanda`: índice único `(IdSucursal, Folio)`; precisión decimal(18,2) en totales.
- Collation de negocio: `utf8mb4_unicode_ci`.

### Uso actual de `Mesa`

`grep Mesa|IdMesa` sobre `*.cs`:

- `Mesa.Estado` NO se lee ni escribe en ningún controlador/servicio hoy.
- Solo aparece en: modelo, constantes, DbContext, migraciones y snapshot.
- No existe lógica de sincronización Mesa ↔ Comanda. Esa lógica es nueva en Fase 8.

### Ventas POS (Controllers/VentasController.cs)

- `NuevaComanda` (POST + antiforgery): crea comanda con todos los datos server-side (claims + DB); NO recibe `IdMesa`; usa `GenerarFolioAsync` con `SELECT ... FOR UPDATE` sobre `FoliosSecuencia` (TipoDocumento `COMANDA`); transacción `ReadCommitted`.
- `ObtenerComandaOperativaAsync`: carga comanda con `FOR UPDATE` y valida ownership `IdSucursal + IdCaja + IdSesionCaja + IdUsuario`.
- `RegistrarPago`: bloquea comanda `FOR UPDATE`; valida ownership; pagos parciales; cuando saldo <= 0 marca `CERRADA` + `FechaCierre`; auditoría `REGISTRAR_PAGO` y `CERRAR_COMANDA`.
- `ValidarSesionCajaAsync`: caja activa + sucursal activa + empresa activa + sesión `ABIERTA` + `IdUsuarioApertura == idUsuario`.
- `ConstruirEstadoAsync`: devuelve `ComandaEstadoViewModel` (sin datos de Mesa).

### Ventas UI (Views/Ventas/Index.cshtml)

- No muestra mesa.
- `ComandaEstadoViewModel` (Models/ViewModels/VentasViewModels.cs) no contiene nombre de mesa.
- `abrirCobrar` crea una nueva instancia `bootstrap.Modal` en cada apertura (deuda técnica F7 ítem 2). Aplica también a Fase 8 la regla de reutilización.

### Caja operativa (Controllers/CajaOperacionController.cs)

- Claims operativos: `IdCaja`, `IdSesionCaja`, `CajaNombre`, `CajaCodigo`.
- Redirección a `Seleccionar` cuando faltan claims o la sesión no es `ABIERTA`/propia.
- Apertura/reanudación/cierre con `FOR UPDATE` y auditoría.

### Administración (Controllers/CajasController.cs)

- Patrón CRUD vigente: `Index` + `Buscar(termino?)` + `Crear` + `Editar` + `Activar` + `Inactivar`.
- NO tiene `Obtener(id)` explícito: la edición recupera el registro vía `Buscar?termino=<id>` (deuda técnica F7 ítem 1).
- Fase 8 NO repite ese patrón en Mesas.

### Layout (Views/Shared/_Layout.cshtml)

- Navegación autenticada: Home, Operación de Caja, Punto de Venta.
- Dropdown “Administración” solo rol Administrador: Empresas, Sucursales, Roles, Usuarios, Cajas, divider, Categorías, Productos, Impuestos, Métodos de Pago.

### Auditoría (Services/Auditoria/IAuditoriaService.cs)

- Única firma: `RegistrarAsync(entidad, idEntidad, accion, datosAnteriores = null, datosNuevos = null)`.
- Snapshots sin secretos.

### Folios

- `FoliosSecuencia` por `(IdSucursal, TipoDocumento)`; tipo usado por Ventas: `COMANDA`.
- Mecanismo robustecido en `GenerarFolioAsync` (creación inicial concurrente + `FOR UPDATE`). No crear segundo algoritmo.

---

## 1. ADMINISTRACIÓN DE MESAS

Crear administración completa de `Mesa` para rol Administrador.

- Controller: `MesasController` (`[Authorize(Roles = "Administrador")]`).
- Vista: `Views/Mesas/Index.cshtml`.

Operaciones:

- `Index`
- `Buscar` (búsqueda por texto; igual patrón de Cajas: filtrar por `Nombre` y `Sucursal.Nombre` / `Empresa.Nombre`; incluir sucursal y empresa en el resultado)
- `Obtener(int id)` explícito (ver sección 19)
- `Crear`
- `Editar`
- `Activar`
- `Inactivar`

IMPORTANTE:

- NO repetir el patrón heredado `Buscar?termino=<id>` para recuperar un registro al editar.
- Crear endpoint explícito `Obtener(int id)`.
- La vista debe usar dicho endpoint.
- Este es el patrón nuevo preferido para CRUD futuros.

### Campos

Usar exclusivamente propiedades físicas existentes de `Mesa`:

- `Sucursal` (`IdSucursal`)
- `Nombre`
- `Capacidad`
- `Activo`

`Estado` NO debe ser libremente editable desde el CRUD administrativo. `Estado` es información operacional.

Al crear una Mesa nueva usar server-side: `EstadosMesa.DISPONIBLE`.

NO aceptar `Estado` desde el navegador (ni crear ni editar).

### Validaciones

- Sucursal existente y activa (`Sucursal.Activo == true`).
- Empresa asociada activa (`Sucursal.Empresa.Activo == true`).
- Nombre obligatorio, sin espacios iniciales/finales (patrón de Cajas).
- `Capacidad > 0`.
- Evitar duplicado según índice físico existente: `(IdSucursal, Nombre)` — validar a nivel de aplicación y respaldar con el índice único.
- Body nulo protegido (`[FromBody]` null → error controlado).
- NO Delete físico (solo `Activo`).

### Inactivación

- NO permitir inactivar Mesa si tiene una Comanda `ABIERTA` asociada (`Comanda.IdMesa == mesa.IdMesa && Estado == EstadosComanda.ABIERTA`).
- Mensaje claro.
- NO cerrar la comanda automáticamente.
- NO liberar Mesa automáticamente mediante la administración.

## 2. PANTALLA OPERACIONAL DE SALÓN

Crear:

- `Controllers/SalonController.cs`
- `Views/Salon/Index.cshtml`

Requisitos de acceso:

- Usuario autenticado (`[Authorize]`).
- NO limitar a Administrador.
- Contexto operacional válido:
  - claim `IdSucursal`;
  - claim `IdCaja`;
  - claim `IdSesionCaja`;
  - `SesionCaja` `ABIERTA` y propiedad (`IdUsuarioApertura == idUsuario`) — replicar validación equivalente a `ValidarSesionCajaAsync` de Ventas.
- Si falta caja/sesión: redirigir a `CajaOperacion/Seleccionar` para navegación normal.
- Fetch debe respetar 401/403 y respuestas controladas (no devolver HTML de Login a Fetch).

### Mostrar mesas

Mostrar exclusivamente mesas:

- `Activo == true`
- `IdSucursal` == claim `IdSucursal`

Cada tarjeta debe mostrar:

- Nombre
- Capacidad
- Estado operacional
- Folio de Comanda si existe una `ABIERTA` asociada
- Total actual si existe
- Usuario responsable cuando sea apropiado
- Acción permitida según estado (sección 13)

Estados visuales (constantes físicas existentes):

- `DISPONIBLE`
- `OCUPADA`
- `RESERVADA` (existe físicamente)
- `FUERA_SERVICIO` (existe físicamente)

NO inventar estado desde JavaScript. Servidor es fuente de verdad.

## 3. FUENTE DE VERDAD OPERACIONAL

Hecho físico: hoy `Mesa.Estado` no se usa en ninguna lógica operativa; no existe sincronización.

Regla deseada:

- Una Mesa con Comanda `ABIERTA` debe considerarse `OCUPADA`.
- NO confiar únicamente en `Mesa.Estado` si existe contradicción con una Comanda abierta.
- Evitar estados imposibles, p. ej.: `Mesa = DISPONIBLE` mientras existe `Comanda ABIERTA` con `IdMesa = Mesa`.
- Mantener `Mesa.Estado` sincronizada transaccionalmente cuando corresponda (abrir mesa, transferir, liberar al cerrar).

Interpretación operativa de la Mesa (lógica server-side, sin inventar campos):

- `OCUPADA` = tiene Comanda `ABIERTA` asociada (o el estado `Mesa.Estado` lo indica y hay coherencia).
- `DISPONIBLE` = sin Comanda `ABIERTA`.
- `RESERVADA` / `FUERA_SERVICIO` = estados existentes, se representan tal cual (sección 11).

## 4. ABRIR MESA

Desde Salón, usuario selecciona Mesa `DISPONIBLE`.

Debe crearse una Comanda asociada a esa Mesa (`Comanda.IdMesa = mesa.IdMesa`).

- NO duplicar la implementación crítica de creación de Comanda/folio.
- Preferencia arquitectónica: reutilizar o extraer mínimamente la lógica existente de `VentasController.NuevaComanda` / `GenerarFolioAsync` si es necesario.
- NO crear un segundo algoritmo independiente de FolioSecuencia.

### Datos server-side

Nunca aceptar desde navegador:

- `IdUsuario`
- `IdSucursal`
- `IdCaja`
- `IdSesionCaja`
- `Estado` de Comanda
- `Estado` de Mesa
- Totales
- Folio

El navegador únicamente identifica la Mesa elegida (p. ej. `idMesa`).

### Transacción

Abrir Mesa debe protegerse concurrentemente. Dentro de operación transaccional:

1. bloquear fila Mesa mediante mecanismo SQL apropiado (`SELECT ... FOR UPDATE`);
2. comprobar `Activo`;
3. comprobar `IdSucursal` == claim;
4. comprobar estado (`DISPONIBLE`; NO `OCUPADA`/`RESERVADA`/`FUERA_SERVICIO`);
5. comprobar que NO exista Comanda `ABIERTA` con `IdMesa` == Mesa;
6. generar folio mediante mecanismo ya robustecido (`GenerarFolioAsync` / `FoliosSecuencia`);
7. crear Comanda `ABIERTA` con `IdMesa`;
8. cambiar Mesa a `OCUPADA`;
9. guardar;
10. commit.

Dos solicitudes simultáneas para la misma Mesa: máximo una debe crear Comanda.

- NO usar `lock { }` C#.
- Patrón validado del proyecto: `SELECT ... FOR UPDATE` dentro de transacción.
- Validar contexto operativo (sesión de caja ABIERTA propia) dentro de la misma transacción.

Auditar: `ABRIR_MESA` (sección 12).

## 5. INTEGRACIÓN CON NUEVA COMANDA

Ventas POS debe continuar soportando:

- venta mostrador (`IdMesa = null`);
- venta asociada a Mesa.

- NO romper flujo Fase 6 / FIX7 / Fase 7.
- Si se modifica `NuevaComanda`, mantener compatibilidad con el flujo de mostrador existente.
- NO obligar a seleccionar Mesa para venta mostrador.

## 6. ENTRAR / REANUDAR COMANDA DE MESA

Si la Mesa tiene una Comanda `ABIERTA` perteneciente al contexto operativo permitido, permitir la acción “Abrir cuenta”.

- Debe llevar a Ventas POS cargando esa Comanda existente.
- NO crear una nueva comanda.
- Toda petición posterior sigue validando server-side: `IdSucursal`, `IdCaja`, `IdSesionCaja`, usuario/ownership conforme a reglas actuales.
- NO confiar en `idComanda` de querystring sin validarlo: el servidor debe validar que la comanda existe, está `ABIERTA`, pertenece a la Mesa elegida y pertenece al contexto operativo (patrón `ObtenerComandaOperativaAsync`).

Cambio mínimo esperado en Ventas:

- Permitir cargar una comanda existente en `Views/Ventas/Index.cshtml` (p. ej. endpoint/acción validada server-side que devuelva `ComandaEstadoViewModel` para la comanda indicada), manteniendo el flujo de mostrador intacto.
- `ComandaEstadoViewModel` deberá exponer, cuando exista, el nombre de la Mesa asociada (sección 14).

## 7. OWNERSHIP

Mantener el aislamiento vigente de Fase 6 / FIX7.

- Una Mesa ocupada por Comanda perteneciente a otra Caja / SesionCaja / Usuario NO puede ser apropiada silenciosamente.
- Mostrar `OCUPADA` y bloquear acción de edición/cobro si el contexto actual no es propietario.
- NO implementar todavía transferencia entre usuarios/cajas.
- NO permitir que Administrador “robe” una cuenta.
- Transferencia de Mesa solo aplica a la misma Comanda/contexto operacional autorizado.

## 8. TRANSFERIR COMANDA A OTRA MESA

Implementar transferencia entre Mesas dentro de la misma Sucursal.

Condiciones:

- Comanda `ABIERTA`.
- Comanda asociada a Mesa origen.
- Pertenecer al contexto operativo actual (ownership).
- Mesa destino `Activa`.
- misma Sucursal.
- Mesa destino `DISPONIBLE`.
- Mesa destino sin Comanda `ABIERTA`.

La transferencia debe ser transaccional.

Para prevenir deadlocks: si deben bloquearse dos Mesas, adquirir locks en orden determinista por `IdMesa` (menor → mayor).

Después (mismo commit):

- origen → `DISPONIBLE`;
- destino → `OCUPADA`;
- `Comanda.IdMesa` → destino.

NO crear nueva Comanda. NO cambiar folio. NO cambiar detalles. NO cambiar pagos.

Auditar transferencia (`TRANSFERIR_MESA`).

## 9. CIERRE DE COMANDA Y LIBERACIÓN DE MESA

Modificar únicamente lo necesario del flujo existente de pago/cierre (`VentasController.RegistrarPago`).

Cuando una Comanda asociada a Mesa pase definitivamente a `EstadosComanda.CERRADA`:

- la Mesa debe volver a `EstadosMesa.DISPONIBLE` dentro de una operación consistente (misma transacción).

Reglas:

- NO liberar Mesa con pago parcial.
- NO liberar Mesa mientras Comanda siga `ABIERTA`.
- NO liberar Mesa si la operación de cierre falla (rollback).
- Venta mostrador con `IdMesa == null`: sin cambios.

Auditar: `LIBERAR_MESA` al cerrar Comanda.

## 10. COMANDA CERRADA

- Una Comanda cerrada queda histórica.
- La Mesa puede posteriormente abrir una nueva Comanda diferente.
- La Mesa NO debe mantener referencia activa a la Comanda vieja aparte del historial relacional en `Comandas` (relación FK existente).

## 11. ESTADO RESERVADA / FUERA_SERVICIO

- NO implementar sistema formal de reservas todavía.
- NO existen datos suficientes para: cliente, hora, número de personas, duración, notas de reserva. NO inventar esos campos.
- Como `RESERVADA` y `FUERA_SERVICIO` existen físicamente en `EstadosMesa`, el Salón debe representarlas correctamente.
- NO implementar funcionalidad avanzada de reservas en esta fase.
- Mesa `FUERA_SERVICIO` o `RESERVADA`: no abrir Comanda desde Salón (acciones bloqueadas).

## 12. AUDITORÍA

Usar `IAuditoriaService` (única firma existente: `RegistrarAsync(entidad, idEntidad, accion, datosAnteriores, datosNuevos)`).

Registrar como mínimo:

- `CREAR_MESA`
- `ACTUALIZAR_MESA`
- `ACTIVAR_MESA`
- `INACTIVAR_MESA`
- `ABRIR_MESA`
- `REANUDAR_COMANDA_MESA` cuando corresponda
- `TRANSFERIR_MESA`
- `LIBERAR_MESA` al cerrar Comanda

Snapshots:

- `IdMesa`
- `IdComanda`
- estados anterior/nuevo
- Mesa origen/destino
- folio cuando sea útil

Sin secretos.

## 13. UI SALÓN

- Pantalla clara para desktop/tablet.
- Preferir cards/grid responsive.
- Cada Mesa debe distinguir visualmente su estado.
- NO fijar colores mediante lógica de negocio; usar clases Bootstrap coherentes.
- Reutilizar instancia de `bootstrap.Modal` (no crear una nueva en cada apertura; ver sección 20).

Acciones posibles según estado:

- `DISPONIBLE`: Abrir Mesa.
- `OCUPADA` propia: Abrir Cuenta, Transferir.
- `OCUPADA` ajena: solo información, sin apropiación.
- `RESERVADA`: representar estado; no crear reserva nueva en esta fase.
- `FUERA_SERVICIO`: sin abrir Comanda.

## 14. VENTAS UI

- Cuando Ventas muestre una Comanda asociada a Mesa: mostrar discretamente `Mesa: <Nombre>`.
- NO reescribir toda la vista.
- Mantener POS actual.
- Si venta mostrador (`IdMesa == null`): mostrar equivalente apropiado o no mostrar Mesa.

## 15. LAYOUT

- Agregar acceso operacional: `Salón / Mesas` → visible para usuario autenticado.
- Administración debe incluir `Mesas` solo dentro del menú Administrador.
- Mantener navegación actual (`_Layout.cshtml`).

## 16. SEGURIDAD

- Toda mutación: POST + antiforgery (`[ValidateAntiForgeryToken]`); Fetch envía `RequestVerificationToken`.
- NO confiar en:
  - `IdSucursal` enviado
  - `IdCaja` enviado
  - `IdSesionCaja` enviado
  - `IdUsuario` enviado
  - estado Mesa enviado
  - estado Comanda enviado
  - totales enviados
- Claims + DB = fuente de verdad.

## 17. CONCURRENCIA

Pruebas obligatorias:

- A. Dos solicitudes simultáneas intentan abrir la misma Mesa.
  - Resultado: exactamente una Comanda `ABIERTA`.
- B. Dos solicitudes simultáneas intentan transferir dos comandas/mesas de forma conflictiva.
  - NO debe producir: dos Comandas en misma Mesa; Mesa origen/destino incoherente; deadlock no controlado; pérdida de datos.
  - Si aparece deadlock MySQL real: capturar evidencia y analizar orden de locks. NO ocultar mediante reintentos ciegos.

## 18. SCHEMA

- Trabajar primero con schema existente.
- NO crear migración automáticamente.
- Si el modelo actual no permite cumplir una regla esencial: DETENER.
- Reportar: modelo afectado, limitación, cambio mínimo necesario, impacto.
- Esperar autorización.

## 19. ADMIN CRUD — DEUDA TÉCNICA

- En Mesas implementar desde el inicio `Obtener(id)`.
- NO usar `Buscar?termino=<id>` para editar.
- NO corregir automáticamente todos los CRUD heredados en esta fase.
- Registrar que Mesas utiliza el patrón nuevo.
- La deuda transversal se atenderá posteriormente.

## 20. MODAL BOOTSTRAP

- Evitar crear una nueva instancia `bootstrap.Modal` en cada apertura si puede reutilizarse una instancia existente.
- Aplicar patrón limpio desde Mesas/Salon.
- NO refactorizar Productos en esta fase salvo requerimiento funcional real.

## 21. BUILD

Antes de pruebas:

`dotnet build .\AtlasRestaurantPOS.slnx`

Objetivo: `0 warnings / 0 errors`.

Si falla: NO levantar servidor.

## 22. PRUEBAS FUNCIONALES

Aplicar `AgentInstructions/SERVER_RUNNER.md` (autoridad del ciclo de vida del servidor) y `AgentInstructions/TESTING.md`.

Guardar evidencia persistente en:

`C:\Users\Admin\AppData\Local\Temp\opencode\fase8`

Crear `resultados.txt`. El resumen debe quedar persistido, no solo en consola.

### Batería mínima

#### A — Administración Mesa

- autorización Administrador;
- crear;
- `Obtener(id)`;
- editar;
- buscar;
- duplicado rechazado (`(IdSucursal, Nombre)`);
- capacidad 0/negativa rechazada;
- sucursal inválida;
- body null;
- activar/inactivar;
- inactivar con Comanda `ABIERTA` rechazado.

#### B — Salón

- usuario con caja abierta accede;
- usuario sin caja/sesión es redirigido;
- muestra mesas activas de sucursal;
- no muestra mesas de otra sucursal;
- estados correctos.

#### C — Abrir Mesa

- Mesa disponible crea Comanda;
- Mesa → `OCUPADA`;
- `Comanda.IdMesa` correcto;
- folio válido;
- claims/contexto correctos.

#### D — Concurrencia

Dos aperturas simultáneas misma Mesa:

- 1 éxito;
- 1 rechazo;
- exactamente 1 Comanda `ABIERTA`.

#### E — Reanudar

- Mesa ocupada propia abre Comanda existente;
- no crea segunda Comanda.

#### F — Ownership

- usuario/caja/sesión ajenos no pueden tomar cuenta.

#### G — Transferencia

- Mesa A ocupada;
- Mesa B disponible;
- transferir;
- A → `DISPONIBLE`;
- B → `OCUPADA`;
- misma Comanda;
- mismo folio;
- mismos detalles/pagos.
- Transferencia a Mesa ocupada: rechazada.

#### H — Venta desde Mesa

- abrir Mesa;
- agregar producto;
- impuestos;
- pago parcial;
- Mesa sigue `OCUPADA`;
- completar pago;
- Comanda `CERRADA`;
- Mesa `DISPONIBLE`.

#### I — Mostrador

Regresión:

- `NuevaComanda` sin Mesa sigue funcionando;
- pago/cierre funciona;
- no afecta estados de Mesa.

#### J — Auditoría

Verificar eventos de Mesa y ausencia de secretos.

## 23. EVIDENCIA

`resultados.txt` debe contener:

- fecha/hora;
- commit base;
- build;
- cada prueba PASS/FAIL;
- total PASS;
- total FAIL;
- PID;
- READY;
- cleanup;
- puerto libre.

NO guardar:

- password
- cookies
- tokens antiforgery
- ConnectionStrings
- secretos

## 24. DATOS DE PRUEBA

- Usar nombres claramente identificables: `F8`.
- NO asumir IDs. Localizar registros por datos reales.
- NO borrar historial físico para limpiar.
- Reportar datos persistentes creados.

## 25. GIT

- Precondición: working tree limpio.
- Commit base esperado: `655bbca`.
- Durante Fase 8: NO commit automático.
- Al terminar: dejar cambios sin commit para `/auditbuild`.
- NO actualizar `PROJECT_STATE.md` todavía.

## 26. CRITERIO DE ÉXITO

Fase 8 solo puede declararse completada si:

- CRUD Mesas funciona;
- Salón funciona;
- abrir/reanudar Mesa funciona;
- concurrencia misma Mesa protegida;
- transferencia funciona;
- cierre libera Mesa;
- mostrador sigue funcionando;
- auditoría funciona;
- build 0/0;
- batería persistente 0 FAIL;
- cleanup correcto.

Cualquier FAIL: NO declarar fase completada.

## 27. FINAL DEL DOCUMENTO

Durante la ejecución futura se seguirá:

`read → edit/write → read → build → test → verify → cleanup → report`

Y al finalizar:

- NO commit;
- NO actualización de `PROJECT_STATE.md`;
- ejecutar después `/auditbuild`;
- corregir hallazgos solo con autorización;
- tras aprobación actualizar `PROJECT_STATE.md` y hacer commit.

---

## Choques reales detectados y decisiones necesarias (registro para ejecución)

Hechos verificados que condicionan la implementación; no son cambios de alcance por iniciativa propia:

1. **`Mesa` no tiene `IdEmpresa`**: la empresa se resuelve vía `Sucursal.IdEmpresa`. Las validaciones de empresa activa deben navegar `Mesa → Sucursal → Empresa` (patrón ya usado en `CajasController.ValidarFormularioAsync`).
2. **`Mesa.Estado` sin uso operativo actual**: toda la sincronización Mesa ↔ Comanda (abrir/transferir/liberar) es lógica nueva. La fuente de verdad de ocupación debe derivarse de la existencia de Comanda `ABIERTA` con `IdMesa`, manteniendo `Mesa.Estado` coherente transaccionalmente.
3. **`NuevaComanda` no acepta `IdMesa`**: la apertura de mesa requiere un endpoint que reutilice `GenerarFolioAsync`/patrón de folio existente. NO duplicar algoritmo de folio. Mostrador debe seguir sin mesa.
4. **`EstadosComanda` incluye `EN_PROCESO` y `CANCELADA`**: la ocupación de mesa se define sobre `ABIERTA`. `EN_PROCESO`/`CANCELADA` no existen operativamente en el flujo actual (no hay código que las asigne); la Fase 8 no introduce cancelación. Decisión de ejecución: la regla de ocupación/liberación aplica únicamente a `ABIERTA`/`CERRADA`; cualquier otra transición de estado queda fuera de esta fase.
5. **Ventas no carga comandas existentes**: para “Abrir cuenta” (sección 6) se requiere el cambio mínimo de permitir cargar una comanda existente validada server-side; `ComandaEstadoViewModel` debe exponer el nombre de Mesa.
6. **Punto de liberación**: el único lugar donde la comanda pasa a `CERRADA` hoy es `VentasController.RegistrarPago`. La liberación de Mesa debe ocurrir en esa misma transacción (modificación mínima).
7. **Concurrencia**: el patrón disponible es `SELECT ... FOR UPDATE` sobre filas (ya usado en Mesas candidatas: `Mesa`, `Comanda`, `FoliosSecuencia`). Para transferencia, locks deterministas por `IdMesa` (menor → mayor).
8. **Deuda técnica registrada (Fase 7)**: `Obtener(id)` es el patrón nuevo para Mesas; la deuda transversal de CRUD heredados (`Buscar?termino=<id>` y `bootstrap.Modal` repetido) NO se corrige en esta fase salvo requerimiento funcional real.