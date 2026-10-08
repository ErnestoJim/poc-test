# 0011 — Ciclo de vida de decisiones técnicas

## Estado

Aprobada para implementación el 8 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Spec ampliada: `specs/0010-technical-decisions/spec.md`.

Las reglas transversales de arquitectura, HTTP, concurrencia, persistencia,
pruebas y calidad se heredan de la constitución.

## Problema

Las decisiones técnicas se pueden crear y editar, pero el sistema no distingue
un borrador de una decisión adoptada o descartada. Tampoco permite registrar que
una decisión aceptada fue sustituida posteriormente por otra.

Sin este ciclo de vida, una edición puede reescribir el razonamiento histórico y
no existe una forma explícita de saber qué decisiones siguen vigentes.

## Objetivo

Incorporar un ciclo de vida irreversible para que una decisión comience como
borrador, pueda aceptarse o rechazarse y, si fue aceptada, pueda quedar
supersedida por otra decisión aceptada del mismo proyecto.

Solo los borradores seguirán siendo editables y eliminables. Las decisiones que
ya forman parte del registro histórico permanecerán inmutables.

## Alcance

- Estados `Draft`, `Accepted`, `Rejected` y `Superseded`.
- Creación de decisiones nuevas en estado `Draft`.
- Migración de las decisiones existentes a `Draft` para preservar su capacidad
  actual de edición.
- Aceptación y rechazo de borradores mediante transiciones explícitas.
- Motivo obligatorio al rechazar.
- Supersesión de una decisión aceptada por otra decisión aceptada y posterior en
  el orden de adopción del mismo proyecto.
- Edición y eliminación física limitadas a borradores.
- ETags para editar, transicionar y eliminar.
- Extensión de las respuestas y de OpenAPI con el estado y sus metadatos.
- Migración de EF Core y pruebas de actualización de datos existentes.

## Fuera de alcance

- Reabrir, restaurar o deshacer una transición.
- Editar decisiones aceptadas, rechazadas o supersedidas.
- Eliminar decisiones que no sean borradores.
- Aprobaciones por varias personas, permisos o identidad del decisor.
- Historial de todas las transiciones o versiones anteriores del contenido.
- Supersesión entre proyectos.
- Sustituir varias decisiones mediante una única operación.
- Estados configurables o un motor genérico de workflows.

## Casos de uso

### UC-01 — Crear y editar un borrador

Una decisión nueva comienza como `draft`. Puede editarse mientras permanezca en
ese estado y conserva el contrato de concurrencia de la spec 0010.

### UC-02 — Aceptar una decisión

Un consumidor acepta un borrador indicando su ETag vigente. La decisión pasa a
`accepted`, registra el instante de decisión y deja de ser editable.

### UC-03 — Rechazar una decisión

Un consumidor rechaza un borrador indicando su ETag vigente y un motivo. La
decisión pasa a `rejected`, registra el instante y conserva el motivo normalizado.

### UC-04 — Marcar una decisión aceptada como supersedida

Un consumidor indica que una decisión aceptada fue sustituida por otra decisión
aceptada, adoptada posteriormente y perteneciente al mismo proyecto. La decisión
anterior pasa a `superseded`; la sustituta no se modifica.

### UC-05 — Eliminar un borrador

Un consumidor elimina permanentemente un borrador con su ETag vigente. Una
decisión aceptada, rechazada o supersedida no puede eliminarse.

## Cambios de dominio

| Campo o concepto | Cambio | Regla específica |
|---|---|---|
| `Status` | Nuevo | `Draft`, `Accepted`, `Rejected` o `Superseded` |
| `DecidedAtUtc` | Nuevo, nullable | Instante de aceptación o rechazo |
| `RejectionReason` | Nuevo, nullable | Solo existe para una decisión rechazada |
| `SupersededAtUtc` | Nuevo, nullable | Instante en que una aceptada queda supersedida |
| `SupersededByDecisionId` | Nuevo, nullable | Decisión aceptada que la sustituye |

Las respuestas de creación, consulta, listado, edición y transición añaden:

```json
{
  "status": "draft",
  "decidedAtUtc": null,
  "rejectionReason": null,
  "supersededAtUtc": null,
  "supersededByDecisionId": null
}
```

Los valores HTTP del estado son `draft`, `accepted`, `rejected` y `superseded`.

### Reglas específicas

1. Una decisión nueva comienza en `Draft` con todos los metadatos de ciclo de
   vida a `null`.
2. Las decisiones existentes se migran a `Draft` sin cambiar título, contenido o
   fechas. Su versión se incrementa una vez para invalidar ETags anteriores al
   cambio de representación.
3. Solo `Draft` permite editar, aceptar, rechazar o eliminar.
4. `Accepted` permite únicamente la transición a `Superseded`.
5. `Rejected` y `Superseded` son estados terminales.
6. Aceptar o rechazar establece `DecidedAtUtc`, actualiza `UpdatedAtUtc` e
   incrementa la versión.
7. El motivo de rechazo se recorta exteriormente, debe contener entre 1 y 1.000
   caracteres y solo se almacena en estado `Rejected`.
8. La supersesión establece `SupersededAtUtc`, `SupersededByDecisionId`,
   `UpdatedAtUtc` y una nueva versión, sin cambiar `DecidedAtUtc`.
9. La sustituta debe pertenecer al mismo proyecto, estar en `Accepted`, ser
   distinta y aparecer después de la sustituida en el orden de adopción definido
   por `(DecidedAtUtc, Id)`. El identificador actúa como desempate determinista
   cuando dos aceptaciones comparten milisegundo. Esta regla impide ciclos de
   supersesión.
10. La sustituta puede reemplazar decisiones aceptadas adicionales mediante
    peticiones independientes.
