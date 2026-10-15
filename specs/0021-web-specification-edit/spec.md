# 0021 — Editar el contenido de especificaciones desde la interfaz web

## Estado

Aprobada para implementación el 15 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0004-specifications/spec.md`,
  `specs/0009-http-concurrency/spec.md`,
  `specs/0013-specification-lifecycle/spec.md` y
  `specs/0018-web-specification-create-read/spec.md`.

Este incremento amplía la web para consumir la edición de contenido ya
disponible en la API. No cambia contratos HTTP, dominio ni persistencia. Es el
primer flujo web que usa ETags e `If-Match`, reservado expresamente por la 0018
para un incremento propio.

## Problema

Desde la web se puede crear y leer una especificación, pero corregir su
contenido obliga a salir del navegador, consultar el ETag y construir un `PUT`
con `If-Match`. El documento suele necesitar varias iteraciones mientras está en
`draft`, que es justo cuando la web solo permite leerlo.

La edición tiene un riesgo que la creación no tenía: otra persona, u otra
pestaña, puede haber cambiado la especificación desde que se cargó la vista. El
ETag de la especificación representa el plan completo (contenido, criterios,
tareas, estados, trazabilidad y evaluaciones), así que un `412` puede deberse a
un cambio de texto o a un cambio del plan hecho por la API. En ningún caso debe
perderse el texto que la persona escribió ni sobrescribirse en silencio el
trabajo ajeno.

## Objetivo

Permitir editar desde la web el contenido Markdown de una especificación en
`draft`, enviando el ETag vigente, y resolver los conflictos de forma explícita:
la web muestra la versión guardada y conserva el texto local, y la persona
decide si lo vuelve a guardar sobre la versión actual.

## Alcance

- Modo de edición en la vista de especificación existente, solo en estado
  `draft`.
- Captura del ETag devuelto por `GET` y `PUT`, y envío literal en `If-Match`.
- Guardado del contenido completo mediante `PUT` y presentación de la
  representación devuelta.
- Resolución explícita de `412`, `409` (especificación no editable), `404` y
  resultado incierto por pérdida de conexión.
- Ampliación del cliente HTTP web para leer el ETag de las respuestas y enviar
  `If-Match`, con sus pruebas.
- Actualización del README con el recorrido disponible.

## Fuera de alcance

- Fusión automática o comparación línea a línea de versiones. Se muestran ambas
  versiones completas, una junto a la otra.
- Vista previa HTML, resaltado o editor enriquecido de Markdown.
- Guardado automático, borradores locales persistentes o historial de versiones.
- Advertencia del navegador al abandonar la página con cambios sin guardar.
- Aprobar, marcar implementada o verificar desde la web.
- Criterios, tareas, evaluaciones y trazabilidad desde la web.
- Cambios en la API, OpenAPI, dominio o esquema.

## Casos de uso

### UC-01 — Editar y guardar

La persona abre una especificación `draft` y pulsa «Editar». La web muestra un
campo multilínea con el contenido guardado y recuerda el ETag de la última
lectura. Al pulsar «Guardar cambios», envía un `PUT` con el texto exacto y ese
ETag en `If-Match`. Con `200`, muestra la representación devuelta, guarda el
nuevo ETag y vuelve al modo de lectura.

«Guardar cambios» está deshabilitado mientras el texto sea idéntico al guardado.
«Cancelar» vuelve al modo de lectura. Si hay cambios, pide una confirmación en
la propia página (no un diálogo del navegador) antes de descartarlos.

### UC-02 — Validación

Un `400` asociado a `content` (vacío, solo espacios o más de 100.000
caracteres) muestra el error junto al campo, mantiene el modo de edición y
conserva el texto.

### UC-03 — Cambio concurrente (`412`)

Si `PUT` responde `412`, la web no reintenta. Consulta la especificación y
muestra:

- la versión guardada actual, con su estado y fecha de actualización;
- el texto local de la persona, en un bloque separado y claramente marcado como
  no guardado;
- un aviso que distingue si el contenido guardado cambió o si solo cambió el
  resto del plan (contenido actual idéntico al que se cargó para editar).

Si la especificación sigue en `draft`, la persona puede elegir:

- «Guardar mi versión»: envía un nuevo `PUT` con su texto y el ETag recién
  leído. Es una decisión explícita de sobrescribir la versión actual.
- «Descartar mi versión»: vuelve al modo de lectura con la versión guardada.
- Seguir editando a partir de su texto local.

Si la especificación ya no está en `draft`, aplica UC-04.

### UC-04 — La especificación dejó de ser editable (`409`)

Si `PUT` responde `409` porque la especificación ya no está en `draft`, o una
lectura de recuperación muestra otro estado, la web presenta la versión
guardada en modo de lectura, sin controles de edición, y mantiene accesible el
texto local no guardado hasta salir de la vista.

### UC-05 — Resultado incierto

Ante pérdida de conexión o tiempo agotado durante `PUT`, la web informa de que
no se sabe si el cambio se guardó, bloquea un nuevo envío y ofrece «Consultar
estado actual»:

- si el contenido guardado coincide exactamente con el texto enviado, muestra
  que el cambio se guardó, actualiza el ETag y vuelve al modo de lectura;
- si no coincide, actúa como en UC-03, con ambas versiones y la decisión
  explícita;
- si la consulta falla, mantiene el bloqueo y permite reintentar la lectura.

### UC-06 — Contexto inexistente

Un `404` durante el guardado o la recuperación (proyecto, propuesta o
especificación que ya no existe en ese contexto) muestra el error contextual
con una salida clara, conserva visible el texto local y no muestra datos de
otro contexto.

## Comportamiento de la interfaz

- «Editar» solo aparece con una especificación `draft` y un ETag conocido. Los
  estados `approved`, `implemented`, `verified` o desconocido se muestran en
  modo de lectura, como hasta ahora.
- La web trata el ETag como un valor opaco: lo copia de la cabecera `ETag` y lo
  envía sin modificarlo. No lo interpreta ni lo muestra.
- Durante el envío se impide un segundo guardado y se deshabilitan las
  acciones de la vista.
- La web no transforma el texto: no recorta espacios ni normaliza saltos de
  línea.
- Un `428` o un `400` sin campo `content` indica un fallo del cliente. Se
  muestra un error genérico, se conserva el texto y se exige consultar el estado
  antes de otro intento.
- Los mensajes están en español y sin JSON bruto, ETags ni detalles internos.

## Consumo del contrato HTTP existente

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Consultar | `GET .../specification` | `200`, `SpecificationResponse` y `ETag` | `400`, `404` |
| Guardar | `PUT .../specification`, `If-Match: <etag>`, JSON `{"content":"..."}` | `200`, `SpecificationResponse` y `ETag` | `400` bajo `content`, `404`, `409`, `412`, `428` |

`...` corresponde a `/api/projects/{projectId}/proposals/{proposalId}`. El orden
de evaluación y los títulos de error son los de la constitución y las specs
0009 y 0013.

## Cambios en la web

- `ApiResult<T>` incorpora el ETag opcional de la respuesta.
- `GetSpecificationAsync` y `CreateSpecificationAsync` exponen el ETag recibido.
- Nuevo `UpdateSpecificationAsync(projectId, proposalId, content, entityTag)`
  que envía `If-Match`.

Son cambios internos de `SpecFlow.Web`; no afectan a la API.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. Una especificación `draft` muestra «Editar». Las especificaciones en otro
   estado no muestran controles de edición.
2. Editar y guardar envía un único `PUT` con el texto exacto y el ETag de la
   última lectura en `If-Match`. Con `200`, la vista muestra el contenido y la
   fecha de actualización devueltos, sin recargar la página, y adopta el nuevo
   ETag para la siguiente edición.
3. «Guardar cambios» está deshabilitado cuando el texto es idéntico al guardado.
4. «Cancelar» con cambios pide confirmación en la página antes de descartarlos.
   Sin cambios, vuelve directamente al modo de lectura.
5. Un `400` bajo `content` muestra el error junto al campo y conserva el texto
   y el modo de edición.
6. Ante `412`, la web no reintenta. Muestra la versión guardada actual y el
   texto local por separado, indica si cambió el contenido o solo el plan, y
   ofrece guardar explícitamente el texto local con el ETag recién leído o
   descartarlo.
7. Ante `409`, o si la recuperación encuentra un estado distinto de `draft`, la
   vista queda en modo de lectura y el texto local sigue accesible.
8. Ante pérdida de conexión durante `PUT`, la web no reenvía y exige consultar
   el estado. Si el contenido guardado coincide con el enviado, lo confirma como
   guardado; si no, presenta la resolución de UC-03.
9. Durante un envío no puede iniciarse un segundo guardado.
10. Ningún flujo de error descarta el texto local sin una acción explícita de la
    persona.
11. Las pruebas del cliente HTTP cubren el envío literal de `If-Match`, la
    lectura del ETag en `GET`, `POST` y `PUT`, y la clasificación de `400`,
    `404`, `409`, `412`, `428` y fallos de conexión.
12. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- La resolución de conflictos es manual y explícita, con ambas versiones
  completas a la vista. Una fusión o un diff añadirían complejidad y,
  probablemente, un paquete nuevo para un caso poco frecuente en este proyecto.
- Reintentar con el ETag nuevo solo ocurre tras una acción de la persona. Un
  reintento automático equivaldría a ignorar la precondición.
- El aviso de abandonar la página con cambios queda fuera porque requiere
  interoperabilidad con JavaScript del navegador. Puede proponerse después si
  la necesidad aparece.
- No se añaden pruebas de componentes Blazor (bUnit), porque supondría un
  paquete nuevo con su propia aprobación. Los recorridos de interfaz se
  verifican en navegador y quedan registrados en la evidencia, como en las
  specs 0016 a 0018.

## Plan de verificación

- Pruebas del cliente HTTP para `If-Match`, captura de ETag y clasificación de
  errores.
- Navegador con API real y SQLite temporal: edición y guardado, botón
  deshabilitado sin cambios, cancelación con y sin cambios, validación (vacío,
  espacios, más de 100.000 caracteres) y estados no editables.
- `412` provocado de dos formas: editando el contenido por la API y añadiendo un
  criterio por la API. Se comprueban el aviso correcto, ambas versiones, guardar
  la versión local y descartarla.
- `409` provocado aprobando la especificación por la API durante la edición.
- API detenida durante `PUT`: bloqueo, consulta y recuperación en los dos casos
  (guardado y no guardado).
- Compilación, pruebas, formato y ausencia de cambios en el modelo EF Core y en
  OpenAPI.

## Evidencia de cierre

Pendiente de implementación y verificación.
