# SpecFlow

POC en .NET para aprender un flujo de desarrollo asistido por IA con Codex,
spec-driven development, skills y MCP.

La aplicación proporciona una API para gestionar proyectos, propuestas de
funcionalidades, especificaciones en Markdown, criterios de aceptación y tareas
de implementación. También mantiene un registro de decisiones técnicas por
proyecto. Las propuestas, especificaciones, tareas y decisiones técnicas cuentan
con ciclos de vida explícitos, y los recursos editables utilizan concurrencia
optimista mediante ETags e `If-Match`.

## Metodología spec-driven

La documentación distingue las reglas compartidas de los incrementos
funcionales:

- [`specs/constitution.md`](specs/constitution.md) define los principios y
  convenciones vigentes.
- [`specs/README.md`](specs/README.md) explica la evolución metodológica y enlaza
  el histórico completo.
- [`specs/spec-template.md`](specs/spec-template.md) es la plantilla compacta
  para nuevas especificaciones.

Las specs 0001–0010 conservan el formato exhaustivo original como material de
aprendizaje. A partir de la 0011, cada documento describe principalmente el
delta respecto de la constitución.

## Requisitos

- .NET SDK 10.0.401 o un parche compatible según `global.json`.

## Preparación

```bash
dotnet tool restore
dotnet restore SpecFlow.slnx
```

## Compilar y verificar

```bash
dotnet build SpecFlow.slnx --no-restore
dotnet test SpecFlow.slnx --no-build
dotnet format SpecFlow.slnx --verify-no-changes --no-restore
```

## Ejecutar

```bash
dotnet run --project src/SpecFlow.Api
```

La API escucha por defecto en `http://localhost:5194`. La base de datos SQLite
se crea de forma persistente en `src/SpecFlow.Api/specflow.db` y está ignorada
por Git.

### Interfaz web

Abre dos terminales desde la raíz del repositorio. En la primera, inicia la API
con el comando anterior. En la segunda, ejecuta:

```bash
dotnet run --project src/SpecFlow.Web
```

Abre `http://localhost:5195` en el navegador. La página inicial muestra los
proyectos y permite crear uno incluso con la base vacía. Desde el detalle del
proyecto puedes crear y consultar propuestas. Desde el detalle de una propuesta
pendiente puedes aceptarla tras confirmación o rechazarla indicando un motivo.
El detalle muestra la decisión y permite consultar el estado actual si hubo un
conflicto o se perdió la conexión. Desde una propuesta aceptada puedes abrir su
especificación, crearla con contenido Markdown si aún no existe y consultar su
estado y contenido. La edición del contenido y el resto del ciclo de vida de
especificaciones siguen gestionándose por la API.

La web llama a la API desde su servidor. Para usar otra dirección, configura
`SpecFlowApi__BaseUrl` en el proceso web; en desarrollo vale por defecto
`http://localhost:5194`. Si la API no está disponible, la interfaz muestra un
error y permite repetir las consultas.

Endpoints:

- `POST /api/projects`
- `GET /api/projects/{id}`
- `GET /api/projects`
- `POST /api/projects/{projectId}/proposals`
- `GET /api/projects/{projectId}/proposals/{proposalId}`
- `GET /api/projects/{projectId}/proposals/{proposalId}/follow-ups`
- `GET /api/projects/{projectId}/proposals`
- `POST /api/projects/{projectId}/proposals/{proposalId}/accept`
- `POST /api/projects/{projectId}/proposals/{proposalId}/reject`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/approve`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/mark-implemented`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/verify`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/order`
- `DELETE /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}/verification`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}/verification`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/tasks`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/order`
- `DELETE /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/start`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/complete`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/acceptance-criteria`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/acceptance-criteria`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}/tasks`
- `POST /api/projects/{projectId}/technical-decisions`
- `GET /api/projects/{projectId}/technical-decisions`
- `GET /api/projects/{projectId}/technical-decisions/{decisionId}`
- `PUT /api/projects/{projectId}/technical-decisions/{decisionId}`
- `DELETE /api/projects/{projectId}/technical-decisions/{decisionId}`
- `POST /api/projects/{projectId}/technical-decisions/{decisionId}/accept`
- `POST /api/projects/{projectId}/technical-decisions/{decisionId}/reject`
- `POST /api/projects/{projectId}/technical-decisions/{decisionId}/supersede`
- `GET /openapi/v1.json`

Ejemplo:

```bash
curl --request POST http://localhost:5194/api/projects \
  --header 'Content-Type: application/json' \
  --data '{"name":"SpecFlow","description":"AI-assisted development POC"}'
