# Atlas Restaurant POS — Estado del Proyecto

## Plataforma

- ASP.NET Core MVC
- .NET 8
- C#
- EF Core 8
- Pomelo MySQL
- Bootstrap
- JavaScript embebido
- Fetch API
- Cookie Authentication
- Dependency Injection
- Services

Solución:
`AtlasRestaurantPOS.slnx`

Base de datos:
`atlas_restaurant_pos`

## Infraestructura completada

Implementado físicamente:

- proyecto MVC .NET 8;
- EF Core + Pomelo;
- MySQL;
- AtlasRestaurantDbContext;
- autenticación mediante cookies;
- autorización global;
- PasswordHasher;
- User Secrets;
- auditoría reusable;
- manejo Fetch 401/403;
- antiforgery;
- claims de empresa/sucursal/usuario;
- infraestructura multicaja;
- configuración lógica para dispositivos;
- configuración lógica para integraciones externas.

## Migraciones existentes

- `InitialRestaurantCore`
- `OperationalInfrastructure`

No asumir en sesiones futuras que están aplicadas actualmente sin verificar DB cuando la tarea lo requiera.

## Fases funcionales implementadas

### Fase 1 — Núcleo del dominio

Implementado:

- Empresa
- Sucursal
- Rol
- Usuario
- Caja
- SesionCaja
- CategoriaProducto
- Producto
- Mesa
- Comanda
- ComandaDetalle
- Pago
- MovimientoCaja
- Dispositivo
- IntegracionExterna

Estado:
COMPLETADA físicamente.

### Fase 2 — Infraestructura operativa

Implementado:

- ConfiguracionPos
- Impuesto
- ProductoImpuesto
- MetodoPago
- FolioSecuencia
- Auditoria
- constantes de estados/tipos
- relaciones operativas adicionales de Pago
- Folio en Comanda

Estado:
COMPLETADA físicamente.

### Fase 3 — Bootstrap y autenticación

Implementado:

- InitialSetupService
- Empresa/Sucursal inicial
- Rol Administrador
- Usuario administrador
- PasswordHasher
- Login
- Logout
- Claims
- autorización global
- dashboard base

Las pruebas funcionales de esta fase fueron reportadas previamente como correctas, pero en una sesión nueva deben verificarse nuevamente si una tarea depende críticamente de ellas.

Estado:
COMPLETADA.

### Fase 4 — Administración

Módulos:

- Empresas
- Sucursales
- Roles
- Usuarios
- Cajas

Incluyen:

- búsqueda;
- crear;
- editar;
- activar/inactivar;
- validaciones;
- Fetch;
- antiforgery;
- auditoría;
- protección administrativa.

Estado:
COMPLETADA.

### Fase 5 — Operación multicaja

Implementado:

- selección de Caja;
- apertura de SesionCaja;
- reanudación;
- claims IdCaja/IdSesionCaja;
- panel operativo;
- Entrada;
- Retiro;
- movimientos;
- cálculo de efectivo esperado;
- cierre;
- limpieza de claims;
- auditoría;
- protección concurrente mediante `SELECT ... FOR UPDATE`.

Pruebas históricamente reportadas:

- dos cajas simultáneas;
- doble apertura de misma caja rechazada;
- reanudación;
- cierre;
- retiro excedente rechazado;
- concurrencia controlada.

Estado:
COMPLETADA.

### Fase 6 — Ventas POS

Implementado físicamente:

- `VentasController`
- `Views/Ventas/Index.cshtml`
- `VentasViewModels`
- catálogo por categorías activas;
- métodos de pago activos;
- NuevaComanda;
- generación de folios mediante FolioSecuencia;
- bloqueo `SELECT ... FOR UPDATE`;
- agregar producto;
- modificar cantidad;
- quitar detalle;
- notas;
- recalcular subtotal;
- impuestos incluidos/no incluidos;
- total;
- pagos parciales;
- multipago;
- cambio en efectivo;
- referencia obligatoria según método;
- cierre automático de Comanda;
- aislamiento por Caja/SesionCaja/Sucursal/Usuario;
- auditoría.

## Evidencia verificable de Fase 6

Existe evidencia física de ejecución anterior en:
`C:\Users\Admin\AppData\Local\Temp\opencode\fase6\`

Se verificó:

- script de batería F6;
- artefactos HTTP/cookies;
- ejecución real contra `http://localhost:5250`;
- `server.out` con verificación final.

Resultados finales registrados como True:

