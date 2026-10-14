# 0015 — Continuaciones de especificaciones verificadas

## Estado

Implementada y verificada el 14 de octubre de 2026, tras su aprobación.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Specs ampliadas: `specs/0002-feature-proposals/spec.md`,
  `specs/0003-feature-proposal-lifecycle/spec.md`,
  `specs/0004-specifications/spec.md`,
  `specs/0013-specification-lifecycle/spec.md` y
  `specs/0014-acceptance-criterion-verification/spec.md`.

Las reglas transversales de arquitectura, HTTP, concurrencia, persistencia,
pruebas y calidad se heredan de la constitución.

## Problema

Una especificación verificada queda cerrada. Si aparece un nuevo requisito, una
corrección o una ampliación, se puede crear otra propuesta, pero el sistema no
puede expresar que continúa el trabajo de la anterior. La relación queda fuera
del producto y es difícil reconstruir la secuencia de decisiones y entregas.

La relación uno a uno entre propuesta y especificación impide crear otra
especificación dentro de la misma propuesta sin cambiar las rutas y contratos
existentes. Tampoco hay un estado para abandonar una especificación aprobada que
aún se está implementando.

## Objetivo

Permitir que una nueva propuesta señale como origen una propuesta del mismo
proyecto cuya especificación ya esté `verified`. El nuevo trabajo sigue el
ciclo habitual de propuesta, especificación, criterios y tareas, mientras el
trabajo verificado permanece consultable e inmutable.

Este incremento establece trazabilidad de continuación. El vínculo expresa
procedencia, no reemplazo automático ni una versión activa única.

## Alcance

- Campo opcional `sourceProposalId` al crear una propuesta.
- Consulta del origen en respuestas individuales y listados de propuestas.
- Consulta de las propuestas que continúan una propuesta existente.
- Validación de proyecto, estado verificado del origen e integridad persistente.
- Migración de propuestas existentes, OpenAPI, README y pruebas.

## Fuera de alcance

- Reabrir, cancelar o sustituir una especificación `approved` o `implemented`.
- Múltiples especificaciones bajo una misma propuesta.
- Copiar contenido, criterios, tareas, evaluaciones o decisiones técnicas.
- Seleccionar automáticamente una especificación «vigente» o marcar la anterior
  como obsoleta.
- Numeración de revisiones, comparación de Markdown o fusión de cambios.
- Impedir varias continuaciones del mismo origen.

## Casos de uso

### UC-01 — Crear una continuación

El consumidor crea una propuesta nueva e indica la propuesta de origen. El
origen pertenece al mismo proyecto y tiene una especificación `verified`. La
nueva propuesta comienza en `pending` y conserva la relación desde su creación.

### UC-02 — Seguir el ciclo normal

La continuación puede aceptarse o rechazarse como cualquier propuesta. Si se
acepta, puede recibir su propia especificación. No se copian datos ni se
modifican el estado o los ETags del origen.

### UC-03 — Consultar la procedencia

El consumidor consulta una propuesta individual o el listado del proyecto y
puede identificar su origen. Una propuesta sin origen devuelve
`sourceProposalId: null`.

### UC-04 — Consultar continuaciones

El consumidor consulta las propuestas que señalan una propuesta de origen.
Puede ver continuaciones `pending`, `accepted` y `rejected`, incluso si todavía
no tienen especificación.

## Cambios de dominio

| Campo o concepto | Cambio | Regla específica |
|---|---|---|
| `FeatureProposal.SourceProposalId` | Nuevo, nullable | Propuesta de origen inmutable |

### Reglas específicas

1. `SourceProposalId` es opcional e inmutable. Las propuestas existentes se
   migran con `null`.
2. Si se proporciona, debe identificar otra propuesta del mismo proyecto con
   una especificación en estado `Verified` en el momento de crear la nueva.
3. La propuesta de origen no cambia de estado, contenido, fechas ni versión al
   crear una continuación.
4. Un origen puede tener varias continuaciones directas. Una continuación
   verificada puede ser a su vez origen de otra propuesta.
5. Los vínculos forman una secuencia acíclica: una propuesta solo puede señalar
   a otra que ya existía antes de su creación; el vínculo no puede editarse.
6. Aceptar o rechazar una continuación no modifica ni elimina su vínculo.
7. La continuación no hereda el estado ni el contenido de la propuesta o
   especificación de origen.

## Cambios del contrato HTTP

`POST /api/projects/{projectId}/proposals` amplía su body con un campo
opcional:

```json
{
  "title": "Corregir el criterio de exportación",
  "description": "El resultado verificado necesita una ampliación.",
  "sourceProposalId": "bd52c30c-b9c6-4975-b58e-548a3bac634c"
}
```

Omitir `sourceProposalId` o enviarlo como `null` conserva el comportamiento de
creación existente. Un UUID inválido o vacío devuelve `400 Validation Problem
Details` bajo `sourceProposalId`. Un origen inexistente o de otro proyecto
devuelve `404 Source proposal not found`. Un origen existente sin especificación
`verified` devuelve `409 Source specification is not verified`.

