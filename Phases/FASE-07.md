# FASE 7 — CATÁLOGO COMERCIAL

> Documento de especificación. Esta fase NO está implementada todavía.
> Este documento describe únicamente QUÉ debe construirse, respetando la arquitectura física verificada en READ.

## 0. Estado previo verificado (READ)

Modelos físicos confirmados:

- `CategoriaProducto`: `IdCategoriaProducto`, `Nombre` (required, max 150), `Descripcion` (nullable, max 500), `Activo`. Índice único sobre `Nombre`. FK `Productos` → `DeleteBehavior.Restrict`. **Entidad global (sin `IdEmpresa`).**
- `Producto`: `IdProducto`, `IdCategoriaProducto`, `Nombre` (required, max 150), `Descripcion` (nullable, max 500), `Precio` (decimal 18,2), `Activo`, `FechaCreacion`. **Sin índice único sobre `Nombre`.** Entidad global (sin `IdEmpresa`).
- `Impuesto`: `IdImpuesto`, `IdEmpresa`, `Nombre` (required, max 150), `Tasa` (decimal 9,4), `IncluidoEnPrecio`, `Activo`, `FechaCreacion`. Índice único `(IdEmpresa, Nombre)`. FK `Empresa` Restrict. **Entidad por empresa.**
- `ProductoImpuesto`: clave compuesta `(IdProducto, IdImpuesto)`. Ambas FK Restrict. **Sin `Activo`, sin `FechaCreacion`, sin borrado lógico.**
- `MetodoPago`: `IdMetodoPago`, `IdEmpresa`, `Nombre` (required, max 150), `Codigo` (required, max 50), `RequiereReferencia`, `PermiteCambio`, `Activo`, `FechaCreacion`. Índice único `(IdEmpresa, Codigo)`. FK `Empresa` Restrict. **Entidad por empresa.**

Comportamiento de Ventas que Fase 7 debe respetar (verificado en `VentasController`):

- Categorías visibles en Ventas: `cat.Activo && cat.Productos.Any(p => p.Activo)`.
- Productos visibles: `p.Activo && p.CategoriaProducto.Activo`.
- Impuestos aplicados: `ProductoImpuesto` → `Impuesto.Activo`, usando `IncluidoEnPrecio`: incluido `base * Tasa/(100+Tasa)`; no incluido `base * Tasa/100`.
- Métodos de pago: `m.IdEmpresa == claim IdEmpresa && m.Activo`; `RequiereReferencia` exige referencia; `PermiteCambio` calcula cambio; `Pago.MetodoPago` guarda `metodo.Codigo`.
- `Program.cs` está limpio (sin seeds temporales de F6). No hay fuente de datos de catálogo salvo la DB; los nuevos módulos serán la única vía de configuración.

## 1. OBJETIVO FUNCIONAL

Eliminar la dependencia operativa de seeds temporales de Fase 6. El administrador debe poder configurar desde la UI los datos que Ventas POS necesita para operar:

1. Categorías de productos
2. Productos
3. Impuestos
4. Asociación Producto–Impuesto
5. Métodos de pago

## 2. PRINCIPIOS

Mantener arquitectura actual:

- ASP.NET Core MVC
- .NET 8
- EF Core 8
- Pomelo MySQL
- Bootstrap
- JavaScript + Fetch
- Cookie Authentication
- DI
- `IAuditoriaService`

NO introducir:

- Repository
- UnitOfWork
- jQuery nuevo
- TypeScript
- APIs externas
- borrado físico de entidades principales
- nueva arquitectura innecesaria

Reglas transversales:

- Retornos tempranos. Evitar `else` innecesarios.
- Toda operación mutable: `POST` + `[ValidateAntiForgeryToken]`.
- Administración: `[Authorize(Roles = "Administrador")]`.
- Fetch: JSON consistente `{ ok, mensaje }` en mutaciones; listas en endpoints de búsqueda (coherente con `CajasController.Buscar`).
- Proteger `[FromBody]` nulo: `if (modelo is null) return JsonError("Datos inválidos.");`.
- Nunca confiar en IDs o valores sensibles enviados por cliente sin validación server-side (existencia, activo, empresa).
- Redondeo monetario idéntico al proyecto: `Math.Round(valor, 2, MidpointRounding.AwayFromZero)`.
- Normalización de texto: `string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()`.
- Manejo Fetch/no-JSON según política de `TESTING.md`/FIX7 (401 → Login, 403 → toast, `!res.ok` → toast, parse con try/catch).

## 3. CATEGORÍAS

