# 0013 — Ciclo de vida de especificaciones

## Estado

Implementada y verificada el 8 de octubre de 2026, tras su aprobación para
implementación el mismo día.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Specs ampliadas: `specs/0004-specifications/spec.md`,
  `specs/0005-acceptance-criteria/spec.md`,
  `specs/0006-implementation-tasks/spec.md`,
  `specs/0007-implementation-task-lifecycle/spec.md`,
  `specs/0009-http-concurrency/spec.md` y
  `specs/0012-acceptance-criterion-task-traceability/spec.md`.

Las reglas transversales de arquitectura, HTTP, concurrencia, persistencia,
pruebas y calidad se heredan de la constitución.

## Problema

Una especificación puede editarse, descomponerse en criterios y tareas y
mantener trazabilidad entre ambos, pero el sistema no distingue un borrador de
un plan aprobado, implementado o verificado.

Sin ese ciclo de vida, el contrato puede cambiar mientras se ejecutan sus tareas
y no existe una declaración persistente de que el trabajo terminó o fue
verificado. La aprobación tampoco puede comprobar que todos los criterios
tengan trabajo planificado.

## Objetivo

Incorporar un ciclo de vida explícito e irreversible para que una especificación
avance de borrador a aprobada, implementada y verificada.

La aprobación congela el contenido y la estructura del plan, exige trazabilidad
completa y habilita la ejecución de tareas. Declarar la implementación exige que
todas las tareas hayan terminado; la verificación continúa siendo una acción
manual y explícita.

## Alcance

- Estados `Draft`, `Approved`, `Implemented` y `Verified`.
- Creación de especificaciones nuevas en estado `Draft`.
- Migración de especificaciones existentes a `Draft`.
- Aprobación condicionada por una planificación mínima y trazable.
- Inmutabilidad del contenido, criterios, tareas y vínculos tras la aprobación.
- Inicio y finalización de tareas únicamente durante la implementación aprobada.
- Declaración explícita de implementación y verificación.
- ETag agregado de la especificación para proteger el plan y sus transiciones.
- Extensión de las respuestas y de OpenAPI con estado y fechas de transición.
- Nueva migración de EF Core y pruebas de actualización de datos existentes.

## Fuera de alcance

- Rechazar, reabrir o devolver una especificación a un estado anterior.
- Versiones sucesivas o snapshots del contenido aprobado.
- Marcar criterios como superados, fallidos o verificados individualmente.
- Evidencias, comentarios, revisores, identidad o aprobaciones múltiples.
- Porcentajes, pesos o métricas de cobertura.
- Transiciones automáticas inferidas a partir de tareas.
- Modificar el ciclo de vida de la propuesta asociada.
- Un motor genérico o estados configurables.

## Casos de uso

### UC-01 — Preparar un borrador

Una especificación nueva comienza como `draft`. Mientras permanece en ese
estado se puede modificar su contenido y planificar, editar, ordenar o eliminar
sus criterios, tareas y vínculos de trazabilidad.

### UC-02 — Aprobar un plan trazable

Un consumidor aprueba el borrador con su ETag vigente. La operación solo se
completa cuando existe al menos un criterio, existe al menos una tarea y cada
criterio está vinculado con una o más tareas.

### UC-03 — Ejecutar las tareas aprobadas

Las tareas pueden iniciarse y completarse mientras la especificación está
`approved`. El contenido y la estructura aprobados permanecen inmutables.

### UC-04 — Declarar la implementación

Cuando todas las tareas están `completed`, un consumidor declara la
especificación como `implemented` con el ETag agregado vigente.

### UC-05 — Verificar la especificación

Un consumidor confirma manualmente una especificación `implemented`. El recurso
pasa a `verified` y queda en su estado terminal.

## Cambios de dominio

| Campo o concepto | Cambio | Regla específica |
|---|---|---|
| `Status` | Nuevo | `Draft`, `Approved`, `Implemented` o `Verified` |
| `ApprovedAtUtc` | Nuevo, nullable | Instante de aprobación |
| `ImplementedAtUtc` | Nuevo, nullable | Instante de declaración de implementación |
| `VerifiedAtUtc` | Nuevo, nullable | Instante de verificación |
| ETag de especificación | Ampliado | Cambia con cualquier mutación del plan o de sus tareas |

