# 0003 — Ciclo de vida de propuestas de funcionalidades

## Estado

Aprobada para implementación el 6 de octubre de 2026.

## Problema

SpecFlow permite crear y consultar propuestas de funcionalidades, pero todas se
comportan de la misma forma y no existe una manera de indicar si una propuesta
ha sido aceptada o rechazada. Sin una decisión explícita no se puede distinguir
qué propuestas deben avanzar posteriormente hacia una especificación.

## Objetivo

Incorporar un ciclo de vida mínimo para las propuestas de funcionalidades. Toda
propuesta comienza pendiente y puede aceptarse o rechazarse mediante operaciones
HTTP explícitas. La solución debe proteger las transiciones en el dominio,
registrar cuándo se tomó la decisión y exigir un motivo al rechazar.

Este incremento define únicamente el flujo necesario actualmente. Las reglas de
transición deben permanecer centralizadas para que futuras especificaciones
puedan incorporar nuevos estados o bifurcaciones sin introducir ahora un motor
de workflows ni abstracciones sin uso concreto.

## Alcance

- Estados `Pending`, `Accepted` y `Rejected` para `FeatureProposal`.
- Estado inicial `Pending` para toda propuesta nueva.
- Transición explícita de `Pending` a `Accepted`.
- Transición explícita de `Pending` a `Rejected` con motivo obligatorio.
- Registro de la fecha de aceptación o rechazo mediante `DecidedAtUtc`.
- Exposición del estado, la fecha de decisión y el motivo de rechazo en el
  contrato HTTP de propuestas.
- Endpoints anidados para aceptar y rechazar una propuesta.
- Protección frente a transiciones no permitidas, incluidas decisiones
  simultáneas sobre la misma propuesta.
- Migración de EF Core y actualización de los datos existentes.
- Actualización de OpenAPI y pruebas unitarias y de integración.

## Fuera de alcance

- Crear una especificación al aceptar una propuesta.
- Modelo, persistencia o endpoints para especificaciones.
- Reabrir una propuesta aceptada o rechazada.
- Transiciones entre `Accepted` y `Rejected`.
- Edición o eliminación de propuestas.
- Historial de transiciones, auditoría o identificación de quién decidió.
- Comentarios, adjuntos, etiquetas o notificaciones.
- Filtrado, búsqueda o paginación de propuestas por estado.
- Estados adicionales, transiciones configurables o un motor de workflows.
- Autenticación o autorización.
- Frontend, MCP o integración con modelos de IA.
- Una capa Application, CQRS, MediatR o repositorios genéricos.

## Casos de uso

### UC-01 — Crear una propuesta pendiente

Al crear una propuesta mediante el contrato existente, el sistema le asigna el
estado `Pending`. Todavía no existe una decisión, por lo que `DecidedAtUtc` y
`RejectionReason` son `null`.

### UC-02 — Aceptar una propuesta

Un consumidor acepta una propuesta pendiente perteneciente a un proyecto
existente. El sistema cambia su estado a `Accepted`, registra el instante de la
decisión y devuelve la propuesta actualizada.

Aceptar una propuesta no crea automáticamente una especificación ni ningún otro
recurso.

### UC-03 — Rechazar una propuesta

Un consumidor rechaza una propuesta pendiente y proporciona un motivo. El
sistema normaliza y valida el motivo, cambia el estado a `Rejected`, registra el
instante de la decisión y devuelve la propuesta actualizada.

### UC-04 — Consultar el estado de una propuesta

Los contratos existentes de creación, consulta y listado incluyen el estado
actual, la fecha de decisión y el motivo de rechazo. El listado conserva el
orden y el alcance definidos en la especificación 0002 y no incorpora filtros.

## Modelo de dominio

