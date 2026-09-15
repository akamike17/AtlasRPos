# FASE 10 — INVENTARIO, INSUMOS, RECETAS, EXISTENCIAS, CÓDIGOS DE BARRAS Y ETIQUETAS (Especificación)

> Documento de especificación de la Fase 10.
> NO implementa nada. NO modifica C#, Razor, JavaScript ni configuración.
> NO build. NO ejecución de aplicación. NO consulta a MySQL. NO migraciones. NO commit.
> Refleja el estado físico real inspeccionado en el repositorio al momento de escribir.

- Commit base esperado: `08d8492 Atlas Restaurant POS - Fase 9 Cancelaciones y devoluciones`
- Working tree al inicio: limpio.
- Solución: `AtlasRestaurantPOS.slnx`
- Proyecto web: `AtlasRestaurantPOS.Web` (.NET 8, MVC, EF Core 8, Pomelo MySQL)

---

## 1. Estado físico inspeccionado

Hechos verificados por lectura directa; no inferencias.

### 1.1 Modelos existentes (sin soporte de inventario)

- `Models/Producto.cs`: `IdProducto`, `IdCategoriaProducto`, `Nombre`, `Descripcion?`, `Precio` (decimal 18,2), `Activo`, `FechaCreacion`. **NO tiene** `Codigo` ni `CodigoBarras` ni relación a recetas.
- `Models/CategoriaProducto.cs`: `IdCategoriaProducto`, `Nombre` (único en DB), `Descripcion?`, `Activo`. Global (sin `IdEmpresa`).
- `Models/Comanda.cs`: incluye Fase 9 (`FechaCancelacion?`, `MotivoCancelacion?`, `IdUsuarioCancelacion?`); `Estado` (ABIERTA/CERRADA/CANCELADA/EN_PROCESO), `Folio?`, `Total`, `IdSucursal`, `IdCaja`, `IdSesionCaja?`, `IdMesa?`, `IdUsuario`.
- `Models/ComandaDetalle.cs`: `IdComandaDetalle`, `IdComanda`, `IdProducto`, `Cantidad` (**decimal(18,3)**), `PrecioUnitario` (18,2), `Importe` (18,2), `Notas?`. La precisión 3 ya es el precedente del proyecto para cantidades.
- `Models/Sucursal.cs`: `IdSucursal`, `IdEmpresa`, `Nombre`, `Direccion?`, `Telefono?`, `Activo`, `FechaCreacion`. Es el alcance correcto de existencia.
- `Models/Empresa.cs`: `IdEmpresa`, `Nombre`, `RazonSocial?`, `Rfc?`, `Telefono?`, `Correo?`, `Activo`, `FechaCreacion`.
- `Models/Usuario.cs`: incluye colecciones de Fase 9 (`ComandasCanceladas`, `PagosDevueltos`). Es la referencia de quién ordena movimientos.
- `Models/Dispositivo.cs` y `Constants/TiposDispositivo.cs`: ya existe la constante **`LECTOR_CODIGO`** (`IMPRESORA_TERMICA`, `IMPRESORA_COCINA`, `CAJON_DINERO`, `LECTOR_CODIGO`, `BASCULA`, `TERMINAL_PAGO`, `DISPLAY_CLIENTE`, `KDS`, `IMPRESORA_FISCAL`, `OTRO`). `Dispositivo` guarda `Nombre`, `Tipo`, `Fabricante?`, `Modelo?`, `Identificador?`, `Conexion?`, `Configuracion?`, `Activo`, `IdSucursal`, `IdCaja?`. **Configuración lógica, no acopla hardware.**
- `Models/IntegracionExterna.cs` y `Constants/TiposIntegracion.cs`: ya existe la constante **`INVENTARIO`** (`FACTURACION`, `PAGO`, `DELIVERY`, `ERP`, `CONTABILIDAD`, `INVENTARIO`, `ECOMMERCE`, `NOTIFICACIONES`, `FISCAL`, `OTRO`). `IntegracionExterna` guarda `Nombre`, `Tipo`, `Proveedor?`, `Endpoint?`, `Configuracion?`, `Activo`, `IdEmpresa?`, `IdSucursal?`. **Configuración lógica.**
- `Models/FolioSecuencia.cs`: `IdFolioSecuencia`, `IdSucursal`, `TipoDocumento`, `UltimoNumero` (long), `Prefijo?`, `Longitud`, `FechaModificacion`; índice único `(IdSucursal, TipoDocumento)`. Es la base probada para generación de códigos con `FOR UPDATE` (ver 1.4).
- `Models/MovimientoCaja.cs`, `Constants/TiposMovimientoCaja.cs`: movimientos de caja con `ENTRADA`, `SALIDA`, `VENTA`, `RETIRO`, `DEPOSITO`, `AJUSTE`, `DEVOLUCION` (Fase 9). No existe nada equivalente para inventario.

### 1.2 DbContext (`Data/AtlasRestaurantDbContext.cs`)

- Collation global: `utf8mb4_unicode_ci`.
- Patrón de unicidad actual:
  - Global: `Rol.Nombre`, `Usuario.UsuarioLogin`, `CategoriaProducto.Nombre`, `Caja (IdSucursal, Codigo)`, `Mesa (IdSucursal, Nombre)`, `Comanda (IdSucursal, Folio)`, `FolioSecuencia (IdSucursal, TipoDocumento)`.
  - Por Empresa: `Impuesto (IdEmpresa, Nombre)`, `MetodoPago (IdEmpresa, Codigo)`.
- **Producto NO tiene índice único en DB** (su duplicado `(IdCategoriaProducto, Nombre)` se valida a nivel de aplicación en `ProductosController.ValidarFormularioAsync`).
- `ComandaDetalle.Cantidad` con `HasPrecision(18, 3)`.
- Todas las FKs usan `DeleteBehavior.Restrict`.
- `Auditoria` con índices `(Fecha)`, `(IdUsuario, Fecha)`, `(Entidad, IdEntidad)` y FKs Restrict a Empresa/Sucursal/Caja/SesionCaja/Usuario.

### 1.3 Flujo de venta actual (punto crítico para consumo)

`Controllers/VentasController.cs` (1209 líneas, leído completo):