Controller propuesto: `CategoriasController`. View: `Views/Categorias/Index.cshtml`.

Acciones:

- `GET Index` → `View()`.
- `GET Buscar(string? termino)` → lista JSON (todas, incl. inactivas; filtro por nombre/descripción).
- `POST Crear([FromBody])` + antiforgery.
- `POST Editar([FromBody])` + antiforgery.
- `POST Activar(int id)` + antiforgery.
- `POST Inactivar(int id)` + antiforgery.

Validaciones server-side:

- `Nombre` obligatorio, sin espacios al inicio/fin, max 150.
- `Descripcion` opcional, max 500.
- Duplicado: la BD ya impone índice único sobre `Nombre`; verificar además con pre-consulta normalizada y capturar `DbUpdateException` como error amigable.

Política de inactivación (explícita, segura):

- **Bloquear la inactivación de una categoría que tenga productos activos** (`AnyAsync(p => p.IdCategoriaProducto == id && p.Activo)`). Respuesta: `"La categoría tiene productos activos. Inactívelos primero."`
- Motivo: Ventas oculta la categoría completa cuando no hay productos activos; permitir la inactivación con productos activos rompería silenciosamente la visibilidad operativa sin una política definida en la arquitectura actual.
- Si se desea otra política (p. ej. inactivar la categoría y arrastrar sus productos): **DETENER durante implementación** — es decisión de producto, no de schema.

Auditoría: `CategoriaProducto`, acciones `CREAR_CATEGORIA`, `ACTUALIZAR_CATEGORIA`, `ACTIVAR_CATEGORIA`, `INACTIVAR_CATEGORIA`. Snapshots con `Nombre`, `Descripcion`, `Activo`.

## 4. PRODUCTOS

Controller propuesto: `ProductosController`. View: `Views/Productos/Index.cshtml`.

Acciones:

- `GET Index` → `View()`.
- `GET Buscar(string? termino)` → lista JSON (incl. nombre de categoría), solo para el administrador logueado (sin filtro de empresa: Producto es global).
- `POST Crear([FromBody])` + antiforgery.
- `POST Editar([FromBody])` + antiforgery.
- `POST Activar(int id)` + antiforgery.
- `POST Inactivar(int id)` + antiforgery.

Validaciones server-side obligatorias:

- `Nombre` obligatorio, sin espacios al inicio/fin, max 150.
- `Descripcion` opcional, max 500.
- `Precio` obligatorio, `>= 0`, rechazar negativos, ajustar a 2 decimales con redondeo del proyecto. `decimal(18,2)`.
- `IdCategoriaProducto` obligatorio, `> 0`, validar existencia.
- Categoría activa requerida: no permitir crear/editar producto en categoría inactiva (`"La categoría seleccionada está inactiva."`). Evita configuraciones incompatibles con Ventas.
- Duplicados: la BD **no** impone índice único sobre `Producto.Nombre`. Política explícita: validar a nivel de aplicación contra duplicados de `(IdCategoriaProducto, Nombre)` normalizado (sin migración). Documentar en auditoría de decisiones. Si se requiere regla global o distinta, DETENER (decisión de producto).
- `FechaCreacion` asignada por servidor en Crear (`DateTime.UtcNow`), no desde el cliente.
- `Activo` inicial = `true` en Crear.

Auditoría: `Producto`, acciones `CREAR_PRODUCTO`, `ACTUALIZAR_PRODUCTO`, `ACTIVAR_PRODUCTO`, `INACTIVAR_PRODUCTO`. Snapshots con `IdCategoriaProducto`, `Nombre`, `Descripcion`, `Precio`, `Activo`.

## 5. IMPUESTOS

Controller propuesto: `ImpuestosController`. View: `Views/Impuestos/Index.cshtml`.

Acciones:

- `GET Index` → `View()`.
- `GET Buscar(string? termino)` → lista JSON (incl. nombre de empresa), filtrada por empresa del administrador (`IdEmpresa` claim) — Impuesto es por empresa.
- `POST Crear([FromBody])` + antiforgery.
- `POST Editar([FromBody])` + antiforgery.
- `POST Activar(int id)` + antiforgery.
- `POST Inactivar(int id)` + antiforgery.

Validaciones server-side:

