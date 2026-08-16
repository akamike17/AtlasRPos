# Atlas Restaurant POS — .NET / EF Core

Complementa AGENTS.md.

## Plataforma fija
- ASP.NET Core MVC
- .NET 8
- C#
- EF Core 8
- Pomelo.EntityFrameworkCore.MySql 8
- Dependency Injection
- Bootstrap
- Fetch API
- JavaScript embebido

El SDK instalado puede ser superior, pero el proyecto permanece en: `net8.0`
No cambiar TargetFramework sin autorización.

## Solución
Archivo real: `AtlasRestaurantPOS.slnx`
Build: `dotnet build .\AtlasRestaurantPOS.slnx`
No asumir que existe `.sln`.
Objetivo: `0 warnings / 0 errors`

## Flujo de modificación
Para código existente: `read → edit/write → read`
Después: `build`

Después de cada edición verificar que:
- no existen duplicados;
- namespaces son correctos;
- DI continúa válida;
- no se eliminó comportamiento existente;
- nullable/binding siguen controlados.

## EF Core
Usar el AtlasRestaurantDbContext existente.
No crear DbContext paralelo.
Preferir: `AsNoTracking()`
en consultas de solo lectura.
Para operaciones críticas utilizar transacciones cuando corresponda.
No mantener entidades EF o DbContext en sesión HTTP.

## ViewModels
Usar ViewModels top-level para entrada del navegador.
No aceptar desde cliente valores que puedan derivarse server-side.
Ejemplos:
- IdUsuario
- IdEmpresa
- IdSucursal
- IdCaja
- IdSesionCaja
- estados
- fechas
- precios
- totales

cuando el servidor ya posee la fuente confiable.

## Dinero
Usar decimal.
Nunca float/double para importes financieros.
Mantener las precisiones definidas por el modelo.

## Services
Reutilizar Services existentes.
Crear Service nuevo solamente cuando exista responsabilidad reutilizable clara.
No crear Repository/UnitOfWork sobre EF Core.

## Código temporal
Puede utilizarse exclusivamente para verificaciones puntuales cuando sea necesario.
Debe eliminarse antes del resultado final.
Después de eliminarlo:
releer archivo y verificar limpieza.

## Migraciones
Nunca crear/aplicar migración si la fase dice NO MIGRACIÓN.
Si se autoriza migración:
1. build;
2. crear;
3. leer Up();
4. leer Down();
5. revisar SQL/estructura;
6. aplicar solamente después de confirmar seguridad;
7. verificar físicamente DB;
8. build final.

Nunca asumir que una migración generada es correcta por el simple hecho de compilar.