- `NuevaComanda` (POST, antiforgery): transacción `ReadCommitted`, `FolioComandaService.GenerarAsync` (`FOR UPDATE` sobre `FoliosSecuencia`), crea Comanda ABIERTA, auditoría `CREAR_COMANDA`.
- `AgregarProducto` / `ModificarCantidad` / `QuitarDetalle`: `ObtenerComandaOperativaAsync` (valida ownership + `FOR UPDATE` sobre Comanda), recalcula totales (`RecalcularComandaAsync` con impuestos incluidos/no incluidos).
- **`RegistrarPago` (líneas 502-715): el cierre de Comanda ocurre AQUÍ**, cuando `nuevoSaldo <= 0`: `comanda.Estado = EstadosComanda.CERRADA`, `FechaCierre`, libera Mesa con `FOR UPDATE` (auditoría `LIBERAR_MESA`), auditorías `REGISTRAR_PAGO` y `CERRAR_COMANDA`, todo en la misma transacción. **Este es el punto donde Fase 10 debe descontar inventario de forma atómica.**
- `CancelarComanda` (Fase 9, solo Administrador): rechaza comandas con pagos sin devolver; solo ABIERTA; libera Mesa (`LIBERAR_MESA_POR_CANCELACION`).
- `DevolverPago` (Fase 9, solo Administrador): marca `Devuelto`, genera `MovimientoCaja DEVOLUCION` solo si `EFECTIVO`; **no toca inventario**.
- `Estado`/`ConstruirEstadoAsync`: devuelve partidas y pagos al frontend.

### 1.4 Servicios y DI (`Program.cs`)

- DI existente: `IPasswordHasher`, `IInitialSetupService`, `IAuditoriaService`, `IFolioComandaService`, `HttpContextAccessor`.
- `Services/Comanda/FolioComandaService.cs`: patrón de secuencia **`SELECT ... FOR UPDATE`** sobre `Sucursales` y `FoliosSecuencia`; crea la fila de secuencia si no existe; incrementa y devuelve folio con `Prefijo` + `UltimoNumero` con `Longitud` de ceros. **Mecanismo reutilizable para códigos internos** (ver sección 7).
- `Services/Auditoria/AuditoriaService.cs`: `IAuditoriaService.RegistrarAsync(entidad, idEntidad, accion, datosAnteriores, datosNuevos)` — snapshots JSON; regla: nunca auditar secretos.

### 1.5 UI existente

- `Views/Ventas/Index.cshtml`: catálogo por pestañas de categorías activas con botones de productos (nombre + precio); comanda actual (partidas, cantidades, notas, cobro, cancelación Fase 9, pagos con VIGENTE/DEVUELTO). **No existe campo de escaneo ni búsqueda por texto**.
- `Views/Shared/_Layout.cshtml`: menú con Operación de Caja, Punto de Venta, Salón/Mesas, y dropdown Administración (Empresas, Sucursales, Roles, Usuarios, Cajas, Mesas, Categorías, Productos, Impuestos, Métodos de Pago).
- Controladores administrativos existentes (patrón: `Index` + `Buscar?termino=` + `Crear`/`Editar`/`Activar`/`Inactivar` con antiforgery, `[Authorize(Roles="Administrador")]`, `ValidarFormularioAsync`, auditoría): Categorias, Productos, Impuestos, MetodosPago, Mesas, Empresas, Sucursales, Roles, Usuarios, Cajas.

### 1.6 Soporte parcial para inventario (conclusión)

| Capacidad | ¿Existe? | Detalle |
|---|---|---|
| inventario / insumos | NO | ninguna entidad |
| unidades de medida | NO | ninguna entidad |
| recetas | NO | ninguna entidad |
| existencias | NO | ninguna entidad |
| movimientos de inventario | NO | solo `MovimientoCaja` (dinero) |
| códigos internos | PARCIAL | `FolioSecuencia` sirve de base (por Sucursal hoy) |
| códigos de barras | NO | ninguna columna |
| etiquetas | NO | nada |
| lectores/escáneres | PARCIAL | constante `TiposDispositivo.LECTOR_CODIGO` + entidad `Dispositivo` (configuración lógica) |
| integración inventario externa | PARCIAL | constante `TiposIntegracion.INVENTARIO` + entidad `IntegracionExterna` (configuración lógica) |

No se detectaron entidades duplicadas ni soporte parcial funcional que obligue a reutilizar algo existente para inventario.

---

## 2. Arquitectura propuesta

- Misma arquitectura del proyecto: ASP.NET Core MVC, EF Core 8, Pomelo MySQL, Services con DI, Fetch API, Bootstrap, JavaScript embebido.
- **Nuevo servicio de dominio**: `InventarioService` (`IInventarioService`) en `Services/Inventario/`, responsable de:
  - registrar Entrada/Ajuste/Merma/Devolución física (movimientos);
  - **consumo automático por venta** (llamado desde `RegistrarPago` al cerrar Comanda);
  - cálculo de consumo agregado, bloqueo de existencias en orden determinista y verificación de stock (rollback total si falta cualquiera);
  - generación de códigos internos (basado en secuencia con `FOR UPDATE`).
- **Nuevo servicio de identificación opcional**: `IdentificacionService` (`IIdentificacionService`) en `Services/Identificacion/`, desacoplado del hardware:
  - busca Producto/Insumo por `Codigo` o `CodigoBarras` (server-side);
  - el frontend solo envía el texto escaneado/escrito; el servidor decide e identifica la entidad real.
- **Nuevo servicio de etiquetas (previsualización)**: `EtiquetaService` (`IEtiquetaService`) en `Services/Etiquetas/`, que devuelve un `EtiquetaViewModel` (nombre, código, código de barras, unidad, precio si aplica). Sin impresión física.
- **Presentación de código de barras**: evaluación de librería desacoplada (ver sección 7.7); encapsulada en un servicio de presentación, NUNCA dentro de entidades EF.
- Consumo automático se integra en `VentasController.RegistrarPago` dentro de la transacción existente, sin romper el flujo actual de cierre de Comanda ni liberación de Mesa.
- Controllers nuevos propuestos: `UnidadesMedidaController`, `InsumosController`, `RecetasController`, `ExistenciasController` (consulta), `MovimientosInventarioController` (entradas/ajustes/mermas/historial), todos `[Authorize(Roles = "Administrador")]` salvo el escaneo en Ventas (usuario operativo con sesión válida).

Flujo principal:

```
INSUMO → EXISTENCIA POR SUCURSAL → RECETA DEL PRODUCTO → VENTA
→ CONSUMO AUTOMÁTICO (al cierre) → MOVIMIENTO DE INVENTARIO → EXISTENCIA ACTUALIZADA
```

Flujo opcional de identificación:

```
ETIQUETA / CÓDIGO DE BARRAS → ESCANEO → IDENTIFICACIÓN SERVER-SIDE → OPERACIÓN NORMAL
```

**Principio fundamental**: el inventario funciona completamente SIN códigos de barras, etiquetas ni lector. Códigos y etiquetas son capacidades OPCIONALES; el hardware jamás es dependencia del dominio.

---

## 3. Modelos (propuesta)

Se requieren **5 entidades nuevas** y **cambios mínimos en Producto e Insumo**. No se crean en esta tarea; FASE-10.md los define.

### 3.1 `UnidadMedida`

Catálogo administrable de unidades (KG, G, L, ML, PZA, y cualquier otra; NO hardcoded).