Se amplía `FeatureProposal` con los campos siguientes:

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Status` | `FeatureProposalStatus` | Sí | Estado actual de la propuesta |
| `DecidedAtUtc` | DateTimeOffset nullable | No | Instante UTC de aceptación o rechazo |
| `RejectionReason` | string nullable | No | Motivo normalizado cuando la propuesta fue rechazada |

`FeatureProposalStatus` contiene inicialmente:

- `Pending`.
- `Accepted`.
- `Rejected`.

El conjunto inicial de estados no se considera un workflow configurable. Los
estados y las transiciones podrán ampliarse mediante especificaciones y
migraciones posteriores.

## Reglas de negocio

1. Toda propuesta nueva se crea con estado `Pending`.
2. Una propuesta `Pending` tiene `DecidedAtUtc` y `RejectionReason` con valor
   `null`.
3. Una propuesta `Pending` puede pasar a `Accepted`.
4. Al aceptar, `DecidedAtUtc` se obtiene mediante `TimeProvider`, se convierte a
   UTC y se normaliza a precisión de milisegundos.
5. Una propuesta `Accepted` tiene `RejectionReason` con valor `null`.
6. Una propuesta `Pending` puede pasar a `Rejected` únicamente con un motivo
   válido.
7. El motivo de rechazo se normaliza eliminando espacios exteriores.
8. Un motivo nulo, vacío o compuesto únicamente por espacios no es válido.
9. El motivo de rechazo puede contener como máximo 1.000 caracteres después de
   normalizarlo.
10. Al rechazar, `DecidedAtUtc` se obtiene mediante `TimeProvider`, se convierte
    a UTC y se normaliza a precisión de milisegundos.
11. Una propuesta aceptada o rechazada es final durante este incremento y no
    admite nuevas transiciones.
12. Una transición no permitida no modifica la propuesta.
13. El cliente no puede proporcionar `Status`, `DecidedAtUtc` ni
    `RejectionReason` al crear una propuesta.
14. Las reglas de transición pertenecen al dominio y no se duplican en los
    endpoints ni en la persistencia.
15. Si dos peticiones intentan decidir simultáneamente la misma propuesta, como
    máximo una transición puede completarse; la otra debe recibir un conflicto.

## Contratos HTTP

### Representación de una propuesta

Las respuestas existentes de creación, consulta y listado incorporan tres
campos:

```json
{
  "id": "bd52c30c-b9c6-4975-b58e-548a3bac634c",
  "projectId": "5fc297e9-cba9-47f4-862f-b984b53c317c",
  "title": "Añadir criterios de aceptación",
  "description": "Permitir documentar criterios verificables",
  "status": "pending",
  "decidedAtUtc": null,
  "rejectionReason": null,
  "createdAtUtc": "2026-10-05T10:30:00+00:00"
}
```

`status` se representa mediante los valores JSON `pending`, `accepted` y
`rejected`. Los futuros estados requerirán una ampliación explícita del
contrato.

### Aceptar una propuesta

`POST /api/projects/{projectId}/proposals/{proposalId}/accept`

La petición no requiere body.

Respuesta correcta: `200 OK` con la propuesta actualizada:

```json
{
  "id": "bd52c30c-b9c6-4975-b58e-548a3bac634c",
  "projectId": "5fc297e9-cba9-47f4-862f-b984b53c317c",
  "title": "Añadir criterios de aceptación",
  "description": "Permitir documentar criterios verificables",
  "status": "accepted",
  "decidedAtUtc": "2026-10-06T09:15:00+00:00",
  "rejectionReason": null,
  "createdAtUtc": "2026-10-05T10:30:00+00:00"
}
```

Errores:

- `400 Bad Request` si algún identificador no es un UUID válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.
- `404 Not Found` con título `Feature proposal not found` si la propuesta no
  existe o pertenece a otro proyecto.
- `409 Conflict` con título `Feature proposal transition conflict` si la
  propuesta ya fue aceptada o rechazada, también cuando el conflicto procede
  de una decisión simultánea.

### Rechazar una propuesta

`POST /api/projects/{projectId}/proposals/{proposalId}/reject`

Request:

```json
{
  "reason": "No aporta suficiente valor para este proyecto"
}
```

Respuesta correcta: `200 OK` con la propuesta actualizada:

```json
{
  "id": "bd52c30c-b9c6-4975-b58e-548a3bac634c",
  "projectId": "5fc297e9-cba9-47f4-862f-b984b53c317c",
  "title": "Añadir criterios de aceptación",
  "description": "Permitir documentar criterios verificables",
  "status": "rejected",
  "decidedAtUtc": "2026-10-06T09:20:00+00:00",
  "rejectionReason": "No aporta suficiente valor para este proyecto",
  "createdAtUtc": "2026-10-05T10:30:00+00:00"
}
```

Errores:

- `400 Bad Request` si algún identificador no es un UUID válido o el motivo no
  es válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.
- `404 Not Found` con título `Feature proposal not found` si la propuesta no
  existe o pertenece a otro proyecto.
- `409 Conflict` con título `Feature proposal transition conflict` si la
  propuesta ya fue aceptada o rechazada, también cuando el conflicto procede
  de una decisión simultánea.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

## Respuestas de error

Los errores continúan utilizando `application/problem+json`. Los errores de
validación del rechazo incluyen un diccionario `errors` indexado por `reason`.

Una transición en conflicto no revela el estado de propuestas pertenecientes a
otro proyecto. Primero se aplican las mismas reglas de existencia y aislamiento
definidas en la especificación 0002.

Las respuestas no exponen stack traces, rutas locales, cadenas de conexión,
detalles de Entity Framework Core ni información interna de SQLite.

## Criterios de aceptación

1. Una propuesta nueva se devuelve con `status` igual a `pending`,
   `decidedAtUtc` igual a `null` y `rejectionReason` igual a `null`.
2. Una propuesta pendiente puede aceptarse y la operación devuelve 200 con
   `status` igual a `accepted`.
3. La aceptación registra `decidedAtUtc` en UTC con precisión de milisegundos.
4. Una propuesta aceptada conserva `rejectionReason` igual a `null`.
5. Aceptar una propuesta no crea una especificación ni otro recurso.
6. Una propuesta pendiente puede rechazarse con un motivo válido y la operación
   devuelve 200 con `status` igual a `rejected`.
7. El rechazo registra `decidedAtUtc` en UTC con precisión de milisegundos.
8. El motivo de rechazo se devuelve sin espacios exteriores.
9. Un motivo nulo, vacío o compuesto por espacios devuelve 400 mediante
   Validation Problem Details.
10. Un motivo de más de 1.000 caracteres devuelve 400 mediante Validation
    Problem Details.
11. Aceptar o rechazar una propuesta ya decidida devuelve 409 y no modifica la
    decisión existente.
12. Dos decisiones simultáneas sobre la misma propuesta no pueden completarse
    ambas; una de ellas devuelve 409.
13. Los endpoints de decisión devuelven 404 si el proyecto no existe.
14. Una propuesta inexistente o perteneciente a otro proyecto devuelve 404 con
    `Feature proposal not found`.
15. Un `projectId` o `proposalId` con formato inválido devuelve 400 mediante
    Validation Problem Details.
16. Las operaciones existentes de creación, consulta y listado muestran los
    nuevos campos sin cambiar el orden del listado, el filtrado ni el aislamiento
    entre proyectos.
17. El listado devuelve conjuntamente propuestas pendientes, aceptadas y
    rechazadas, sin filtros.
18. OpenAPI documenta los dos endpoints y los nuevos campos del contrato.
19. Los datos de estado y decisión permanecen disponibles entre scopes y
    reinicios usando la base SQLite persistente.
20. Las propuestas creadas antes de la migración quedan con estado `Pending` y
    sin datos de decisión.

## Estrategia de pruebas

### Pruebas unitarias

- Estado inicial y ausencia de datos de decisión.
- Aceptación válida y normalización temporal.
- Rechazo válido, normalización del motivo y normalización temporal.
- Rechazo sin motivo o con motivo demasiado largo.
- Rechazo de cualquier transición desde un estado final.
- Conservación de la decisión inicial después de un intento inválido.

### Pruebas de integración

Se mantiene `WebApplicationFactory` con SQLite relacional en memoria y un
`TimeProvider` controlable. Se comprueban:

- los contratos HTTP de aceptación y rechazo;
- la representación de los nuevos campos en creación, consulta y listado;
- validación, Problem Details y aislamiento entre proyectos;
- conflictos al repetir o competir por una decisión;
- migración de propuestas existentes;
- persistencia entre reinicios;
- actualización del documento OpenAPI.

No se utilizan mocks de EF Core ni el proveedor EF Core InMemory.

## Impacto en persistencia

La tabla `FeatureProposals` incorporará:

- `Status`, requerido y almacenado como texto;
- `DecidedAtUtc`, nullable y almacenado como milisegundos Unix;
- `RejectionReason`, nullable y con longitud máxima de 1.000 caracteres;
- un mecanismo de concurrencia que impida completar dos decisiones sobre la
  misma versión de una propuesta.

La migración asignará `Pending` a todas las propuestas existentes y dejará
`DecidedAtUtc` y `RejectionReason` con valor `null`.

El estado se almacena como texto para mantener datos legibles y evitar acoplar
la persistencia a los valores ordinales del enum. Añadir estados futuros seguirá
requiriendo una decisión explícita de dominio y, cuando corresponda, una
migración.

## Requisitos no funcionales

- Compilación sin warnings y nullable reference types habilitado.
- Operaciones asíncronas con `CancellationToken`.
- Consultas de lectura con `AsNoTracking`.
- Fechas ISO 8601 en UTC y precisión de milisegundos.
- Reglas de transición centralizadas en `SpecFlow.Domain`.
- Endpoints separados de las entidades de dominio mediante DTOs.
- Pruebas deterministas, aisladas y sin dependencia de la hora real.
- Ningún cambio en las rutas existentes de proyectos o propuestas.
- Sin secretos ni archivos SQLite generados en Git.
- Compatibilidad conceptual con una futura sustitución de SQLite por
  PostgreSQL.

## Estructura prevista

- `src/SpecFlow.Domain/FeatureProposals`: enum, transición e invariantes.
- `src/SpecFlow.Infrastructure/Persistence`: configuración y migración.
- `src/SpecFlow.Api/Contracts/FeatureProposals`: request de rechazo y respuesta
  ampliada.
- `src/SpecFlow.Api/Endpoints/FeatureProposalEndpoints.cs`: rutas de decisión.
- `tests/SpecFlow.Domain.Tests/FeatureProposals`: pruebas de transiciones.
- `tests/SpecFlow.Api.IntegrationTests/FeatureProposals`: pruebas HTTP,
  concurrencia y persistencia.

Se mantienen los tres proyectos de producción actuales y no se crea una capa
Application.

## Decisiones acordadas

- Los estados iniciales son `Pending`, `Accepted` y `Rejected`.
- Toda propuesta comienza en `Pending`.
- Únicamente se permiten las transiciones `Pending` → `Accepted` y `Pending` →
  `Rejected`.
- `Accepted` y `Rejected` son estados finales durante este incremento.
- Rechazar requiere un motivo.
- `DecidedAtUtc` registra tanto la aceptación como el rechazo.
- No se añaden filtros de listado por estado.
- Aceptar no crea automáticamente una especificación.
- La creación y gestión de especificaciones se reserva para una especificación
  posterior.
- La posibilidad de añadir estados y bifurcaciones futuras se conserva mediante
  reglas de transición centralizadas, sin introducir ahora un motor de
  workflows.
- El motivo de rechazo admite un máximo de 1.000 caracteres.
- Los endpoints de decisión utilizan acciones `POST` anidadas y devuelven la
  propuesta actualizada con `200 OK`.
- Repetir cualquier decisión sobre una propuesta final devuelve `409 Conflict`,
  incluso si se repite la misma acción.
- El estado se representa y persiste mediante nombres de texto en lugar de
  valores ordinales.
- Las decisiones concurrentes se protegen mediante concurrencia optimista.

## Plan de implementación

1. Implementar el estado y las transiciones en `FeatureProposal` junto con sus
   pruebas unitarias.
2. Ampliar la configuración de EF Core y generar la migración correspondiente.
3. Actualizar la respuesta de propuestas y las pruebas de los contratos
   existentes.
4. Implementar la aceptación con sus respuestas de error.
5. Implementar el rechazo, su validación y sus respuestas de error.
6. Verificar conflictos, concurrencia y aislamiento entre proyectos.
7. Verificar la migración y la persistencia entre reinicios.
8. Actualizar OpenAPI y README.
9. Ejecutar restauración, compilación, pruebas y formato.
10. Auditar cada criterio de aceptación antes de cambiar el estado de la
    especificación a implementada y verificada.
