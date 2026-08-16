---
description: Ejecutar la tarea indicada en Instructions/NEXT_TASK.txt
---
Cumple:
@AGENTS.md

OBJETIVO:
Ejecutar exactamente la tarea descrita en @Instructions/NEXT_TASK.txt.
La tarea actual del TXT es la ÚNICA fuente de instrucciones de esta ejecución.

==================================================
1. RELECTURA OBLIGATORIA EN CADA INVOCACIÓN
==================================================
Cada vez que el usuario escriba /ejecutar:

- El comando DEBE comenzar leyendo nuevamente y COMPLETAMENTE:
  @Instructions/NEXT_TASK.txt
- PROHIBIDO reutilizar de una ejecución anterior:
  - contenido del TXT;
  - precondiciones;
  - decisiones;
  - autorizaciones;
  - clasificación NUEVA/PARCIAL;
  - conclusiones;
  - hashes anteriores;
  - instrucciones almacenadas en contexto.
- La conversación previa NO sustituye la lectura física del archivo.
- Si el contenido leído actualmente contradice una versión anterior:
  MANDA EL CONTENIDO ACTUAL DEL ARCHIVO.

==================================================
2. SIN PARÁMETROS
==================================================
/ejecutar debe funcionar escribiendo únicamente:

/ejecutar

No debe contener:
- $ARGUMENTS
- placeholders
- argumentos obligatorios
- parámetros adicionales

==================================================
3. RUTA ÚNICA
==================================================
El único buzón válido es:

Instructions/NEXT_TASK.txt

- Ruta RELATIVA a la raíz del proyecto actual.
- Nunca usar rutas absolutas.
- Nunca buscar NEXT_TASK.txt en la raíz u otras carpetas.
- Si existe otro NEXT_TASK.txt: ignorarlo y, si es relevante, reportarlo.

==================================================
4. TXT INMUTABLE
==================================================
NEXT_TASK.txt es entrada de SOLO LECTURA.

Antes de ejecutar:
1. leerlo completamente;
2. calcular SHA-256;
3. conservar el hash inicial únicamente para esta ejecución.

PROHIBIDO:
- editarlo;
- sobrescribirlo;
- truncarlo;
- borrarlo;
- moverlo;
- renombrarlo;
- regenerarlo;
- marcar tareas dentro de él;
- guardar resultados dentro de él.

Al finalizar:
- calcular SHA-256 nuevamente;
- debe coincidir con el inicial.

Si cambia durante la ejecución:
- NO sobrescribirlo;
- NO restaurarlo automáticamente;
- DETENER nuevas modificaciones;
- REPORTAR hash inicial y final;
- DETENERSE.

==================================================
5. ORDEN DE AUTORIDAD
==================================================
Para decidir qué hacer en CADA ejecución:

PRIMERO: leer NEXT_TASK.txt ACTUAL.
SEGUNDO: leer AGENTS.md si existe.
TERCERO: consultar PROJECT_STATE.md y AgentInstructions si existen y son aplicables.
CUARTO: inspeccionar estado físico/Git necesario.
QUINTO: clasificar la tarea.

La clasificación automática NUNCA puede contradecir una autorización
explícita válida contenida en el NEXT_TASK.txt ACTUAL.

==================================================
6. CLASIFICACIÓN CORRECTA
==================================================
Después de leer el TXT actual, clasificar:

- NUEVA
- CONTINUACIÓN_AUTORIZADA
- PARCIAL_NO_AUTORIZADA
- YA_EJECUTADA

NUEVA:
No existe evidencia de ejecución previa.
→ ejecutar.

CONTINUACIÓN_AUTORIZADA:
Existen cambios previos y el NEXT_TASK.txt ACTUAL autoriza explícitamente
continuar sobre ellos.
→ continuar.
NO detenerse únicamente porque el working tree tenga esos cambios autorizados.

PARCIAL_NO_AUTORIZADA:
Existen cambios previos relevantes y el TXT actual NO autoriza continuar.
→ no modificar.
→ reportar.
→ detenerse.

YA_EJECUTADA:
Existe evidencia verificable de que la misma tarea ya terminó.
→ no repetir.
→ pedir actualizar NEXT_TASK.txt.
→ detenerse.

==================================================
7. AUTORIZACIÓN EXPLÍCITA TIENE PRIORIDAD
==================================================
Si NEXT_TASK.txt ACTUAL contiene instrucciones como:
- continuar sobre implementación existente;
- cambios existentes autorizados;
- recuperar trabajo parcial;
- completar implementación previa;
- archivos modificados autorizados;

esas instrucciones DEBEN considerarse antes de aplicar el bloqueo
genérico por tarea parcial.

Ejemplo:
Si Git contiene cambios Fase 8 y el TXT actual dice:
"Está autorizado continuar sobre estos cambios existentes"
NO responder: "Precondición violada porque existen cambios."
Debe validar que los cambios coincidan con los autorizados y continuar.

Si aparecen OTROS cambios no autorizados:
DETENER Y REPORTAR únicamente esos cambios inesperados.

==================================================
8. PROHIBIDO ARRASTRAR PRECONDICIONES VIEJAS
==================================================
Una frase perteneciente a una versión ANTERIOR de NEXT_TASK.txt jamás
puede bloquear la versión actual.

Ejemplo:
TXT anterior: "cualquier cambio previo inesperado: DETENTE"
TXT actual: "los cambios existentes de Fase 8 están autorizados"
Debe obedecer el TXT ACTUAL.
No citar ni aplicar la precondición anterior.

==================================================
9. TAREA VACÍA
==================================================
Si NEXT_TASK.txt está:
- vacío;
- solo espacios;
- o contiene únicamente la plantilla inicial;

reportar:
"No existe una nueva tarea en Instructions/NEXT_TASK.txt."

No ejecutar nada.

==================================================
10. CONTEXTO OPCIONAL
==================================================
Si existe:
- AGENTS.md → cumplirlo.
- Phases/PROJECT_STATE.md → consultarlo cuando sea relevante.
- AgentInstructions/ → consultar manuales aplicables.

Si NO existen:
- NO fallar.
- NO inventarlos.
- NO crearlos automáticamente.

==================================================
11. EJECUCIÓN
==================================================
Cuando corresponda ejecutar:

read → edit/write → read → build → test → verify → cleanup → report

según el alcance real de NEXT_TASK.txt.
No ampliar alcance.
No inventar requisitos.
Razonamiento visible, diagnósticos y reportes: ESPAÑOL.

==================================================
12. FINAL
==================================================
Al finalizar:
1. releer/verificar NEXT_TASK.txt;
2. calcular SHA-256 final;
3. comparar contra SHA-256 inicial;
4. entregar REPORTE FINAL;
5. DETENERSE.

==================================================
13. TAREA VACÍA O PLANTILLA
==================================================
Si tras la lectura el TXT está vacío o solo contiene la plantilla por defecto
(p. ej. "# Reemplazar este contenido con la siguiente tarea a ejecutar."):
- DETENTE.
- Reporta que no hay tarea pendiente y no hagas nada más.