11. Toda transición inválida conserva el estado anterior y responde `409`.

## Cambios del contrato HTTP

La ruta base continúa siendo:

`/api/projects/{projectId}/technical-decisions`

| Método | Ruta relativa | Resultado correcto | Error propio |
|---|---|---|---|
| `PUT` | `/{decisionId}` | `200` si sigue en `draft` | `409 Technical decision is not editable` |
| `POST` | `/{decisionId}/accept` | `200` con estado `accepted` | `409 Technical decision transition conflict` |
| `POST` | `/{decisionId}/reject` | `200` con estado `rejected` | `409 Technical decision transition conflict` |
| `POST` | `/{decisionId}/supersede` | `200` con estado `superseded` | `409 Technical decision transition conflict` |
| `DELETE` | `/{decisionId}` | `204` si era `draft` | `409 Technical decision cannot be deleted` |

Todos los endpoints de la tabla exigen el ETag individual vigente mediante
`If-Match`. Los errores de precondición se heredan de la constitución.

Rechazar recibe:

```json
{
  "reason": "The operational cost is too high."
}
```

Marcar como supersedida recibe:

```json
{
  "replacementDecisionId": "3aeb9719-28a4-42d3-b433-428211e064f2"
}
```

Un identificador de sustituta desconocido o perteneciente a otro proyecto
responde `404` con título `Replacement technical decision not found`. Un UUID
vacío responde `400` bajo la clave `replacementDecisionId`.

## Compatibilidad con la spec 0010

- `POST` continúa creando el mismo recurso y añade los campos de ciclo de vida.
- `GET` y el listado conservan ruta y orden y añaden los nuevos campos.
- `PUT` conserva request y respuesta, pero queda restringido a borradores.
- Las decisiones existentes permanecen editables porque se migran a `Draft`.
- Se añaden tres transiciones y la eliminación condicionada de borradores.
- No se elimina ni renombra ninguna propiedad existente.

## Impacto en persistencia

Nueva migración: `AddTechnicalDecisionLifecycle`.

- Añade `Status`, `DecidedAtUtc`, `RejectionReason`, `SupersededAtUtc` y
  `SupersededByDecisionId` a `TechnicalDecisions`.
- Crea una foreign key autorreferenciada restrictiva para
  `SupersededByDecisionId`.
- Migra filas existentes a `Draft` e incrementa su `Version` una vez.
- Conserva identificadores, títulos, contenido y fechas existentes.

## Criterios de aceptación

1. Una decisión nueva se devuelve como `draft` y sus cuatro metadatos de ciclo de
   vida son `null`.
2. Una migración desde 0010 conserva los datos, asigna `Draft` e incrementa la
   versión de las decisiones existentes.
3. Un borrador continúa siendo editable con el ETag vigente.
4. Editar una decisión que no está en `draft` devuelve 409 y no la modifica.
5. Aceptar un borrador registra `decidedAtUtc`, actualiza el ETag y devuelve
   `accepted`.
6. Rechazar un borrador con motivo válido registra el motivo normalizado, el
   instante y un nuevo ETag.
7. Un motivo ausente, en blanco o mayor de 1.000 caracteres devuelve 400.
8. Aceptar o rechazar una decisión que no está en `draft` devuelve 409.
9. Una decisión aceptada puede ser supersedida por otra aceptada, posterior en el
   orden de adopción y del mismo proyecto.
10. La supersesión conserva `decidedAtUtc`, registra sus metadatos y
    no modifica la sustituta.
11. Una sustituta inexistente o perteneciente a otro proyecto devuelve 404 sin
    revelar su existencia.
12. Una sustituta igual, no aceptada o no posterior devuelve 409 sin cambios.
13. Las reglas de orden impiden crear ciclos de supersesión, incluso mediante
    peticiones concurrentes.
14. Eliminar un borrador con su ETag vigente devuelve 204 y lo elimina
    permanentemente.
15. Eliminar una decisión que no está en `draft` devuelve 409 y la conserva.
16. Editar, transicionar o eliminar sin ETag, con uno no admitido u obsoleto
    conserva el recurso y devuelve el error constitucional correspondiente.
17. Dos transiciones concurrentes basadas en el mismo ETag no pueden completarse
    ambas.
18. Consultas y listados incluyen los nuevos campos sin cambiar el orden ni el
    aislamiento entre proyectos.
19. OpenAPI documenta las cuatro operaciones existentes, las cuatro nuevas, los
    estados, requests, ETags y respuestas.
20. El estado, motivos, relaciones y fechas permanecen disponibles entre scopes
    y reinicios.

## Decisiones

### Acordadas

- Las decisiones existentes se migran a `Draft` para conservar su capacidad de
  edición; su versión se incrementa para invalidar ETags anteriores.
- El ciclo utiliza `Draft`, `Accepted`, `Rejected` y `Superseded`, sin
  transiciones de reapertura.
- Solo los borradores pueden editarse o eliminarse.
- Rechazar exige un motivo normalizado de hasta 1.000 caracteres.
- La decisión anterior mantiene `SupersededByDecisionId` hacia una sustituta
  aceptada, posterior según `(DecidedAtUtc, Id)` y del mismo proyecto.
- Una decisión aceptada puede reemplazar varias decisiones anteriores mediante
  operaciones independientes.

## Plan de verificación

- Pruebas de dominio para estados, transiciones, timestamps, rechazo,
  supersesión e inmutabilidad.
- Pruebas HTTP para los contratos nuevos, Problem Details, aislamiento y ETags.
- Pruebas de carreras entre edición, transiciones, supersesión y eliminación.
- Prueba de migración desde `AddTechnicalDecisions`.
- Pruebas de persistencia, foreign key autorreferenciada y OpenAPI.

## Evidencia de cierre

Pendiente de implementación.