- sesión de caja verificada;
- comandas de sesión verificadas;
- suma de pagos igual a total;
- efectivo de sesión verificado;
- FolioSecuencia coherente;
- integridad global;
- ausencia de MovimientoCaja VENTA duplicado;
- auditoría completa;
- snapshots sin secretos.

## Limitaciones de evidencia Fase 6

NO está demostrado mediante artefacto persistente:

- PASS/FAIL individual de cada assert;
- resultado crudo de pruebas concurrentes;
- conteo warnings/errors del último build;
- estado actual de MySQL.

Existe una DLL posterior a la batería de pruebas, pero su existencia no demuestra por sí sola un build actual limpio.

Por lo tanto:
**Fase 6 debe considerarse IMPLEMENTADA y con evidencia fuerte de verificación final, pero no usar la frase "todos los asserts pasaron" sin una nueva ejecución verificable.**

## FIX7 — Endurecimiento de Ventas POS

Estado:
`VERIFICADO`

Hallazgos corregidos:

- cuerpos `[FromBody]` nulos protegidos;
- protección concurrente de operaciones sobre Comanda;
- creación inicial concurrente robusta de FolioSecuencia;
- ownership reforzado en RegistrarPago;
- manejo controlado de fallo/no-JSON en postJson.

Build verificado:
`0 warnings / 0 errors`

Batería focalizada:
`39 PASS / 0 FAIL`

Cobertura verificada:

- body vacío/null no produce HTTP 500;
- operaciones concurrentes sobre detalles mantienen totales consistentes;
- tres NuevaComanda concurrentes sin FolioSecuencia previa generan folios 000001, 000002 y 000003;
- usuario/caja/sesión ajenos no pueden operar la comanda;
- flujo normal multipago cierra correctamente;
- fallo frontend controlado mediante catch/toast.

Limitación registrada:
En `ModificarCantidad` concurrente no se exige un ganador fijo; se verificó consistencia final de los totales.

Evidencia persistente:
`C:\Users\Admin\AppData\Local\Temp\opencode\fix7\resultados.txt`
`C:\Users\Admin\AppData\Local\Temp\opencode\fix7\concurrent_raw.txt`

Cleanup verificado:

- servidor detenido;
- PID 2920 finalizado;
- puerto 5250 libre.

### Fase 7 — Catálogo comercial

Estado:
COMPLETADA Y VERIFICADA

Módulos administrativos implementados:

- Categorías de productos (búsqueda, crear, editar, activar/inactivar; inactivación bloqueada si tiene productos activos; duplicado por índice único `Nombre`).
- Productos (búsqueda, crear, editar, activar/inactivar; categoría obligatoria y activa; precio >= 0 con redondeo 2 decimales; duplicados validados a nivel de aplicación por `(IdCategoriaProducto, Nombre)`).
- Impuestos (búsqueda, crear, editar, activar/inactivar; por empresa del administrador; tasa 0–100; duplicado por índice único `(IdEmpresa, Nombre)`).
- Asociación Producto–Impuesto (asignar/quitar desde administración de Producto; impuestos activos de la empresa; clave compuesta `(IdProducto, IdImpuesto)`; borrado físico de la fila de asociación).
- Métodos de Pago (búsqueda, crear, editar, activar/inactivar; por empresa del administrador; código único `(IdEmpresa, Codigo)`; `RequiereReferencia`/`PermiteCambio`).

Navegación administrativa:

- Menú desplegable Administración en `_Layout.cshtml` (solo rol Administrador) con enlaces a Categorías, Productos, Impuestos y Métodos de Pago.

Autorización:

- Todos los controladores nuevos protegidos con `[Authorize(Roles = "Administrador")]`.
- Fetch sin sesión → 401; Fetch sin permiso → 403; navegación sin permiso → Denegado.

Antiforgery:

- Toda operación POST mutable valida `[ValidateAntiForgeryToken]`; el token se envía desde la vista mediante `RequestVerificationToken`.

Auditoría:

- `IAuditoriaService` con acciones `CREAR_/ACTUALIZAR_/ACTIVAR_/INACTIVAR_CATEGORIA/PRODUCTO/IMPUESTO/METODO_PAGO` y `ASIGNAR_IMPUESTO`/`QUITAR_IMPUESTO`; snapshots sin secretos.

Regresión de Ventas POS:

- Ventas sigue operando con catálogo configurado desde los nuevos módulos: categorías activas con productos activos, impuestos (incluido/no incluido), métodos de pago activos, comanda, pago y cierre correctos.

