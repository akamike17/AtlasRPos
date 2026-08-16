# Atlas Restaurant POS — SERVER RUNNER (autoridad del ciclo de vida del servidor)

Autoridad específica para START / READY / STOP del servidor ASP.NET Core durante pruebas HTTP.
Complementa `AGENTS.md` y `TESTING.md`.
Si existe contradicción con una regla genérica de `TESTING.md` respecto al ciclo de vida del servidor, este archivo tiene prioridad.
No improvisar mecanismos alternativos para levantar el servidor.

---

## Inicio (START)

- Usar `dotnet run` únicamente de forma controlada; NUNCA esperar a que termine por sí solo.
- Prohibido dejar `dotnet run` bloqueante en primer plano.
- Prohibido abrir ventana física adicional (sin terminales nuevas).
- Capturar obligatoriamente el PID del proceso iniciado y persistirlo (ej. `server.pid`).
- Redirigir obligatoriamente stdout a `server.out` y stderr a `server.err`.
- Iniciar una sola instancia del servidor; no iniciar una segunda porque la primera siga viva.
- El proceso puede iniciarse desacoplado (p. ej. bootstrap vía `Win32_Process.Create`) para que sobreviva al shell que lanza la prueba.
- Antes de iniciar: comprobar que no exista un PID de prueba anterior y que el puerto objetivo esté libre.

## READY

- Criterio de éxito: aparición de `Now listening on:` en stdout o stderr.
- Timeout máximo de startup: `7 segundos` (5 iniciales + 2 adicionales).
- Al alcanzar READY: comenzar inmediatamente las pruebas HTTP.
- No esperar a que el servidor termine; no usar `Wait-Process`; no usar ciclos infinitos.

## Si NO queda READY

Después de máximo 7 segundos:
1. leer máximos 30 líneas finales de `server.out`;
2. leer máximos 30 líneas finales de `server.err`;
3. detener el PID exacto iniciado;
4. verificar que terminó;
5. comprobar puerto;
6. reportar error real y DETENERSE.

No reiniciar automáticamente ni entrar en bucle de reintentos.

## Detección de método bloqueante

- Cualquier llamada a `dotnet run` sin redirección de streams, sin captura de PID o esperada en primer plano es un método bloqueante prohibido.
- No usar `Wait-Process` esperando servidor web.
- No leer logs esperando nuevos mensajes cuando la app ya responde.

## STOP

1. Detener únicamente el PID exacto iniciado por la prueba;
2. esperar máximo 3 segundos;
3. comprobar si sigue vivo; si sigue vivo, forzar terminación únicamente de ese PID;
4. comprobar nuevamente;
5. verificar que el puerto objetivo quedó libre.

Nunca usar `taskkill /IM dotnet.exe`; nunca matar procesos `dotnet` ajenos.

## Cleanup obligatorio

Al finalizar cualquier batería:
- servidor detenido;
- PID inexistente;
- puerto libre;
- procesos huérfanos de la prueba: ninguno;
- código temporal fuera del proyecto cuando aplique.

Reporte obligatorio:
`Servidor de prueba detenido = sí/no`
`Puerto libre = sí/no`