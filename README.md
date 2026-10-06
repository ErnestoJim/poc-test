# SpecFlow

POC en .NET para aprender un flujo de desarrollo asistido por IA con Codex,
spec-driven development, skills y MCP.

La aplicación proporciona una API para gestionar proyectos y sus propuestas de
funcionalidades. Las especificaciones están en:

- [`specs/0001-projects/spec.md`](specs/0001-projects/spec.md)
- [`specs/0002-feature-proposals/spec.md`](specs/0002-feature-proposals/spec.md)
- [`specs/0003-feature-proposal-lifecycle/spec.md`](specs/0003-feature-proposal-lifecycle/spec.md)

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
