---
description: Auditar cambios y compilar
---
Cumple:
@AGENTS.md
@AgentInstructions/DOTNET.md
@AgentInstructions/SECURITY.md

OBJETIVO:
Auditar el estado actual sin modificar código.

1. Determina qué archivos del proyecto fueron modificados o creados durante la tarea actual cuando esa información sea verificable.
2. Lee los archivos relevantes.
3. Busca:

- código duplicado;
- namespaces incorrectos;
- referencias rotas;
- NullReference evidentes;
- payloads inseguros;
- secretos;
- Password/PasswordHash expuestos;
- lógica accidentalmente eliminada;
- problemas evidentes de DI;
- inconsistencias con AGENTS.md.

4. NO corrijas nada.

5. Si la inspección no detecta un problema crítico que haga inútil compilar, ejecuta:

dotnet build .\AtlasRestaurantPOS.slnx

6. Reporta:

- archivos revisados;
- hallazgos;
- resultado build;
- warnings;
- errors.

Si build falla:
NO modifiques código.
Reporta el error exacto y DETENTE.

Si build pasa:
reporta y DETENTE.

No ejecutes la aplicación.
No consultes MySQL.
No hagas migraciones.