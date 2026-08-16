---
description: Ejecutar una fase de Atlas
---
Cumple obligatoriamente:
@AGENTS.md
@AgentInstructions/DOTNET.md
@AgentInstructions/MYSQL.md
@AgentInstructions/SECURITY.md
@AgentInstructions/TESTING.md
@Phases/PROJECT_STATE.md

El argumento recibido es:
$ARGUMENTS

Interprétalo exclusivamente como identificador o nombre del archivo de fase.

Busca primero:
Phases/FASE-$ARGUMENTS.md

Si no existe, busca una coincidencia inequívoca dentro de Phases.

Si no existe exactamente una fase válida:
DETENTE y reporta que falta el archivo de instrucciones.

NO inventes una fase.

Antes de ejecutar:

1. lee completamente el archivo de fase;
2. compara sus precondiciones con PROJECT_STATE.md;
3. inspecciona únicamente el código necesario para confirmar estado físico;
4. si existe discrepancia significativa, DETENTE.

Si es coherente:
ejecuta exactamente la fase siguiendo:

read → edit/write → read → build → test → verify → cleanup → report

No amplíes alcance.

Al terminar satisfactoriamente:
NO actualices PROJECT_STATE.md automáticamente salvo que el archivo de fase lo autorice explícitamente.

Entrega REPORTE FINAL.
DETENTE.