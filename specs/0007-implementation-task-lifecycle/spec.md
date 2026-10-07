# 0007 — Ciclo de vida de tareas de implementación

## Estado

Aprobada para implementación el 7 de octubre de 2026.

## Problema

SpecFlow permite planificar tareas de implementación ordenadas, pero todas se
comportan como elementos estáticos. No existe una forma de distinguir el trabajo
que todavía no ha comenzado, el que está en curso y el que ya terminó, ni de
registrar cuándo se inició o completó una tarea.

## Objetivo

Incorporar un ciclo de vida mínimo y estrictamente secuencial para las tareas de
implementación. Toda tarea comienza pendiente, puede iniciarse y posteriormente
completarse mediante operaciones HTTP explícitas.

El estado `Completed` será final: durante este incremento una tarea completada
no puede reabrirse ni volver a `InProgress`. Esta condición se limita a las
transiciones de estado; las operaciones existentes de edición, reordenación y
eliminación continuarán disponibles para tareas en cualquier estado.

Las reglas se centralizarán en el dominio para que futuras especificaciones
puedan incorporar estados o bifurcaciones adicionales sin introducir ahora un
motor de workflows ni convertir el conjunto inicial de estados en una decisión
permanente.

## Alcance

- Estados `Pending`, `InProgress` y `Completed` para `ImplementationTask`.
- Estado inicial `Pending` para toda tarea nueva.
- Transición explícita de `Pending` a `InProgress`.
- Transición explícita de `InProgress` a `Completed`.
- Registro de `StartedAtUtc` al iniciar una tarea.
- Registro de `CompletedAtUtc` al completar una tarea.
- Exposición del estado y las fechas de ejecución en todos los contratos HTTP
  de tareas.
- Endpoints anidados para iniciar y completar una tarea.
- Protección frente a transiciones no permitidas y transiciones o ediciones
  simultáneas sobre la misma tarea.
- Conservación del CRUD, orden y eliminación definidos en la especificación
  0006 para todos los estados.
- Migración de EF Core y actualización de tareas existentes.
- Actualización de OpenAPI y README.
- Pruebas unitarias y de integración.

## Fuera de alcance

- Completar una tarea directamente desde `Pending`.
- Reabrir una tarea `Completed`.
- Volver de `InProgress` a `Pending`.
- Estados adicionales como bloqueada, cancelada, fallida u omitida.
- Motivos de cancelación, bloqueo o finalización.
- Historial de transiciones, auditoría o identificación de quién ejecutó una
  acción.
- Eliminación lógica o recuperación de tareas eliminadas.
- Asociación entre tareas y criterios de aceptación.
- Dependencias o bloqueos entre tareas.
- Reglas que obliguen a ejecutar tareas según su posición.
- Asignaciones, prioridades, estimaciones, fechas objetivo o registro de tiempo.
- Resultados, evidencias, comentarios, adjuntos o etiquetas.
- Filtrado, búsqueda o paginación de tareas por estado.
- Edición del estado mediante `PUT` o `PATCH`.
- ETags o versiones expuestas mediante HTTP.
- Estados configurables o un motor de workflows.
- Ejecución o actualización automática mediante IA.
- Prompts, agentes, skills o MCP.
- Autenticación o autorización.
- Frontend, Docker, Aspire, despliegue o PostgreSQL.
- Una capa Application, CQRS, MediatR o repositorios genéricos.

La auditoría y el historial deberán reconsiderarse cuando sea necesario saber
quién realizó cada transición, recuperar información eliminada o reconstruir el
estado anterior de una tarea.

## Casos de uso

### UC-01 — Crear una tarea pendiente

Al crear una tarea mediante el contrato existente, el sistema le asigna el
estado `Pending`. Como todavía no ha comenzado, `StartedAtUtc` y
`CompletedAtUtc` son `null`.

### UC-02 — Iniciar una tarea

Un consumidor inicia una tarea pendiente perteneciente a una especificación
existente. El sistema cambia su estado a `InProgress`, registra el instante de
inicio y devuelve la tarea actualizada.

### UC-03 — Completar una tarea

Un consumidor completa una tarea en progreso. El sistema cambia su estado a
`Completed`, conserva el instante de inicio, registra el instante de
finalización y devuelve la tarea actualizada.