```

Para crear una propuesta, sustituye `{projectId}` por el identificador de un
proyecto existente:

```bash
curl --request POST http://localhost:5194/api/projects/{projectId}/proposals \
  --header 'Content-Type: application/json' \
  --data '{"title":"Add acceptance criteria","description":"Allow verifiable criteria"}'
```

Una propuesta nueva puede continuar el trabajo de otra propuesta del mismo
proyecto cuya especificación ya esté `verified`. Se indica su origen con
`sourceProposalId`; la nueva propuesta empieza en `pending` y sigue su propio
ciclo. La propuesta y la especificación de origen permanecen intactas. Las
respuestas y listados de propuestas incluyen `sourceProposalId` (`null` cuando
no hay origen), y la ruta `follow-ups` devuelve las continuaciones directas:

```bash
curl --request POST http://localhost:5194/api/projects/{projectId}/proposals \
  --header 'Content-Type: application/json' \
  --data '{"title":"Extend verified behavior","sourceProposalId":"{verifiedProposalId}"}'

curl http://localhost:5194/api/projects/{projectId}/proposals/{verifiedProposalId}/follow-ups
```

Para aceptarlas o rechazarlas, sustituye también `{proposalId}` por el
identificador de la propuesta:

```bash
curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/accept

curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/reject \
  --header 'Content-Type: application/json' \
  --data '{"reason":"Not enough value for this project"}'
```

Una propuesta aceptada puede tener una especificación en Markdown. La creación
es explícita y `PUT` reemplaza el contenido completo:

```bash
curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification \
  --header 'Content-Type: application/json' \
  --data '{"content":"# Specification\n\n## Objective\n\nDescribe the expected behavior."}'

curl --request PUT \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification \
  --header 'Content-Type: application/json' \
  --header 'If-Match: {specificationEtag}' \
  --data '{"content":"# Updated specification\n\nRefined behavior."}'
```

Una especificación puede contener criterios de aceptación Markdown ordenados.
Los criterios nuevos se añaden al final y la reordenación solo necesita sus
identificadores:

```bash
curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria \
  --header 'Content-Type: application/json' \
  --data '{"content":"A valid request returns `201 Created`."}'

curl --request PUT \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/order \
  --header 'Content-Type: application/json' \
  --header 'If-Match: {criteriaListEtag}' \
  --data '{"criterionIds":["{firstCriterionId}","{secondCriterionId}"]}'
```

Una especificación también puede descomponerse en tareas de implementación
ordenadas. Cada tarea tiene un título único dentro de la especificación y una
descripción Markdown opcional:

```bash
curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/tasks \
  --header 'Content-Type: application/json' \
  --data '{"title":"Implement domain entity","description":"Add the entity and its unit tests."}'

curl --request PUT \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/order \
  --header 'Content-Type: application/json' \
  --header 'If-Match: {tasksListEtag}' \
  --data '{"taskIds":["{firstTaskId}","{secondTaskId}"]}'

curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/start \
  --header 'If-Match: {taskEtag}'

curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/complete \
  --header 'If-Match: {updatedTaskEtag}'
```

Las tareas comienzan en estado `pending`, pasan a `in_progress` al iniciarse y
terminan en `completed`. Solo pueden iniciarse y completarse cuando la
especificación está aprobada.

Los criterios y las tareas de una misma especificación pueden vincularse para
mantener trazabilidad muchos-a-muchos. La relación se consulta en ambas
direcciones y se sustituye como un conjunto completo mediante el ETag propio de
la colección, sin cambiar el estado ni el ETag individual de sus extremos:

```bash
curl --include \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/acceptance-criteria

curl --request PUT \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/acceptance-criteria \
  --header 'Content-Type: application/json' \
  --header 'If-Match: {traceabilityEtag}' \
  --data '{"acceptanceCriterionIds":["{criterionId}"]}'
