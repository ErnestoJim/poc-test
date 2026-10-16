# 0026 — Aprobar y marcar implementada una especificación desde la interfaz web

## Estado

Implementada y verificada el 16 de octubre de 2026, tras su aprobación.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0009-http-concurrency/spec.md`,
  `specs/0013-specification-lifecycle/spec.md`,
  `specs/0021-web-specification-edit/spec.md`,
  `specs/0023-web-acceptance-criteria/spec.md`,
  `specs/0024-web-implementation-tasks/spec.md` y
  `specs/0025-web-criterion-task-links/spec.md`.

Este incremento amplía la web para consumir las transiciones de aprobación e
implementación ya disponibles en la API. No cambia contratos HTTP, dominio ni
persistencia.

## Problema

Desde la web ya se puede preparar el plan completo de una especificación y
ejecutar sus tareas, pero los dos pasos que separan esas fases siguen
requiriendo la API: aprobar la especificación cuando el plan está listo y
marcarla implementada cuando todas sus tareas están completadas. Ambos
necesitan el ETag de la especificación, que cubre el plan entero.

## Objetivo

Desde la vista de una especificación, la persona puede aprobarla mientras está
en `draft` y marcarla implementada mientras está en `approved`, tras confirmar
en la página. La aprobación se aplica exactamente al plan que la persona tiene
delante y los rechazos se explican sin interpretar textos de error de la API.

## Alcance

- En `draft`: «Aprobar especificación», con confirmación en la página que
  avisa de que contenido, criterios, tareas y vínculos dejarán de poder
  modificarse.
- En `approved`: «Marcar como implementada», con confirmación en la página.
- Envío de `If-Match` con el ETag de especificación vigente en la vista.
- Explicación de los requisitos cuando la API rechaza la transición.
- Relectura del listado de criterios cuando cambia el ETag de la
  especificación, como ya hace la sección de tareas desde la 0025.
- Ampliación del cliente HTTP web con las dos transiciones.
- Actualización del README con el recorrido disponible.

## Fuera de alcance

- Verificar criterios y marcar verificada la especificación (0027).
- Calcular en la web si la especificación cumple los requisitos antes de
  enviar. La API es la única que decide; ver Decisiones.
- Volver a `draft` o deshacer una transición, que la API no permite.
- Cambios en la API, OpenAPI, dominio o esquema.

## Casos de uso

### UC-01 — Aprobar

En `draft`, la vista muestra «Aprobar especificación» junto al estado. Al
pulsarlo, pide confirmación en la página. Al confirmar, envía
`POST .../specification/approve` con `If-Match` y el ETag que la vista tiene.
Con `200`, adopta la especificación y el ETag devueltos, muestra «Aprobada» con
su fecha, y las secciones de criterios y tareas pasan a su modo de `approved`.

### UC-02 — Marcar como implementada

En `approved`, la vista muestra «Marcar como implementada». Al confirmar, envía
`POST .../specification/mark-implemented` con `If-Match`. Con `200`, muestra
«Implementada» con su fecha y las tareas quedan en modo de lectura.

### UC-03 — Rechazos y conflictos

- `409`: la web relee la especificación y clasifica por su estado, no por el
  título del Problem Details:
  - Si ya está en el estado buscado, informa de que otra persona hizo la
    transición y la muestra.
  - Si está en otro estado distinto del de partida, informa del cambio y la
    vista se adapta.
  - Si sigue en el estado de partida, explica los requisitos: para aprobar, al
    menos un criterio, al menos una tarea y todos los criterios cubiertos por
    alguna tarea, remitiendo al resumen de cobertura; para marcarla
    implementada, todas las tareas completadas.
- `412`: el plan cambió desde que se mostró. La web no reintenta: relee la
  especificación, los criterios y las tareas, e informa de que debe revisarse
  el plan actual antes de volver a intentarlo.
- `404`: error contextual como en la 0021.
- Pérdida de conexión o `5xx`: no se reenvía y se exige «Consultar estado
  actual». Si la especificación ya está en el estado buscado, se confirma; si
  no, se informa de que no se aplicó.
- `428` o `400`: error genérico y se exige consultar el estado.

## Comportamiento de la interfaz

- Las acciones de ciclo de vida están deshabilitadas mientras hay una edición,
  una selección, una confirmación, un envío o una recuperación en curso en
  cualquier parte de la vista. Mientras se confirma o envía una transición, las
  secciones quedan bloqueadas.
- La confirmación indica qué deja de poder hacerse después de la transición.
- Mensajes en español y sin JSON bruto, ETags ni detalles internos.

## Consumo del contrato HTTP existente

`...` corresponde a `/api/projects/{projectId}/proposals/{proposalId}`.

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Aprobar | `POST .../specification/approve` con `If-Match` | `200`, especificación y ETag | `400`, `404`, `409`, `412`, `428` |
| Marcar implementada | `POST .../specification/mark-implemented` con `If-Match` | `200`, especificación y ETag | `400`, `404`, `409`, `412`, `428` |

La API comprueba `If-Match` antes que el estado y los requisitos, así que un
`409` con el ETag vigente significa que la transición no es válida en ese
momento.

## Cambios en la web

- Dos métodos nuevos en `SpecFlowApiClient`, sin cuerpo y con `If-Match`.
- `SpecificationDetail` muestra las acciones, la confirmación y la
  recuperación, y amplía el bloqueo de la vista.
- `AcceptanceCriteriaSection` recibe el ETag de la especificación y relee su
  listado cuando cambia y no tiene una operación en curso.

Son cambios internos de `SpecFlow.Web`; no afectan a la API.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. En `draft` se muestra «Aprobar especificación» y en `approved` «Marcar como
   implementada»; en `implemented` y `verified` no se muestra ninguna de las
   dos.
2. Cada acción pide confirmación en la página y, al confirmar, envía un único
   `POST` sin cuerpo con el ETag de especificación vigente en `If-Match`.
3. Con `200`, la vista muestra el nuevo estado y su fecha, y las secciones de
   criterios y tareas se adaptan sin recargar la página.
4. Ante `409` con la especificación todavía en el estado de partida, la web
   explica los requisitos de esa transición.
5. Ante `409` con la especificación ya en otro estado, la web lo informa y
   muestra el estado actual.
6. Ante `412`, la web no reintenta y muestra el plan actual, incluidos los
   criterios y las tareas.
7. Ante pérdida de conexión, la web no reenvía, exige consultar el estado y
   confirma la transición si ya se aplicó.
8. Las acciones están deshabilitadas mientras hay otra edición o envío en la
   vista, y las secciones lo están mientras se confirma o envía una transición.
9. Tras aprobar, la tarea puede iniciarse y completarse desde la web sin `412`,
   y tras completarlas se puede marcar implementada sin recargar.
10. Las pruebas del cliente HTTP cubren rutas, ausencia de cuerpo, `If-Match`,
    ETag de la respuesta y la clasificación de `400`, `404`, `409`, `412`,
    `428` y fallos de conexión.
11. Los recorridos de las specs 0021 y 0023 a 0025 no cambian.
12. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- La web no precalcula si la especificación está lista. El botón está
  disponible en `draft` y `approved`, y la API decide. Precalcularlo duplicaría
  reglas del dominio en la web y podría desincronizarse. El resumen de
  cobertura de la 0025 ya orienta a la persona antes de aprobar.
- No hay lectura previa antes de enviar. El ETag de la especificación cubre
  todo el plan, así que `If-Match` ya garantiza que se aprueba exactamente lo
  que la vista muestra.
- Las dos transiciones van en la misma spec porque comparten acción,
  confirmación y recuperación. La verificación va aparte porque depende de
  verificar cada criterio.
- La sección de criterios adopta la relectura por ETag de la 0025 para que un
  `412` o un cambio externo actualice todo el plan visible. Es un cambio
  interno que no altera sus recorridos.

## Plan de verificación

- Pruebas del cliente HTTP para las dos transiciones.
- Navegador con API real y SQLite temporal:
  - Aprobar sin criterios, sin tareas y con un criterio sin tarea: `409` y
    requisitos explicados; aprobar con el plan completo: `200`, fecha y
    secciones en modo `approved`.
  - Iniciar y completar las tareas desde la web y marcar implementada: `200`,
    fecha y tareas en modo de lectura. Marcar implementada con una tarea sin
    completar: `409` y requisito explicado.
  - `412` añadiendo un criterio por la API antes de confirmar; `409` aprobando
    por la API antes de confirmar.
  - API detenida al confirmar: bloqueo y recuperación en los dos resultados.
  - Bloqueo mutuo entre las acciones de ciclo de vida, el contenido, los
    criterios y las tareas.
- Compilación, pruebas, formato y ausencia de cambios en el modelo EF Core y en
  OpenAPI.

## Evidencia de cierre

- `SpecFlowApiClient` añade las dos transiciones, sin cuerpo y con `If-Match`.
  `SpecificationDetail` muestra la acción disponible según el estado, con su
  confirmación y recuperación, y amplía el bloqueo de la vista.
  `AcceptanceCriteriaSection` relee su listado cuando cambia el ETag de la
  especificación. No cambian la API, OpenAPI, el dominio ni el esquema.
- Tres pruebas nuevas del cliente HTTP (siete casos): rutas, ausencia de
  cuerpo, `If-Match` literal, estado, fecha y ETag de la respuesta,
  clasificación de `400`, `404`, `409`, `412` y `428`, y fallos de conexión.
- Navegador con API real y SQLite temporal:
  - Aprobar sin criterios ni tareas y con un criterio sin tarea: `409`, estado
    «Borrador» y requisitos explicados. Tras vincular el criterio desde la web,
    la aprobación respondió `200`, mostró «Aprobada» con su fecha y las
    secciones pasaron a su modo de `approved` sin recargar.
  - Marcar implementada con la tarea pendiente: `409` y requisito explicado.
    Tras iniciar y completar la tarea desde la web, `200`, «Implementada» con su
    fecha, tareas en modo de lectura y sin acciones de ciclo de vida. Ningún
    paso respondió `412`.
  - Durante la confirmación solo quedan habilitados «Confirmar» y «Cancelar».
    La acción queda deshabilitada, con su aviso, mientras se edita el
    contenido o un criterio, hay texto en el alta de tareas o se eligen
    criterios.
  - Criterio añadido por la API antes de confirmar la aprobación: `412`, aviso
    de plan cambiado, el nuevo criterio apareció en su sección y el resumen de
    cobertura pasó a «Criterio sin tarea: 2».
  - Aprobación por la API antes de confirmar: la web informó de que ya estaba
    aprobada y pasó a ofrecer «Marcar como implementada».
  - API detenida al confirmar «Marcar como implementada»: bloqueo con
    «Consultar estado actual» como única acción; tras reiniciar, «no se
    aplicó», y «se aplicó correctamente» cuando la API ya la había marcado.
- Corrección y desviación del plan: el plan preveía un `409` aprobando por la
  API antes de confirmar, pero toda transición cambia el ETag, así que llega
  como `412`. El tratamiento de `412` clasifica ahora también por estado e
  informa de la transición hecha por otra persona en lugar de un plan cambiado.
  Por el mismo motivo, el `409` por estado distinto solo se alcanza si el ETag
  coincide, y se cubre con la misma clasificación.
- Revisión de idioma de la interfaz, pedida al aprobar esta spec: se recorrieron
  todas las páginas (proyectos, nuevo proyecto, proyecto, nueva propuesta,
  propuestas pendiente, rechazada y aceptada, especificación en cada estado,
  ruta inexistente, identificador no válido y error) recogiendo texto visible,
  títulos de página, `aria-label`, `title` y `placeholder`, y se revisaron los
  literales de los componentes y el cliente. Todo está en español, el documento
  declara `lang="es"`, el diálogo de reconexión está traducido y no hay
  validaciones nativas del navegador que muestren mensajes en su idioma. Los
  Problem Details de la API están en inglés, pero la web nunca los muestra:
  el cliente traduce cada estado HTTP a un mensaje propio.
- `dotnet build SpecFlow.slnx --no-restore`: correcto, 0 warnings.
- `dotnet test SpecFlow.slnx --no-build`: 523 pruebas correctas (127 de dominio,
  339 de integración y 57 web).
- `dotnet format SpecFlow.slnx --verify-no-changes --no-restore` y
  `git diff --check`: correctos.
- `dotnet ef migrations has-pending-model-changes`: sin cambios del modelo.