Build:

- `0 warnings / 0 errors`.

Batería HTTP:

- `73 PASS / 0 FAIL` (A autorización, B categorías, C productos, D impuestos, E producto-impuesto, F métodos de pago, G regresión Ventas, H seguridad).

Migraciones:

- Ninguna requerida; schema existente suficiente (sin cambios de schema).

Cleanup:

- servidor detenido;
- PID 1616 finalizado;
- puerto 5250 libre;
- sin código temporal en el repo;
- evidencia persistente en `C:\Users\Admin\AppData\Local\Temp\opencode\fase7\resultados.txt`.

Deuda técnica NO bloqueante (no son fallos de Fase 7):

1. Patrón heredado de edición: algunas vistas administrativas usan `Buscar?termino=<id>` para recuperar un registro antes de editar. Debe evaluarse posteriormente reemplazarlo por un endpoint `Obtener(id)` explícito.
2. `Productos/gestionarImpuestos` crea nuevas instancias `bootstrap.Modal` repetidamente. Evaluar reutilización de instancia.

### Fase 8 — Mesas y Salón

Estado:
`COMPLETADA Y VERIFICADA`

Implementado físicamente:

- CRUD de Mesas (búsqueda, crear, editar, activar/inactivar; duplicado por índice único `(IdSucursal, Nombre)`; inactivación bloqueada si la mesa tiene comanda ABIERTA; capacidad > 0; sucursal activa y empresa activa).
- Pantalla Salón: mesas activas de la sucursal del usuario con caja/sesión; estado visual Disponible/Ocupada; comanda abierta asociada (folio, total, usuario responsable); distinción de ownership `EsPropia`/`EsAjena`.
- Apertura de mesa: crea Comanda con folio generado por `FolioComandaService`; bloqueo `SELECT ... FOR UPDATE` sobre la mesa; validación de sucursal, activo y Disponible; auditoría `ABRIR_MESA`.
- Reanudación: `Salon/Reanudar` redirige a Ventas con `idComanda`; validación de ownership estricta (sucursal, caja, sesión, usuario); auditoría `REANUDAR_COMANDA_MESA`.
- Ownership: operaciones sobre comanda ajena rechazadas (transferir, pagar, reanudar).
- Transferencia de mesa: transacción con locks deterministas `FOR UPDATE` por `IdMesa` orden menor → mayor (evita deadlocks); valida activas, sucursal, destino Disponible y sin comanda abierta; auditoría `TRANSFERIR_MESA`.
- Concurrencia: doble apertura simultánea de la misma mesa → solo una comanda ABIERTA (verificado: 1 éxito de 2 solicitudes).
- Integración con Ventas: `Ventas.Index(idComanda)` valida ownership y carga la comanda de mesa; endpoint `Ventas/Estado` para consulta; muestra `NombreMesa` en la cuenta; al cerrar la comanda con pago se libera la mesa (`FOR UPDATE` + auditoría `LIBERAR_MESA`).
- Venta mostrador conservada: `NuevaComanda` sin mesa sigue operando y cerrando correctamente.
- Pago parcial: mantiene la mesa OCUPADA; el cierre del total libera la mesa.
- Extracción de `FolioComandaService` (`IFolioComandaService`) reutilizado por Ventas y Salón; registro DI en `Program.cs`.
- Auditoría completa con `IAuditoriaService`; snapshots sin secretos.
- Antiforgery en todo POST mutable; Fetch 401/403 manejados; navegación sin sesión → redirect a Login/Caja.

Build final:
`0 warnings / 0 errors`.

Batería HTTP A–J:
`50 PASS / 0 FAIL` (A CRUD Mesas, B Salón sin/con caja, C apertura, D concurrencia, E reanudación, F ownership, G transferencia, H venta en mesa, I mostrador, J auditoría).

Evidencia persistente:
`C:\Users\Admin\AppData\Local\Temp\opencode\fase8\resultados.txt`

Migraciones:
Ninguna requerida; schema existente suficiente (sin cambios de schema).

Cleanup:
- servidor detenido;
- PID 9388 finalizado;
- puerto 5250 libre;
- sin código temporal en el repo;
- sesiones de caja de prueba cerradas.