```csharp
public class UnidadMedida
{
    public int IdUnidadMedida { get; set; }
    public int IdEmpresa { get; set; }
    public string Codigo { get; set; } = string.Empty;   // ej. "KG", "PZA"; único por Empresa
    public string Nombre { get; set; } = string.Empty;   // ej. "Kilogramo", "Pieza"
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<Insumo> Insumos { get; set; } = new List<Insumo>();
}
```

- `Codigo`/`Nombre` con trim y sin espacios laterales; `Codigo` único `(IdEmpresa, Codigo)`.
- **No se incluye campo `Decimales`/precisión por unidad**: las cantidades usan `decimal(18,3)` uniforme (consistente con `ComandaDetalle.Cantidad` ya existente). Decisión documentada en secciones 9.9 y 16 (decisión 6); si una fase futura lo justifica, se agrega.

### 3.2 `Insumo`

Producto (lo que se vende) e Insumo (materia prima/recurso que se consume) son conceptos diferentes.

```csharp
public class Insumo
{
    public int IdInsumo { get; set; }
    public int IdEmpresa { get; set; }
    public int IdUnidadMedida { get; set; }
    public string? Codigo { get; set; }            // código interno opcional, único por Empresa
    public string? CodigoBarras { get; set; }      // opcional, único por Empresa
    public string Nombre { get; set; } = string.Empty;
    public decimal CostoReferencia { get; set; }   // decimal(18,2), valuación básica
    public decimal StockMinimo { get; set; }       // decimal(18,3), default 0
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public UnidadMedida UnidadMedida { get; set; } = null!;
    public ICollection<ExistenciaInsumo> Existencias { get; set; } = new List<ExistenciaInsumo>();
    public ICollection<RecetaProducto> Recetas { get; set; } = new List<RecetaProducto>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}
```

- `CodigoBarras` **opcional**; no se impide crear Insumos sin código (ambos nullables).
- `CostoReferencia` para valuación básica; sin PEPS/UEPS/promedio ponderado/contabilidad (fuera de alcance).

### 3.3 `ExistenciaInsumo`

Stock por **Sucursal + Insumo** (dos sucursales tienen stocks independientes).

```csharp
public class ExistenciaInsumo
{
    public int IdSucursal { get; set; }
    public int IdInsumo { get; set; }
    public decimal CantidadActual { get; set; }    // decimal(18,3)
    public DateTime FechaModificacion { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
}
```

- **PK compuesta** `(IdSucursal, IdInsumo)` — evita duplicados naturalmente.
- `CantidadActual` **jamás se edita desde CRUD**; toda variación produce `MovimientoInventario` (ver sección 10).

### 3.4 `RecetaProducto`

Relación `Producto → Insumo → Cantidad`.

```csharp
public class RecetaProducto
{
    public int IdRecetaProducto { get; set; }
    public int IdProducto { get; set; }
    public int IdInsumo { get; set; }
    public decimal Cantidad { get; set; }          // decimal(18,3), > 0

    public Producto Producto { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
}
```

- Índice único `(IdProducto, IdInsumo)` → evita duplicado Producto/Insumo.
- `Cantidad > 0` (validado en servidor).
- Producto **sin receta** puede venderse sin afectar inventario.

### 3.5 `MovimientoInventario`

Historial inmutable (append-only).

```csharp
public class MovimientoInventario
{
    public long IdMovimientoInventario { get; set; }
    public int IdSucursal { get; set; }
    public int IdInsumo { get; set; }
    public int IdUsuario { get; set; }
    public long? IdComanda { get; set; }
    public long? IdComandaDetalle { get; set; }
    public int? IdCaja { get; set; }
    public long? IdSesionCaja { get; set; }
    public string Tipo { get; set; } = string.Empty;      // ver 3.6
    public decimal Cantidad { get; set; }                 // decimal(18,3)
    public decimal ExistenciaAnterior { get; set; }       // decimal(18,3)
    public decimal ExistenciaNueva { get; set; }          // decimal(18,3)
    public decimal? CostoUnitario { get; set; }           // decimal(18,2) opcional
    public string Concepto { get; set; } = string.Empty;  // motivo/referencia
    public DateTime FechaMovimiento { get; set; }

    public Sucursal Sucursal { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Comanda? Comanda { get; set; }
    public ComandaDetalle? ComandaDetalle { get; set; }
    public Caja? Caja { get; set; }
    public SesionCaja? SesionCaja { get; set; }
}
```

- FKs Restrict (patrón del proyecto); `IdComanda`/`IdComandaDetalle`/`IdCaja`/`IdSesionCaja` opcionales (consumo por venta y trazabilidad multicaja).
- `CostoUnitario` opcional para valuación básica.
- `ExistenciaAnterior`/`ExistenciaNueva` capturan el antes/después → historial auditable e inmutable.
- **No confundir `DEVOLUCION_INVENTARIO` con la devolución financiera de Fase 9** (sección 12).

### 3.6 `Constants/TiposMovimientoInventario.cs` (nueva)

```csharp
public static class TiposMovimientoInventario
{
    public const string ENTRADA = "ENTRADA";
    public const string VENTA = "VENTA";
    public const string AJUSTE_POSITIVO = "AJUSTE_POSITIVO";
    public const string AJUSTE_NEGATIVO = "AJUSTE_NEGATIVO";
    public const string MERMA = "MERMA";
    public const string DEVOLUCION_INVENTARIO = "DEVOLUCION_INVENTARIO";
}
```

### 3.7 Cambios mínimos en `Producto` (identificación opcional)

```csharp
public class Producto
{
    // ... existentes ...
    public string? Codigo { get; set; }          // código interno opcional, único (ver 6)
    public string? CodigoBarras { get; set; }    // opcional, único (ver 6)
    public ICollection<RecetaProducto> Recetas { get; set; } = new List<RecetaProducto>();
}
```

- `Codigo`/`CodigoBarras` sirven únicamente para IDENTIFICAR. Nunca contienen/predefinen precio, impuestos, categoría, cantidad ni estado: todo eso permanece server-side (precio e impuestos se recuperan de la DB en `AgregarProducto`).
- No asumir EAN-13 únicamente: conceptualmente soporta EAN, UPC, Code 128 y códigos internos de Atlas, sin implementar manualmente cada estándar (el código es una cadena de identificación; la validación de formato es opcional y ligera).

### 3.8 DbSet y configuración (`AtlasRestaurantDbContext`)

DbSets nuevos: `UnidadesMedida`, `Insumos`, `ExistenciasInsumo`, `RecetasProducto`, `MovimientosInventario`.
Configuraciones `ConfigureUnidadMedida/ConfigureInsumo/ConfigureExistenciaInsumo/ConfigureRecetaProducto/ConfigureMovimientoInventario` siguiendo el patrón existente (FK Restrict, precisión, índices — sección 4/5).

---

## 4. Schema exacto propuesto

Convenciones del proyecto: collation global `utf8mb4_unicode_ci`; columnas de código con collation **`utf8mb4_bin`** (case-sensitive, ver sección 5.3); FKs `Restrict`; PK autoincrementales (`UseMySqlIdentityColumn`), salvo `ExistenciaInsumo` (PK compuesta).

