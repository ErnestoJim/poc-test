# 0028 — Gestionar decisiones técnicas desde la interfaz web

## Estado

Aprobada para implementación el 16 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0009-http-concurrency/spec.md`,
  `specs/0010-technical-decisions/spec.md`,
  `specs/0011-technical-decision-lifecycle/spec.md`,
  `specs/0017-web-proposal-decisions/spec.md`,
  `specs/0021-web-specification-edit/spec.md` y
  `specs/0022-web-form-field-errors/spec.md`.

Este incremento amplía la web para consumir la gestión y el ciclo de vida de
decisiones técnicas ya disponibles en la API. No cambia contratos HTTP, dominio
ni persistencia.

## Problema

Las decisiones técnicas de un proyecto (registros de decisión con título y
contenido Markdown) solo se gestionan por la API. Desde la web no se pueden
consultar, redactar, aceptar, rechazar ni sustituir, aunque forman parte del
contexto con el que se escriben y revisan las especificaciones.

## Objetivo

Desde un proyecto, la persona puede consultar sus decisiones técnicas,
redactarlas y corregirlas mientras están en borrador, aceptarlas o rechazarlas
con un motivo, eliminar un borrador y marcar una decisión aceptada como
sustituida por otra posterior. Cada operación envía el ETag de la decisión y
los conflictos se resuelven de forma explícita.

## Alcance

- Sección «Decisiones técnicas» en la vista de proyecto, debajo de las
  propuestas, con título, estado y fecha de cada decisión en el orden de la API
  (creación).
- Página «Nueva decisión técnica» con «Título» y «Contenido Markdown».
- Página de detalle de una decisión con su contenido como texto Markdown, su
  estado, sus fechas, el motivo de rechazo si lo tiene y, si está sustituida,
  un enlace a la decisión que la sustituye.
- En `draft`: editar título y contenido, eliminar con confirmación, aceptar con
  confirmación y rechazar con un motivo obligatorio.
- En `accepted`: «Marcar como sustituida», eligiendo la decisión que la
  sustituye entre las aceptadas después en el mismo proyecto.
- Ampliación del cliente HTTP web con las operaciones de decisiones.
- Actualización del README con el recorrido disponible.

## Fuera de alcance

- Vincular decisiones con propuestas o especificaciones, que la API no ofrece.
- Renderizar el contenido Markdown; se muestra como texto, como el de las
  especificaciones.
- Filtrar o buscar decisiones.
- Reabrir una decisión decidida o deshacer una sustitución, que la API no
  permite.
- Cambios en la API, OpenAPI, dominio o esquema.

## Casos de uso

### UC-01 — Consultar decisiones

La vista de proyecto lista sus decisiones con título, estado («Borrador»,
«Aceptada», «Rechazada», «Sustituida») y fecha de decisión si la tiene. Sin
decisiones, invita a registrar la primera. Si el listado falla, las propuestas
siguen visibles y la sección permite reintentar. Cada decisión enlaza a su
detalle.

### UC-02 — Registrar una decisión

«Nueva decisión técnica» envía `POST` con título y contenido. La API elimina
los espacios exteriores del título; el contenido se envía exacto. Los `400`
bajo `title` o `content` se muestran junto a su campo con la utilidad de la
0022. Con `201`, la web abre el detalle de la decisión creada. Si se pierde la
conexión, no se reenvía y se indica que el resultado es desconocido, con un
enlace a la lista del proyecto para comprobarlo.

### UC-03 — Editar un borrador

En `draft`, «Editar» muestra título y contenido en campos. «Guardar cambios»
envía `PUT` con `If-Match` y está deshabilitado sin cambios. «Cancelar» con
cambios pide confirmación en la página. Ante `412`, igual que la 0021: la web
muestra la versión guardada junto al texto propio y deja decidir.

### UC-04 — Aceptar, rechazar o eliminar un borrador

- «Aceptar» pide confirmación y envía `POST .../accept` con `If-Match`.
- «Rechazar» muestra un campo «Motivo del rechazo» obligatorio (hasta 1.000
  caracteres) y envía `POST .../reject` con el motivo e `If-Match`.
- «Eliminar» pide confirmación y envía `DELETE` con `If-Match`. Con `204`, la
  web vuelve a la vista de proyecto e informa.

### UC-05 — Marcar como sustituida

En `accepted`, «Marcar como sustituida» muestra las decisiones aceptadas del
proyecto decididas después que esta. Al elegir una y confirmar, envía
`POST .../supersede` con `{"replacementDecisionId"}` e `If-Match`. Con `200`,
la decisión muestra «Sustituida», su fecha y el enlace a la sustituta. Si no
hay candidatas, se explica que primero debe aceptarse una decisión posterior.

### UC-06 — Conflictos y errores

- `412`: la web no reintenta. Relee la decisión y clasifica por su estado, no
  por el título del Problem Details. Cualquier cambio de estado incrementa la
  versión de la decisión, así que una decisión aceptada, rechazada o sustituida
  por otra persona llega como `412`:
  - Si ya no está en el estado de partida, informa del cambio y adapta las
    acciones; en una edición, el texto propio sigue visible como texto no
    guardado.
  - Si sigue en el mismo estado, en una edición muestra la versión guardada
    junto al texto propio y deja decidir; en una transición informa de que la
    decisión cambió y muestra la actual.
- `409`: con el ETag vigente indica que la operación no es válida en ese
  estado. La web relee y aplica la misma clasificación. Al sustituir con la
  decisión todavía aceptada, explica que la sustituta debe estar aceptada y
  ser posterior.
- `404`: si falta la decisión, error contextual con enlace al proyecto; si falta
  la sustituta, se informa y se recarga la lista de candidatas.
- Pérdida de conexión o `5xx`: no se reenvía y se exige «Consultar estado
  actual». Si la decisión ya está en el estado buscado (o ya no existe tras
  eliminarla), se confirma; si no, se informa de que no se aplicó.
- `428` o `400` sin campo conocido: error genérico y se exige consultar el
  estado.

## Comportamiento de la interfaz

- Solo una acción activa a la vez en el detalle: edición, confirmación,
  rechazo o sustitución.
- Las acciones irreversibles (aceptar, rechazar, eliminar, sustituir) piden
  confirmación en la página e indican qué no podrá deshacerse.
- La web no transforma título, contenido ni motivo.
- Mensajes en español y sin JSON bruto, ETags ni detalles internos.

## Consumo del contrato HTTP existente

`...` corresponde a `/api/projects/{projectId}/technical-decisions`.

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Listar | `GET ...` | `200` y lista | `400`, `404` |
| Registrar | `POST ...` con `{"title","content"}` | `201`, decisión y ETag | `400`, `404` |
| Consultar | `GET .../{decisionId}` | `200`, decisión y ETag | `400`, `404` |
| Editar | `PUT .../{decisionId}` con `If-Match` | `200`, decisión y ETag | `400`, `404`, `409`, `412`, `428` |
| Aceptar | `POST .../{decisionId}/accept` con `If-Match` | `200`, decisión y ETag | `404`, `409`, `412`, `428` |
| Rechazar | `POST .../{decisionId}/reject` con `{"reason"}` e `If-Match` | `200`, decisión y ETag | `400`, `404`, `409`, `412`, `428` |
| Sustituir | `POST .../{decisionId}/supersede` con `{"replacementDecisionId"}` e `If-Match` | `200`, decisión y ETag | `400`, `404`, `409`, `412`, `428` |
| Eliminar | `DELETE .../{decisionId}` con `If-Match` | `204` | `404`, `409`, `412`, `428` |

El listado no tiene ETag de colección, así que cada modificación usa el ETag de
la decisión, obtenido al consultar su detalle. Al sustituir, la API comprueba
que existan ambas decisiones antes que `If-Match`.

## Cambios en la web

- Nuevo `TechnicalDecisionDto` y ocho métodos en `SpecFlowApiClient`.
- Sección de decisiones en `ProjectDetail`.
- Páginas nuevas `NewTechnicalDecision` y `TechnicalDecisionDetail`.

Son cambios internos de `SpecFlow.Web`; no afectan a la API.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. La vista de proyecto lista sus decisiones con título, estado y fecha de
   decisión; sin decisiones muestra la invitación, y si el listado falla las
   propuestas siguen visibles y se puede reintentar.
2. Registrar envía un único `POST` con el título tal cual y el contenido exacto,
   y abre el detalle de la decisión creada.
3. El detalle muestra contenido, estado, fechas, motivo de rechazo y enlace a la
   sustituta según corresponda.
4. Editar envía `PUT` con el ETag de la decisión; «Guardar cambios» está
   deshabilitado sin cambios y «Cancelar» con cambios pide confirmación.
5. Errores `400` de título, contenido o motivo se muestran junto a su campo con
   `aria-invalid="true"` y conservan el texto.
6. Aceptar, rechazar, eliminar y sustituir piden confirmación y envían un único
   `POST` o `DELETE` con `If-Match`; rechazar exige un motivo.
7. «Marcar como sustituida» solo ofrece decisiones aceptadas posteriores del
   mismo proyecto y, sin candidatas, explica por qué.
8. Las acciones de cada estado son las únicas visibles: edición, aceptación,
   rechazo y eliminación en `draft`; sustitución en `accepted`; ninguna en
   `rejected` y `superseded`.
9. Ante `412`, la web no reintenta, relee la decisión y, si sigue en el mismo
   estado, conserva el texto propio junto a la versión guardada hasta que la
   persona decide.
10. Ante `412` o `409` con la decisión en otro estado, la web se adapta a ese
    estado e informa, y el texto propio sigue visible.
11. Ante pérdida de conexión, la web no reenvía, exige consultar el estado y
    confirma la operación si ya se aplicó.
12. Las pruebas del cliente HTTP cubren rutas, cuerpos, `If-Match`, ETags,
    `204` y la clasificación de `400`, `404`, `409`, `412`, `428` y fallos de
    conexión de cada operación.
13. Los recorridos de las specs 0016 a 0027 no cambian.
14. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Las decisiones se gestionan en páginas propias, como las propuestas, y no
  dentro de la vista de proyecto: su contenido Markdown puede ser largo y su
  ciclo de vida tiene cuatro estados.
- La web filtra las candidatas a sustituta con la misma regla que comunica la
  API (aceptadas, del mismo proyecto y decididas después). Es la única regla de
  dominio que la web repite, para no ofrecer opciones que siempre fallarían; la
  API sigue decidiendo y un `409` se explica igualmente.
- La gestión completa y el ciclo de vida van en la misma spec porque comparten
  cliente, páginas y tratamiento de conflictos, como en la 0024.
- Tras registrar una decisión, la web abre su detalle en lugar de volver al
  proyecto, porque el siguiente paso habitual es revisarla y decidirla.
- Se mantiene la verificación en navegador sin bUnit.

## Plan de verificación

- Pruebas del cliente HTTP para cada operación de decisiones.
- Navegador con API real y SQLite temporal:
  - Lista vacía y con decisiones en los cuatro estados.
  - Registrar con título y contenido válidos, vacíos y demasiado largos;
    título con espacios exteriores.
  - Editar, cancelar con y sin cambios y botón deshabilitado sin cambios.
  - Aceptar; rechazar sin motivo, con motivo de 1.001 caracteres y con motivo
    válido; eliminar un borrador.
  - Sustituir sin candidatas y con una candidata posterior; enlace a la
    sustituta.
  - `412` editando con la decisión editada por la API; `412` editando y
    aceptando con la decisión aceptada por la API, que debe presentarse como
    cambio de estado; `409` sustituyendo con una candidata que deja de ser
    válida.
  - API detenida al aceptar y al eliminar: bloqueo y recuperación en los dos
    resultados.
- Compilación, pruebas, formato y ausencia de cambios en el modelo EF Core y en
  OpenAPI.

## Evidencia de cierre

Pendiente de implementación y verificación.
