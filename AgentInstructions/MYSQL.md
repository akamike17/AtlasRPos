# Atlas Restaurant POS — MySQL

Complementa AGENTS.md.

## Motor
Persistencia principal: MySQL + Pomelo EF Core.
Charset objetivo: `utf8mb4`
Collation de negocio: `utf8mb4_unicode_ci`
Respetar configuración física existente.

## Regla principal
EF Core puede generar una migración sintácticamente válida para EF pero problemática para MySQL.
Siempre inspeccionar antes de aplicar.

## Foreign Keys e índices
MySQL puede requerir un índice para respaldar una Foreign Key.
No eliminar un índice sin comprobar si una FK depende de él.
Ante: `Cannot drop index ... needed in a foreign key constraint`
DETENTE.
No encadenar automáticamente DropForeignKey/DropIndex/AddForeignKey sin autorización.

## DeleteBehavior
Mantener comportamiento existente.
Actualmente se prioriza: `Restrict`
para evitar cascadas destructivas.
No introducir Cascade sin decisión explícita.

## Operaciones destructivas
Prohibidas sin autorización:
- DROP DATABASE
- DROP TABLE
- TRUNCATE
- DELETE masivo
- eliminación irreversible de históricos

## Verificación
Cuando una fase requiera comprobar persistencia, verificar físicamente cuando sea razonable:
- filas;
- FK;
- índices;
- nulabilidad;
- precisión;
- estados;
- relaciones.

HTTP 200 por sí solo no demuestra integridad de DB.

## Concurrencia
No usar memoria del proceso como garantía.
Para recursos críticos utilizar transacción/bloqueo SQL apropiado.
Patrón ya validado: `SELECT ... FOR UPDATE`
Debe aplicarse sobre una fila apropiada y dentro de una transacción.

## Folios y secuencias
Nunca generar identificadores concurrentes mediante:
- MAX + 1
- variables static
- contador en memoria
- lock C#

Utilizar persistencia/transacción/bloqueo apropiado.

## Credenciales
Nunca imprimir ConnectionString completa.
Para verificaciones reportar únicamente presencia/conectividad sin revelar password.