### UC-04 — Consultar el estado de las tareas

Los contratos existentes de creación, consulta, listado y edición incluyen el
estado actual y las dos fechas de ejecución. El listado conserva el alcance y
el orden de la especificación 0006 y no incorpora filtros.

### UC-05 — Mantener una tarea completada

Una tarea completada puede seguir editándose, reordenándose o eliminándose con
las mismas reglas existentes. Estas operaciones no reabren la tarea ni cambian
sus fechas de inicio y finalización.

## Modelo de dominio

Se amplía `ImplementationTask` con los campos siguientes:

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Status` | `ImplementationTaskStatus` | Sí | Estado actual de ejecución |
| `StartedAtUtc` | DateTimeOffset nullable | No | Instante UTC en que comenzó la tarea |
| `CompletedAtUtc` | DateTimeOffset nullable | No | Instante UTC en que se completó la tarea |

`ImplementationTaskStatus` contiene inicialmente:

- `Pending`.
- `InProgress`.
- `Completed`.

El conjunto inicial de estados no se considera un workflow configurable ni una
limitación permanente. Los estados y transiciones podrán ampliarse mediante
especificaciones y migraciones posteriores.

## Reglas de negocio

1. Toda tarea nueva se crea con estado `Pending`.
2. Una tarea `Pending` tiene `StartedAtUtc` y `CompletedAtUtc` con valor `null`.
3. Una tarea `Pending` puede pasar únicamente a `InProgress`.
4. Al iniciar, `StartedAtUtc` se obtiene mediante `TimeProvider`, se convierte a
   UTC y se normaliza a precisión de milisegundos.
5. Al iniciar, `UpdatedAtUtc` recibe el mismo instante normalizado que
   `StartedAtUtc` y la versión interna de la tarea se incrementa.
6. Una tarea `InProgress` tiene `StartedAtUtc` con valor y `CompletedAtUtc` con
   valor `null`.
7. Una tarea `InProgress` puede pasar únicamente a `Completed`.
8. Al completar, `CompletedAtUtc` se obtiene mediante `TimeProvider`, se
   convierte a UTC y se normaliza a precisión de milisegundos.
9. Al completar, `UpdatedAtUtc` recibe el mismo instante normalizado que
   `CompletedAtUtc` y la versión interna de la tarea se incrementa.
10. Completar conserva `StartedAtUtc` sin modificaciones.
11. Una tarea `Completed` contiene `StartedAtUtc` y `CompletedAtUtc` y no admite
    ninguna nueva transición durante este incremento.
12. No se permite completar una tarea `Pending` directamente.
13. No se permite iniciar una tarea `InProgress` o `Completed`.
14. No se permite completar de nuevo una tarea `Completed`.
15. Una transición no permitida no modifica ningún campo de la tarea.
16. Editar título o descripción no modifica `Status`, `StartedAtUtc` ni
    `CompletedAtUtc`, con independencia del estado actual.
17. Reordenar o compactar posiciones no modifica el estado ni las fechas de
    ejecución.
18. Una tarea puede editarse, reordenarse o eliminarse en cualquiera de los tres
    estados.
19. El cliente no puede proporcionar `Status`, `StartedAtUtc` ni
    `CompletedAtUtc` al crear o editar una tarea.
20. Las reglas de transición pertenecen al dominio y no se duplican en los
    endpoints ni en persistencia.
21. Si dos operaciones modifican simultáneamente la misma versión de una tarea,
    como máximo una puede completarse; la otra recibe un conflicto.

`UpdatedAtUtc` representa el último cambio observable de la tarea. Por ello
puede ser posterior a `CompletedAtUtc` si una tarea completada se edita o cambia
de posición posteriormente.

## Contratos HTTP

### Representación de una tarea

Las respuestas existentes de creación, consulta, listado y edición incorporan
tres campos:

```json
{
  "id": "d92ed73f-9225-40ac-99b8-cc36ac87afff",
  "specificationId": "5d346b9e-3449-4f79-ab86-a17af587689b",
  "title": "Implementar la entidad de dominio",
  "description": "Crear la entidad y sus pruebas unitarias.",
  "status": "pending",
  "startedAtUtc": null,
  "completedAtUtc": null,
  "position": 1,
  "createdAtUtc": "2026-10-08T08:30:00+00:00",
  "updatedAtUtc": "2026-10-08T08:30:00+00:00"
}
```

`status` se representa mediante los valores JSON `pending`, `in_progress` y
`completed`. Los futuros estados requerirán una ampliación explícita del
contrato.

### Iniciar una tarea

`POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/start`

La petición no requiere body.

Respuesta correcta: `200 OK` con la tarea actualizada:

```json
{
  "id": "d92ed73f-9225-40ac-99b8-cc36ac87afff",
  "specificationId": "5d346b9e-3449-4f79-ab86-a17af587689b",
  "title": "Implementar la entidad de dominio",
  "description": "Crear la entidad y sus pruebas unitarias.",
  "status": "in_progress",
  "startedAtUtc": "2026-10-08T09:00:00+00:00",
  "completedAtUtc": null,
  "position": 1,
  "createdAtUtc": "2026-10-08T08:30:00+00:00",
  "updatedAtUtc": "2026-10-08T09:00:00+00:00"
}
```

Errores específicos:

- `400 Bad Request` si `taskId` no es un UUID válido.
- `404 Not Found` con título `Implementation task not found` si la tarea no
  existe o no pertenece a la especificación indicada.
- `409 Conflict` con título `Implementation task transition conflict` si la
  tarea no está `Pending` o cambia simultáneamente durante la transición.

### Completar una tarea

`POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/complete`

La petición no requiere body.

Respuesta correcta: `200 OK` con la tarea actualizada:

```json
{
  "id": "d92ed73f-9225-40ac-99b8-cc36ac87afff",
  "specificationId": "5d346b9e-3449-4f79-ab86-a17af587689b",
  "title": "Implementar la entidad de dominio",
  "description": "Crear la entidad y sus pruebas unitarias.",
  "status": "completed",
  "startedAtUtc": "2026-10-08T09:00:00+00:00",
  "completedAtUtc": "2026-10-08T11:15:00+00:00",
  "position": 1,
  "createdAtUtc": "2026-10-08T08:30:00+00:00",
  "updatedAtUtc": "2026-10-08T11:15:00+00:00"
}
```

Errores específicos:

- `400 Bad Request` si `taskId` no es un UUID válido.
- `404 Not Found` con título `Implementation task not found` si la tarea no
  existe o no pertenece a la especificación indicada.
- `409 Conflict` con título `Implementation task transition conflict` si la
  tarea no está `InProgress` o cambia simultáneamente durante la transición.

## Errores del contexto anidado

Los dos endpoints de transición comparten además:

- `400 Bad Request` si `projectId` o `proposalId` no es un UUID válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.
- `404 Not Found` con título `Feature proposal not found` si la propuesta no
  existe o pertenece a otro proyecto.
- `404 Not Found` con título `Specification not found` si la propuesta existe
  pero todavía no tiene una especificación.

Las respuestas usan `application/problem+json`. Los identificadores inválidos
producen Validation Problem Details con errores indexados por `projectId`,
`proposalId` o `taskId`, según corresponda.

Las respuestas no revelan tareas pertenecientes a otro contexto ni exponen
stack traces, rutas locales, cadenas de conexión, versiones internas, detalles
de Entity Framework Core o información interna de SQLite.

## Comportamiento de los endpoints existentes

- `POST /tasks` devuelve la tarea nueva como `pending`, sin fechas de ejecución.
- `GET /tasks/{taskId}` y `GET /tasks` muestran el estado y las fechas actuales.
- `PUT /tasks/{taskId}` conserva el estado y las fechas de ejecución.
- `PUT /tasks/order` conserva el estado y las fechas de ejecución de todas las
  tareas, aunque actualice `UpdatedAtUtc` de las tareas movidas.
- `DELETE /tasks/{taskId}` continúa permitido en cualquier estado y conserva su
  semántica de eliminación permanente y compactación.
- El listado contiene conjuntamente tareas de todos los estados, ordenadas como
  define la especificación 0006 y sin filtros adicionales.

Las rutas anteriores representan abreviadamente el prefijo completo definido en
la especificación 0006.

## Criterios de aceptación

1. Una tarea nueva se devuelve con `status` igual a `pending`,
   `startedAtUtc` igual a `null` y `completedAtUtc` igual a `null`.
2. Una tarea pendiente puede iniciarse y la operación devuelve 200 con `status`
   igual a `in_progress`.
3. Iniciar registra `startedAtUtc` en UTC con precisión de milisegundos.
4. Al iniciar, `completedAtUtc` permanece `null` y `updatedAtUtc` coincide con
   `startedAtUtc`.
5. Una tarea en progreso puede completarse y la operación devuelve 200 con
   `status` igual a `completed`.
6. Completar conserva `startedAtUtc` y registra `completedAtUtc` en UTC con
   precisión de milisegundos.
7. Al completar, `updatedAtUtc` coincide con `completedAtUtc`.
8. Completar directamente una tarea pendiente devuelve 409 y no la modifica.
9. Iniciar de nuevo una tarea en progreso devuelve 409 y conserva el primer
   instante de inicio.
10. Iniciar o completar una tarea completada devuelve 409 y conserva su estado y
    fechas.
11. No existe una operación para reabrir o retroceder una tarea.
12. Dos transiciones simultáneas sobre la misma versión no pueden completarse
    ambas; una devuelve 409.
13. Una edición simultánea y una transición sobre la misma versión no pueden
    sobrescribirse silenciosamente; una devuelve 409.
14. Crear, consultar, listar y editar incluyen `status`, `startedAtUtc` y
    `completedAtUtc`.
15. Editar una tarea pendiente, en progreso o completada conserva exactamente su
    estado y fechas de ejecución.
16. Una edición real posterior a completar puede actualizar `updatedAtUtc` sin
    modificar `completedAtUtc`.
17. Reordenar tareas de cualquier estado conserva sus estados y fechas de
    ejecución.
18. Eliminar una tarea pendiente, en progreso o completada continúa permitido.
19. El listado devuelve conjuntamente tareas de todos los estados y conserva el
    orden existente.
20. Los identificadores con formato inválido devuelven 400 mediante Validation
    Problem Details.
21. Un proyecto, propuesta o especificación inexistente produce el Problem
    Details correspondiente.
22. Consultar o transicionar una tarea mediante otro contexto devuelve 404 con
    `Implementation task not found`.
23. OpenAPI documenta los dos endpoints de transición y los tres campos nuevos
    de la respuesta.
24. El estado y las fechas permanecen disponibles entre scopes y reinicios.
25. Las tareas creadas antes de la migración quedan `Pending` y sin fechas de
    ejecución.
26. La base de datos almacena el estado mediante su nombre textual y permite
    distinguirlo sin depender de ordinales del enum.
27. Los contratos HTTP existentes no cambian salvo por incorporar los tres
    campos de respuesta nuevos.

## Estrategia de pruebas

### Pruebas unitarias

- Estado inicial y ausencia de fechas de ejecución.
- Inicio válido y normalización temporal.
- Finalización válida, conservación del inicio y normalización temporal.
- Rechazo de completar una tarea pendiente.
- Rechazo de repetir, retroceder o transicionar desde `Completed`.
- Conservación del estado y las fechas tras una transición inválida.
- Edición y movimiento en los tres estados sin cambios de ciclo de vida.
- Incremento de la versión interna al realizar cada transición.

### Pruebas de integración

Se mantiene `WebApplicationFactory` con SQLite relacional en memoria y un
`TimeProvider` controlable. Los escenarios concurrentes, de migración y de
persistencia utilizan archivos SQLite temporales.

Se comprueban los contratos HTTP de inicio y finalización; la representación en
creación, consulta, listado y edición; validación, Problem Details y aislamiento
entre contextos; conflictos de transición y concurrencia; edición, reordenación
y eliminación en estado final; migración de tareas existentes; persistencia y
OpenAPI. No se utilizan mocks de EF Core ni el proveedor EF Core InMemory.

## Impacto en persistencia

La tabla `ImplementationTasks` incorporará:

- `Status`, requerido y almacenado como texto;
- `StartedAtUtc`, nullable y almacenado como milisegundos Unix;
- `CompletedAtUtc`, nullable y almacenado como milisegundos Unix.

La migración asignará `Pending` a todas las tareas existentes y dejará
`StartedAtUtc` y `CompletedAtUtc` con valor `null`.

El estado se almacena como texto para mantener datos legibles y evitar acoplar
la persistencia a los valores ordinales del enum. Añadir estados futuros seguirá
requiriendo una decisión explícita de dominio y, cuando corresponda, una
migración.

Se reutiliza `Version`, el token de concurrencia interno incorporado por la
especificación 0006. Cada transición incrementa ese valor y participa en la
misma protección optimista que la edición de contenido y los cambios de
posición. No se añaden ETags ni versiones al contrato HTTP.

## Requisitos no funcionales

- Compilación sin warnings y nullable reference types habilitado.
- Operaciones asíncronas con `CancellationToken`.
- Consultas de lectura con `AsNoTracking`.
- Fechas ISO 8601 en UTC y precisión de milisegundos.
- Reglas de transición centralizadas en `SpecFlow.Domain`.
- Endpoints separados de las entidades de dominio mediante DTOs.
- Pruebas deterministas, aisladas y sin dependencia de la hora real.
- Ningún cambio en las rutas ni requests existentes de tareas.
- Sin secretos ni archivos SQLite generados en Git.
- Compatibilidad conceptual con una futura sustitución de SQLite por
  PostgreSQL.

## Estructura prevista

- `src/SpecFlow.Domain/ImplementationTasks`: enum, transición e invariantes.
- `src/SpecFlow.Infrastructure/Persistence`: configuración y migración.
- `src/SpecFlow.Api/Contracts/ImplementationTasks`: respuesta ampliada.
- `src/SpecFlow.Api/Endpoints/ImplementationTaskEndpoints.cs`: rutas de
  transición.
- `tests/SpecFlow.Domain.Tests/ImplementationTasks`: pruebas unitarias.
- `tests/SpecFlow.Api.IntegrationTests/ImplementationTasks`: pruebas HTTP,
  migración, persistencia y concurrencia.

Se mantienen los tres proyectos de producción actuales y no se crea una capa
Application.

## Decisiones acordadas

- Los estados iniciales son `Pending`, `InProgress` y `Completed`.
- Toda tarea comienza en `Pending`.
- El flujo es estrictamente `Pending` → `InProgress` → `Completed`.
- No se permite completar directamente una tarea pendiente.
- `Completed` es final y no existe reapertura ni transición hacia atrás.
- `StartedAtUtc` registra el inicio y `CompletedAtUtc` registra la finalización.
- Las transiciones también actualizan `UpdatedAtUtc`.
- Editar, reordenar y eliminar continúa permitido en todos los estados.
- Estas operaciones no modifican el estado ni las fechas de ejecución.
- No se añade historial ni auditoría durante este incremento, pero se conserva
  como una necesidad futura.
- No se añaden filtros de listado por estado.
- Los endpoints de transición utilizan acciones `POST` anidadas y devuelven la
  tarea actualizada con `200 OK`.
- Repetir una transición o intentarla fuera de orden devuelve `409 Conflict`.
- El estado se representa y persiste mediante nombres de texto en lugar de
  valores ordinales.
- Los estados y bifurcaciones podrán ampliarse mediante especificaciones
  futuras, sin introducir ahora un motor de workflows.
- Las transiciones concurrentes se protegen mediante la versión interna
  existente.

## Plan de implementación

1. Implementar `ImplementationTaskStatus` y las transiciones en el dominio junto
   con sus pruebas unitarias.
2. Ampliar la configuración de EF Core y generar la migración
   `AddImplementationTaskLifecycle`.
3. Actualizar la respuesta de tareas y las pruebas de los contratos existentes.
4. Implementar el inicio y sus respuestas de error.
5. Implementar la finalización y sus respuestas de error.
6. Verificar conflictos, concurrencia y aislamiento entre contextos.
7. Verificar edición, reordenación y eliminación en los tres estados.
8. Verificar la migración y la persistencia entre reinicios.
9. Actualizar OpenAPI y README.
10. Ejecutar restauración, compilación, pruebas y formato.
11. Auditar cada criterio de aceptación antes de cambiar el estado de la
    especificación a implementada y verificada.
