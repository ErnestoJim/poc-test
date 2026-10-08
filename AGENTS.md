# AGENTS.md

## Propósito del repositorio

SpecFlow es un proyecto de aprendizaje sobre desarrollo asistido por IA con
Codex, spec-driven development, .NET y C#.

El producto gestiona progresivamente proyectos, propuestas, especificaciones,
criterios de aceptación, tareas y decisiones técnicas. Las capacidades nuevas no
deben anticiparse sin una especificación aprobada.

## Fuentes de verdad

Antes de modificar el repositorio, leer:

1. este archivo para las instrucciones operativas;
2. `README.md` para preparar y utilizar el proyecto;
3. `specs/constitution.md` para las reglas compartidas vigentes;
4. `specs/README.md` para el proceso y el histórico;
5. la especificación numerada relevante para el comportamiento concreto.

La constitución no sustituye los contratos particulares de las specs. Una spec
no puede contradecirla silenciosamente: debe proponer una enmienda explícita.
Las specs 0001 a 0010 se conservan como registro histórico; las nuevas utilizan
`specs/spec-template.md`.

## Estructura del repositorio

- `src/SpecFlow.Domain`: dominio independiente de ASP.NET Core, EF Core y
  detalles de infraestructura.
- `src/SpecFlow.Infrastructure`: persistencia, configuración de EF Core y
  migraciones.
- `src/SpecFlow.Api`: composición, contratos HTTP, endpoints, OpenAPI y Problem
  Details.
- `tests/SpecFlow.Domain.Tests`: pruebas unitarias del dominio.
- `tests/SpecFlow.Api.IntegrationTests`: contrato HTTP e integración con SQLite.
- `specs`: constitución, plantilla e histórico de especificaciones versionadas.

Las responsabilidades y restricciones arquitectónicas completas están en la
constitución; no deben duplicarse aquí.

## Flujo de trabajo

1. Inspeccionar el estado del repositorio y preservar los cambios existentes.
2. Identificar la constitución y las specs que gobiernan el cambio.
3. Crear o actualizar primero la spec cuando cambien alcance, contratos o
   decisiones importantes.
4. Esperar aprobación explícita antes de implementar comportamiento nuevo.
5. Implementar en pasos verticales pequeños y trazables a criterios de
   aceptación.
6. Añadir o actualizar pruebas junto con el comportamiento.
7. Ejecutar verificaciones proporcionales y registrar la evidencia en la spec.
8. Comunicar resultados, desviaciones y decisiones pendientes.

No reescribir specs implementadas para adaptarlas a la plantilla actual. Una
modificación funcional posterior debe quedar en una nueva spec que identifique
el comportamiento sustituido.

## Comandos de trabajo

```bash
dotnet tool restore
dotnet restore SpecFlow.slnx
dotnet build SpecFlow.slnx --no-restore
dotnet test SpecFlow.slnx --no-build
dotnet format SpecFlow.slnx --verify-no-changes --no-restore
dotnet run --project src/SpecFlow.Api
```

No usar `--no-restore` o `--no-build` si el paso requerido no se ha ejecutado
antes en el entorno actual.

Crear una migración con:

```bash
dotnet ef migrations add <MigrationName> --project src/SpecFlow.Infrastructure --startup-project src/SpecFlow.Api --output-dir Persistence/Migrations
```

Todo cambio de esquema debe incluir una migración y pruebas relevantes. No
editar una migración ya integrada salvo que exista una razón explícita y
revisada.

## Límites operativos

- No modificar, descartar ni sobrescribir trabajo ajeno sin autorización.
- No ejecutar acciones destructivas ni cambiar historial, staging o ramas sin
  una petición expresa.
- No instalar paquetes, herramientas o servicios sin una necesidad concreta y
  la autorización correspondiente.
- No almacenar secretos, credenciales ni datos sensibles en el repositorio.
- Ante una decisión que cambie significativamente la arquitectura, presentar
  opciones y esperar aprobación.
- Aplicar siempre la definición de terminado de `specs/constitution.md`.