```

Las especificaciones comienzan como `draft`. Aprobar exige al menos un criterio,
una tarea y que todos los criterios estén vinculados con alguna tarea. La
aprobación congela contenido y planificación; entonces se pueden ejecutar las
tareas. Cuando todas terminan, la especificación puede declararse implementada.
Entonces se registra el resultado de cada criterio como `passed`, `failed` o
`unverified`, con evidencia Markdown opcional para los dos primeros. El resultado
tiene un ETag propio y puede corregirse mientras la especificación está
`implemented`. La verificación final exige que todos los criterios estén en
`passed`, aunque no tengan evidencia, y sigue siendo explícita:

```bash
curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/approve \
  --header 'If-Match: {specificationEtag}'

curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/mark-implemented \
  --header 'If-Match: {updatedSpecificationEtag}'

curl --include \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}/verification

curl --request PUT \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}/verification \
  --header 'Content-Type: application/json' \
  --header 'If-Match: {verificationEtag}' \
  --data '{"status":"passed","evidence":null}'

curl --request POST \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification/verify \
  --header 'If-Match: {currentSpecificationEtag}'
```

El ETag de una especificación representa el plan completo y cambia cuando se
modifican su contenido, criterios, tareas, estados de tareas, trazabilidad o
evaluaciones. Las especificaciones verificadas antes de incorporar evaluaciones
conservan su estado sin resultados ni evidencias históricas inventadas.

Cada proyecto también puede mantener un registro cronológico de decisiones
técnicas. Cada decisión contiene un título y un documento Markdown flexible; los
títulos pueden repetirse. Las decisiones comienzan como `draft`, único estado en
el que pueden editarse o eliminarse. Un borrador puede aceptarse o rechazarse y
una decisión aceptada puede quedar supersedida por otra decisión aceptada
posterior del mismo proyecto. Todas estas operaciones requieren el ETag vigente:

```bash
curl --request POST \
  http://localhost:5194/api/projects/{projectId}/technical-decisions \
  --header 'Content-Type: application/json' \
  --data '{"title":"Persist with SQLite","content":"# Decision\n\nUse SQLite through EF Core."}'

curl --request PUT \
  http://localhost:5194/api/projects/{projectId}/technical-decisions/{decisionId} \
  --header 'Content-Type: application/json' \
  --header 'If-Match: {decisionEtag}' \
  --data '{"title":"Persist with SQLite","content":"# Updated decision\n\nKeep SQLite for the POC."}'

curl --request POST \
  http://localhost:5194/api/projects/{projectId}/technical-decisions/{decisionId}/accept \
  --header 'If-Match: {decisionEtag}'

curl --request POST \
  http://localhost:5194/api/projects/{projectId}/technical-decisions/{decisionId}/reject \
  --header 'Content-Type: application/json' \
  --header 'If-Match: {decisionEtag}' \
  --data '{"reason":"The operational cost is too high."}'

curl --request POST \
  http://localhost:5194/api/projects/{projectId}/technical-decisions/{decisionId}/supersede \
  --header 'Content-Type: application/json' \
  --header 'If-Match: {decisionEtag}' \
  --data '{"replacementDecisionId":"{acceptedReplacementDecisionId}"}'
```

## Concurrencia optimista

Las respuestas de especificaciones, criterios, tareas y decisiones técnicas
incluyen un header `ETag`. Los listados de criterios y tareas también incluyen
un ETag propio de la colección. Antes de editar, eliminar, transicionar o
reordenar, el consumidor debe leer el recurso correspondiente y enviar ese
valor sin interpretarlo en `If-Match`.

```bash
curl --include \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification

curl --request PUT \
  http://localhost:5194/api/projects/{projectId}/proposals/{proposalId}/specification \
  --header 'Content-Type: application/json' \
  --header 'If-Match: "{etagReturnedByGet}"' \
  --data '{"content":"# Updated specification"}'
```

La ausencia de la precondición devuelve `428 Precondition Required`, un header
no admitido devuelve `400 Bad Request` y un ETag obsoleto devuelve
`412 Precondition Failed`. Las respuestas correctas devuelven el ETag vigente;
una reordenación lo devuelve incluso con estado `204 No Content`.
