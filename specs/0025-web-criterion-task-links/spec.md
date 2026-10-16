# 0025 — Vincular criterios y tareas desde la interfaz web

## Estado

Implementada y verificada el 16 de octubre de 2026, tras su aprobación.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0012-acceptance-criterion-task-traceability/spec.md`,
  `specs/0013-specification-lifecycle/spec.md`,
  `specs/0023-web-acceptance-criteria/spec.md` y
  `specs/0024-web-implementation-tasks/spec.md`.

Este incremento amplía la web para consumir la trazabilidad entre criterios y
tareas ya disponible en la API. No cambia contratos HTTP, dominio ni
persistencia.

## Problema

Aprobar una especificación exige que cada criterio esté vinculado con al menos
una tarea. Desde la web se pueden crear criterios y tareas, pero no vincularlos
ni saber qué criterios siguen sin cubrir, así que el último paso antes de
aprobar todavía obliga a usar la API con el ETag propio de cada relación.

## Objetivo

Desde la sección de tareas, la persona ve qué criterios cubre cada tarea y qué
criterios no cubre ninguna. Mientras la especificación está en `draft`, puede
elegir los criterios de cada tarea y guardar la selección completa con el ETag
de esa relación, con los conflictos resueltos de forma explícita.

## Alcance

- En cada tarea, en cualquier estado: «Cubre los criterios 1 y 3», según la
  numeración visible de los criterios, o «No cubre ningún criterio».
- En `draft`: un resumen de cobertura en la sección de tareas («Todos los
  criterios tienen al menos una tarea» o «Criterios sin tarea: 2 y 4»).
- En `draft`: «Vincular criterios» en cada tarea, que abre una lista de
  casillas con todos los criterios (número y texto) y guarda la selección
  completa.
- Ampliación del cliente HTTP web para consultar y sustituir los criterios de
  una tarea.
- Actualización del README con el recorrido disponible.

## Fuera de alcance

- Vincular desde la sección de criterios o mostrar en ella las tareas de cada
  criterio. La relación se gestiona desde la tarea, que es donde la API la
  expone para escritura.
- La ruta de consulta desde el criterio
  (`GET .../acceptance-criteria/{criterionId}/tasks`), que no hace falta para
  mostrar la cobertura.
- Aprobar la especificación desde la web (0026).
- Cambios en la API, OpenAPI, dominio o esquema.

## Casos de uso

### UC-01 — Ver la cobertura

Al cargar la vista, la sección de tareas consulta los criterios y los vínculos
de cada tarea. Cada tarea indica qué criterios cubre, con el número que el
criterio tiene en su sección. En `draft`, el resumen indica los criterios sin
tarea. Si la consulta de vínculos falla, las tareas siguen visibles, la
cobertura se marca como no disponible y se puede reintentar.

### UC-02 — Elegir los criterios de una tarea

En `draft`, «Vincular criterios» consulta los vínculos actuales de la tarea para
obtener su ETag y muestra una casilla por criterio, marcada si está vinculado.
«Guardar vínculos» envía `PUT` con la lista completa de criterios marcados e
`If-Match`, y está deshabilitado mientras la selección sea igual a la guardada.
Desmarcar todos y guardar deja la tarea sin criterios. «Cancelar» con cambios
pide confirmación en la página.

### UC-03 — Mantener la cobertura al día

Los vínculos cambian también cuando se elimina un criterio o una tarea. La
sección de tareas vuelve a consultar criterios y vínculos cuando cambia el ETag
de la especificación, que cubre todo el plan, de modo que la cobertura refleja
los cambios hechos desde la sección de criterios o por la API y detectados por
la vista.

### UC-04 — Conflictos y errores

- `412`: la web no reintenta. Relee los vínculos de la tarea, muestra la
  selección guardada actual junto a la de la persona y ofrece «Guardar mi
  selección» con el ETag recién leído o «Descartar mi selección».
- `409`: la web relee la especificación. Si ya no está en `draft`, la sección
  pasa a modo de lectura y la selección no guardada se muestra como texto.
- `404`: la web relee la especificación, los criterios y las tareas. Si la tarea
  ya no existe, cierra la selección e informa. Si lo que falta es un criterio,
  mantiene la selección abierta sin él e informa.
- Pérdida de conexión: no se reenvía y se exige consultar el estado. Si los
  vínculos guardados coinciden con la selección enviada, se confirma; si no, se
  presenta la resolución de `412`.
- `428` o `400`: error genérico, se conserva la selección y se exige consultar
  el estado.

## Comportamiento de la interfaz

- Elegir criterios cuenta como edición activa y respeta el bloqueo de toda la
  vista acordado en las specs 0023 y 0024.
- Tras guardar vínculos, la vista relee la especificación para conservar su
  ETag vigente.
- Los vínculos se identifican por los identificadores de la API; la numeración
  solo se usa para mostrarlos y se recalcula al reordenar criterios.
- Las casillas tienen etiquetas accesibles con el número y el texto del
  criterio.
- Mensajes en español y sin JSON bruto, ETags ni detalles internos.

## Consumo del contrato HTTP existente

`...` corresponde a
`/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/acceptance-criteria`.

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Consultar vínculos | `GET ...` | `200`, `{"acceptanceCriterionIds":[...]}` y ETag | `400`, `404` |
| Guardar vínculos | `PUT ...` con `{"acceptanceCriterionIds":[...]}` e `If-Match` | `204` y ETag | `400`, `404`, `409`, `412`, `428` |

La API evalúa la existencia de los criterios antes que `If-Match`, así que un
criterio eliminado entretanto produce `404`, no `412`.

## Cambios en la web

- Nuevo `TaskAcceptanceCriteriaDto` y dos métodos en `SpecFlowApiClient`.
- `ImplementationTasksSection` muestra la cobertura, el resumen y la selección,
  y recibe el ETag de la especificación para saber cuándo releer criterios y
  vínculos.

Son cambios internos de `SpecFlow.Web`; no afectan a la API.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. Cada tarea indica los criterios que cubre con su número visible, o que no
   cubre ninguno, en cualquier estado de la especificación.
2. En `draft`, el resumen indica qué criterios no tienen tarea, o que todos
   tienen al menos una.
3. «Vincular criterios» muestra una casilla por criterio, marcada según los
   vínculos actuales, y solo aparece en `draft`.
4. Guardar envía un único `PUT` con la lista completa de criterios marcados y
   el ETag de la relación. Una selección vacía deja la tarea sin criterios.
5. «Guardar vínculos» está deshabilitado sin cambios y «Cancelar» con cambios
   pide confirmación en la página.
6. Tras guardar, la cobertura y el resumen se actualizan, y editar el contenido,
   un criterio o una tarea no responde `412` por ese cambio.
7. Eliminar o reordenar un criterio desde la vista actualiza la cobertura y la
   numeración mostrada.
8. Ante `412` la web no reintenta y muestra la selección guardada y la propia
   hasta que la persona decide.
9. Ante `409` con la especificación fuera de `draft`, la sección queda en modo
   de lectura y la selección no guardada sigue visible.
10. Ante `404` por un criterio eliminado, la selección sigue abierta sin ese
    criterio.
11. Ante pérdida de conexión, la web no reenvía y exige consultar el estado.
12. Si la consulta de vínculos falla, las tareas siguen visibles y se puede
    reintentar.
13. Las pruebas del cliente HTTP cubren rutas, cuerpo con la lista completa y
    vacía, `If-Match`, ETag en `GET` y `204`, y la clasificación de `400`,
    `404`, `409`, `412`, `428` y fallos de conexión.
14. Los recorridos de las specs 0021, 0023 y 0024 no cambian.
15. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- La relación se edita desde la tarea con casillas, porque la API la sustituye
  como conjunto completo por tarea. Así cada guardado corresponde exactamente a
  una petición y a un ETag.
- La cobertura se calcula en la web a partir de los vínculos de cada tarea, sin
  la ruta inversa por criterio.
- Se hace una consulta de vínculos por tarea. Es aceptable para el tamaño de
  plan de este proyecto; una consulta agregada requeriría cambiar la API y
  sería un incremento propio.
- La numeración de criterios es solo de presentación; nunca se envía.
- Se mantienen la duplicación deliberada de la 0024 y la verificación en
  navegador sin bUnit.

## Plan de verificación

- Pruebas del cliente HTTP para consultar y guardar vínculos.
- Navegador con API real y SQLite temporal:
  - Cobertura y resumen sin vínculos, con algunos y con todos.
  - Vincular, desvincular todos, cancelar con y sin cambios y botón
    deshabilitado sin cambios.
  - Eliminar y reordenar un criterio desde la vista: cobertura y numeración
    actualizadas.
  - `412` con vínculos cambiados por la API; `404` con un criterio eliminado por
    la API durante la selección; `409` aprobando por la API.
  - API detenida al guardar: bloqueo y recuperación en los dos resultados.
  - Edición posterior de contenido, criterio y tarea sin `412`.
  - Aprobación por la API justo después de cubrir todos los criterios desde la
    web, como comprobación de extremo a extremo.
- Compilación, pruebas, formato y ausencia de cambios en el modelo EF Core y en
  OpenAPI.

## Evidencia de cierre

- `SpecFlowApiClient` añade la consulta y la sustitución de los criterios de
  una tarea con el nuevo `TaskAcceptanceCriteriaDto`. `ImplementationTasksSection`
  calcula la cobertura a partir del listado de criterios y de los vínculos de
  cada tarea, y recibe el ETag de la especificación para releerla cuando cambia
  el plan. No cambian la API, OpenAPI, el dominio ni el esquema.
- Tres pruebas nuevas del cliente HTTP (siete casos): ruta, `GET` sin cuerpo con
  ETag, `PUT` con la lista completa y con la lista vacía, `If-Match` literal,
  ETag en `204`, clasificación de `400` (`acceptanceCriterionIds`), `404`,
  `409`, `412` y `428`, y fallos de conexión.
- Navegador con API real y SQLite temporal:
  - Sin vínculos: «Criterios sin tarea: 1, 2 y 3» y «No cubre ningún criterio»
    en cada tarea. Al vincular los criterios 1 y 3, «Cubre los criterios 1 y 3»
    y «Criterio sin tarea: 2». Las casillas muestran número y texto en su
    etiqueta.
  - «Guardar vínculos» deshabilitado sin cambios; «Cancelar» con cambios pidió
    confirmación. Mientras se eligen criterios, solo queda habilitado
    «Cancelar» en toda la vista, incluidos contenido, criterios y alta de
    tareas. Desmarcar todo y guardar dejó la tarea sin criterios.
  - Tras guardar vínculos, editar el contenido y un criterio guardó a la
    primera, sin `412`.
  - Subir el criterio 3 cambió la cobertura a «Cubre los criterios 1 y 2» y
    eliminar el criterio 1 a «Cubre el criterio 1», sin recargar.
  - `412` con los vínculos cambiados por la API: se mostró la selección guardada
    junto a la propia y «Guardar mi selección» la guardó.
  - `404` con un criterio elegido eliminado por la API: la selección siguió
    abierta sin él y se guardó después. Si el resto ya coincidía con lo
    guardado, la selección se cerró informando.
  - Aprobación por la API con una selección abierta: `409`, tareas en modo de
    lectura con su cobertura y la selección no guardada visible como texto.
  - API detenida al guardar: bloqueo y «Consultar estado actual»; tras
    reiniciar, «no se guardó» con la comparación de `412`, y «se guardaron
    correctamente» cuando la API ya tenía la selección enviada.
  - Extremo a extremo: en una especificación nueva se cubrieron todos los
    criterios desde la web y la aprobación por la API respondió `200`.
- Corrección durante la verificación: mientras se esperaba la consulta de
  estado tras perder la conexión, «Guardar vínculos» seguía pareciendo
  habilitado, aunque no reenviaba. Ahora las casillas y los botones de la
  selección se deshabilitan hasta consultar el estado.
- Desviación del plan: no se reprodujo en navegador un fallo aislado de la
  consulta de vínculos con el resto de la vista disponible, porque se carga
  justo después del listado de tareas y la API detenida hace fallar antes la
  vista entera. El aviso con «Reintentar» comparte la carga de cobertura
  verificada en los demás recorridos.
- `dotnet build SpecFlow.slnx --no-restore`: correcto, 0 warnings.
- `dotnet test SpecFlow.slnx --no-build`: 516 pruebas correctas (127 de dominio,
  339 de integración y 50 web).
- `dotnet format SpecFlow.slnx --verify-no-changes --no-restore` y
  `git diff --check`: correctos.
- `dotnet ef migrations has-pending-model-changes`: sin cambios del modelo.
