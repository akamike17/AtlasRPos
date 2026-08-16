# Atlas Restaurant POS — Protocolo de Pruebas y Procesos

Este archivo complementa `AGENTS.md`.

Estas reglas son obligatorias para cualquier ejecución de:
* `dotnet run`
* pruebas HTTP
* procesos persistentes
* servidores locales
* validaciones funcionales
* procesos temporales

---

## Regla fundamental
Todo proceso persistente debe manejarse mediante: `PID + timeout + criterio de éxito + cleanup`

Nunca esperar a que un servidor termine por sí mismo.
Un servidor ASP.NET Core con: `HasExited == false`
normalmente significa: `servidor vivo`
NO significa: `servidor colgado`

---

# Inicio de servidor
Antes de iniciar:
1. comprobar si existe un PID de prueba anterior;
2. comprobar si el puerto objetivo está ocupado;
3. detener únicamente procesos creados por pruebas propias anteriores;
4. nunca matar procesos `dotnet` ajenos;
5. nunca usar: `taskkill /IM dotnet.exe`

---

# Una sola instancia
Iniciar UNA sola instancia de Atlas Restaurant POS.
No iniciar una segunda instancia porque la primera:
* siga viva;
* no haya terminado;
* todavía esté generando logs.

Una aplicación web está diseñada para permanecer viva.

---

# Inicio controlado
Al iniciar mediante PowerShell:
* capturar PID;
* redirigir stdout;
* redirigir stderr;
* no abrir ventana física adicional;
* usar `Start-Process` únicamente de forma controlada.

Archivos recomendados:
`server.out`
`server.err`

Si stdout fue redirigido a `server.out`:
leer `server.out`.

Si stderr fue redirigido a `server.err`:
leer `server.err`.

No leer `server.log` si ningún stream fue enviado allí.

---

# Timeout de arranque
Después de iniciar:
esperar máximo: `5 segundos`
Buscar en stdout: `Now listening on:`
Si no aparece:
esperar máximo: `2 segundos adicionales`
Tiempo máximo total: `7 segundos`

No usar ciclos infinitos.
No usar esperas indefinidas.
No usar `Start-Sleep` mayores de 5 segundos para esta finalidad.

---

# Criterio READY
Si aparece: `Now listening on:`
el servidor se considera: `READY`

Después de READY:
* NO seguir esperando;
* NO esperar a que el servidor termine;
* NO usar `Wait-Process`;
* NO seguir leyendo logs sin motivo;
* comenzar inmediatamente pruebas HTTP.

---

# Si NO queda READY
Después de máximo 7 segundos:
1. leer máximo 30 líneas finales de `server.out`;
2. leer máximo 30 líneas finales de `server.err`;
3. detener el PID exacto iniciado;
4. verificar que terminó;
5. comprobar puerto;
6. reportar error real;
7. DETENERSE.

No reiniciar automáticamente.
No entrar en bucle de reintentos.

---

# HTTP
Toda petición HTTP debe tener timeout explícito.
Máximo recomendado: `10 segundos` por petición.
Si excede el timeout:
* considerar prueba fallida;
* capturar endpoint;
* capturar status/error;
* no esperar indefinidamente.

Máximo un reintento si existe una causa técnica concreta.
No repetir solicitudes solo porque "tal vez ahora funcione".

---

# Servidor ya iniciado
Si ya existe un servidor de prueba READY:
NO ejecutar nuevamente: `dotnet run`
Usar la instancia existente para toda la batería HTTP.
No reiniciar entre pruebas salvo que un cambio de código lo haga estrictamente necesario.

---

# Logs
No imprimir continuamente logs de EF Core.
No leer archivos completos enormes.
Cuando exista un error:
usar como máximo: `-Tail 30`
salvo necesidad técnica específica.
Si la aplicación responde correctamente:
no seguir leyendo logs esperando nuevos mensajes.

---

# Cleanup obligatorio
Al finalizar cualquier batería:
1. detener PID exacto iniciado por la prueba;
2. esperar máximo 3 segundos;
3. comprobar si sigue vivo;
4. si sigue vivo, forzar terminación únicamente de ese PID;
5. comprobar nuevamente;
6. verificar que el puerto objetivo quedó libre.

Reporte obligatorio:
`Servidor de prueba detenido = sí/no`
`Puerto libre = sí/no`

---

# Error durante pruebas
Si aparece una excepción inesperada:
1. primero limpiar servidor/PID;
2. luego recopilar error;
3. luego reportar.

No dejar servidor vivo mientras se razona sobre el error.

---

# Build y servidor
Antes de `dotnet build`:
comprobar que ningún proceso de Atlas esté bloqueando archivos de salida.
Si build falla con errores como:
`file is being used by another process`
o similares:
1. identificar PID de Atlas creado por las pruebas;
2. detener ese PID;
3. verificar puerto libre;
4. repetir build una sola vez.

No matar procesos ajenos.

---

# Archivos temporales
Los scripts, JSON temporales, logs o archivos de prueba deben vivir preferentemente fuera del proyecto.
Ejemplo:
`C:\Users\Admin\AppData\Local\Temp\opencode\...`

