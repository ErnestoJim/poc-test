# 0024 — Gestionar tareas de implementación desde la interfaz web

## Estado

Implementada y verificada el 15 de octubre de 2026, tras su aprobación.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0006-implementation-tasks/spec.md`,
  `specs/0007-implementation-task-lifecycle/spec.md`,
  `specs/0009-http-concurrency/spec.md`,
  `specs/0012-acceptance-criterion-task-traceability/spec.md`,
  `specs/0013-specification-lifecycle/spec.md`,
  `specs/0021-web-specification-edit/spec.md`,
  `specs/0022-web-form-field-errors/spec.md` y
  `specs/0023-web-acceptance-criteria/spec.md`.

Este incremento amplía la web para consumir la gestión y el ciclo de vida de
tareas ya disponibles en la API. No cambia contratos HTTP, dominio ni
persistencia.

## Problema

Aprobar una especificación exige al menos una tarea, y una especificación
aprobada solo avanza cuando sus tareas se inician y completan. Hoy todo eso
requiere la API: crear, editar, eliminar y ordenar tareas en `draft`, e
iniciarlas y completarlas en `approved`, cada operación con su ETag de tarea o
de listado.

## Objetivo

Desde la vista de una especificación, la persona puede planificar tareas
mientras está en `draft` y ejecutarlas mientras está en `approved`. Cada
operación envía el ETag que corresponde y los conflictos se resuelven de forma
explícita, con el mismo comportamiento que la 0023 para criterios.

## Alcance

- Sección «Tareas de implementación» en la vista de especificación, debajo de
  los criterios, con el listado en orden, el estado de cada tarea y sus fechas
  de inicio y fin, en cualquier estado.
- En `draft`: añadir (título y descripción opcional), editar, eliminar con
  confirmación en la página, y mover con «Subir» y «Bajar».
- En `approved`: «Iniciar» una tarea `pending` y «Completar» una tarea
  `in_progress`, ambas con confirmación en la página.
- Ampliación del cliente HTTP web con las operaciones de tareas.
- Coordinación con el contenido (0021) y los criterios (0023): una sola edición
  activa en toda la vista y relectura de la especificación tras cada cambio.
- Actualización del README con el recorrido disponible.

## Fuera de alcance

- Vincular criterios y tareas (0025) y mostrar los vínculos de una tarea.
- Aprobar o marcar implementada la especificación desde la web (0026).
- Reabrir una tarea completada o completarla directamente desde `pending`, que
  la API no permite.
- Renderizar la descripción como Markdown; se muestra como texto.
- Extraer código común con la sección de criterios. Ver Decisiones.
- Cambios en la API, OpenAPI, dominio o esquema.

## Casos de uso

### UC-01 — Consultar tareas

La vista muestra las tareas numeradas en su orden, con título, descripción si
existe, estado («Pendiente», «En curso», «Completada») y fechas de inicio y fin
cuando existen. Sin tareas, indica que se necesita al menos una antes de
aprobar. Si el listado falla, el resto de la vista sigue disponible y la
sección permite reintentar.

### UC-02 — Añadir una tarea

En `draft`, un formulario con «Título» y «Descripción (opcional)» envía `POST`.
El título se envía tal cual; la API elimina sus espacios exteriores. Una
descripción vacía se envía como ausente (`null`); cualquier otro texto se envía
exacto. Un `400` bajo `title` o `description` se muestra junto a su campo con la
utilidad de la 0022. Un título duplicado (la API no distingue mayúsculas) se
muestra junto al título.

### UC-03 — Editar una tarea

«Editar» consulta la tarea para obtener su ETag y muestra título y descripción
en campos. «Guardar» envía `PUT` con ambos y `If-Match`, y está deshabilitado
sin cambios. «Cancelar» con cambios pide confirmación en la página.

### UC-04 — Eliminar y reordenar

Igual que la 0023: eliminar consulta la tarea, no la elimina si cambió desde que
se mostró, y avisa de que sus vínculos con criterios se perderán. «Subir» y
«Bajar» envían el orden completo con el ETag del listado.

### UC-05 — Iniciar y completar

En `approved`, una tarea `pending` muestra «Iniciar» y una `in_progress` muestra
«Completar». Al confirmar, la web consulta la tarea y, si sigue en el estado
mostrado, envía `POST .../start` o `POST .../complete` con su ETag. Con `200`,
muestra el nuevo estado y sus fechas, y relee la especificación. En otros
estados de la especificación no se muestran estas acciones.

### UC-06 — Conflictos y errores

Mismo tratamiento que la 0023, con estas precisiones:

- `412`: la web no reintenta. En una edición conserva el texto local junto a la
  versión guardada; en una transición muestra el estado actual de la tarea.
- `409`: la web relee la especificación. Si cambió de estado (por ejemplo, dejó
  de ser `draft` o de ser `approved`), la sección se adapta y el texto local
  sigue visible. Si no cambió, un `409` al añadir o editar es un título
  duplicado; al reordenar, un listado cambiado; y en una transición, que la
  tarea ya cambió de estado, que se muestra actualizado.
- Pérdida de conexión: no se reenvía y se exige consultar el estado. En una
  transición, si la tarea ya está en el estado buscado, se confirma.
- `404`, `428` y `400` sin campo conocido: como en la 0023.

## Comportamiento de la interfaz

- Una sola edición activa en toda la vista: contenido, un criterio o una tarea.
  Mientras una está abierta o hay un envío o una consulta pendiente, las demás
  acciones de modificación de la vista están deshabilitadas.
- Tras cualquier cambio correcto en tareas, la vista relee la especificación
  para conservar su ETag vigente.
- La web no transforma título ni descripción, salvo enviar como ausente una
  descripción vacía.
- En `implemented` y `verified` las tareas se muestran en modo de lectura.
- Mensajes en español y sin JSON bruto, ETags ni detalles internos.

## Consumo del contrato HTTP existente

`...` corresponde a
`/api/projects/{projectId}/proposals/{proposalId}/specification/tasks`.

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Listar | `GET ...` | `200`, lista y ETag de listado | `400`, `404` |
| Añadir | `POST ...` con `{"title","description"}` | `201` y tarea | `400`, `404`, `409` |
| Consultar | `GET .../{taskId}` | `200`, tarea y ETag de recurso | `404` |
| Editar | `PUT .../{taskId}` con `If-Match` | `200`, tarea y ETag | `400`, `404`, `409`, `412`, `428` |
| Eliminar | `DELETE .../{taskId}` con `If-Match` | `204` | `404`, `409`, `412`, `428` |
| Reordenar | `PUT .../order` con `{"taskIds":[...]}` e `If-Match` de listado | `204` y ETag de listado | `400`, `404`, `409`, `412`, `428` |
| Iniciar | `POST .../{taskId}/start` con `If-Match` | `200`, tarea y ETag | `404`, `409`, `412`, `428` |
| Completar | `POST .../{taskId}/complete` con `If-Match` | `200`, tarea y ETag | `404`, `409`, `412`, `428` |

Como en la 0023, la web no interpreta los títulos de Problem Details para
distinguir los `409`.

## Cambios en la web

- Nuevo `ImplementationTaskDto` en los modelos del cliente.
- Nuevos métodos en `SpecFlowApiClient` para las ocho operaciones.
- Nuevo componente `ImplementationTasksSection`, integrado en la vista de
  especificación con los mismos parámetros de coordinación que la sección de
  criterios.
- La vista amplía la coordinación para que criterios y tareas se bloqueen
  mutuamente.

Son cambios internos de `SpecFlow.Web`; no afectan a la API.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. La vista lista las tareas en orden con estado y fechas en cualquier estado.
   Sin tareas muestra el aviso. Si el listado falla, el resto sigue visible y
   se puede reintentar.
2. En `draft`, añadir envía un único `POST` con el título tal cual y la
   descripción exacta, o `null` si está vacía, y la tarea aparece al final.
3. Editar envía `PUT` con título, descripción y el ETag de la tarea. «Guardar»
   está deshabilitado sin cambios y «Cancelar» con cambios pide confirmación.
4. Eliminar pide confirmación, no elimina una tarea cambiada desde que se
   mostró, y retira la tarea del listado.
5. «Subir» y «Bajar» envían el orden completo con el ETag del listado y están
   deshabilitados en los extremos.
6. Errores `400` de título o descripción y un título duplicado se muestran
   junto a su campo, con `aria-invalid="true"`, y conservan el texto.
7. En `approved`, «Iniciar» y «Completar» aparecen solo en las tareas
   `pending` e `in_progress`, piden confirmación y muestran el nuevo estado y
   sus fechas. En `draft`, `implemented` y `verified` no aparecen.
8. Ante `412` la web no reintenta, relee el estado y conserva el texto local
   hasta que la persona decide.
9. Ante `409` por un cambio de estado de la especificación, la sección se
   adapta al nuevo estado y el texto local sigue visible.
10. Ante pérdida de conexión, la web no reenvía y exige consultar el estado; una
    transición que ya se aplicó se confirma.
11. Solo hay una edición activa en la vista entre contenido, criterios y
    tareas. Tras cambios en tareas, editar el contenido o un criterio no
    responde `412` por esos cambios.
12. Las pruebas del cliente HTTP cubren rutas, cuerpos, descripción ausente,
    `If-Match`, ETags de recurso y de listado, `204`, y la clasificación de
    `400`, `404`, `409`, `412`, `428` y fallos de conexión de cada operación.
13. Los recorridos de las specs 0018, 0021, 0022 y 0023 no cambian.
14. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Planificación y ejecución de tareas van en la misma spec: comparten listado,
  cliente y componente, y la ejecución solo añade dos acciones.
- «Iniciar» y «Completar» piden confirmación porque la API no permite deshacer
  ninguna de las dos transiciones.
- Una descripción vacía se envía como `null`, porque la API rechaza una
  descripción presente sin contenido y un campo vacío expresa «sin
  descripción». Una descripción de solo espacios se envía tal cual y la API la
  rechaza con un error junto al campo.
- La sección de tareas repite de forma deliberada el patrón de la de criterios
  en lugar de extraer ahora una base común. Refactorizar la 0023 dentro de este
  incremento mezclaría un cambio de comportamiento con una reestructuración.
  La duplicación se registra para el checkpoint de mantenibilidad previsto tras
  completar la web.
- Se mantienen las decisiones de la 0023: botones en lugar de arrastrar, `409`
  clasificado releyendo el estado y sin bUnit.

## Plan de verificación

- Pruebas del cliente HTTP para cada operación de tareas.
- Navegador con API real y SQLite temporal:
  - `draft`: listado vacío, añadir con y sin descripción, validación de título
    (vacío y 201 caracteres) y de descripción (solo espacios y 10.001
    caracteres), título duplicado con otras mayúsculas, editar, cancelar,
    eliminar, subir y bajar.
  - Aprobación por la API: «Iniciar» y «Completar» con confirmación, estados y
    fechas, y ausencia de acciones de planificación.
  - `412` editando y en una transición; `409` al reordenar con la
    especificación aprobada por la API; tarea iniciada por la API antes de
    confirmar «Iniciar».
  - API detenida al añadir y al iniciar: bloqueo y recuperación en los dos
    resultados.
  - Bloqueo mutuo entre contenido, criterios y tareas, y edición del contenido
    y de un criterio sin `412` tras cambios en tareas.
- Compilación, pruebas, formato y ausencia de cambios en el modelo EF Core y en
  OpenAPI.

## Evidencia de cierre

- `SpecFlowApiClient` añade las ocho operaciones de tareas. El componente
  `ImplementationTasksSection` repite el patrón de `AcceptanceCriteriaSection`,
  como se acordó, y la vista amplía la coordinación para que contenido,
  criterios y tareas se bloqueen mutuamente. No cambian la API, OpenAPI, el
  dominio ni el esquema.
- Ocho pruebas nuevas del cliente HTTP: rutas, título enviado sin recortar,
  descripción exacta y `null`, `If-Match` literal, ETags de recurso y de
  listado, `204`, transiciones sin cuerpo, clasificación de `400` (`title`,
  `description` y `taskIds`), `404`, `409`, `412` y `428`, y fallos de conexión
  de cada operación.
- Navegador con API real y SQLite temporal:
  - `draft`: aviso de lista vacía; título vacío y de 201 caracteres;
    descripción de solo espacios y de 10.001 caracteres, cada error junto a su
    campo con `aria-invalid="true"`. Al añadir, la API recortó el título y
    conservó la descripción exacta. Título duplicado con otras mayúsculas al
    añadir y al editar.
  - Editar con «Guardar» deshabilitado sin cambios y confirmación al cancelar;
    vaciar la descripción la guardó como `null`. Subir, bajar y eliminar con
    confirmación dejaron el orden esperado en la API.
  - Bloqueo mutuo: con texto en el alta de tareas, los criterios y «Editar» del
    contenido quedan bloqueados; editando un criterio o el contenido, las
    tareas también. Tras los cambios en tareas, editar el contenido y un
    criterio guardó a la primera; el registro de la API no contiene ningún
    `412`.
  - `412` al editar una tarea cambiada por la API: ambas versiones y «Guardar
    mi versión». Reordenar con una tarea añadida por la API: orden sin aplicar
    y listado actualizado.
  - Aprobación por la API durante la edición de una tarea: `409`, estado
    «Aprobada», texto local visible y aparición de «Iniciar».
  - `approved`: «Iniciar» y «Completar» con confirmación, estados «En curso» y
    «Completada» con sus fechas. Una tarea iniciada por la API antes de
    confirmar no se reenvió y se mostró «En curso».
  - `implemented`: tareas en modo de lectura, sin controles.
  - API detenida al añadir: bloqueo con el texto visible; tras reiniciar, «no
    se añadió» con el texto en los campos, y «se añadió correctamente» cuando la
    API ya tenía la tarea, comparando el título recortado.
- Corrección durante la verificación: el error de título duplicado no se
  limpiaba al volver a escribir, porque solo se limpiaban errores `400`. Ahora
  se limpia cualquier error asociado a `title` o `description`.
- Desviación del plan: no se pudo reproducir en navegador un resultado incierto
  en una transición. Con la API detenida falla primero la lectura previa de la
  tarea, de modo que no se envía nada ni hay incertidumbre; la vista lo informa
  sin bloquear. La rama de recuperación de transiciones comparte el tratamiento
  de las demás operaciones y su clasificación se cubre en las pruebas del
  cliente. Tampoco se provocó un `412` en una transición, por el mismo motivo.
- `dotnet build SpecFlow.slnx --no-restore`: correcto, 0 warnings.
- `dotnet test SpecFlow.slnx --no-build`: 509 pruebas correctas (127 de dominio,
  339 de integración y 43 web).
- `dotnet format SpecFlow.slnx --verify-no-changes --no-restore` y
  `git diff --check`: correctos.
- `dotnet ef migrations has-pending-model-changes`: sin cambios del modelo.