### Tabla `UnidadesMedida`

| Columna | Tipo | Nulabilidad | Notas |
|---|---|---|---|
| IdUnidadMedida | int PK identity | NO | |
| IdEmpresa | int | NO | FK → Empresas (Restrict) |
| Codigo | varchar(20) | NO | índice único `(IdEmpresa, Codigo)` |
| Nombre | varchar(100) | NO | |
| Activo | tinyint(1) | NO | |
| FechaCreacion | datetime(6) | NO | |

### Tabla `Insumos`

| Columna | Tipo | Nulabilidad | Notas |
|---|---|---|---|
| IdInsumo | int PK identity | NO | |
| IdEmpresa | int | NO | FK → Empresas (Restrict) |
| IdUnidadMedida | int | NO | FK → UnidadesMedida (Restrict) |
| Codigo | varchar(50) | SÍ | índice único `(IdEmpresa, Codigo)`; NULL múltiple permitido en MySQL |
| CodigoBarras | varchar(50) | SÍ | índice único `(IdEmpresa, CodigoBarras)`; NULL múltiple permitido |
| Nombre | varchar(150) | NO | |
| CostoReferencia | decimal(18,2) | NO | default 0 |
| StockMinimo | decimal(18,3) | NO | default 0 |
| Activo | tinyint(1) | NO | |
| FechaCreacion | datetime(6) | NO | |

### Tabla `ExistenciasInsumo`

| Columna | Tipo | Nulabilidad | Notas |
|---|---|---|---|
| IdSucursal | int | NO | PK compuesta; FK → Sucursales (Restrict) |
| IdInsumo | int | NO | PK compuesta; FK → Insumos (Restrict) |
| CantidadActual | decimal(18,3) | NO | default 0 |
| FechaModificacion | datetime(6) | NO | |

### Tabla `RecetasProducto`

| Columna | Tipo | Nulabilidad | Notas |
|---|---|---|---|
| IdRecetaProducto | int PK identity | NO | |
| IdProducto | int | NO | FK → Productos (Restrict) |
| IdInsumo | int | NO | FK → Insumos (Restrict) |
| Cantidad | decimal(18,3) | NO | > 0 |

Índice único `(IdProducto, IdInsumo)`.

### Tabla `MovimientosInventario`

| Columna | Tipo | Nulabilidad | Notas |
|---|---|---|---|
| IdMovimientoInventario | bigint PK identity | NO | |
| IdSucursal | int | NO | FK → Sucursales (Restrict) |
| IdInsumo | int | NO | FK → Insumos (Restrict) |
| IdUsuario | int | NO | FK → Usuarios (Restrict) |
| IdComanda | bigint | SÍ | FK → Comandas (Restrict) |
| IdComandaDetalle | bigint | SÍ | FK → ComandaDetalles (Restrict) |
| IdCaja | int | SÍ | FK → Cajas (Restrict) |
| IdSesionCaja | bigint | SÍ | FK → SesionesCaja (Restrict) |
| Tipo | varchar(50) | NO | `TiposMovimientoInventario` |
| Cantidad | decimal(18,3) | NO | |
| ExistenciaAnterior | decimal(18,3) | NO | |
| ExistenciaNueva | decimal(18,3) | NO | |
| CostoUnitario | decimal(18,2) | SÍ | opcional |
| Concepto | varchar(500) | NO | motivo/referencia |
| FechaMovimiento | datetime(6) | NO | |

### Cambios en `Productos`

| Columna | Tipo | Nulabilidad | Notas |
|---|---|---|---|
| Codigo | varchar(50) | SÍ | índice único (scope: ver sección 6) |
| CodigoBarras | varchar(50) | SÍ | índice único (scope: ver sección 6) |

---

## 5. Migración propuesta e índices

### 5.1 Nombre de migración propuesto

**`AgregarInventarioInsumos`**

### 5.2 Orden compatible con MySQL (Up)

1. Crear `UnidadesMedida` (sin dependencias).
2. Crear `Insumos` (FK a `UnidadesMedida` y `Empresas`).
3. Crear `ExistenciasInsumo` (FK a `Sucursales` e `Insumos`; PK compuesta).
4. Crear `RecetasProducto` (FK a `Productos` e `Insumos`).
5. Crear `MovimientosInventario` (FK a `Sucursales`, `Insumos`, `Usuarios`, `Comandas`, `ComandaDetalles`, `Cajas`, `SesionesCaja`).
6. `AddColumn` `Codigo` y `CodigoBarras` en `Productos` (nullables).
7. Crear índices únicos y de búsqueda (después de las columnas).

### 5.3 Índices

| Tabla | Índice | Único | Propósito |
|---|---|---|---|
| UnidadesMedida | `(IdEmpresa, Codigo)` | SÍ | catálogo por empresa, sin duplicados |
| Insumos | `(IdEmpresa, Codigo)` | SÍ | unicidad de código interno |
| Insumos | `(IdEmpresa, CodigoBarras)` | SÍ | unicidad de código de barras |
| Insumos | `(IdUnidadMedida)` | NO | FK |
| Insumos | `(Nombre)` | NO | búsqueda administrativa |
| ExistenciasInsumo | PK `(IdSucursal, IdInsumo)` | SÍ | par único; PK compuesta |
| RecetasProducto | `(IdProducto, IdInsumo)` | SÍ | sin duplicado de receta |
| RecetasProducto | `(IdInsumo)` | NO | FK/inversa |
| MovimientosInventario | `(IdInsumo, FechaMovimiento)` | NO | historial por insumo |
| MovimientosInventario | `(IdSucursal, FechaMovimiento)` | NO | historial por sucursal |
| MovimientosInventario | `(IdComanda)` | NO | trazabilidad por venta |
| MovimientosInventario | `(IdUsuario)` | NO | FK |
| MovimientosInventario | `(IdCaja)`, `(IdSesionCaja)`, `(IdComandaDetalle)` | NO | FK |
| Productos | `Codigo` | SÍ | identificación única |
| Productos | `CodigoBarras` | SÍ | identificación única |

**Impacto MySQL de índices de códigos de barras**:

- MySQL InnoDB permite **múltiples NULL** en índices únicos → `Codigo`/`CodigoBarras` nullables con índice único funcionan correctamente (varios insumos/productos sin código).
- Longitud: `varchar(50)` en `utf8mb4` = máx. 200 bytes por columna; índices únicos compuestos con `IdEmpresa` (int, 4 bytes) quedan muy por debajo del límite InnoDB (3072 bytes con DYNAMIC). Sin riesgo.
- **Collation recomendada `utf8mb4_bin` (case-sensitive) para columnas de código**: evita que `abc` y `ABC` (diferentes en Code 128) colisionen en el índice único case-insensitive global (`utf8mb4_unicode_ci`). Aplica a `Productos.Codigo`, `Productos.CodigoBarras`, `Insumos.Codigo`, `Insumos.CodigoBarras` (definir con `.UseCollation("utf8mb4_bin")` y especificar `collation: "utf8mb4_bin"` en la migración).
- `Down()` debe eliminar índices antes de columnas y tablas en orden inverso (hijas → padres).

