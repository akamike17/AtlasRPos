# Atlas Restaurant POS — Reglas del Agente

## Rol
Actúas exclusivamente como agente ejecutor sobre los archivos físicos del proyecto.
La arquitectura, decisiones técnicas, lógica de negocio y alcance son definidos por el usuario y ChatGPT.
No debes rediseñar, improvisar, ampliar alcance ni sustituir instrucciones por soluciones propias.

## Flujo obligatorio
Siempre: `read → edit/write → read → build → test → verify → cleanup → report`

Nunca escribas sobre un archivo existente sin leerlo primero.

Después de modificar un archivo, vuelve a leerlo y verifica:
* que el cambio realmente existe;
* que no duplicaste código;
* que no eliminaste lógica existente por accidente;
* que namespaces, llaves y estructura siguen correctos.

## PowerShell
Entorno principal: Windows PowerShell.
Reglas:
* No usar `&&`.
* Usar comandos separados o `;`.
* No abrir terminales físicas adicionales.
* No repetir comandos que ya terminaron correctamente.

## .NET
Solución: `AtlasRestaurantPOS.slnx`
Build estándar: `dotnet build .\AtlasRestaurantPOS.slnx`
Objetivo final: `0 warnings / 0 errors`
No inventar un archivo `.sln`.

## Arquitectura fija
Stack:
* C#
* ASP.NET Core MVC
* .NET 8
* EF Core 8
* Pomelo MySQL
* Bootstrap
* JavaScript embebido
* Fetch API
* Dependency Injection
* Services

No introducir:
* React
* Angular
* TypeScript
* Blazor
* jQuery
* otro ORM
* Repository Pattern
* UnitOfWork

salvo instrucción explícita.

## Migraciones
Si una tarea dice NO MIGRACIÓN y detectas que el schema es insuficiente: DETENTE.
No generes una migración por iniciativa propia.

Antes de aplicar cualquier migración:
* build limpio;
* inspeccionar `Up()`;
* inspeccionar `Down()`;
* revisar FK;
* revisar índices;
* revisar cascadas;
* revisar nulabilidad;
* revisar precisión decimal;
* revisar operaciones destructivas.

No aplicar cambios destructivos sin autorización explícita.

## MySQL
No asumir que todo SQL generado por EF Core será aceptado directamente por MySQL.
Ante conflictos de índices, claves foráneas o schema: DETENTE Y REPORTA.

No ejecutar por iniciativa propia:
* DROP DATABASE
* DROP TABLE
* TRUNCATE
* DELETE masivo

## Seguridad
Nunca mostrar, registrar ni almacenar en texto plano:
* passwords;
* PasswordHash;
* ConnectionStrings completas;
* API keys;
* tokens;
* secretos.

User Secrets se usan para configuración sensible en desarrollo.
No incluir secretos en:
* código;
* appsettings;
* migraciones;
* auditoría;
* claims;
* logs.

## Claims
No confiar en IDs operativos enviados por navegador cuando ya existen server-side.
Usar claims existentes cuando corresponda:
* NameIdentifier
* Name
* Role
* IdEmpresa
* IdSucursal
* IdRol
* UsuarioLogin
* IdCaja
* IdSesionCaja
* CajaNombre
* CajaCodigo

## Multicaja
No asumir una sola caja.
Toda operación financiera debe poder trazarse mediante: `Empresa → Sucursal → Caja → SesionCaja → Usuario`

`Caja.Activo` representa configuración, no estado operativo.
El estado operativo de Caja depende de `SesionCaja`.

## Concurrencia
No usar como garantía de concurrencia distribuida:
* `lock { }`
* variables static
* flags en memoria

Para operaciones críticas usar transacciones y mecanismos SQL apropiados.
Patrón ya validado en el proyecto: `SELECT ... FOR UPDATE`

## Fetch / MVC
Mantener:
* navegación sin sesión → 302 Login;
* Fetch sin sesión → 401;
* Fetch sin permiso → 403.
No devolver HTML de Login a Fetch esperando JSON.

## Antiforgery
Toda operación mutable mediante POST debe validar antiforgery.
No desactivar esta protección.

## ViewModels
No usar entidades EF directamente como payload cuando expongan campos controlados por servidor.
Preferir ViewModels/DTOs top-level.
Validar `ModelState`.
Evitar NullReferenceException por cuerpos `[FromBody]` nulos.

## Auditoría
Usar el `IAuditoriaService` existente.
Nunca auditar:
* Password
* PasswordHash
* tokens
* secretos
* ConnectionStrings

No crear un segundo sistema de auditoría paralelo.

## Periféricos e integraciones
El dominio debe permanecer desacoplado de:
* marcas;
* impresoras;
* terminales;
* hardware;
* proveedores;
* APIs concretas.

No instalar drivers, SDKs ni integraciones reales salvo instrucción explícita.

## Procesos persistentes
Todo proceso persistente debe manejarse mediante: `PID + timeout + criterio de éxito + cleanup`
Nunca esperar a que `dotnet run` termine por sí solo.
Nunca usar `Wait-Process` esperando un servidor.
Nunca dejar procesos de prueba vivos.

Para START / READY / STOP del servidor ASP.NET Core durante pruebas HTTP, la autoridad específica es: `AgentInstructions/SERVER_RUNNER.md`.
No improvisar mecanismos alternativos para levantar el servidor.
Si existe contradicción entre una regla genérica de TESTING.md y SERVER_RUNNER.md respecto al ciclo de vida del servidor, SERVER_RUNNER.md tiene prioridad.

Las reglas completas para pruebas y servidores estarán en: `AgentInstructions/TESTING.md`

## Errores
Ante un error real:
1. leer el error exacto;
2. no repetir automáticamente;
3. no improvisar arquitectura;
4. no entrar en bucles;
5. reportar impacto;
6. proponer únicamente el cambio mínimo;
7. esperar instrucción si requiere decisión arquitectónica.

## Reporte final
Reportar únicamente hechos verificados:
* archivos creados;
* archivos modificados;
* build;
* warnings;
* errors;
* pruebas;
* datos de prueba;
* limitaciones;
* discrepancias;
* cleanup.

Nunca declarar éxito si una validación importante falló.

Después de emitir el REPORTE FINAL: **DETENTE.**
No ejecutes más herramientas ni continúes trabajo hasta recibir una nueva instrucción.

Después de escribir `AGENTS.md`:
1. vuelve a leerlo completo;
2. confirma que quedó exactamente en `C:\AtlasRestaurantPOS\AGENTS.md`;
3. no hagas build;
4. no modifiques ningún otro archivo;
5. reporta únicamente archivo creado y verificación;
6. DETENTE.