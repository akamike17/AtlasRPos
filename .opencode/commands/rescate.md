---
description: Recuperar ejecución atascada
---
Cumple @AGENTS.md y @AgentInstructions/TESTING.md.

Recupera la tarea actual desde el último estado realmente verificado.

NO empieces nuevamente la tarea.
NO repitas pasos ya completados.
NO repitas comandos exitosos.
NO crees otra instancia del servidor si existe una utilizable.
NO inventes estado.

Primero determina:

- última acción completada;
- proceso/PID de prueba conocido si existe;
- servidor READY si existe;
- error real si existe;
- siguiente acción pendiente.

Si existe servidor READY:
úsalo y continúa con las pruebas pendientes.

Si existe proceso propio bloqueado o inútil:
aplica cleanup según TESTING.md.

Si existe un error real:
analízalo una sola vez y aplica únicamente una corrección ya autorizada por la tarea actual.

Si resolverlo exige:

- cambio de arquitectura;
- migración no autorizada;
- operación destructiva;
- decisión no especificada;

DETENTE Y REPORTA.

Continúa solamente desde el punto pendiente.

Al finalizar respeta cleanup y reporte establecidos.