Las respuestas de creación, consulta, edición y transición añaden:

```json
{
  "status": "draft",
  "approvedAtUtc": null,
  "implementedAtUtc": null,
  "verifiedAtUtc": null
}
```

Los valores HTTP del estado son `draft`, `approved`, `implemented` y
`verified`.

### Reglas específicas

1. Una especificación nueva comienza en `Draft` con las tres fechas de
   transición a `null`.
2. Las especificaciones existentes se migran a `Draft` sin cambiar su contenido
   ni fechas. Su versión se incrementa una vez para invalidar ETags anteriores.
3. Solo `Draft` permite cambiar el contenido o crear, editar, reordenar,
   eliminar o vincular criterios y tareas.
4. Una especificación `Draft` solo puede aprobarse si contiene al menos un
   criterio, al menos una tarea y todos sus criterios están vinculados con una o
   más tareas. Las tareas sin criterios vinculados están permitidas.
5. Solo se pueden iniciar o completar tareas de una especificación `Approved`.
6. `Approved` solo puede pasar a `Implemented` cuando todas sus tareas están
   `Completed` y la planificación aprobada conserva su trazabilidad.
7. `Implemented` solo puede pasar a `Verified` mediante una petición explícita.
8. `Verified` es terminal.
9. Cada transición establece su fecha correspondiente, actualiza
   `UpdatedAtUtc` e incrementa la versión de la especificación.
10. Las fechas de transiciones anteriores se conservan al avanzar.
11. Toda mutación efectiva del contenido, criterios, tareas, estados de tareas o
    vínculos incrementa también una vez la versión agregada de la
    especificación. Una operación idéntica no la incrementa.
12. Una transición o mutación estructural inválida conserva todo el estado
    anterior y responde `409`.

## Cambios del contrato HTTP

La ruta base continúa siendo:

`/api/projects/{projectId}/proposals/{proposalId}/specification`

| Método | Ruta relativa | Resultado correcto | Error propio |
|---|---|---|---|
| `PUT` | `/` | `200` si está en `draft` | `409 Specification is not editable` |
| `POST` | `/approve` | `200` con estado `approved` | `409 Specification is not ready for approval` |
| `POST` | `/mark-implemented` | `200` con estado `implemented` | `409 Specification implementation is incomplete` |
| `POST` | `/verify` | `200` con estado `verified` | `409 Specification transition conflict` |

Las tres transiciones no reciben body y exigen el ETag agregado vigente de la
especificación mediante `If-Match`. Devuelven la representación completa y el
nuevo ETag.

Los endpoints existentes que cambian contenido, criterios, tareas u orden y el
`PUT` de trazabilidad responden `409 Specification is not editable` cuando la
especificación ya no está en `draft`.

Los endpoints existentes para iniciar o completar tareas responden
`409 Specification is not approved for implementation` cuando la especificación
no está en `approved`. Los conflictos propios del estado de la tarea se evalúan
después de esta regla.

El ETag devuelto por `GET` de la especificación representa desde este incremento
su contenido, ciclo de vida y planificación completa. Tras cualquier mutación
efectiva de un criterio, tarea o vínculo, un consumidor debe volver a consultar
la especificación antes de transicionarla. Los ETags individuales y de
colección continúan protegiendo sus contratos actuales.

## Compatibilidad con specs anteriores

- `POST` de especificación conserva su request y añade los campos de ciclo de
  vida en la respuesta.
- `GET` conserva su ruta y añade los mismos campos.
- `PUT` conserva request y respuesta, pero queda limitado a `draft`.
- Las mutaciones de criterios, tareas y trazabilidad quedan limitadas a
  especificaciones `draft`.
- El inicio y finalización de tareas quedan limitados a especificaciones
  `approved`.
- Una mutación hija invalida también el ETag agregado de la especificación, sin
  sustituir los ETags individuales existentes.
- Los datos existentes permanecen editables porque se migran a `Draft`.

## Impacto en persistencia

Nueva migración: `AddSpecificationLifecycle`.