### 5.4 DeleteBehavior / nulabilidad / precisión

- Todas las FKs nuevas: `DeleteBehavior.Restrict` (patrón del proyecto).
- `Cantidad`/`CantidadActual`/`ExistenciaAnterior`/`ExistenciaNueva`/`StockMinimo`: `decimal(18,3)`. Dinero (`CostoReferencia`, `CostoUnitario`, `Precio`): `decimal(18,2)`. **Nunca double/float.**
- `Concepto`: varchar(500) obligatorio; `CostoUnitario` nullable.
- Sin operaciones destructivas (sin DROP de tablas existentes, sin columnas no-null sobre datos existentes).

---

## 6. Unicidad de códigos y scope

### 6.1 Decisión de scope

- **Insumo** (pertenece a Empresa, como `Impuesto`/`MetodoPago`): unicidad **por Empresa** → `(IdEmpresa, Codigo)` y `(IdEmpresa, CodigoBarras)`.
- **UnidadMedida** (pertenece a Empresa): unicidad **por Empresa** → `(IdEmpresa, Codigo)`.
- **Producto**: el catálogo de `Producto`/`CategoriaProducto` es **GLOBAL hoy** (sin `IdEmpresa`). Para no imponer una política incompatible con el modelo actual ni tocar la cadena de Ventas (que filtra por categoría activa + producto activo, no por empresa), la unicidad de `Producto.Codigo` y `Producto.CodigoBarras` será **GLOBAL** (índices únicos simples). **Desalineación de scope documentada**: si en una fase futura `Producto` pasa a pertenecer a Empresa, la unicidad deberá migrar a `(IdEmpresa, Codigo)`; no se hace en Fase 10 para no ampliar alcance.

### 6.2 Reglas de colisión Producto ↔ Insumo

- Los códigos de Producto e Insumo viven en tablas distintas con índices propios. Para que un escaneo **no sea ambiguo**, la identificación en Ventas busca **primero Producto** (por `Codigo`/`CodigoBarras`) y en inventario busca **primero Insumo**; cada contexto usa su tabla. La colisión entre un código de Producto y uno de Insumo es aceptable y sin ambigüedad porque cada flujo resuelve en su dominio. (Se documenta como decisión; alternativa de código de prefijo único global queda abierta si el usuario la prefiere.)

---

## 7. Códigos internos de Atlas y códigos de barras

### 7.1 Decisión de generación

**Opción C) ambos** — código administrable manualmente O generado por Atlas:

- El administrador puede asignar `Codigo`/`CodigoBarras` manualmente (códigos comerciales: EAN, UPC, Code 128, o internos).
- Si un Producto/Insumo queda sin código comercial, Atlas puede **generar un código interno estable** mediante un mecanismo de secuencia persistente (extensión del patrón `FolioComandaService`).

### 7.2 Requisitos del código interno

- Único (índice único del schema).
- **No cambia al renombrar** (es un campo persistido, no derivado del nombre).
- Apto para imprimirse posteriormente como Code 128 (cadena alfanumérica corta, sin caracteres problemáticos).
- Seguro ante concurrencia.

### 7.3 Mecanismo propuesto (sin MAX+1 / static / lock C#)

- **Nueva tabla `SecuenciasCodigo`** (infraestructura, no dominio): `IdSecuenciaCodigo` (int PK), `IdEmpresa` (int), `TipoEntidad` (varchar(50): `PRODUCTO`/`INSUMO`), `UltimoNumero` (long), `Prefijo?` (varchar(20), ej. `P-`/`I-`), `Longitud` (int, ej. 6), `FechaModificacion`, índice único `(IdEmpresa, TipoEntidad)`.
- `CodigoInternoService.GenerarAsync(idEmpresa, tipoEntidad)` replica el patrón probado de `FolioComandaService`: `SELECT ... FOR UPDATE` sobre la fila de secuencia; si no existe, la crea (dentro de la misma transacción); incrementa y devuelve `Prefijo + UltimoNumero.ToString("D" + Longitud)`.
- Esto cumple: único por Empresa+tipo, estable, imprimible, seguro ante concurrencia (el lock de fila es persistente en DB).
- Alternativa evaluada y **descartada**: reutilizar `FoliosSecuencia` existente, porque su índice único es `(IdSucursal, TipoDocumento)` y generaría códigos repetidos entre sucursales (incompatible con unicidad por Empresa). No se toca `FoliosSecuencia` para evitar regresión en `COMANDA`.

### 7.4 Identificación server-side (escáner)

- `IdentificacionService.BuscarProductoPorCodigo(texto)` y `BuscarInsumoPorCodigo(texto)`:
  1. normalizar (trim) el texto recibido;
  2. buscar por `Codigo` o `CodigoBarras` (índice único);
  3. devolver la entidad real (con precio/impuestos/activo para Producto; con unidad/costo para Insumo).
- El servidor **nunca confía en información proveniente del escáner** (solo recibe el texto; el resto se lee de la DB).
- Sin coincidencia → mensaje controlado ("Código no encontrado."). **NO se crea Producto/Insumo automáticamente.**

### 7.5 Lectura en Ventas (opcional)

- Campo de escaneo en `Views/Ventas/Index.cshtml` (input con foco opcional).
- Lector USB tipo teclado escribe el código y envía `Enter` → JavaScript (evento `keydown`/`keyup` en el input) → `fetch` a endpoint de Ventas → el servidor identifica el Producto (activo + categoría activa) → **agrega a la Comanda actual** (misma lógica que `AgregarProducto`, precio/impuestos server-side).
- Coexiste con categorías, botones y selección visual; la escritura manual equivalente siempre funciona.
- Producto inactivo o categoría inactiva → rechazado con mensaje controlado.
- Si no hay comanda actual → se crea primero (flujo normal de `NuevaComanda`).

### 7.6 Lectura en Inventario (opcional)

- Endpoint de búsqueda por código usado en Entrada/Ajuste/Merma/Consulta de existencia:
  - escanear → servidor busca Insumo por `Codigo`/`CodigoBarras` → devuelve la entidad real → el usuario completa cantidad/operación.
- El escáner **nunca determina cantidades automáticamente** en Fase 10 (diseño futuro posible).

### 7.7 Generación visual de código de barras