Incidencia registrada (NO bloqueante, NO corregida en esta fase):
- El password configurado actualmente en User Secrets para `InitialSetup:AdminPassword` no coincide con el `PasswordHash` actual del usuario `admin` (verificado con `PasswordHasher`). No se incluyen ni password ni hash en este registro. Pendiente de decisión futura; no se regeneró nada durante esta fase.

### Fase 9 — Cancelaciones y Devoluciones

Estado:
`COMPLETADA Y VERIFICADA`

Implementado físicamente:

- Cancelación de comandas ABIERTAS (`Ventas/CancelarComanda`): solo rol Administrador; motivo obligatorio (mínimo 5 caracteres); valida sesión de caja activa, ownership (caja/sesión/usuario/sucursal) y que la comanda esté ABIERTA; rechaza comandas con pagos sin devolver; registra `FechaCancelacion`, `MotivoCancelacion`, `IdUsuarioCancelacion`; auditoría `CANCELAR_COMANDA`; libera la Mesa atómicamente con `SELECT ... FOR UPDATE` y auditoría `LIBERAR_MESA_POR_CANCELACION`.
- Devolución de pagos (`Ventas/DevolverPago`): solo rol Administrador; motivo obligatorio (mínimo 5 caracteres); bloqueo `SELECT ... FOR UPDATE` sobre el pago; valida ownership (caja/sesión) y sucursal de la comanda; idempotente (pago ya devuelto rechazado); marca `Devuelto`/`FechaDevolucion`/`IdUsuarioDevolucion`/`MotivoDevolucion`; auditoría `DEVOLVER_PAGO`.
- Devolución EFECTIVO con impacto correcto en Caja: registra `MovimientoCaja` tipo `DEVOLUCION` (auditoría `DEVOLUCION_CAJA`) y queda EXCLUIDA del efectivo esperado del arqueo (el pago devuelto no cuenta como venta en efectivo).
- Devolución no EFECTIVO sin salida ficticia del cajón: TARJETA devuelta NO genera movimiento de caja (no hay efectivo que devolver).
- Arqueo correcto: `CajaOperacionController.CalcularTotalesAsync` excluye pagos devueltos de `VentasEfectivo`; esperado = FondoInicial + Entradas + VentasEfectivo(sin devueltos) − Retiros; verificado con cierre de caja tras devoluciones.
- Idempotencia: doble cancelación y doble devolución rechazadas con mensaje controlado.
- Concurrencia: cancelación concurrente → exactamente 1 éxito; devolución concurrente → exactamente 1 éxito; cancelación vs apertura de misma mesa → estado coherente (bloqueo `FOR UPDATE`).
- Ownership: cancelar comanda ajena o devolver pago ajeno rechazado (403).
- Autorización: endpoints exclusivos del rol Administrador; navegación sin sesión → 302; Fetch sin sesión → 401; Fetch sin permiso → 403.
- UI `Views/Ventas/Index.cshtml`: botón "Cancelar comanda" (solo Administrador y comanda ABIERTA), tabla de pagos con estado VIGENTE/DEVUELTO y acción "Devolver", modal de motivo reutilizable, alertas de estado CERRADA/CANCELADA.
- Auditoría completa con `IAuditoriaService`; snapshots sin secretos.
- Antiforgery en todo POST mutable; ViewModels top-level (`CancelarComandaViewModel`, `DevolverPagoViewModel`) con protección de cuerpos `[FromBody]` nulos.

Migraciones:

- `20260816054239_AgregarDevoluciones` aplicada (registrada en `__EFMigrationsHistory`): columnas `Devuelto`, `FechaDevolucion`, `IdUsuarioDevolucion`, `MotivoDevolucion` en `Pagos`; `FechaCancelacion`, `IdUsuarioCancelacion`, `MotivoCancelacion` en `Comandas`; FK Restrict hacia `Usuarios`; índice compuesto `IX_Pagos_IdComanda_Devuelto` (reemplaza el simple respetando el respaldo de FK en MySQL); índices de FK nuevos. Sin operaciones destructivas.

Build final:

- `0 warnings / 0 errors`.

Batería HTTP A–K + concurrencia + verificación física:

- `60 PASS / 0 FAIL` (autorización, cancelación, motivos, ownership, comanda cerrada rechazada, mesa liberada, devolución EFECTIVO, devolución TARJETA, doble devolución, devolución concurrente, cancelación concurrente, regresión Ventas/Mesas/Salón, arqueo y cierre con devoluciones, verificación física DB).

Evidencia persistente:

- `C:\Users\Admin\AppData\Local\Temp\opencode\fase9\resultados.txt`