- `IdEmpresa` obligatorio, `> 0`, validar que la empresa existe y está activa (server-side, nunca confiar solo en el cliente).
- `Nombre` obligatorio, sin espacios al inicio/fin, max 150.
- `Tasa` decimal(9,4): **`0 <= Tasa <= 100`**, rechazar negativos y valores fuera de rango.
- Racionalidad con Ventas: para impuestos `IncluidoEnPrecio`, `100 + Tasa` es divisor; con `Tasa >= 0` el denominador siempre es `> 0`. No permitir tasas negativas.
- `IncluidoEnPrecio` booleano, respetar exactamente la semántica que Ventas ya usa (no inventar comportamiento fiscal).
- Duplicado: la BD impone índice único `(IdEmpresa, Nombre)`; pre-consulta + captura de `DbUpdateException`.

Auditoría: `Impuesto`, acciones `CREAR_IMPUESTO`, `ACTUALIZAR_IMPUESTO`, `ACTIVAR_IMPUESTO`, `INACTIVAR_IMPUESTO`. Snapshots con `IdEmpresa`, `Nombre`, `Tasa`, `IncluidoEnPrecio`, `Activo`.

## 6. PRODUCTO ↔ IMPUESTO

Usar la entidad `ProductoImpuesto` existente (clave compuesta `(IdProducto, IdImpuesto)`).

Dentro de la administración de Producto, sin obligar a editar la DB:

- Endpoint `GET ObtenerImpuestos(int idProducto)` → `{ asignados: [...], disponibles: [...] }` con los impuestos activos de la empresa del administrador (claim `IdEmpresa`) no asignados aún.
- Endpoint `POST AsignarImpuesto([FromBody] AsignarImpuestoViewModel)` + antiforgery: `{ IdProducto, IdImpuesto }`.
- Endpoint `POST QuitarImpuesto([FromBody] QuitarImpuestoViewModel)` + antiforgery: `{ IdProducto, IdImpuesto }`.

Requisitos:

- Mostrar impuestos activos disponibles.
- Mostrar impuestos actualmente asignados.
- Asignar.
- Desasignar.
- Impedir asociación duplicada: la clave primaria compuesta **ya lo garantiza a nivel BD**; pre-consulta amigable + captura de `DbUpdateException` (`"Este impuesto ya está asignado al producto."`).
- Validar existencia de `Producto` y `Impuesto` server-side; `Impuesto.Activo == true` requerido para asignar.
- Operación sobre la tabla de asociación: **borrado físico de la fila `ProductoImpuesto`** (Remove). El modelo no soporta borrado lógico (no tiene `Activo`/`FechaCreacion`); NO inventar borrado lógico.
- Integridad `Restrict`: no afecta aquí porque no se eliminan entidades principales.

Auditoría: entidad `ProductoImpuesto`, `IdEntidad = "{IdProducto}:{IdImpuesto}"`, acciones `ASIGNAR_IMPUESTO` y `QUITAR_IMPUESTO`. Snapshots con `{ IdProducto, IdImpuesto }` únicamente.

## 7. MÉTODOS DE PAGO

Controller propuesto: `MetodosPagoController`. View: `Views/MetodosPago/Index.cshtml`.

Acciones:

- `GET Index` → `View()`.
- `GET Buscar(string? termino)` → lista JSON (incl. nombre de empresa), filtrada por empresa del administrador.
- `POST Crear([FromBody])` + antiforgery.
- `POST Editar([FromBody])` + antiforgery.
- `POST Activar(int id)` + antiforgery.
- `POST Inactivar(int id)` + antiforgery.

Validaciones server-side:

- `IdEmpresa` obligatorio, `> 0`, validar existencia y activo.
- `Nombre` obligatorio, sin espacios al inicio/fin, max 150.
- `Codigo` obligatorio, sin espacios al inicio/fin, max 50.
- Duplicado: la BD impone índice único `(IdEmpresa, Codigo)`; pre-consulta + `DbUpdateException`.
- `RequiereReferencia` y `PermiteCambio` booleanos, gestionados desde la UI.
- No codificar comportamiento especial por nombre visible (EFECTIVO/TARJETA) cuando el modelo ya expresa el comportamiento con propiedades: usar siempre `RequiereReferencia`/`PermiteCambio`.
- No romper EFECTIVO/TARJETA existentes: al editar se permite cambiar propiedades, pero el registro existente (con sus `Pagos` históricos que conservan `MetodoPago` = `Codigo` snapshot) debe permanecer íntegro.

Nota de integridad: `Pago.MetodoPago` es un snapshot de `Codigo`; editar `Codigo` no altera pagos históricos. No hay impacto de schema.

Auditoría: `MetodoPago`, acciones `CREAR_METODO_PAGO`, `ACTUALIZAR_METODO_PAGO`, `ACTIVAR_METODO_PAGO`, `INACTIVAR_METODO_PAGO`. Snapshots con `IdEmpresa`, `Nombre`, `Codigo`, `RequiereReferencia`, `PermiteCambio`, `Activo`.

