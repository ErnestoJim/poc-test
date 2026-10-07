# SpecFlow

POC en .NET para aprender un flujo de desarrollo asistido por IA con Codex,
spec-driven development, skills y MCP.

La aplicación proporciona una API para gestionar proyectos y sus propuestas de
funcionalidades. Las especificaciones están en:

- [`specs/0001-projects/spec.md`](specs/0001-projects/spec.md)
- [`specs/0002-feature-proposals/spec.md`](specs/0002-feature-proposals/spec.md)
- [`specs/0003-feature-proposal-lifecycle/spec.md`](specs/0003-feature-proposal-lifecycle/spec.md)
- [`specs/0004-specifications/spec.md`](specs/0004-specifications/spec.md)
- [`specs/0005-acceptance-criteria/spec.md`](specs/0005-acceptance-criteria/spec.md)
- [`specs/0006-implementation-tasks/spec.md`](specs/0006-implementation-tasks/spec.md)
- [`specs/0007-implementation-task-lifecycle/spec.md`](specs/0007-implementation-task-lifecycle/spec.md)
- [`specs/0008-api-maintainability-checkpoint/spec.md`](specs/0008-api-maintainability-checkpoint/spec.md)
- [`specs/0009-http-concurrency/spec.md`](specs/0009-http-concurrency/spec.md)

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

Endpoints:

- `POST /api/projects`
- `GET /api/projects/{id}`
- `GET /api/projects`
- `POST /api/projects/{projectId}/proposals`
- `GET /api/projects/{projectId}/proposals/{proposalId}`
- `GET /api/projects/{projectId}/proposals`
- `POST /api/projects/{projectId}/proposals/{proposalId}/accept`
- `POST /api/projects/{projectId}/proposals/{proposalId}/reject`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/order`
- `DELETE /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/tasks`
- `GET /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/order`
- `DELETE /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/start`
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/complete`
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

Las propuestas comienzan en estado `pending`. Para aceptarlas o rechazarlas,
sustituye también `{proposalId}` por el identificador de la propuesta:

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
terminan en `completed`. Una tarea completada no puede reabrirse, aunque puede
seguir editándose, reordenándose o eliminándose.

## Concurrencia optimista

Las respuestas de especificaciones, criterios y tareas incluyen un header
`ETag`. Los listados de criterios y tareas también incluyen un ETag propio de
la colección. Antes de editar, eliminar, transicionar o reordenar, el consumidor
debe leer el recurso correspondiente y enviar ese valor sin interpretarlo en
`If-Match`.

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
