# 0023 — Gestionar criterios de aceptación desde la interfaz web

## Estado

Implementada y verificada el 15 de octubre de 2026, tras su aprobación.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0005-acceptance-criteria/spec.md`,
  `specs/0009-http-concurrency/spec.md`,
  `specs/0012-acceptance-criterion-task-traceability/spec.md`,
  `specs/0013-specification-lifecycle/spec.md`,
  `specs/0021-web-specification-edit/spec.md` y
  `specs/0022-web-form-field-errors/spec.md`.

Este incremento amplía la web para consumir la gestión de criterios ya
disponible en la API. No cambia contratos HTTP, dominio ni persistencia.

## Problema

Una especificación necesita criterios de aceptación antes de aprobarse, pero la
web solo muestra su contenido. Añadir, corregir, eliminar u ordenar criterios
obliga a usar la API directamente, con dos tipos de ETag distintos: el del
criterio para editarlo o eliminarlo y el del listado para reordenarlo.

Además, cualquier cambio en los criterios cambia el ETag de la especificación,
que representa el plan completo. Si la vista no lo tiene en cuenta, la edición
de contenido de la 0021 respondería `412` tras añadir un criterio desde la
misma pantalla.

## Objetivo

Desde la vista de una especificación en `draft`, la persona puede consultar,
añadir, editar, eliminar y reordenar sus criterios. Cada modificación envía el
ETag que corresponde y los conflictos se resuelven de forma explícita, sin
perder texto escrito ni sobrescribir trabajo ajeno en silencio.

## Alcance

- Sección «Criterios de aceptación» en la vista de especificación existente,
  con el listado en orden en cualquier estado.
- En `draft`: añadir, editar, eliminar con confirmación en la página y mover un
  criterio una posición arriba o abajo.
- Ampliación del cliente HTTP web con las operaciones de criterios, captura de
  ETags de recurso y de listado, y envío de `If-Match`.
- Coordinación con la edición de contenido de la 0021: una sola edición activa
  y relectura de la especificación tras cada cambio en los criterios.
- Actualización del README con el recorrido disponible.

## Fuera de alcance

- Tareas de implementación, vínculos entre criterios y tareas, y verificación
  de criterios. Tienen sus propios incrementos.
- Aprobar o cambiar el estado de la especificación desde la web.
- Reordenar arrastrando elementos, que requiere JavaScript propio.
- Editar o reordenar varios criterios en una sola operación.
- Mostrar qué tareas están vinculadas a un criterio.
- Cambios en la API, OpenAPI, dominio o esquema.

## Casos de uso

### UC-01 — Consultar criterios

La vista de especificación muestra, debajo del contenido, los criterios
numerados en su orden. Sin criterios, muestra un mensaje que indica que se
necesitan antes de aprobar. Si el listado no puede cargarse, el contenido de la
especificación sigue visible y la sección ofrece reintentar.

### UC-02 — Añadir un criterio

En `draft`, un formulario al final de la sección envía `POST` con el texto
exacto. Con `201`, el criterio aparece al final, el campo se vacía y la vista
relee la especificación y el listado. Un `400` bajo `content` (vacío, solo
espacios o más de 10.000 caracteres) se muestra junto al campo con la utilidad
de la 0022 y conserva el texto.

### UC-03 — Editar un criterio

«Editar» en un criterio consulta ese criterio para obtener su ETag de recurso y
muestra su texto en un campo. «Guardar» envía `PUT` con `If-Match` y está
deshabilitado si el texto no cambió. «Cancelar» con cambios pide confirmación en
la página.

### UC-04 — Eliminar un criterio

«Eliminar» pide confirmación en la página e indica que se perderán sus vínculos
con tareas, si los tuviera. Al confirmar, consulta el criterio y envía `DELETE`
con su ETag. Con `204`, desaparece del listado y la vista relee especificación
y listado.

### UC-05 — Reordenar

Cada criterio ofrece «Subir» y «Bajar», deshabilitados en el primero y el
último. Al pulsar, la web envía `PUT .../order` con el orden completo resultante
y el ETag del listado mostrado. Con `204`, adopta el ETag devuelto y relee la
especificación.

### UC-06 — Conflictos y errores

- `412` al editar, eliminar o reordenar: la web no reintenta. Relee la
  especificación y el listado, informa de que los criterios cambiaron y, si se
  estaba editando, conserva el texto local junto a la versión guardada para que
  la persona decida guardarlo de nuevo o descartarlo.
- `409`: la web relee la especificación. Si ya no está en `draft`, la sección
  pasa a solo lectura y el texto local sigue visible. Si sigue en `draft`, el
  `409` de añadir o editar se presenta como criterio duplicado junto al campo, y
  el de reordenar como listado cambiado, con el listado actual.
- `404` del criterio al editar o eliminar: informa de que ya no existe y relee
  el listado. `404` de la especificación o su contexto: error contextual como en
  la 0021.
- Pérdida de conexión: no se reenvía. Se bloquean nuevos cambios en criterios y
  se ofrece «Consultar estado actual», que relee especificación y listado. Si un
  texto enviado aparece ya guardado, se confirma; si no, se conserva para que la
  persona decida.
- `428` o `400` sin campo `content`: error genérico, se conserva el texto y se
  exige consultar el estado antes de otro intento.

## Comportamiento de la interfaz

- Solo puede haber una edición activa en la vista: el contenido de la
  especificación o un criterio. Mientras una está abierta o se envía una
  petición, las demás acciones de modificación están deshabilitadas.
- Tras cualquier cambio correcto en los criterios, la vista relee la
  especificación para conservar su ETag vigente. Así la edición de contenido de
  la 0021 no responde `412` por un cambio hecho desde la propia vista.
- Los ETags se tratan como valores opacos, igual que en la 0021.
- La web no transforma el texto de los criterios.
- Los estados distintos de `draft` muestran los criterios en modo de lectura.
- Los mensajes están en español y sin JSON bruto, ETags ni detalles internos.
- Los campos con validación aplican `aria-invalid` y `aria-describedby` con la
  utilidad de la 0022.

## Consumo del contrato HTTP existente

`...` corresponde a
`/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`.

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Listar | `GET ...` | `200`, lista y ETag de listado | `400`, `404` |
| Añadir | `POST ...` con `{"content":"..."}` | `201` y criterio | `400` bajo `content`, `404`, `409` |
| Consultar | `GET .../{criterionId}` | `200`, criterio y ETag de recurso | `404` |
| Editar | `PUT .../{criterionId}` con `If-Match` | `200`, criterio y ETag | `400`, `404`, `409`, `412`, `428` |
| Eliminar | `DELETE .../{criterionId}` con `If-Match` | `204` | `404`, `409`, `412`, `428` |
| Reordenar | `PUT .../order` con `{"criterionIds":[...]}` e `If-Match` de listado | `204` y ETag de listado | `400`, `404`, `409`, `412`, `428` |

La web no interpreta los títulos de Problem Details para distinguir los `409`.
Los clasifica releyendo el estado de la especificación, como se describe en
UC-06.

## Cambios en la web

- Nuevo `AcceptanceCriterionDto` en los modelos del cliente.
- Nuevos métodos en `SpecFlowApiClient`: listar, añadir, consultar, editar,
  eliminar y reordenar criterios. Los que modifican un recurso existente
  reciben el ETag y lo envían en `If-Match`.
- `SendAsync` admite respuestas `204` sin cuerpo y conserva su ETag.

Son cambios internos de `SpecFlow.Web`; no afectan a la API.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. La vista de especificación lista los criterios en orden en cualquier estado.
   Sin criterios muestra el aviso correspondiente. Si el listado falla, el
   contenido sigue visible y se puede reintentar la carga.
2. En `draft`, añadir un criterio envía un único `POST` con el texto exacto y lo
   muestra al final del listado. Los estados distintos de `draft` no muestran
   controles de modificación.
3. Editar envía `PUT` con el ETag de recurso del criterio. «Guardar» está
   deshabilitado sin cambios y «Cancelar» con cambios pide confirmación en la
   página.
4. Eliminar pide confirmación en la página, envía `DELETE` con el ETag de
   recurso y retira el criterio del listado.
5. «Subir» y «Bajar» envían el orden completo con el ETag del listado mostrado,
   y están deshabilitados en los extremos.
6. Un `400` bajo `content` y un `409` por duplicado se muestran junto al campo,
   con `aria-invalid="true"`, y conservan el texto.
7. Ante `412`, la web no reintenta, relee el estado y conserva el texto local
   junto a la versión guardada hasta que la persona decide.
8. Ante `409` con la especificación fuera de `draft`, la sección queda en solo
   lectura y el texto local sigue visible.
9. Ante pérdida de conexión, la web no reenvía y exige consultar el estado
   antes de permitir otro cambio.
10. Solo hay una edición activa en la vista. Tras añadir, editar, eliminar o
    reordenar criterios, editar el contenido de la especificación funciona sin
    `412`.
11. Las pruebas del cliente HTTP cubren rutas, cuerpos, envío literal de
    `If-Match`, lectura de ETags de recurso y de listado, respuestas `204` y la
    clasificación de `400`, `404`, `409`, `412`, `428` y fallos de conexión.
12. Los recorridos de las specs 0018, 0021 y 0022 no cambian.
13. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Se separan criterios y tareas en incrementos distintos para mantener el
  cambio vertical y verificable. Las tareas tienen además ciclo de vida y
  vínculos, que justifican su propia spec.
- Se reordena con botones «Subir» y «Bajar» en lugar de arrastrar, porque no
  requiere JavaScript propio y es accesible desde el teclado.
- Los `409` se clasifican releyendo la especificación y no por el título del
  Problem Details, para no acoplar la web a textos de error.
- La edición y la eliminación consultan el criterio justo antes para obtener su
  ETag de recurso, porque el listado solo expone el ETag de la colección.
- Se mantiene la decisión de la 0021 de no añadir bUnit. Los recorridos de
  interfaz se verifican en navegador y quedan en la evidencia.

## Plan de verificación

- Pruebas del cliente HTTP para cada operación de criterios.
- Navegador con API real y SQLite temporal: listado vacío y con criterios,
  añadir, validación (vacío, espacios, más de 10.000 caracteres), duplicado,
  editar, cancelar con y sin cambios, eliminar, subir y bajar con extremos
  deshabilitados, y modo de lectura en estado `approved`.
- `412` provocado editando, eliminando y reordenando por la API durante la
  edición en la web.
- `409` provocado aprobando la especificación por la API.
- API detenida durante un envío: bloqueo y recuperación.
- Edición del contenido de la especificación tras cambiar criterios, sin `412`.
- Compilación, pruebas, formato y ausencia de cambios en el modelo EF Core y en
  OpenAPI.

## Evidencia de cierre

- `SpecFlowApiClient` añade listar, añadir, consultar, editar, eliminar y
  reordenar criterios, con `If-Match` literal en las modificaciones y lectura
  del ETag en respuestas `204`. La sección vive en el componente
  `AcceptanceCriteriaSection`, que la vista de especificación integra con una
  sola edición activa y relectura de la especificación tras cada cambio. No
  cambian la API, OpenAPI, el dominio ni el esquema.
- Siete pruebas nuevas del cliente HTTP: rutas, cuerpos con texto exacto,
  `If-Match` literal, ETags de recurso y de listado, `204` sin cuerpo,
  clasificación de `400` (`content` y `criterionIds`), `404`, `409`, `412` y
  `428`, y fallos de conexión de cada operación.
- Navegador con API real y SQLite temporal:
  - Listado vacío con el aviso de aprobación. Añadir tres criterios; la API
    conservó espacios y saltos de línea exactos.
  - Solo espacios y 10.001 caracteres: error junto al campo con
    `aria-invalid="true"` y `aria-describedby` hacia ayuda y error. Duplicado al
    añadir y al editar: mensaje de duplicado junto al campo y texto conservado.
  - Editar: «Guardar» deshabilitado sin cambios, «Cancelar» con cambios pide
    confirmación y «Seguir editando» conserva el texto. Durante la edición, el
    resto de acciones, el formulario de alta y «Editar» del contenido quedan
    deshabilitados. Con texto en el campo de alta, «Editar» del contenido
    también.
  - «Subir» y «Bajar» deshabilitados en los extremos. Bajar el primero, subir el
    tercero y eliminar el primero dejaron el orden esperado en la API.
  - Editar el contenido de la especificación tras cinco cambios de criterios
    guardó a la primera; el registro de la API no contiene ningún `412`.
  - `412` al editar por un cambio hecho por la API: versión guardada y texto
    local por separado; «Guardar mi versión» guardó y «Descartar mi versión»
    volvió al listado.
  - Eliminar un criterio cambiado por la API tras abrir la confirmación: no se
    eliminó y se mostró el aviso con el listado actual.
  - Reordenar con un criterio añadido por la API: `412`, orden sin aplicar y
    listado actualizado con el criterio nuevo.
  - Editar un criterio eliminado por la API: aviso de que ya no existe y
    listado actual.
  - Aprobación por la API durante la edición de un criterio: `409`, estado
    «Aprobada» en la vista, sección en modo de lectura y texto local visible.
    Al cargar una especificación aprobada no hay controles de modificación.
  - API detenida al añadir: bloqueo con el texto visible y deshabilitado; la
    consulta sin API mantuvo el bloqueo. Tras reiniciar: «no se añadió» con el
    texto en el campo, y «se añadió correctamente» cuando la API ya tenía ese
    texto.
- Ajustes durante la verificación: «Descartar» limpia el aviso de conflicto y
  el formulario de alta permanece visible y deshabilitado mientras hay una
  consulta pendiente.
- No se verificó en navegador el `404` de la especificación durante un cambio,
  porque la API no permite eliminar propuestas ni especificaciones; esa rama
  entrega el texto local al error contextual de la vista (revisión de código).
  Tampoco se comprobó el diseño a ancho de móvil.
- `dotnet build SpecFlow.slnx --no-restore`: correcto, 0 warnings.
- `dotnet test SpecFlow.slnx --no-build`: 501 pruebas correctas (127 de dominio,
  339 de integración y 35 web).
- `dotnet format SpecFlow.slnx --verify-no-changes --no-restore` y
  `git diff --check`: correctos.
- `dotnet ef migrations has-pending-model-changes`: sin cambios del modelo.