- Evaluación: usar una **librería desacoplada de presentación** (candidata: `BarcodeLib` o `ZXing.Net`, ambas .NET, sin drivers) para renderizar Code 128/EAN como imagen.
- **Por qué**: la etiqueta necesita una representación imprimible; implementar el estándar manualmente es innecesario.
- **Dónde se encapsula**: en `Services/Etiquetas/` (servicio de presentación) o un tag helper/filtro de vista; **NUNCA dentro de entidades EF ni en el dominio**.
- **Cómo evitar acoplarla al dominio**: el dominio solo expone datos (`Codigo`/`CodigoBarras` como string); la librería queda confinada al servicio de presentación; se registra en DI como dependencia de presentación.
- **NO instalar dependencia durante esta tarea** (solo diseño). Se instalaría en la fase de implementación tras aprobación.

### 7.8 Lector como periférico

- No acoplar a marca específica. Soporte inicial preferido: **lector USB tipo teclado** (escribe + Enter), sin SDK/driver.
- Flujo UI: focus en campo de escaneo → lector escribe código → Enter → JavaScript envía búsqueda → servidor valida → operación normal.
- Debe permitir escritura manual equivalente.
- Lectores especiales futuros: modelar con `Dispositivo` (`Tipo = TiposDispositivo.LECTOR_CODIGO`, ya existe) + `Service` desacoplado (fuera de Fase 10).

---

## 8. Etiquetas

- Soporte **opcional** de etiquetas para Producto e Insumo.
- Una etiqueta puede contener: nombre, código interno, código de barras, unidad (Insumo) y precio (Producto cuando corresponda).
- Fase 10 **NO necesita impresión física real**; deja preparado:
  1. datos necesarios (ya en el modelo: `Codigo`, `CodigoBarras`, `Nombre`, `UnidadMedida`, `Precio`);
  2. endpoint/viewmodel de **previsualización**: `EtiquetaViewModel { Nombre, Codigo, CodigoBarras, Unidad, Precio? , Tipo }` servido por `EtiquetaService`; la vista renderiza una tarjeta HTML con el código de barras (imagen del servicio de presentación) y los datos;
  3. plantilla lógica básica (Razor parcial reutilizable para Producto e Insumo).
- La impresión real se hará en la fase de Periféricos/Impresión.

---

## 9. Reglas de inventario

### 9.1 Existencia

- `ExistenciaInsumo` por `(IdSucursal, IdInsumo)`; dos sucursales tienen stocks independientes.
- `CantidadActual` solo cambia a través de movimientos (`MovimientoInventario`). CRUD no la edita.
- Cuando una operación toca un insumo sin fila de existencia: en Entrada/Ajuste positivo se crea la fila (CantidadActual = 0 → se aplica el movimiento); en consumo se trata como 0 (insuficiente). El alta inicial debe manejarse de forma segura ante concurrencia (captura de `DuplicateKeyException` del índice único y re-lock, o `INSERT ... ON DUPLICATE KEY UPDATE` acotado) — detalle a resolver en implementación.

### 9.2 Entradas

- Flujo: seleccionar/escanear Insumo → cantidad (> 0) → costo opcional → concepto/referencia → actualizar Existencia → crear `MovimientoInventario` tipo `ENTRADA`.
- El servidor obtiene Sucursal, Usuario, Fecha y existencia anterior/nueva (nunca el cliente).
- Transaccional; auditoría `ENTRADA_INVENTARIO`.

### 9.3 Ajustes

- Nunca editar stock silenciosamente.
- `AJUSTE_POSITIVO` / `AJUSTE_NEGATIVO`; **motivo obligatorio**; auditoría obligatoria (`AJUSTE_INVENTARIO`).
- `AJUSTE_NEGATIVO` con cantidad > 0 pero validada contra disponibilidad (no puede dejar stock negativo; ver 9.5).

### 9.4 Merma

- Tipo `MERMA`; cantidad > 0; **reduce stock**; motivo obligatorio (caducidad, derrame, daño, desperdicio...).
- No necesita catálogo rígido todavía (concepto libre en `Concepto`).
- Auditoría `MERMA_INVENTARIO`.

### 9.5 Stock negativo — política

- **NO permitir stock negativo.**
- Antes de cerrar una venta (y en AJUSTE_NEGATIVO/MERMA):
  1. calcular consumo total de todos los productos de la comanda (`CantidadVendida × CantidadReceta`);
  2. agrupar por Insumo;
  3. bloquear existencias en **orden determinista por IdInsumo ascendente** (`SELECT ... FOR UPDATE`);
  4. verificar disponibilidad de TODOS;
  5. si falta cualquiera → **rollback del cierre completo**; NO consumir parcialmente;
  6. el mensaje identifica de forma controlada qué Insumo tiene stock insuficiente (ej. "Stock insuficiente de Carne (disponible 0.180, requerido 0.360).").
- Para el consumo automático, el rechazo del cierre completo impide la venta (el usuario debe ajustar la comanda o reponer stock).

### 9.6 Concurrencia de stock (crítico)

- Dos cajas de la misma Sucursal pueden vender simultáneamente productos que consumen el mismo Insumo.
- Esquema: transacción `ReadCommitted` + `SELECT ... FOR UPDATE` sobre `ExistenciasInsumo` en **orden determinista por IdInsumo** (menor → mayor) para evitar deadlocks (mismo patrón ya validado en `SalonController.TransferirMesa`).
- Nunca `lock { }` C#, static, ni flags en memoria.
- `RegistrarPago` ya bloquea la Comanda con `FOR UPDATE`; el bloqueo de existencias se añade en la misma transacción, después de confirmar el cierre y antes del `CommitAsync`.

### 9.7 Multisucursal

- La venta de Sucursal A **no modifica** stock de Sucursal B: la fila de existencia es por `IdSucursal` (claim `IdSucursal` del usuario).
- La receta es por `(IdProducto, IdInsumo)` (sin sucursal): se consume la existencia de la sucursal que vende.

### 9.8 Multicaja

- Dos Cajas de la misma Sucursal **comparten stock** → el locking DB es obligatorio (9.6).

### 9.9 Precisión y costos

- Cantidades: `decimal(18,3)` (consistente con `ComandaDetalle.Cantidad`). Dinero: `decimal(18,2)`. Nunca double/float.
- `CostoReferencia`/`CostoUnitario` para valuación básica; **no** PEPS/UEPS/promedio ponderado complejo/contabilidad en Fase 10.

### 9.10 Reportes básicos

- existencias actuales;
- bajo mínimo (CantidadActual <= StockMinimo);
- movimientos por fecha;
- movimientos por Insumo;
- consumo por venta (movimientos `VENTA` agrupados por comanda/insumo).
- No es suite gerencial completa.

---

## 10. Integración con Ventas (consumo automático)

### 10.1 Punto de descuento

- Política preferida (y adoptada): **descontar inventario al cierre definitivo de la Comanda** (en `RegistrarPago` cuando `nuevoSaldo <= 0` y la comanda pasa a `CERRADA`).
- Razón: no consumir inventario por comandas abandonadas (una comanda ABIERTA puede cancelarse sin haber consumido físicamente).
- El descuento ocurre **dentro de la misma transacción** del cierre (atómico con el cobro): si el stock es insuficiente, el cierre se rechaza completo.

