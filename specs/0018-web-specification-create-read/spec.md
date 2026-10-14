# 0018 — Crear y consultar especificaciones desde la interfaz web

## Estado

Aprobada para implementación el 14 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Specs relacionadas: `specs/0004-specifications/spec.md`,
  `specs/0009-http-concurrency/spec.md`,
  `specs/0013-specification-lifecycle/spec.md`,
  `specs/0016-first-web-interface/spec.md` y
  `specs/0017-web-proposal-decisions/spec.md`.

Este incremento amplía la web para consumir la creación y consulta de
especificaciones ya disponibles en la API. No cambia sus contratos ni las reglas
de dominio. La creación continúa siendo explícita después de aceptar una
propuesta.

## Problema

El equipo ya puede aceptar una propuesta desde la web, pero debe abandonar el
navegador y construir una petición HTTP para crear o leer la especificación que
describe el trabajo siguiente. El flujo visible se detiene justo después de la
decisión sobre la propuesta.

## Objetivo

Permitir que una persona continúe desde una propuesta aceptada hasta crear y
consultar su única especificación en Markdown. La interfaz debe distinguir la
ausencia de especificación de un error de contexto y conservar el texto escrito
cuando el resultado de una creación sea incierto.

## Alcance

- Acceso a la especificación desde el detalle de una propuesta `accepted`.
- Vista singular `/projects/{projectId}/proposals/{proposalId}/specification`.
- Estado vacío con formulario de creación cuando la propuesta aceptada aún no
  tiene especificación.
- Consulta de una especificación existente y presentación de su contenido
  Markdown como texto, estado y fechas devueltos por la API.
- Manejo de validación de `content`, creación concurrente, recurso no
  encontrado y pérdida de conexión durante la creación.
- Pruebas del cliente HTTP y de los recorridos observables en navegador.
- Actualización del README con el recorrido disponible en la web.

## Fuera de alcance

- Editar el contenido de una especificación existente. La edición con ETag se
  abordará en otra spec para definir la resolución de cambios concurrentes sin
  pérdida de texto.
- Aprobar, marcar implementada o verificar una especificación.
- Crear, editar o consultar criterios, tareas, evaluaciones y trazabilidad desde
  la web.
- Vista previa HTML, conversión o ejecución del Markdown.
- Plantillas, generación asistida, carga de archivos o guardado automático.
- Crear especificaciones para propuestas pendientes o rechazadas.

## Casos de uso

### UC-01 — Abrir la especificación desde una propuesta aceptada

La persona abre una propuesta `accepted` y sigue el enlace «Especificación».
La web consulta el proyecto y la propuesta de la ruta y después la
especificación. Si existe, presenta su contenido y estado. Si la API confirma
que la propuesta existe pero no tiene especificación, presenta el formulario de
creación. Una propuesta pendiente, rechazada o de estado desconocido no ofrece
esta acción; acceder directamente a la ruta no ofrece creación.

### UC-02 — Crear la especificación

La persona escribe Markdown en un campo multilínea y pulsa «Crear
especificación». La web envía el contenido exacto sin recortarlo ni cambiar los
saltos de línea. Con `201`, muestra la representación devuelta, incluidas fechas
y estado `draft`, sin requerir una recarga completa de la página. Una respuesta
`400` asociada a `content` muestra el error junto al campo y mantiene el texto.

### UC-03 — Recuperar una creación concurrente o incierta

Si `POST` responde `409` porque otra petición creó la especificación, la web
consulta de nuevo la ruta singular y muestra el recurso existente. Conserva el
texto que no pudo enviar hasta que la persona abandone la vista, para que pueda
copiarlo si lo necesita. Si la lectura no encuentra la especificación o falla,
muestra el conflicto y ofrece volver a consultar el contexto; no habilita otro
`POST` basándose solo en ese `409`. No repite automáticamente el `POST`.

Si se pierde la conexión o se agota el tiempo durante `POST`, la web informa de
que el resultado es incierto y bloquea un segundo envío. La persona puede
consultar manualmente el estado: si `GET` devuelve la especificación, se muestra
la creada; si confirma que aún no existe, se vuelve a habilitar el formulario
con el texto intacto. Un fallo de esa consulta mantiene el bloqueo y ofrece
reintentar la lectura.

## Comportamiento de la interfaz

- La ruta de especificación conserva `projectId` y `proposalId` y permite volver
  al detalle de la propuesta.