## 8. AUDITORÍA

Todas las mutaciones relevantes usan `IAuditoriaService.RegistrarAsync`. El servicio ya resuelve `IdEmpresa/IdSucursal/IdCaja/IdSesionCaja/IdUsuario` desde claims y registra IP/fecha.

Acciones mínimas:

- `CREAR_CATEGORIA`, `ACTUALIZAR_CATEGORIA`, `ACTIVAR_CATEGORIA`, `INACTIVAR_CATEGORIA`
- `CREAR_PRODUCTO`, `ACTUALIZAR_PRODUCTO`, `ACTIVAR_PRODUCTO`, `INACTIVAR_PRODUCTO`
- `CREAR_IMPUESTO`, `ACTUALIZAR_IMPUESTO`, `ACTIVAR_IMPUESTO`, `INACTIVAR_IMPUESTO`
- `ASIGNAR_IMPUESTO`, `QUITAR_IMPUESTO`
- `CREAR_METODO_PAGO`, `ACTUALIZAR_METODO_PAGO`, `ACTIVAR_METODO_PAGO`, `INACTIVAR_METODO_PAGO`

Snapshots:

- Sin secretos.
- Únicamente datos relevantes.
- Anteriores/nuevos cuando corresponda (Editar/Inactivar/Activar).

## 9. UI

Mantener estilo administrativo existente (patrón `Cajas/Index.cshtml`):

- Tabla + modal Bootstrap.
- Debounce de búsqueda 300 ms.
- `@Html.AntiForgeryToken()` en la vista y lectura del token en JS para `RequestVerificationToken`.
- Partial `_Toast` para notificaciones.
- `respuestaJson` idéntica al patrón existente (401 → Login, 403 → toast, no-ok → toast, try/catch).
- Sin jQuery nuevo.

Navegación en `_Layout.cshtml`, menú desplegable **Administración** (bloque `User.IsInRole("Administrador")`), agregar:

- Categorías → `asp-controller="Categorias"`
- Productos → `asp-controller="Productos"`
- Impuestos → `asp-controller="Impuestos"`
- Métodos de Pago → `asp-controller="MetodosPago"`

Producto debe permitir administrar impuestos (sección 6) sin editar la DB.

## 10. VENTAS POS — REGRESIÓN

Fase 7 NO reescribirá `VentasController` salvo incompatibilidad demostrada. Tras configurar el catálogo mediante los nuevos módulos, Ventas debe seguir permitiendo:

- cargar categorías;
- cargar productos activos;
- calcular impuestos;
- crear comanda;
- agregar producto;
- registrar pago;
- cerrar comanda.

Los datos de la prueba de regresión deben provenir de los módulos de configuración real (catálogo creado vía UI/API de Fase 7), NO de bloques temporales en `Program.cs`.

## 11. SCHEMA / MIGRACIONES

Trabajar primero con el schema existente. NO crear migración automáticamente.

Hallazgos del READ (schema suficiente para todos los requisitos, sin migración):

- Índices únicos existentes cubren duplicados de Categoría (`Nombre`), Impuesto (`IdEmpresa, Nombre`) y MétodoPago (`IdEmpresa, Codigo`).
- Clave compuesta de `ProductoImpuesto` cubre la asociación duplicada.
- `Producto.Nombre` sin índice único: cubierto con validación a nivel de aplicación (política explícita de esta fase, sin schema).
- FK todos `Restrict`: no hay cascadas destructivas.

Si durante implementación se descubre que una función requerida exige cambio de schema: **DETENER** y reportar limitación, modelo afectado, cambio mínimo propuesto y motivo. Esperar autorización.

## 12. CONCURRENCIA

- No agregar `FOR UPDATE` indiscriminadamente.
- CRUD administrativo se apoya en constraints + validación server-side + transacciones solo cuando sean realmente necesarias.
- No usar `lock { }` ni flags en memoria como garantía.
- Para asociación `ProductoImpuesto` y duplicados de catálogo, la garantía real es la constraint de BD (clave única/primaria), reforzada por captura de `DbUpdateException` → mensaje amigable.

## 13. PRUEBAS OBLIGATORIAS

Antes de pruebas: seguir `SERVER_RUNNER.md` para START/READY/STOP.

Directorio temporal específico:

`C:\Users\Admin\AppData\Local\Temp\opencode\fase7`

Conservar al menos:

- `resultados.txt`
- evidencia relevante de concurrencia si existe
- `server.out`
- `server.err` cuando sean útiles