### 10.2 Cálculo

- Por cada detalle de la comanda: `CantidadVendida × CantidadReceta` por Insumo.
- Agregación por Insumo; luego bloqueo y verificación (9.5).
- Ejemplo: 2 hamburguesas × 0.180 KG carne = **0.360 KG carne**.
- Solo se consumen productos con receta; productos sin receta no afectan inventario.

### 10.3 Movimientos generados

- Un `MovimientoInventario` tipo `VENTA` por Insumo consumido, con `IdComanda`, `IdComandaDetalle` (opcional), `IdSucursal`, `IdCaja`, `IdSesionCaja`, `IdUsuario`, `ExistenciaAnterior`, `ExistenciaNueva`, `Cantidad` (negativa o positiva según convención; se propone almacenar el valor absoluto y derivar el signo del `Tipo`, igual que el patrón de `MovimientoCaja`).

### 10.4 Pago parcial

- El pago parcial NO cierra la comanda → NO consume. Solo el cierre definitivo consume.

---

## 11. Integración con Fase 9 (devoluciones y cancelaciones)

### 11.1 Regla fundamental

**DEVOLUCIÓN FINANCIERA NO implica automáticamente DEVOLUCIÓN FÍSICA DE INVENTARIO.** La comida/producto puede haber sido consumida.

- `DevolverPago` (Fase 9) **no** toca inventario (sin cambios en su lógica).
- `CancelarComanda` (Fase 9) solo opera sobre comandas ABIERTA **sin pagos**; y como el consumo ocurre al cierre, **una comanda ABIERTA cancelada no debería haber consumido stock**. Documentado: si el descuento ocurre al cierre, la cancelación de una ABIERTA no produce ni requiere devolución de inventario.

### 11.2 Devolución física / reposición explícita

- Fase 10 diseña una **operación explícita manual** de reposición física: movimiento tipo `DEVOLUCION_INVENTARIO` (cantidad > 0, aumenta stock) con motivo obligatorio, operada por Administrador (flujo similar a Entrada).
- **No se automatiza** desde `DevolverPago`; el operador decide si el producto regresó físicamente al inventario.

---

## 12. Autorización y auditoría

### 12.1 Autorización

- Administración (Unidades de Medida, Insumos, Recetas, Existencias, Entradas, Ajustes, Mermas, Movimientos, códigos/etiquetas): **`[Authorize(Roles = "Administrador")]`**.
- Consumo automático: sistema interno derivado de una venta autorizada (no requiere rol adicional; se ejecuta en el cierre).
- Escaneo en Ventas: usuario operativo con sesión de caja válida (mismo requisito que `AgregarProducto`).
- Fetch sin sesión → 401; Fetch sin permiso → 403; navegación sin sesión → 302 (patrón global ya configurado en `Program.cs`).

### 12.2 Auditoría (`IAuditoriaService`)

Acciones propuestas (snapshots sin secretos):

- `CREAR_UNIDAD_MEDIDA` / `ACTUALIZAR_UNIDAD_MEDIDA` / `ACTIVAR_UNIDAD_MEDIDA` / `INACTIVAR_UNIDAD_MEDIDA`
- `CREAR_INSUMO` / `ACTUALIZAR_INSUMO` / `ACTIVAR_INSUMO` / `INACTIVAR_INSUMO`
- `RECETA_AGREGAR_INSUMO` / `RECETA_MODIFICAR_CANTIDAD` / `RECETA_QUITAR_INSUMO`
- `ENTRADA_INVENTARIO`
- `AJUSTE_INVENTARIO` (positivo/negativo)
- `MERMA_INVENTARIO`
- `DEVOLUCION_INVENTARIO`
- `ACTUALIZAR_CODIGO_PRODUCTO` / `ACTUALIZAR_CODIGO_INSUMO` (cambios relevantes de `Codigo`/`CodigoBarras`)

Separación de responsabilidades:

- `MovimientoInventario` registra el **movimiento físico** (historial inmutable).
- `Auditoria` registra **quién ordenó/cambió configuración** (operaciones y cambios de catálogo).

---

## 13. UI propuesta

### Administración (rol Administrador)

- Menú `Administración` (o submenú `Inventario` en `_Layout.cshtml`):
  - **Unidades de Medida**: lista, búsqueda, crear, editar, activar/inactivar.
  - **Insumos**: lista, búsqueda (nombre/código/código de barras), crear, editar, activar/inactivar; campos de código opcionales; enlace a etiqueta.
- **Existencias**: tabla por Sucursal+Insumo con CantidadActual y StockMinimo; estado bajo mínimo; sin edición directa de cantidad.
- **Entradas / Ajustes / Mermas**: formularios con selección o escaneo de Insumo, cantidad, costo (entrada), concepto/motivo obligatorio; tabla de movimientos.
- **Movimientos**: historial con filtros por fecha/insumo/sucursal.
- **Producto** (en `Productos/Editar`): campos `Codigo`/`CodigoBarras` opcionales; sección **Receta** (agregar/modificar/quitar insumo con cantidad); botón **Previsualizar etiqueta**.
- **Insumo**: campos de código opcionales y **Previsualizar etiqueta**.

### Operación

- **Ventas POS**: campo de escaneo (opcional) que agrega producto por código; mantiene categorías/botones/búsqueda visual.
- **Etiqueta**: modal/parcial de previsualización con datos y código de barras (sin impresora física).

---

## 14. Batería futura (diseño A–M)

Evidencia futura en `C:\Users\Admin\AppData\Local\Temp\opencode\fase10\resultados.txt`.

