# 0017 — Decidir propuestas desde la interfaz web

## Estado

Aprobada para implementación el 14 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Specs relacionadas: `specs/0003-feature-proposal-lifecycle/spec.md`,
  `specs/0009-http-concurrency/spec.md` y
  `specs/0016-first-web-interface/spec.md`.

La API y el dominio conservan los contratos y transiciones aprobados en esas
specs. Este incremento amplía el detalle de propuesta de la 0016, donde las
decisiones quedaban fuera de alcance, y consume dos operaciones existentes.

## Problema

La interfaz permite crear y consultar propuestas, pero una persona debe salir
de ella y llamar directamente a la API para aceptarlas o rechazarlas. El equipo
no puede evaluar en el navegador el primer cambio de estado del flujo funcional
de SpecFlow.

## Objetivo

Permitir que una persona decida una propuesta pendiente desde su detalle web.
La interfaz debe mostrar la decisión resultante, recoger el motivo obligatorio
de rechazo y explicar los errores sin atribuirse reglas de negocio que
pertenecen a la API.

## Alcance

- Acción de aceptar en el detalle de una propuesta `pending`, con un paso de
  confirmación visible antes de enviar la petición.
- Acción de rechazar en ese mismo detalle, con un formulario de motivo y opción
  de cancelarlo sin modificar la propuesta.
- Actualización del detalle con la respuesta de la API tras una decisión
  correcta, incluidas fecha y motivo cuando correspondan.
- Tratamiento de validación del motivo, conflicto por decisión simultánea,
  recurso no encontrado y pérdida de conexión durante el envío.
- Pruebas del cliente HTTP y de los recorridos observables en navegador.
- Actualización del README con las acciones disponibles en la web.

## Fuera de alcance

- Crear o editar una especificación después de aceptar una propuesta.
- Reabrir o cambiar una propuesta ya aceptada o rechazada.
- Decidir propuestas directamente desde el listado del proyecto.
- Crear continuaciones o alterar `sourceProposalId`.
- Añadir estados, reglas de transición o contratos HTTP a la API.
- Notificaciones en tiempo real, sondeo automático o historial de decisiones.

## Casos de uso

### UC-01 — Aceptar una propuesta pendiente

La persona abre el detalle de una propuesta `pending` y elige «Aceptar». La
interfaz presenta una confirmación con opciones de confirmar o cancelar. Solo
la confirmación envía la petición. Si la API la acepta, la vista muestra el
estado `accepted` y la fecha de decisión devueltos, y deja de ofrecer acciones
de decisión.

### UC-02 — Rechazar una propuesta pendiente

La persona elige «Rechazar» y escribe un motivo. Puede cancelar el formulario
sin enviar nada. Al confirmar, la interfaz envía el texto introducido. Si la
API lo rechaza por validación, muestra el error asociado a `reason` y conserva
el texto para corregirlo. Si la operación prospera, muestra `rejected`, la
fecha y el motivo devueltos y oculta las acciones de decisión.

### UC-03 — Resolver una decisión concurrente

Si otra petición decidió la propuesta después de cargar el detalle, la API
responde `409`. La interfaz explica que la propuesta ya puede haber cambiado y
consulta de nuevo su estado. Si la nueva consulta funciona, muestra la decisión
vigente sin reintentar la petición original. Si falla, ofrece una acción manual
para recargar el detalle; hasta entonces no ofrece otro envío de decisión.

### UC-04 — Recuperar un resultado incierto

Si se pierde la conexión o se agota el tiempo durante una decisión, la interfaz
no presupone si la API la aplicó. Conserva el motivo escrito, informa de que
el resultado es incierto y solicita recargar el detalle antes de permitir un
nuevo intento. La recarga utiliza el `GET` existente sin descartar el motivo
mientras la propuesta siga `pending`.

## Comportamiento de la interfaz

- Las acciones aparecen únicamente cuando el detalle cargado tiene estado
  `pending`. En `accepted`, `rejected` o un estado desconocido, la vista es de
  solo lectura.
- La confirmación de aceptación y el formulario de rechazo son opciones
  separadas. Elegir una no envía la otra. Cancelar cualquiera de ellas vuelve
  al detalle sin petición de decisión.
- El motivo de rechazo se presenta como campo obligatorio con ayuda de
  longitud máxima de 1.000 caracteres. La API decide si es válido; la web no
  lo normaliza ni modifica antes de enviarlo.