Batería mínima:

- **A. Autorización**: anónimo no accede; usuario no-Administrador no administra; Administrador sí accede.
- **B. Categorías**: crear; editar; buscar; duplicado rechazado; activar/inactivar; inactivación bloqueada con productos activos; body inválido/null controlado.
- **C. Productos**: crear; editar; buscar; categoría inválida rechazada; categoría inactiva rechazada; precio negativo rechazado; nombre requerido; activar/inactivar; body null controlado.
- **D. Impuestos**: crear; editar; buscar; tasa inválida rechazada; duplicado rechazado (por empresa); activar/inactivar; body null controlado.
- **E. Producto–Impuesto**: asignar; impedir duplicado; quitar; reasignar; producto inexistente rechazado; impuesto inexistente rechazado.
- **F. Métodos de pago**: crear; editar; buscar; activar/inactivar; `RequiereReferencia`; `PermiteCambio`; duplicados/valores inválidos.
- **G. Ventas — regresión** (con catálogo válido creado por los módulos): abrir/reanudar sesión necesaria; NuevaComanda; agregar producto; impuesto correcto; pago; cierre correcto.
- **H. Seguridad**: antiforgery; body null; snapshots sin secretos; aislamiento administrativo según autorización.

## 14. EVIDENCIA

No basta imprimir PASS/FAIL en consola. Guardar resumen persistente en `fase7\resultados.txt` con:

- nombre de cada prueba;
- PASS/FAIL;
- resumen total;
- fecha/hora;
- build usado;
- PID del servidor;
- resultado de cleanup.

NO guardar: passwords, cookies, antiforgery tokens, ConnectionStrings, secretos.

## 15. BUILD

Antes de ejecutar pruebas:

```powershell
dotnet build .\AtlasRestaurantPOS.slnx
```

Objetivo: **0 warnings / 0 errors**. Si el build falla: NO ejecutar servidor. Diagnosticar según manuales (`AgentInstructions/DOTNET.md`).

## 16. GIT

- Precondición: working tree limpio antes de comenzar.
- Baseline esperado: `f78e68d` — `Atlas Restaurant POS - Baseline inicial`.
- Durante la implementación NO hacer commit automáticamente.
- Al terminar la fase: dejar los cambios sin commit para permitir `/auditbuild`.
- No modificar el baseline. No usar reset destructivo.

## 17. CLEANUP

Al finalizar pruebas:

- servidor detenido;
- PID terminado;
- puerto 5250 libre;
- sin procesos propios huérfanos;
- sin código temporal;
- sin seed temporal en `Program.cs`;
- sin archivos de prueba dentro del repo.

Los datos de prueba en MySQL deben reportarse claramente. NO borrar datos mediante operaciones destructivas no autorizadas solo para "limpiar".

## 18. CRITERIO DE ÉXITO

Fase 7 solo puede reportarse COMPLETADA si:

- módulos administrativos funcionan;
- autorización funciona;
- validaciones funcionan;
- auditoría funciona;
- Producto–Impuesto funciona;
- Ventas continúa funcionando;
- build = 0 warnings / 0 errors;
- batería persistente = 0 FAIL;
- cleanup correcto.

Si existe cualquier FAIL: NO declarar la fase completada.

## 19. DECISIONES/POLÍTICAS EXPLÍCITAS (registro)

1. **Inactivación de categoría con productos activos**: bloqueada por la fase (política segura explícita). Otra política = decisión de producto → DETENER.
2. **Duplicados de Producto**: sin constraint real; la fase valida `(IdCategoriaProducto, Nombre)` a nivel de aplicación. Otra regla = decisión de producto → DETENER.
3. **Alcance de empresa**: Impuestos y Métodos de Pago son por empresa (claim `IdEmpresa` del administrador). Productos/Categorías globales. `ProductoImpuesto` cruza scopes; la lista de impuestos asignables se filtra por la empresa del administrador.
4. **`ProductoImpuesto`**: operación física de borrado de fila (Remove); sin borrado lógico inventado.
5. **Sin migración necesaria** según el READ. Cualquier necesidad real de schema → DETENER y esperar autorización.

## 20. FINAL

Durante la ejecución futura de esta fase:

1. seguir `read → edit/write → read → build → test → verify → cleanup → report`;
2. NO hacer commit;
3. NO actualizar `Phases/PROJECT_STATE.md` todavía;
4. ejecutar después `/auditbuild`;
5. solo tras revisión aprobada se actualizará `PROJECT_STATE.md` y se hará commit.