- La vista comprueba el contexto antes de interpretar un `404` de la consulta
  singular como «Todavía no hay especificación». Un proyecto o propuesta
  inexistente, o una propuesta de otro proyecto, muestra el error contextual y
  una salida clara sin presentar datos ajenos. Un fallo de red al cargar la
  vista ofrece reintentar la lectura y no se presenta como estado vacío.
- Solo `accepted` habilita la creación. Una especificación existente siempre se
  presenta en modo de lectura, incluido `draft`; un estado desconocido también
  se presenta sin acciones.
- El contenido se muestra como texto literal con saltos de línea preservados y
  sin interpretarlo como HTML. Se muestran `createdAtUtc`, `updatedAtUtc` y las
  fechas de transición que no sean nulas. El formulario indica que el contenido es
  obligatorio y admite como máximo 100.000 caracteres. La API decide la
  validez; la web no transforma el texto antes de enviarlo.
- Durante el envío se impide una segunda creación desde la misma vista.
- Si aparece una especificación tras un `409` o una consulta de recuperación,
  se muestra la versión de la API y el texto local no enviado permanece
  accesible en un bloque separado hasta salir de la vista. No se presenta como
  contenido guardado.
- Los errores se muestran en español y sin JSON bruto ni detalles internos.

## Consumo del contrato HTTP existente

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Consultar | `GET /api/projects/{projectId}/proposals/{proposalId}/specification` | `200` con `SpecificationResponse` y `ETag` | `400`, `404` |
| Crear | `POST /api/projects/{projectId}/proposals/{proposalId}/specification`, JSON `{"content":"..."}` | `201` con `SpecificationResponse`, `Location` y `ETag` | `400` bajo `content`, `404`, `409` |

El cliente web no necesita enviar `If-Match` en estas dos operaciones. La API
devuelve un ETag para la especificación existente; su uso en `PUT` queda fuera
de este incremento. No se modifica la concurrencia definida en la spec 0009.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. Una propuesta `accepted` muestra acceso a su especificación; las propuestas
   `pending`, `rejected` o de estado desconocido no lo muestran.
2. La ruta web de una propuesta aceptada sin especificación muestra un estado
   vacío y un formulario de creación; no confunde ese `404` con un proyecto o
   propuesta inexistente ni con un fallo de conexión.
3. Crear con contenido válido envía una sola petición con el texto exacto y,
   con `201`, muestra el contenido, `draft` y las fechas devueltas sin recargar
   toda la página.
4. Contenido nulo, vacío, compuesto solo por espacios o superior a 100.000
   caracteres que la API rechaza muestra un error junto al campo y conserva la
   entrada.
5. Una especificación existente muestra su contenido literal, estado y fechas;
   no muestra un segundo formulario de creación ni controles de edición.
6. Una ruta directa de especificación bajo una propuesta no aceptada no permite
   crearla y ofrece volver al detalle de la propuesta.
7. Durante un envío no puede iniciarse una segunda creación desde la misma
   vista.
8. Ante `409`, la web no repite el `POST`, consulta el recurso y muestra la
   especificación existente si la lectura prospera; mantiene accesible el texto
   local no guardado. Si la lectura falla, no habilita otra creación hasta
   consultar de nuevo el contexto.
9. Ante pérdida de conexión o tiempo agotado durante `POST`, la web no reenvía
   automáticamente y exige consultar el estado antes de otro intento, sin
   descartar el contenido escrito.
10. Un proyecto o propuesta inexistente, o una propuesta ajena al proyecto, no
    muestra datos de otro contexto y ofrece una salida clara.
11. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Incluir creación y consulta completa el siguiente paso visible del flujo tras
  aceptar una propuesta, manteniendo la creación explícita de la spec 0004.
- Reservar la edición para un incremento propio permite tratar el `412` y la
  comparación de versiones con un diseño revisable; una creación no requiere
  `If-Match`.
- Presentar Markdown como texto preserva el contrato actual de contenido opaco
  y evita introducir un renderizador en esta etapa.

## Plan de verificación

- Pruebas del cliente HTTP para rutas, JSON exacto, deserialización de la
  especificación y clasificación de `400`, `404`, `409` y fallos de conexión.
- Prueba en navegador con API real: propuesta aceptada sin especificación,
  validación, creación, consulta posterior y estados de solo lectura.
- Simular creación concurrente y pérdida de conexión durante el `POST`;
  comprobar ausencia de reenvío automático y conservación del texto local.
- Comprobar aislamiento con un `projectId` ajeno y acceso directo desde una
  propuesta no aceptada.
- Compilación, pruebas y formato de la solución. Confirmar ausencia de cambios
  pendientes en el modelo EF Core y en OpenAPI.

## Evidencia de cierre

Pendiente de implementación y verificación.
