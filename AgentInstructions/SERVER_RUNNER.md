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

## Manejo de PID activo

Al iniciar una batería, **encontrar un PID activo en `server.pid` NO constituye por sí solo un error ni un bloqueo.**

El runner debe distinguir, después de leer el PID persistido, aplicando estos casos en orden:

### CASO 1 — PID NO EXISTE

Clasificar: `STALE_PID`.

- El PID persistido no corresponde a ningún proceso actual.
- Eliminar **únicamente** el archivo `server.pid` obsoleto.
- Verificar puerto objetivo.
- Si puerto libre: continuar START normalmente.
- **NO DETENERSE.**

### CASO 2 — PID EXISTE + OWNERSHIP DEMOSTRADO

- El PID corresponde inequívocamente a una ejecución anterior del runner.
- La evidencia persistida (archivo de ownership, stdout/stderr) demuestra ownership.
- **NO esperar.**
- Ejecutar STOP controlado:
  1. conservar stdout/stderr;
  2. detener **ÚNICAMENTE** el PID propio;
  3. espera acotada máximo 3 segundos;
  4. si continúa, forzar **ÚNICAMENTE** ese PID;
  5. verificar terminación;
  6. verificar puerto libre.
- Si cleanup correcto: continuar normalmente.
- **NO DETENERSE.**
- Si no puede detenerse: `SERVER_STOP_FAILED` → **DETENTE Y REPORTA.**

### CASO 3 — PID EXISTE PERO PUERTO OBJETIVO LIBRE

- No asumir que es nuestro servidor.
- Puede tratarse de PID reutilizado por Windows.
- Clasificar: `STALE_OR_REUSED_PID`.
- Eliminar **únicamente** la referencia `server.pid` obsoleta.
- **NO matar el proceso.**
- Continuar START.
- **NO DETENERSE.**

### CASO 4 — PUERTO OCUPADO POR PROCESO AJENO

- El puerto está ocupado y **NO** puede demostrarse ownership.
- Clasificar: `PORT_IN_USE`.
- **NO matar proceso.**
- NO `taskkill`. NO `Stop-Process`.
- **DETENTE Y REPORTA:** PID encontrado, puerto, proceso propietario si puede determinarse, evidencia disponible.
- **DETENERSE.**

### CASO 5 — PID PROPIO + SERVIDOR YA READY

- Existe evidencia inequívoca de que:
  - PID pertenece al runner actual;
  - puerto correcto;
  - servidor está READY;
  - corresponde a la misma ejecución.
- **NO** iniciar otro servidor.
- REUTILIZAR el servidor existente y continuar la batería.
- **NO DETENERSE.**

---

## MANEJO DE PID ACTIVO

Al iniciar una batería, **encontrar un PID activo en `server.pid` NO constituye por sí solo un error ni un bloqueo.**

El runner debe distinguir, después de leer el PID persistido, aplicando estos casos en orden:

### CASO 1 — PID NO EXISTE

Clasificar: `STALE_PID`.

- El PID persistido no corresponde a ningún proceso actual.
- Eliminar **únicamente** el archivo `server.pid` obsoleto.
- Verificar puerto objetivo.
- Si puerto libre: continuar START normalmente.
- **NO DETENERSE.**

### CASO 2 — PID EXISTE + OWNERSHIP DEMOSTRADO

- El PID corresponde inequívocamente a una ejecución anterior del runner.
- La evidencia persistida (archivo de ownership, stdout/stderr) demuestra ownership.
- **NO esperar.**
- Ejecutar STOP controlado:
  1. conservar stdout/stderr;
  2. detener **ÚNICAMENTE** el PID propio;
  3. espera acotada máximo 3 segundos;
  4. si continúa, forzar **ÚNICAMENTE** ese PID;
  5. verificar terminación;
  6. verificar puerto libre.
- Si cleanup correcto: continuar normalmente.
- **NO DETENERSE.**
- Si no puede detenerse: `SERVER_STOP_FAILED` → **DETENTE Y REPORTA.**

### CASO 3 — PID EXISTE PERO PUERTO OBJETIVO LIBRE

- No asumir que es nuestro servidor.
- Puede tratarse de PID reutilizado por Windows.
- Clasificar: `STALE_OR_REUSED_PID`.
- Eliminar **únicamente** la referencia `server.pid` obsoleta.
- **NO matar el proceso.**
- Continuar START.
- **NO DETENERSE.**

### CASO 4 — PUERTO OCUPADO POR PROCESO AJENO

- El puerto está ocupado y **NO** puede demostrarse ownership.
- Clasificar: `PORT_IN_USE`.
- **NO matar proceso.**
- NO `taskkill`. NO `Stop-Process`.
- **DETENTE Y REPORTA:** PID encontrado, puerto, proceso propietario si puede determinarse, evidencia disponible.
- **DETENERSE.**

### CASO 5 — PID PROPIO + SERVIDOR YA READY

- Existe evidencia inequívoca de que:
  - PID pertenece al runner actual;
  - puerto correcto;
  - servidor está READY;
  - corresponde a la misma ejecución.
- **NO** iniciar otro servidor.
- REUTILIZAR el servidor existente y continuar la batería.
- **NO DETENERSE.**

---

## Flujo obligatorio