- Añade `Status`, `ApprovedAtUtc`, `ImplementedAtUtc` y `VerifiedAtUtc` a
  `Specifications`.
- Migra filas existentes a `Draft` e incrementa su `Version` una vez.
- Conserva identificadores, contenido, relaciones y fechas existentes.
- No crea nuevas asociaciones ni infiere estados a partir de tareas existentes.

## Criterios de aceptación

1. Una especificación nueva se devuelve como `draft` y sus tres fechas de
   transición son `null`.
2. Una migración desde 0012 conserva los datos, asigna `Draft` e incrementa la
   versión de las especificaciones existentes.
3. Contenido, criterios, tareas, orden y vínculos continúan siendo modificables
   durante `draft`.
4. Aprobar sin criterios, sin tareas o con algún criterio sin tareas vinculadas
   devuelve 409 y no modifica la especificación.
5. Una tarea sin criterios vinculados no impide aprobar cuando todos los
   criterios sí están cubiertos.
6. Aprobar un borrador listo registra `approvedAtUtc`, devuelve `approved` y un
   nuevo ETag.
7. Tras la aprobación, cualquier mutación de contenido, criterios, tareas,
   orden o trazabilidad devuelve 409 sin cambios.
8. Una tarea solo puede iniciarse o completarse cuando su especificación está
   `approved`.
9. Declarar la implementación con alguna tarea no completada devuelve 409 sin
   cambios.
10. Declarar la implementación con todas las tareas completadas registra
    `implementedAtUtc`, devuelve `implemented` y un nuevo ETag.
11. Verificar una especificación implementada registra `verifiedAtUtc`, devuelve
    `verified` y un nuevo ETag.
12. Una transición desde un estado distinto del inmediatamente anterior
    devuelve 409 y conserva todas las fechas.
13. Editar, transicionar o mutar un hijo sin su precondición vigente conserva el
    estado y devuelve el error constitucional correspondiente.
14. Una mutación efectiva de contenido, criterio, tarea, estado de tarea, orden
    o trazabilidad invalida el ETag de la especificación; una operación idéntica
    lo conserva.
15. Una aprobación no puede competir con una mutación del plan y completarse
    sobre datos distintos de los representados por su ETag.
16. Dos transiciones concurrentes basadas en el mismo ETag no pueden completarse
    ambas.
17. Las consultas incluyen los nuevos campos sin cambiar el aislamiento entre
    proyectos y propuestas.
18. El estado y sus fechas permanecen disponibles entre scopes y reinicios.
19. OpenAPI documenta las operaciones nuevas, restricciones, estados, ETags y
    respuestas de conflicto.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- El ciclo es secuencial e irreversible:
  `Draft → Approved → Implemented → Verified`.
- Las especificaciones existentes se migran a `Draft` para preservar su
  editabilidad, sin inferir un estado histórico.
- Aprobar congela contenido, criterios, tareas, orden y trazabilidad; este
  incremento no permite reabrir la especificación.
- Aprobar exige al menos un criterio, al menos una tarea y todos los criterios
  vinculados con alguna tarea. Las tareas auxiliares pueden quedar sin criterio.
- Las tareas solo se ejecutan en `Approved` y declarar `Implemented` exige que
  todas estén en `Completed`.
- El ETag de la especificación representa el agregado y cambia con cualquier
  mutación efectiva de su planificación o ejecución.

## Plan de verificación

- Pruebas de dominio para estados, transiciones, timestamps, precondiciones de
  aprobación e inmutabilidad.
- Pruebas HTTP para contratos nuevos, restricciones de endpoints existentes,
  Problem Details, aislamiento y ETags.
- Pruebas de carreras entre aprobación y mutaciones de contenido, colecciones,
  tareas y vínculos.
- Prueba de migración desde `AddAcceptanceCriterionTaskTraceability`.
- Pruebas de persistencia y OpenAPI.

## Evidencia de cierre

- Compilación sin warnings ni errores.
- 119 pruebas de dominio superadas.
- 304 pruebas de integración superadas.
- Formato verificado sin cambios.
- Modelo de EF Core sin migraciones pendientes.