La respuesta de creación, `GET /api/projects/{projectId}/proposals/{proposalId}`
y `GET /api/projects/{projectId}/proposals` añaden `sourceProposalId` como UUID
o `null`, sin eliminar ni renombrar campos previos. La creación sigue devolviendo
`201 Created` y `Location`.

Se añade `GET /api/projects/{projectId}/proposals/{proposalId}/follow-ups`.
Devuelve `200` con una lista de `FeatureProposalResponse` ordenada por
`(CreatedAtUtc, Id)` ascendente, o `[]` cuando la propuesta existe pero aún no
tiene continuaciones. Una propuesta de otro proyecto o inexistente devuelve el
404 contextual existente. Solo se devuelven continuaciones directas, no toda la
cadena de descendientes.

Crear una continuación no exige `If-Match`: se crea un recurso nuevo y la
especificación de origen `Verified` es terminal. No se modifica el contrato de
concurrencia de los recursos existentes.

## Compatibilidad con specs anteriores

- La relación uno a uno entre propuesta y especificación de la 0004 permanece.
- El ciclo `Draft → Approved → Implemented → Verified` de la 0013 permanece.
- Una especificación `Verified` de la 0013 y la 0014 sigue siendo terminal e
  inmutable; crear una continuación no la reabre.
- Las propuestas existentes conservan sus datos y presentan
  `sourceProposalId: null` en las respuestas nuevas.
- La ampliación del JSON de propuesta es aditiva; un consumidor que rechace
  propiedades adicionales deberá adaptar su deserialización.

## Impacto en persistencia

Nueva migración propuesta: `AddFeatureProposalFollowUps`.

- Añade `SourceProposalId` nullable a `FeatureProposals`.
- Crea una foreign key autorreferenciada restrictiva y un índice sobre el campo.
- Conserva identificadores, estados, fechas y versiones de las propuestas
  existentes; las filas previas reciben `null`.
- No crea propuestas, especificaciones ni relaciones inferidas.

## Criterios de aceptación

1. Crear una propuesta sin origen conserva el contrato y devuelve
   `sourceProposalId: null`.
2. Crear una continuación de una propuesta del mismo proyecto con
   especificación `verified` devuelve `201`, estado `pending` y el origen.
3. Un origen con UUID inválido o vacío devuelve `400` bajo `sourceProposalId`.
4. Un origen inexistente o de otro proyecto devuelve 404 sin revelar su
   existencia; uno sin especificación verificada devuelve 409.
5. Crear una continuación no modifica el origen, su especificación, ETags ni
   fechas.
6. Consultas individuales y listados muestran el vínculo; el nuevo listado de
   continuaciones devuelve solo hijas directas en orden determinista.
7. Pueden existir varias continuaciones del mismo origen; una continuación
   verificada puede originar la siguiente sin formar ciclos.
8. Aceptar, rechazar o crear la especificación de una continuación conserva el
   vínculo y aplica las reglas ya existentes.
9. La migración conserva los datos y asigna `null` a propuestas anteriores.
10. El vínculo persiste entre scopes y reinicios, y la foreign key impide
    referencias huérfanas.
11. OpenAPI documenta el campo nuevo, el listado nuevo y sus errores.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Enlazar propuestas mediante `sourceProposalId` evita cambiar la relación
  uno a uno entre propuesta y especificación.
- Permitir varias continuaciones evita imponer una línea única de revisiones.
- El origen debe estar `verified` para que no haya dos planes activos cuya
  prioridad quede sin definir.
- El historial verificado permanece íntegro y consultable; la continuación
  inicia un ciclo independiente.
- Este incremento no corrige planes en `approved` o `implemented`. Hacerlo
  requiere definir cómo abandonar o sustituir tareas y estados en curso.
- El vínculo expresa procedencia sin supersesión automática. Una declaración de
  reemplazo formal requeriría una spec posterior con sus efectos de
  compatibilidad.

## Plan de verificación

- Pruebas de dominio para identidad del origen e inmutabilidad del vínculo.
- Pruebas HTTP para creación, errores, aislamiento, listados y continuidad del
  ciclo habitual.
- Pruebas de migración de datos existentes y de persistencia entre reinicios.
- Pruebas de carrera relevantes para creación y consulta del origen, más
  verificación de integridad por foreign key.
- OpenAPI y ausencia de migraciones pendientes.

## Evidencia de cierre

- Dominio, HTTP, SQLite y migración `AddFeatureProposalFollowUps` implementados
  según los criterios de aceptación. OpenAPI y README reflejan el contrato nuevo.
- Pruebas de dominio y HTTP cubren el origen opcional, validación, aislamiento por
  proyecto, estado verificado, continuidad del ciclo y consulta de hijas directas.
  También se comprobaron dos creaciones concurrentes, persistencia tras reinicio,
  integridad por foreign key y migración de una propuesta anterior.
- `dotnet build SpecFlow.slnx --no-restore`: correcto, 0 warnings.
- `dotnet test SpecFlow.slnx --no-build`: 451 pruebas correctas (127 de dominio y
  324 de integración).
- `dotnet format SpecFlow.slnx --verify-no-changes --no-restore`: correcto.
- `dotnet ef migrations has-pending-model-changes --project
  src/SpecFlow.Infrastructure --startup-project src/SpecFlow.Api --no-build`:
  sin cambios pendientes del modelo.