```
LEER server.pid (si existe)
→ CLASIFICAR PID ACTIVO (CASO 1–5)
→ START (si procede)
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
- Capturar obligatoriamente y persistir en un archivo de ownership (ver sección **ARCHIVO DE OWNERSHIP**):
  - PID propio;
  - puerto objetivo;
  - timestamp de inicio;
  - ruta/proyecto ejecutado;
  - stdout;
  - stderr.
- Iniciar UNA sola instancia del servidor; no iniciar una segunda porque la primera siga viva.
- El proceso puede iniciarse desacoplado (p. ej. `Start-Process` con redirección de streams) para que sobreviva al shell que lanza la prueba.
- Antes de iniciar:
  1. aplicar **MANEJO DE PID ACTIVO** (CASO 1–5);
  2. comprobar que el puerto objetivo esté libre después de resolver el PID.

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

Antes de START, aplicar **MANEJO DE PID ACTIVO** (CASO 1–5).

- Si el puerto está ocupado: comprobar si el proceso corresponde a una ejecución propia verificable
  (PID registrado en el archivo de ownership con stdout/stderr concluyentes).
- Si puede demostrarse ownership (CASO 2 o CASO 5): limpiar o reutilizar según corresponda.
- Si NO puede demostrarse ownership (CASO 4 → `PORT_IN_USE`):
  - **NO matar procesos desconocidos.**
  - **NO ejecutar** `taskkill` ni `Stop-Process` contra procesos ajenos.
  - DETENERSE y reportar `PORT_IN_USE`.

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

## ARCHIVO DE OWNERSHIP

Para poder distinguir un PID propio de un PID reutilizado por Windows,
el runner debe persistir un archivo de estado (ej. `server.owner.json`) con al menos:

- `pid` — PID del proceso iniciado;
- `puerto` — puerto objetivo configurado;
- `timestampInicio` — timestamp Unix de inicio;
- `proyecto` — ruta/proyecto ejecutado;
- `stdout` — ruta al archivo de stdout;
- `stderr` — ruta al archivo de stderr.

Al validar un PID activo:
1. Leer el archivo de ownership.
2. Comprobar si el PID existe actualmente.
3. Comparar el timestamp de inicio con el del proceso (si el proceso arrasó después, el PID fue reutilizado).
4. No asumir ownership únicamente porque el número coincide.

No inventar infraestructura excesiva. Un archivo JSON simple es suficiente.

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

Ver también **MANEJO DE PID ACTIVO**: encontrar un PID activo **NO ES POR SÍ MISMO UN ERROR**.
Solo se detienen los casos listados en **POLÍTICA DE ERROR**.

### STALE_PID

- Condición: el PID persistido en `server.pid` (o archivo de ownership) no existe actualmente.
- Evidencia: PID persistido; `Get-Process -Id $pid -ErrorAction SilentlyContinue` devuelve nada.
- Cleanup permitido: eliminar únicamente el archivo de ownership/`server.pid` obsoleto; verificar puerto.
- Reintento: N/A (esto no es un error de arranque).
- Cuándo DETENERSE: nunca, si el puerto queda libre. Continuar START.

### STALE_OR_REUSED_PID

- Condición: el PID existe, pero el puerto objetivo está libre (PID reutilizado por Windows).
- Evidencia: PID vivo, puerto libre, imposible demostrar ownership del runner.
- Cleanup permitido: eliminar únicamente el archivo de ownership obsoleto. **NO** matar el proceso.
- Reintento: N/A.
- Cuándo DETENERSE: nunca, si el puerto queda libre. Continuar START.

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

---

## POLÍTICA DE ERROR

Encontrar un PID activo: **NO ES POR SÍ MISMO UN ERROR**.

Solo DETENERSE cuando:

- `PORT_IN_USE` sin ownership demostrable;
- `SERVER_STOP_FAILED` (el PID propio no termina tras el STOP forzado);
- `SERVER_START_FAILED` tras el único reintento permitido;
- contradicción real que impida demostrar seguridad.

Recuperable automáticamente:

- `STALE_PID`: recuperarse eliminando el archivo obsoleto y continuando.
- `STALE_OR_REUSED_PID`: recuperarse si el puerto queda libre (sin matar procesos).
- PID propio anterior: cleanup acotado (STOP máximo 3 s, luego forzar).
- PID propio actual READY: reutilizar.

Nunca matar procesos sin ownership.

---

## PowerShell 5.1

Todo ejemplo debe ser compatible con Windows PowerShell 5.1.

- NO usar `&&`.
- NO waits indefinidos.
- NO ventanas interactivas.
- NO comandos que maten todos los `dotnet.exe`.
- Usar comandos separados o `;` para encadenar.
- Usar `Start-Process` de forma controlada con redirección de streams (no `-Wait`).

---

## TESTING.md

Revisar `AgentInstructions/TESTING.md`. Este archivo (SERVER_RUNNER.md) tiene prioridad
sobre `TESTING.md` respecto al ciclo de vida del servidor.

Modificar `TESTING.md` únicamente si hace falta una referencia breve a esta política
de manejo de PID activo. No duplicar este archivo completo.

---

## VERIFICACIÓN

Después de editar:

1. **PID activo NO implica DETENTE.**
2. **PID obsoleto se recupera** (`STALE_PID`).
3. **PID reutilizado no provoca matar proceso ajeno** (`STALE_OR_REUSED_PID`).
4. **PID propio anterior se limpia con timeout** (STOP máximo 3 s).
5. **PID propio READY puede reutilizarse** (`CASO 5`).
6. **Puerto ajeno produce `PORT_IN_USE` + DETENTE**.
7. **Ninguna espera puede ser infinita** (sondaje READY máximo 7 s).
8. **STOP máximo 3 segundos** antes de escalamiento controlado.
9. **Máximo 1 reintento** de START.
10. **Nunca matar procesos sin ownership**.