Cleanup:

- servidor detenido;
- puerto 5250 libre;
- sesiones de caja de prueba cerradas (0 abiertas después de F9);
- sin código temporal en el repo.

Deuda técnica NO bloqueante (no es fallo de Fase 9):
- Recuperación administrativa de comandas huérfanas asociadas a sesiones de caja cerradas: una ejecución concurrente interrumpida durante las pruebas dejó una Comanda ABIERTA asociada a una SesionCaja posteriormente CERRADA; sin flujo funcional normal para resolverla por ownership; durante la batería F9 se realizó una limpieza puntual controlada (fuera del repo) marcando la comanda de prueba como CANCELADA y liberando su Mesa. No diseñar ni implementar la solución todavía.
- Se mantiene separada la incidencia NO bloqueante del password del admin registrada en Fase 8 (no se toca).

### Fase 10 — Inventario, Insumos, Recetas, Existencias, Códigos y Etiquetas

Estado:
IMPLEMENTADA Y VERIFICADA

Implementado físicamente:

- servicios IInventarioService, IIdentificacionService e IEtiquetaService;
- movimientos transaccionales con FOR UPDATE, UPSERT seguro para existencia ausente, stock no negativo y auditoría;
- entradas, ajustes, mermas y devolución física explícita;
- escaneo server-side de producto en Ventas e insumo en Inventario;
- generación concurrente de códigos internos por empresa con secuencia persistente;
- previsualización HTML de etiquetas para Producto/Insumo, sin impresión física;
- campos de código/código de barras y generación automática en UI de Productos e Insumos;
- auditoría de recetas con eventos agregar/modificar/quitar;
- menú administrativo para Recetas, Existencias y Movimientos;
- aislamiento por empresa/sucursal y validación de sesión/caja en Ventas conservados.

Persistencia:

- no se agregó una migración nueva; el esquema de Fase 10 existente fue suficiente;
- upgrade verificado desde 20260816081324_AgregarInventarioInsumos hasta la migración más reciente;
- fresh verificado en base MySQL temporal.

Verificación ejecutada:

- build Release: 0 warnings / 0 errors;
- MySQL real: 2 PASS / 0 FAIL;
- cobertura: tenant isolation, colisión de códigos entre empresas, escaneo POS, movimiento de entrada sobre existencia ausente, merma, secuencia concurrente, multicaja, consumo al cierre, pago parcial existente, rollback por stock insuficiente, devolución financiera sin reposición física y trazabilidad caja/sesión;
- Playwright: login renderizado y Fetch sin sesión devuelve 401;
- cleanup: servidor propio detenido, PID finalizado, puerto 5101 libre y artefactos temporales eliminados.

Limitaciones externas:

- impresión física, lector y generación gráfica estándar de códigos de barras quedan EXTERNAL; la UI entrega código y previsualización de datos sin instalar SDK/hardware;
- no se ejecutó login autenticado de navegador porque no existe una credencial E2E de aplicación autorizada en esta tarea; los flujos autenticados se verificaron en MySQL/controladores.

## Módulos todavía sin administración completa

Modelos presentes sin CRUD/UI administrativo completo:

- ConfiguracionPos
- Dispositivo
- IntegracionExterna

## Próximo objetivo funcional

Con Fase 10 implementada y verificada, los siguientes módulos siguen fuera del alcance de esta fase:

- ConfiguracionPos;
- Dispositivo;
- IntegracionExterna;
- cocina/KDS, integraciones reales y reportes adicionales.

## Estado técnico actual verificable

- Fuente temporal de F6 eliminada de Program.cs.
- Build Release ejecutado: 0 warnings / 0 errors.
- MySQL real ejecutado: 2 pruebas PASS / 0 FAIL, con bases temporales fresh y upgrade; cleanup aplicado.
- Playwright ejecutado: login renderizado y Fetch sin sesión con 401.
- Servidor E2E propio detenido; PID finalizado; puerto 5101 libre.
- No hay servidor de prueba activo al cerrar la fase.

## Reglas para actualizar este archivo

`PROJECT_STATE.md` es el checkpoint operativo del proyecto.
Solo actualizarlo después de una fase cuando:

1. implementación terminada;
2. verificaciones requeridas completadas;
3. cleanup realizado;
4. discrepancias registradas.

No convertir inferencias en hechos.
Separar siempre:

- IMPLEMENTADO
- VERIFICADO
- NO VERIFICADO
- PENDIENTE