No contaminar el repositorio con archivos temporales.
Si se crea código temporal dentro del proyecto:
debe eliminarse antes del build final.
Luego releer el archivo para confirmar limpieza.

---

# Pruebas mutables
Antes de una prueba que cambie estado:
conocer:
* estado inicial;
* entidad objetivo;
* sesión;
* caja;
* usuario;
* criterio de éxito.

Después:
verificar físicamente el resultado cuando corresponda.
No declarar éxito solo porque HTTP devolvió 200.

---

# Datos de prueba
Usar nombres identificables por fase.
Ejemplos:
`F5`
`F6`

No borrar físicamente historial para "limpiar".
Los registros históricos pueden permanecer si están claramente identificados.
No crear datos innecesarios cuando existen registros reutilizables.

---

# Concurrencia
Para pruebas concurrentes:
* ejecutar un número pequeño y controlado de requests;
* usar timeout;
* esperar todas las respuestas de forma acotada;
* verificar estado final en DB;
* no crear ciclos infinitos.

Resultado debe validarse físicamente, no solo por códigos HTTP.

---

# Criterio de finalización
Una batería de pruebas termina únicamente cuando:
* pruebas ejecutadas;
* resultados recopilados;
* DB verificada cuando aplique;
* servidor detenido;
* PID inexistente;
* puerto libre;
* código temporal eliminado;
* build final completado si corresponde.

---

# Prohibiciones
Nunca:
* esperar indefinidamente;
* ejecutar múltiples servidores por accidente;
* usar `Wait-Process` esperando servidor web;
* usar bucles `while` sin límite;
* matar todos los procesos dotnet;
* reiniciar continuamente;
* leer logs equivocados;
* dejar PID vivo al terminar;
* reportar éxito sin cleanup.

---

# Diagnóstico HTTP — evidencia antes que hipótesis

Ante un fallo HTTP:

NO modificar código basándose solamente en hipótesis.

Orden obligatorio:
`REPRODUCIR → CAPTURAR → CLASIFICAR → DIAGNOSTICAR → CORREGIR`

Antes de modificar código capturar, cuando aplique:

- método;
- endpoint;
- status HTTP;
- headers relevantes;
- redirect;
- body crudo;
- cookie jar utilizada;
- presencia del antiforgery token SIN mostrar su valor.

Clasificar primero entre:

- autenticación;
- autorización;
- antiforgery;
- model binding;
- ModelState/validación;
- regla de negocio;
- excepción;
- respuesta no JSON;
- timeout/red.

Máximo 1–2 hipótesis iniciales antes de obtener evidencia.
No investigar simultáneamente múltiples endpoints si existe un caso mínimo reproducible.

Para Fetch/JSON:

NO asumir que la respuesta siempre es JSON.
Una respuesta HTML cuando se esperaba JSON puede indicar:

- redirect;
- Login;
- AccessDenied;
- excepción;
- middleware.

Para antiforgery verificar conjuntamente:

- misma sesión/cookie jar;
- antiforgery cookie;
- token correspondiente a esa sesión;
- header RequestVerificationToken;
- endpoint POST correcto.

Nunca imprimir cookies, tokens o secretos completos.

Después del diagnóstico:
aplicar únicamente el cambio mínimo autorizado y volver a probar primero el caso mínimo.

---

# PowerShell 5.1 — peculiaridades verificadas

Hechos observados durante FIX7:

1. Concatenaciones usadas directamente como argumentos pueden comportarse de forma inesperada; usar paréntesis explícitos cuando se construyan argumentos.
2. `@(comando | ConvertFrom-Json)` puede producir arrays anidados; inspeccionar la forma real del resultado antes de asumir su estructura.
3. `curl.exe -o` combinado con `--data-binary` vacío puede no crear el archivo de salida esperado; no usar la existencia de ese archivo como única evidencia.
4. Scripts `.ps1` sin BOM pueden ser interpretados como ANSI por Windows PowerShell 5.1 y romper texto/patrones con acentos.

Para scripts PowerShell 5.1 con texto Unicode que dependa de acentos:
usar una codificación compatible y verificable.

Estas reglas deben evitar repetir diagnósticos ya resueltos.

---

# Autoridad del ciclo de vida del servidor

Para START / READY / STOP del servidor ASP.NET Core durante pruebas HTTP, la autoridad específica es:
`AgentInstructions/SERVER_RUNNER.md`

SERVER_RUNNER.md tiene prioridad sobre reglas genéricas de TESTING.md respecto al ciclo de vida del servidor.
No duplicar el manual completo.

---

# Reporte de procesos
En todo REPORTE FINAL de una fase con ejecución web incluir:

## Procesos
* PID utilizado.
* Servidor detenido: sí/no.
* Puerto libre: sí/no.
* Procesos huérfanos: sí/no.

Después de escribir este archivo:
1. vuelve a leerlo completo;
2. confirma ruta exacta;
3. no ejecutes build;
4. no modifiques otros archivos;
5. reporta archivo creado y verificación;
6. DETENTE.