- Mientras se envía una decisión, se deshabilitan ambos envíos para impedir
  dos peticiones desde la misma vista.
- Después de una respuesta correcta, la representación devuelta sustituye la
  mostrada. Al volver al proyecto, el listado obtiene el estado vigente
  mediante su consulta habitual.
- Un `400` con `errors.reason` muestra un mensaje junto al campo. Un `404`
  mantiene el aislamiento contextual y ofrece volver al listado de proyectos.
  Un `409` sigue el caso de uso UC-03. Otros errores se muestran sin JSON bruto
  ni detalles internos.
- La recarga manual consulta de nuevo la propuesta dentro de la misma vista.
  Si permanece `pending`, permite un nuevo intento y conserva el motivo de
  rechazo escrito; si ya está decidida, muestra su estado final.
- Los textos nuevos de la interfaz están en español, como en la 0016.

## Consumo del contrato HTTP existente

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Aceptar | `POST /api/projects/{projectId}/proposals/{proposalId}/accept`, sin body | `200` con `FeatureProposalResponse` | `400`, `404`, `409` |
| Rechazar | `POST /api/projects/{projectId}/proposals/{proposalId}/reject`, JSON `{"reason":"..."}` | `200` con `FeatureProposalResponse` | `400` bajo `reason`, `404`, `409` |
| Refrescar tras error | `GET /api/projects/{projectId}/proposals/{proposalId}` | `200` con estado actual | `400`, `404` |

La spec 0009 excluyó expresamente los ETags de propuestas: son decisiones
terminales y la API protege decisiones simultáneas con `409`. La web no envía
`If-Match` en estas dos operaciones ni interpreta `409` como `412`. Esta spec
no modifica la excepción existente ni el contrato de concurrencia de otros
recursos.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. Una propuesta `pending` muestra «Aceptar» y «Rechazar» en su detalle; una
   propuesta `accepted`, `rejected` o de estado desconocido no muestra acciones
   de decisión.
2. Pulsar «Aceptar» muestra una confirmación; cancelarla no envía un `POST` ni
   cambia la propuesta.
3. Confirmar la aceptación envía una sola petición sin body y, con respuesta
   `200`, muestra `accepted` y la fecha devuelta sin recargar toda la página.
4. Abrir y cancelar el formulario de rechazo no envía un `POST` ni cambia la
   propuesta.
5. Confirmar el rechazo envía el motivo escrito sin normalización del cliente;
   con respuesta `200`, muestra `rejected`, la fecha y el motivo devueltos.
6. Un motivo nulo, vacío, compuesto por espacios o demasiado largo que la API
   rechaza muestra el error de `reason` y conserva el texto introducido.
7. Durante un envío no puede iniciarse una segunda decisión desde la misma
   vista.
8. Ante `409`, la web no repite el `POST`, consulta la propuesta y muestra su
   decisión actual. Si esa consulta falla, ofrece recarga manual y bloquea
   nuevos envíos hasta conocer el estado.
9. Ante pérdida de conexión o tiempo agotado durante un `POST`, la web no
   reenvía automáticamente, conserva el motivo y pide recargar antes de volver
   a decidir.
10. Un proyecto o propuesta inexistente, o una propuesta perteneciente a otro
    proyecto, no muestra datos de otro contexto y ofrece una salida clara.
11. Tras decidir y volver al proyecto, el listado muestra el estado vigente
    obtenido de la API.
12. La implementación no cambia el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Mantener las decisiones en el detalle evita introducir controles de estado
  en el listado y permite mostrar el contexto y la consecuencia de la acción.
- Tras `409` o un fallo de red, leer el recurso antes de otro intento evita
  presentar como pendiente una decisión cuyo resultado ya pudo cambiar.

## Plan de verificación

- Pruebas del cliente HTTP para ruta y ausencia de body en aceptación, JSON
  exacto del motivo en rechazo, respuesta actualizada y clasificación de
  `400`, `404`, `409` y fallos de conexión.
- Comprobación en navegador de aceptación, rechazo, cancelación, errores de
  motivo, estados finales y actualización del listado usando la API real.
- Comprobación de conflicto desde dos vistas abiertas o mediante una decisión
  externa entre lectura y envío; verificar que no hay reintento automático.
- Compilación, pruebas y formato de la solución. Confirmar ausencia de cambios
  pendientes del modelo EF Core y del contrato OpenAPI.

## Evidencia de cierre

Pendiente de implementación y verificación.
