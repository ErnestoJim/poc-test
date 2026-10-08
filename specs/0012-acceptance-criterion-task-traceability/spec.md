# 0012 — Trazabilidad entre criterios de aceptación y tareas

## Estado

Implementada y verificada el 8 de octubre de 2026, tras su aprobación para
implementación el mismo día.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Specs ampliadas: `specs/0005-acceptance-criteria/spec.md`,
  `specs/0006-implementation-tasks/spec.md`,
  `specs/0007-implementation-task-lifecycle/spec.md` y
  `specs/0009-http-concurrency/spec.md`.

Las reglas transversales de arquitectura, HTTP, concurrencia, persistencia,
pruebas y calidad se heredan de la constitución.

## Problema

Una especificación puede contener criterios de aceptación y tareas de
implementación, pero ambas colecciones permanecen aisladas. El sistema no puede
expresar qué trabajo contribuye a satisfacer cada criterio ni detectar criterios
que todavía no tienen trabajo planificado.

La relación tampoco puede mantenerse de forma segura cuando varios consumidores
o agentes modifican simultáneamente la planificación.

## Objetivo

Permitir que cada tarea declare el conjunto de criterios de aceptación que cubre
dentro de su misma especificación, y consultar la trazabilidad en ambas
direcciones.

La relación será explícita, persistente y protegida mediante un ETag propio, sin
alterar el contenido ni el ciclo de vida de criterios y tareas.

## Alcance

- Relación muchos-a-muchos entre tareas y criterios de una misma especificación.
- Consulta de los criterios vinculados a una tarea.
- Consulta de las tareas vinculadas a un criterio.
- Sustitución atómica del conjunto completo de criterios de una tarea.
- Colecciones de identificadores deterministas y sin duplicados.
- ETag dedicado para modificar la relación desde una tarea.
- Eliminación automática de vínculos cuando se elimina uno de sus extremos.
- Nueva migración de EF Core y protección mediante claves foráneas e índice.
- OpenAPI, README y pruebas del contrato nuevo.

## Fuera de alcance

- Marcar criterios como superados, fallidos o verificados.
- Inferir que un criterio se cumple cuando terminan sus tareas.
- Impedir completar una tarea o una especificación por falta de cobertura.
- Crear, editar, reordenar o transicionar criterios o tareas desde estos
  endpoints.
- Relaciones entre recursos de especificaciones diferentes.
- Dependencias entre tareas, asignaciones, estimaciones o prioridades.
- Porcentajes, pesos o métricas de cobertura.
- Historial de vínculos eliminados.
- Vinculación automática mediante IA.

## Casos de uso

### UC-01 — Vincular criterios a una tarea

Un consumidor lee la relación actual de una tarea, conserva su ETag y sustituye
el conjunto completo por los identificadores deseados. La operación se aplica de
forma atómica.

### UC-02 — Quitar todos los vínculos de una tarea

Un consumidor envía una colección vacía con el ETag vigente. La tarea queda sin
criterios vinculados.

### UC-03 — Consultar la trazabilidad desde una tarea

Un consumidor obtiene los identificadores de los criterios vinculados a una
tarea y el ETag vigente de esa relación.

### UC-04 — Consultar la trazabilidad desde un criterio

Un consumidor obtiene los identificadores de todas las tareas que declaran
cubrir un criterio.

### UC-05 — Eliminar un recurso vinculado

Al eliminar una tarea o un criterio mediante su contrato existente, sus vínculos
desaparecen sin bloquear la operación ni eliminar el otro extremo.

## Cambios de dominio

| Campo o concepto | Cambio | Regla específica |
|---|---|---|
| `ImplementationTaskAcceptanceCriterion` | Nuevo | Asociación entre una tarea y un criterio |
| `AcceptanceCriteriaVersion` | Nuevo, interno en tarea | Versión de la colección de vínculos |

La asociación utiliza como identidad compuesta
`(ImplementationTaskId, AcceptanceCriterionId)` y no tiene identificador ni
timestamps propios.

`AcceptanceCriteriaVersion` no forma parte del JSON. Solo participa en el ETag
opaco de la relación y comienza en cero para tareas nuevas y existentes.

### Reglas específicas

1. Una tarea puede vincular cero o más criterios y un criterio puede vincular
   cero o más tareas.
2. Ambos extremos deben pertenecer a la misma especificación.
3. Una pareja tarea-criterio solo puede existir una vez.
4. La relación es un conjunto sin orden de negocio; los identificadores se
   devuelven en orden ascendente determinista.
5. Vincular criterios no modifica título, descripción, contenido, posición,
   estado ni fechas de ninguno de los extremos.
6. Pueden mantenerse vínculos para tareas `pending`, `in_progress` o
   `completed`.
7. Sustituir el conjunto cambia la versión de vínculos una vez cuando el estado
   resultante es diferente.
8. Enviar exactamente el conjunto actual no cambia la versión ni el ETag.
9. La sustitución vacía elimina todos los vínculos de la tarea.
10. La petición debe contener la colección completa deseada; no admite
    identificadores vacíos ni repetidos.
11. Si cualquier criterio no existe o pertenece a otra especificación, no se
    aplica ningún cambio.
12. Eliminar una tarea elimina únicamente sus asociaciones.
13. Eliminar un criterio elimina sus asociaciones e invalida el ETag de vínculos
    de cada tarea afectada.
14. Creación, sustitución y eliminación de asociaciones preservan un estado
    atómico incluso ante carreras.

## Cambios del contrato HTTP

Las rutas se anidan bajo el contexto existente de la especificación:

| Método | Ruta relativa | Resultado correcto | Error propio |
|---|---|---|---|
| `GET` | `/tasks/{taskId}/acceptance-criteria` | `200` y ETag | Ninguno |
| `PUT` | `/tasks/{taskId}/acceptance-criteria` | `204` y ETag | `404 Acceptance criterion not found` |
| `GET` | `/acceptance-criteria/{criterionId}/tasks` | `200` | Ninguno |

La colección de una tarea utiliza:

```json
{
  "acceptanceCriterionIds": [
    "225ceb34-721c-44ea-aa87-97d679028b1a",
    "a6d8c9d2-a625-4c92-92f7-581b596ec681"
  ]
}
```

El mismo contrato se utiliza como body del `PUT`. Los identificadores se
representan como strings para conservar errores de validación estables bajo la
clave `acceptanceCriterionIds`.

La consulta inversa devuelve:

```json
{
  "implementationTaskIds": [
    "22831d74-f8bd-44f6-a53b-d4aa06e4d786"
  ]
}
```

Reglas particulares del contrato:

- `PUT` exige el ETag obtenido mediante el `GET` de la relación, no el ETag
  individual de la tarea.
- Una relación inexistente se representa como una colección vacía, no como 404,
  cuando la tarea sí existe.
- Una tarea o criterio desconocido conserva los títulos Problem Details ya
  existentes.
- Un criterio incluido en el body que no existe o pertenece a otro contexto
  responde 404 con título `Acceptance criterion not found`.
- Una precondición obsoleta o una carrera posterior responde 412 sin cambios
  parciales.
- Los contratos existentes de tareas y criterios no añaden propiedades.

## Impacto en persistencia

Nueva migración: `AddAcceptanceCriterionTaskTraceability`.

- Crea `ImplementationTaskAcceptanceCriteria` con clave primaria compuesta.
- Añade foreign keys hacia `ImplementationTasks` y `AcceptanceCriteria`.
- Las dos foreign keys eliminan en cascada únicamente las filas de asociación.
- Añade un índice inverso por `AcceptanceCriterionId` e
  `ImplementationTaskId`.
- Añade `AcceptanceCriteriaVersion` con valor inicial cero a
  `ImplementationTasks`.
- No crea ni transforma asociaciones para datos existentes.

La eliminación en cascada de las asociaciones es una excepción explícita a la
regla restrictiva general: preserva los contratos de eliminación física ya
publicados para tareas y criterios.

## Criterios de aceptación

1. Una tarea nueva o existente sin vínculos devuelve una colección vacía y un
   ETag fuerte.
2. Un `PUT` con criterios válidos de la misma especificación responde 204,
   devuelve un nuevo ETag y persiste el conjunto completo.
3. Consultar la relación después de modificarla devuelve los mismos
   identificadores en orden determinista.
4. La consulta inversa devuelve todas las tareas vinculadas y ninguna tarea de
   otras especificaciones.
5. Una tarea puede vincular varios criterios y un criterio puede vincular varias
   tareas.
6. Una colección vacía elimina todos los vínculos de la tarea.
7. Repetir el conjunto actual conserva el ETag.
8. Un body ausente, una colección nula o un identificador vacío, inválido o
   repetido devuelve 400 sin cambios.
9. Un criterio inexistente o de otra especificación devuelve 404 con
   `Acceptance criterion not found` sin revelar otro contexto.
10. Consultar o modificar vínculos de una tarea inexistente o ajena devuelve 404
    con `Implementation task not found`.
11. Consultar tareas desde un criterio inexistente o ajeno devuelve 404 con
    `Acceptance criterion not found`.
12. El `PUT` sin ETag, con uno no admitido u obsoleto devuelve el error
    constitucional correspondiente y conserva la relación.
13. Dos sustituciones concurrentes basadas en el mismo ETag no pueden
    completarse ambas.
14. Vincular o desvincular criterios no cambia los datos, fechas, estado ni ETag
    individual de la tarea o los criterios.
15. Las relaciones pueden modificarse para tareas de cualquier estado.
16. Eliminar una tarea vinculada elimina sus asociaciones y conserva los
    criterios.
17. Eliminar un criterio vinculado elimina sus asociaciones, conserva las tareas
    e invalida sus ETags de relación.
18. La base de datos impide duplicados y asociaciones con extremos inexistentes.
19. Las relaciones permanecen disponibles entre scopes y reinicios.
20. OpenAPI documenta las tres operaciones, sus cuerpos, ETags y respuestas.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- La cardinalidad es muchos-a-muchos y no exige cobertura mínima.
- La escritura sustituye atómicamente el conjunto completo mediante `PUT`; no
  se exponen operaciones individuales de vincular y desvincular.
- La trazabilidad se consulta mediante dos contratos dedicados de
  identificadores, sin ampliar las respuestas existentes de tareas y criterios.
- La asociación no tiene orden propio y los UUID se devuelven en orden
  ascendente determinista.
- Los vínculos pueden cambiar en cualquier estado de la tarea y no producen
  transiciones automáticas.
- Eliminar una tarea o criterio elimina sus vínculos en cascada y preserva los
  contratos de eliminación actuales.

## Plan de verificación

- Pruebas del conjunto, duplicados, pertenencia a la especificación e
  idempotencia.
- Pruebas HTTP de ambas direcciones, validación, aislamiento y Problem Details.
- Pruebas de ETag y carreras entre sustitución y eliminación de extremos.
- Pruebas de las dos foreign keys, clave compuesta y borrado de asociaciones.
- Prueba de migración para tareas existentes con versión de vínculos cero.
- Persistencia entre reinicios y contrato OpenAPI.

## Evidencia de cierre

- Compilación sin warnings ni errores.
- 108 pruebas de dominio superadas.
- 296 pruebas de integración superadas.
- Formato verificado sin cambios.
- Modelo de EF Core sin migraciones pendientes.
