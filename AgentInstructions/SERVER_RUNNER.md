# Atlas Restaurant POS — SERVER RUNNER (autoridad del ciclo de vida del servidor)

Autoridad específica para START / READY / STOP del servidor ASP.NET Core durante pruebas HTTP.
Complementa `AGENTS.md` y `TESTING.md`.
Si existe contradicción con una regla genérica de `TESTING.md` respecto al ciclo de vida del servidor, este archivo tiene prioridad.
No improvisar mecanismos alternativos para levantar el servidor.

---

## Regla fundamental: prohibido bloquearse después del START

Después de iniciar el servidor:

- NUNCA ejecutar una espera bloqueante.
- NUNCA esperar indefinidamente a `dotnet`.
- NUNCA depender de que el proceso termine por sí mismo.

El proceso del servidor queda desacoplado y controlado mediante PID.
Un bloqueo tras capturar el PID es un ERROR DE INFRAESTRUCTURA RECUPERABLE:
diagnosticar, limpiar y reintentar como máximo una vez; nunca quedarse esperando.

---

## Flujo obligatorio

```
START
→ CAPTURAR PID
→ SONDEAR READY
→ TIMEOUT
→ DIAGNÓSTICO
→ CLEANUP
→ REINTENTO CONTROLADO O ERROR
```

---

## START

- Usar `dotnet run` únicamente de forma controlada y NO bloqueante.
- Prohibido dejar `dotnet run` bloqueante en primer plano.
- Prohibido abrir ventana física adicional (sin terminales nuevas).
- Capturar obligatoriamente y persistir:
  - PID propio (ej. `server.pid`);
  - stdout (ej. `server.out`);
  - stderr (ej. `server.err`).
- Iniciar UNA sola instancia del servidor; no iniciar una segunda porque la primera siga viva.
- El proceso puede iniciarse desacoplado (p. ej. `Start-Process` con redirección de streams) para que sobreviva al shell que lanza la prueba.
- Antes de iniciar:
  1. comprobar que no exista un PID de prueba anterior;
  2. comprobar que el puerto objetivo esté libre (ver sección PUERTO OCUPADO).

---

## READY (sondeo acotado)

- Criterio de éxito: aparición de `Now listening on:` en `server.out` o `server.err`.
- Después de START, sondear periódicamente la salida con un mecanismo ACOTADO.
- Máximo total de espera: `7 segundos`.
- Patrón de referencia compatible PowerShell 5.1 (acotado, sin waits indefinidos):

```powershell
$ready = $false
for ($i = 0; $i -lt 7; $i++) {            # 7 iteraciones x 1 s = máximo 7 s
    Start-Sleep -Seconds 1
    $out = Get-Content -LiteralPath 'server.out' -Raw -ErrorAction SilentlyContinue
    $err = Get-Content -LiteralPath 'server.err' -Raw -ErrorAction SilentlyContinue
    if (($out -match 'Now listening on:') -or ($err -match 'Now listening on:')) {
        $ready = $true
        break
    }
}
```

- Alternativa equivalente permitida: iteraciones de 500 ms con límite de 14; nunca superar 7 s totales.
- PROHIBIDO durante el sondeo y en cualquier punto posterior al START:
  - `Start-Process -Wait`;
  - `Wait-Process` esperando servidor web;
  - `Wait-Job` sin timeout;
  - bucles `while` sin límite;
  - pipelines que dependan de que `dotnet run` termine;
  - `Start-Sleep` mayores de 5 segundos para esta finalidad.
- Al alcanzar READY: comenzar inmediatamente las pruebas HTTP.
  No seguir esperando; no seguir leyendo logs sin motivo.

---

## Si READY NO aparece en 7 segundos → SERVER_START_TIMEOUT

Clasificar como `SERVER_START_TIMEOUT` y ejecutar obligatoriamente:

1. capturar `server.out`;
2. capturar `server.err`;
3. comprobar si el PID iniciado sigue vivo;
4. comprobar el estado del puerto objetivo;
5. detener EXCLUSIVAMENTE el PID iniciado por esta ejecución si sigue vivo;
6. verificar que ese PID terminó;
7. verificar el estado final del puerto;
8. reportar diagnóstico.

NO quedarse esperando.
Si el diagnóstico indica un fallo recuperable de infraestructura, aplicar REINTENTO (abajo).

---

## REINTENTO controlado

- Se permite como máximo `1 reintento automático`.
- Únicamente cuando el diagnóstico indique un fallo recuperable de infraestructura.
- Antes del reintento se deben cumplir TODAS las condiciones:
  - PID anterior terminado;
  - puerto objetivo disponible;
  - `server.out` / `server.err` anteriores conservados como evidencia.
- Si el segundo START tampoco alcanza READY:
  - NO intentar nuevamente;
  - DETENERSE y reportar `SERVER_START_FAILED`
    incluyendo stdout, stderr y estado del puerto.

---

## PUERTO OCUPADO

Si antes de START el puerto ya está ocupado:

- NO matar procesos desconocidos.
- Identificar si el proceso que ocupa el puerto pertenece a una ejecución propia verificable
  (PID registrado en `server.pid` de una prueba propia anterior).
- Si NO puede demostrarse ownership:
  - DETENERSE y reportar `PORT_IN_USE`;
  - NO ejecutar `taskkill` ni `Stop-Process` contra procesos ajenos.

---

## STOP

1. Detener únicamente el PID exacto iniciado por la prueba.
2. Esperar de forma ACOTADA su terminación (máximo 3 segundos).
3. Comprobar si sigue vivo; si sigue vivo, forzar terminación únicamente de ese PID.
4. Comprobar nuevamente.
5. Verificar que el puerto objetivo quedó libre.

Si STOP falla:
- intentar cleanup seguro únicamente sobre el PID propio;
- reportar el resultado (incluido `SERVER_STOP_FAILED` si el PID no terminó).

Nunca usar `taskkill /IM dotnet.exe`; nunca matar procesos `dotnet` ajenos.

---

## PowerShell

- Documentar y usar comandos compatibles con PowerShell 5.1.
- Evitar:
  - waits indefinidos;
  - pipelines que dependan de que `dotnet run` termine;
  - ventanas adicionales;
  - mecanismos interactivos;
  - `&&`.

---

## ERRORES DEL RUNNER

Cada error indica: condición, evidencia a capturar, cleanup permitido, si admite reintento y cuándo DETENERSE.

### SERVER_START_TIMEOUT

- Condición: tras START, `Now listening on:` no aparece en `server.out`/`server.err` dentro de 7 segundos.
- Evidencia a capturar: `server.out`, `server.err` (máximo 30 líneas finales), PID vivo sí/no, puerto ocupado sí/no.
- Cleanup permitido: detener únicamente el PID propio si sigue vivo; verificar que terminó; verificar puerto.
- Reintento: SÍ, máximo 1, solo si el diagnóstico indica fallo recuperable y el puerto quedó disponible.
- Cuándo DETENERSE: si el reintento tampoco alcanza READY (pasar a `SERVER_START_FAILED`); nunca esperar más.

### SERVER_START_FAILED

- Condición: el segundo START tampoco alcanzó READY, o el primer fallo no es recuperable.
- Evidencia a capturar: stdout, stderr y estado del puerto de ambos intentos.
- Cleanup permitido: detener únicamente el PID propio del último intento si sigue vivo; verificar puerto.
- Reintento: NO.
- Cuándo DETENERSE: inmediatamente; reportar el diagnóstico completo y no volver a intentar.

### PORT_IN_USE

- Condición: antes de START el puerto objetivo está ocupado y NO puede demostrarse ownership de la ejecución propia.
- Evidencia a capturar: PID(s) que ocupan el puerto (`Get-NetTCPConnection`), comando de verificación, resultado de la comprobación de ownership.
- Cleanup permitido: ninguno sobre procesos ajenos; únicamente limpiar un PID de prueba propio si aplica.
- Reintento: NO automático; solo tras demostrar que el puerto quedó libre.
- Cuándo DETENERSE: inmediatamente; reportar `PORT_IN_USE` y esperar instrucción.

### SERVER_EXITED_EARLY

- Condición: el proceso iniciado (PID propio) termina antes de alcanzar READY, o muere durante la batería sin que la prueba lo detuviera.
- Evidencia a capturar: PID finalizado sí/no, últimas líneas de `server.out`/`server.err` (posible excepción de arranque), estado del puerto.
- Cleanup permitido: verificar que el PID terminó; si sigue vivo, detener únicamente ese PID propio; verificar puerto libre.
- Reintento: SÍ, máximo 1, solo si la causa es recuperable y con evidencia conservada.
- Cuándo DETENERSE: si el proceso vuelve a morir antes de READY (pasar a `SERVER_START_FAILED`), o si la batería quedó inconsistente.

### SERVER_STOP_FAILED

- Condición: al detener, el PID propio no termina tras la espera acotada (3 s) ni tras el forzado.
- Evidencia a capturar: PID vivo sí/no tras el STOP, puerto ocupado sí/no, intentos realizados.
- Cleanup permitido: forzar terminación únicamente del PID propio (una vez más) y verificar puerto.
- Reintento: SÍ, un segundo intento de forzar el mismo PID propio; nunca contra procesos ajenos.
- Cuándo DETENERSE: sí, reportar PID vivo y puerto ocupado.

### READY_NOT_DETECTED

- Condición: no aparece `Now listening on:` en los streams capturados aunque el proceso siga vivo o el puerto responda.
- Evidencia a capturar: `server.out`, `server.err`, estado del puerto, verificación de que el PID está vivo.
- Cleanup permitido: según diagnóstico; detener únicamente el PID propio si no hay READY verificable.
- Reintento: NO automático; requiere diagnóstico antes de decidir.
- Cuándo DETENERSE: reportar y esperar instrucción; no improvisar mecanismos alternativos de arranque.

---

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