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

## Módulos todavía sin administración completa

Modelos presentes sin CRUD/UI administrativo completo:

- Mesa
- ConfiguracionPos
- Dispositivo
- IntegracionExterna

## Próximo objetivo funcional

Después de la administración comercial (Fase 7):

- Mesas / salón
- cocina / KDS
- inventario / recetas
- cancelaciones/devoluciones
- periféricos reales
- integraciones/APIs
- reportes
- permisos/configuración avanzada
- pruebas end-to-end finales

## Estado técnico actual verificable

- Fuente temporal de F6 eliminada de Program.cs.
- Program.cs limpio.
- Última DLL física detectada:
  `AtlasRestaurantPOS.Web\bin\Debug\net8.0\AtlasRestaurantPOS.Web.dll`
- No afirmar build limpio actual sin ejecutar `dotnet build`.
- No afirmar estado actual de MySQL sin consultar DB.
- No afirmar servidor activo: las últimas verificaciones disponibles indican servidor de prueba detenido.

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