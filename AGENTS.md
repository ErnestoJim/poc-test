# AGENTS.md

## Propósito del repositorio

SpecFlow es un POC para aprender desarrollo asistido por IA con Codex mediante un flujo spec-driven sobre .NET y C#.

El producto gestiona progresivamente proyectos, propuestas, especificaciones, tareas, criterios de aceptación y decisiones técnicas. Skills, MCP, frontend e integración con modelos de IA son objetivos posteriores: no deben anticiparse sin una especificación aprobada.

## Estructura y responsabilidades

- `src/SpecFlow.Domain`: entidades, reglas e invariantes del dominio. Debe permanecer independiente de ASP.NET Core, EF Core y detalles de infraestructura.
- `src/SpecFlow.Infrastructure`: persistencia con EF Core, configuración de entidades y migraciones.
- `src/SpecFlow.Api`: composición de la aplicación, configuración HTTP, endpoints, OpenAPI y Problem Details.
- `tests/SpecFlow.Domain.Tests`: pruebas unitarias de reglas e invariantes del dominio.
- `tests/SpecFlow.Api.IntegrationTests`: pruebas del contrato HTTP y de la integración real con EF Core y SQLite.
- `specs`: especificaciones versionadas que definen alcance, comportamiento y criterios de aceptación.

No crear una capa `Application` hasta que exista lógica de casos de uso que justifique separarla. No introducir CQRS, MediatR, event sourcing, repositorios genéricos ni abstracciones sin un uso concreto.

## Flujo spec-driven

1. Leer `AGENTS.md`, el `README.md` y la especificación relevante antes de modificar código.
2. Inspeccionar el estado del repositorio y preservar los cambios existentes del usuario.
3. Aclarar o actualizar primero la especificación cuando cambien alcance, contratos o decisiones importantes.
4. Implementar en pasos verticales pequeños, trazables a criterios de aceptación.
5. Añadir o actualizar pruebas junto con el comportamiento.
6. Ejecutar las verificaciones proporcionales al cambio y comunicar resultados y decisiones pendientes.

La fuente principal de verdad para la funcionalidad inicial es `specs/0001-projects/spec.md`. No ampliar silenciosamente su alcance.

## Convenciones .NET y C#

- Usar .NET 10 y C# 14 según `global.json` y `Directory.Build.props`.
- Mantener nullable reference types e implicit usings habilitados.
- Tratar warnings como errores y respetar los analizadores configurados.
- Preferir código simple, explícito y mantenible frente a abstracciones prematuras.
- Mantener las reglas del dominio dentro de `SpecFlow.Domain`; no duplicarlas en endpoints o persistencia.
- Usar nombres técnicos e identificadores en inglés.
- Usar `TimeProvider` para comportamiento dependiente del tiempo y almacenar fechas en UTC.
- Exponer errores HTTP consistentes mediante Problem Details.

## Persistencia y migraciones

SQLite es la base de datos persistente local. La configuración de EF Core debe conservar la posibilidad de migrar posteriormente a PostgreSQL sin introducir ahora infraestructura para ese proveedor.

Todo cambio de esquema debe incluir una migración de EF Core en `src/SpecFlow.Infrastructure/Persistence/Migrations` y pruebas relevantes. No editar manualmente una migración existente salvo que exista una razón explícita y revisada.

Crear una migración con:

```bash
dotnet ef migrations add <MigrationName> --project src/SpecFlow.Infrastructure --startup-project src/SpecFlow.Api --output-dir Persistence/Migrations
```

## Comandos de trabajo

```bash
dotnet tool restore
dotnet restore SpecFlow.slnx
dotnet build SpecFlow.slnx --no-restore
dotnet test SpecFlow.slnx --no-build
dotnet format SpecFlow.slnx --verify-no-changes --no-restore
dotnet run --project src/SpecFlow.Api
```

No usar `--no-restore` o `--no-build` si el paso requerido no se ha ejecutado antes en el entorno actual.

## Estrategia de pruebas

- Probar invariantes y normalización del dominio con pruebas unitarias rápidas y deterministas.
- Probar endpoints, validación, Problem Details, persistencia y unicidad con pruebas de integración.
- Las pruebas de integración deben usar SQLite real, normalmente en memoria con una conexión abierta durante cada prueba, para conservar su comportamiento relacional.
- Controlar el tiempo en pruebas mediante un `TimeProvider` sustituible.
- Añadir una prueba de regresión para cada defecto corregido cuando sea razonable.

## Definición de terminado

Un cambio está terminado cuando:

- satisface la especificación y sus criterios de aceptación;
- mantiene los límites entre proyectos;
- incluye pruebas adecuadas y estas pasan;
- compila sin warnings;
- `dotnet format` no detecta cambios;
- incluye migración cuando cambia el esquema;
- actualiza documentación o especificación cuando corresponde;
- no incorpora funcionalidad fuera de alcance.

## Límites operativos

- No modificar, descartar ni sobrescribir trabajo ajeno sin autorización explícita.
- No ejecutar acciones destructivas ni comandos Git que cambien historial, staging o ramas sin petición expresa.
- No instalar paquetes, herramientas o servicios sin una necesidad concreta y autorización cuando corresponda.
- No añadir autenticación, frontend, MCP, IA, Docker, Aspire o despliegue hasta que una especificación aprobada los incluya.
- No almacenar secretos, credenciales ni datos sensibles en el repositorio.
- Ante una decisión que cambie significativamente la arquitectura, presentar opciones y esperar aprobación.