- **A — Seguridad/autorización**: navegación sin sesión → 302; Fetch sin sesión → 401; Fetch sin permiso (no-Admin) → 403; POST sin antiforgery → 400; cuerpos null → mensaje controlado.
- **B — Unidades de Medida**: crear/editar/activar/inactivar; duplicado `(IdEmpresa, Codigo)` rechazado; nombre/código con trim; inactivar unidad usada por insumo activo → bloqueado.
- **C — Insumos**: crear/editar/activar/inactivar; unidad obligatoria y activa; duplicado de código/código de barras por Empresa rechazado; Insumo sin código funciona; costo >= 0; stock mínimo >= 0.
- **D — Recetas**: ver receta; agregar insumo; modificar cantidad; quitar insumo; duplicado `(IdProducto, IdInsumo)` rechazado; cantidad > 0; insumo activo; producto activo.
- **E — Entradas**: cantidad > 0; costo opcional; concepto obligatorio; actualiza Existencia; crea Movimiento `ENTRADA` con anterior/nueva; auditoría `ENTRADA_INVENTARIO`.
- **F — Ajustes**: positivo/negativo; motivo obligatorio; sin stock negativo (negativo rechazado si excede); auditoría `AJUSTE_INVENTARIO`.
- **G — Merma**: cantidad > 0; reduce stock; motivo obligatorio; sin stock negativo; auditoría `MERMA_INVENTARIO`.
- **H — Venta consume Inventario**: venta de producto con receta → al cierre descuenta `CantidadVendida × CantidadReceta`; movimientos `VENTA` con trazabilidad (comanda/caja/sesión/usuario); producto sin receta no afecta stock.
- **I — Stock insuficiente**: cierre rechazado completo; mensaje identifica Insumo; sin consumo parcial; comanda sigue ABIERTA editable.
- **J — Multicaja/concurrencia**: dos cajas misma sucursal consumen el mismo insumo simultáneamente → stock final correcto; sin deadlocks (orden determinista).
- **K — Multisucursal**: venta en Sucursal A no altera stock de Sucursal B; entradas independientes por sucursal.
- **L — Regresiones**: Caja (apertura/cierre/arqueo), Ventas (mostrador y multipago), Mesas/Salón (apertura/transferencia/cierre/liberación), Fase 9 (cancelación sin consumo; devolución financiera sin devolución física; arqueo intacto).
- **M — Código de barras / Etiquetas** (debe probar):
  - Insumo sin código funciona; Producto sin código funciona;
  - crear Insumo con código; duplicado rechazado;
  - localizar Insumo por escaneo; Entrada mediante escaneo; Merma mediante escaneo;
  - Producto localizado en Ventas por código; escaneo agrega el Producto correcto;
  - código inexistente → mensaje controlado; Producto inactivo no se vende mediante escaneo;
  - lector simulado mediante escritura + Enter; operación manual funciona sin lector;
  - datos necesarios para etiqueta disponibles; previsualización de etiqueta no requiere impresora física.

Criterio final de la batería: **0 FAIL** y build **0 warnings / 0 errors**.

---

## 15. Evidencia (diseño futuro)

- `C:\Users\Admin\AppData\Local\Temp\opencode\fase10\resultados.txt` (batería A–M con PASS/FAIL individuales).
- Volcado físico de DB sin secretos (schema, existencias, movimientos, auditorías Fase 10).
- Verificación física de concurrencia (multicaja), multisucursal, stock insuficiente y escaneo.

---

## 16. Decisiones abiertas (requieren aprobación)

1. **Scope de unicidad de `Producto`**: hoy Producto es global (sin `IdEmpresa`); se propone unicidad GLOBAL de `Codigo`/`CodigoBarras` de Producto y POR EMPRESA para Insumo/UnidadMedida. Alternativa (cambio mayor, fuera de alcance): migrar Producto a Empresa.
2. **Librería de código de barras**: se propone `BarcodeLib` o `ZXing.Net` encapsulada en servicio de presentación (sin tocar dominio). Requiere aprobación antes de instalar dependencia en implementación.
3. **Generación de códigos internos**: opción C (manual y automático) con nueva tabla `SecuenciasCodigo`. Alternativa A (solo manual) si se prefiere simplicidad.
4. **Colisión Producto↔Insumo en códigos**: permitida (cada flujo resuelve en su dominio). Alternativa: prefijo global por dominio (más restrictiva).
5. **Operación de devolución física**: manual `DEVOLUCION_INVENTARIO` por Administrador, sin automatización desde `DevolverPago`. Confirmar si se desea automatización futura.
6. **Precisión de cantidades**: `decimal(18,3)` uniforme sin campo `Decimales` por unidad. Añadir decimales por unidad solo si se justifica en el futuro.
7. **`IdComandaDetalle` en `MovimientoInventario`**: opcional; se propone capturarlo para trazabilidad fina (impacto: FK Restrict a `ComandaDetalles`). Alternativa: omitirlo si se prefiere menos FK.

---

## 17. Criterio de éxito futuro

Fase 10 solo podrá declararse completada si:

- schema/migración correctos (modelo ↔ migración ↔ snapshot ↔ DB coherentes);
- Unidades de Medida administrables;
- Insumos administrables con códigos opcionales;
- Existencias por Sucursal+Insumo sin edición directa;
- Recetas por producto (sin duplicados);
- Movimientos inmutables con existencia anterior/nueva;
- Entrada/Ajuste/Merma con motivo y auditoría;
- consumo automático al cierre de Comanda (atómico);
- stock insuficiente bloquea el cierre completo (sin consumo parcial);
- concurrencia multicaja probada (stock final correcto, sin deadlocks);
- multisucursal probada (stocks independientes);
- código de barras opcional funcional (sin código también funciona);
- Ventas por escaneo funcional (identificación server-side);
- Inventario por escaneo funcional;
- operación sin escáner sigue funcionando;
- datos de etiqueta disponibles y previsualización sin impresora física;
- auditoría correcta (IAuditoriaService, sin secretos);
- build `0 warnings / 0 errors`;
- batería A–M `0 FAIL`;
- cleanup correcto (servidor detenido, puerto libre, sin código temporal en el repo, evidencia persistente).

---

## 18. Fuera de alcance (NO implementar en Fase 10)

- compras/proveedores completas;
- órdenes de compra;
- transferencias entre sucursales;
- múltiples almacenes por sucursal;
- lotes;
- caducidades;
- números de serie;
- PEPS/UEPS/contabilidad;
- cocina/KDS;
- propinas (módulo opcional futuro; inventario jamás trata Propina como Producto/Insumo/Receta/Impuesto/consumo — dominios separados);
- impresión física;
- drivers/SDKs de lectores especiales (solo lector USB tipo teclado);
- facturación;
- APIs externas.

---

## 19. Resumen de archivos propuestos para la fase de implementación

- Models: `UnidadMedida.cs`, `Insumo.cs`, `ExistenciaInsumo.cs`, `RecetaProducto.cs`, `MovimientoInventario.cs`, `SecuenciasCodigo.cs` (infraestructura); modificar `Producto.cs`.
- Constants: `TiposMovimientoInventario.cs` (nueva).
- Data: `AtlasRestaurantDbContext.cs` (DbSets + configuraciones).
- Migración: `AgregarInventarioInsumos` (+ Designer/Snapshot).
- Services: `Inventario/InventarioService.cs`, `Identificacion/IdentificacionService.cs`, `CodigoInterno/CodigoInternoService.cs`, `Etiquetas/EtiquetaService.cs` (+ interfaces); registro DI en `Program.cs`.
- Controllers: `UnidadesMedidaController`, `InsumosController`, `RecetasController`, `ExistenciasController`, `MovimientosInventarioController`; modificar `VentasController` (escaneo + consumo en `RegistrarPago`).
- Views/ViewModels: administrativas de inventario; campos de código en Producto/Insumo; sección Receta en Producto; campo de escaneo y modal de etiqueta en Ventas; `_Layout.cshtml` (menú).

---

## 20. Restricciones de esta tarea

- NO implementar; NO modificar código; NO build; NO app; NO MySQL; NO migraciones; NO PROJECT_STATE; NO commit; NO modificar `NEXT_TASK.txt`.
- Único archivo creado: `Phases/FASE-10.md`.