# 0002 — Gestión básica de propuestas de funcionalidades

## Estado

Implementada y verificada el 5 de octubre de 2026.

## Problema

SpecFlow permite registrar proyectos, pero todavía no puede capturar las
propuestas de funcionalidades que se quieren analizar o desarrollar dentro de
cada proyecto. Sin esta capacidad no existe una unidad sobre la que asociar
posteriormente especificaciones, tareas, criterios de aceptación o decisiones
técnicas.

## Objetivo

Proporcionar una API HTTP que permita crear, consultar y listar propuestas de
funcionalidades dentro de un proyecto existente. La solución debe mantener los
límites arquitectónicos establecidos en la especificación 0001 y evitar añadir
workflow o abstracciones que todavía no tengan casos de uso concretos.

## Alcance

- Modelo de dominio `FeatureProposal` como raíz independiente.
- Asociación obligatoria de cada propuesta con un proyecto mediante `ProjectId`.
- Persistencia mediante Entity Framework Core y SQLite.
- Creación de propuestas dentro de un proyecto existente.
- Consulta de una propuesta mediante una ruta anidada bajo su proyecto.
- Listado de las propuestas de un proyecto sin paginación.
- Títulos duplicados permitidos, incluso dentro del mismo proyecto.
- Validación y normalización de título y descripción.
- Restricción de foreign key con eliminación restrictiva.
- Errores HTTP mediante Problem Details.
- Actualización del documento OpenAPI.
- Pruebas unitarias y de integración.

## Fuera de alcance

- Edición o eliminación de propuestas.
- Estados, transiciones, aceptación o rechazo de propuestas.
- Historial, auditoría, comentarios, adjuntos o etiquetas.
- Especificaciones, tareas o decisiones técnicas asociadas a una propuesta.
- Rutas globales de propuestas fuera del contexto de un proyecto.
- Paginación, búsqueda o filtrado.
- Eliminación de proyectos.
- Frontend Blazor.
- Autenticación o autorización.
- MCP o integración con modelos de IA.
- Docker, Aspire, despliegue o PostgreSQL.
- Una capa Application, CQRS, MediatR o repositorios genéricos.

## Casos de uso

### UC-01 — Crear una propuesta

Un consumidor proporciona el identificador de un proyecto, un título y,
opcionalmente, una descripción. El sistema valida los datos y la existencia del
proyecto, crea la propuesta, la persiste y devuelve el recurso creado.

### UC-02 — Consultar una propuesta

Un consumidor proporciona los identificadores de un proyecto y de una
propuesta. El sistema devuelve la propuesta solamente si pertenece al proyecto
indicado. Una propuesta inexistente o perteneciente a otro proyecto se presenta
como no encontrada.

### UC-03 — Listar propuestas de un proyecto

El sistema devuelve exclusivamente las propuestas del proyecto indicado, desde
la más reciente hasta la más antigua. Si el proyecto existe pero no contiene
propuestas, devuelve una colección vacía.

## Modelo de dominio

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Id` | UUID | Sí | Identificador global generado por la aplicación |
| `ProjectId` | UUID | Sí | Proyecto al que pertenece la propuesta |
| `Title` | string | Sí | Título visible normalizado exteriormente |
| `Description` | string nullable | No | Descripción normalizada |
| `CreatedAtUtc` | DateTimeOffset | Sí | Instante UTC de creación con precisión de milisegundos |

`FeatureProposal` es una raíz independiente. El modelo de dominio no expone una
navegación hacia `Project`, y `Project` no mantiene una colección de propuestas.
La relación se representa mediante `ProjectId` y se protege en persistencia con
una foreign key.

## Reglas de negocio

1. `Id` no puede ser un UUID vacío.
2. `ProjectId` no puede ser un UUID vacío y es inmutable.
3. `Title` se normaliza eliminando espacios exteriores.
4. `Title` debe contener entre 1 y 200 caracteres después de normalizarlo.
5. Dos propuestas pueden compartir el mismo título dentro del mismo proyecto.
6. `Description` se normaliza eliminando espacios exteriores.
7. Una descripción vacía después de normalizarse se almacena como `null`.
8. `Description` puede contener como máximo 4.000 caracteres.
9. `CreatedAtUtc` se obtiene mediante `TimeProvider`, se convierte a UTC y se
   normaliza a precisión de milisegundos.
10. El cliente no puede proporcionar `Id`, `ProjectId` en el body ni
    `CreatedAtUtc`.
11. El proyecto debe existir antes de crear o listar propuestas.
12. La base de datos debe impedir propuestas huérfanas mediante una foreign key.
13. La eliminación de un proyecto con propuestas debe quedar restringida en la
    relación, aunque eliminar proyectos siga fuera de alcance.

## Contratos HTTP

### Crear una propuesta

`POST /api/projects/{projectId}/proposals`

Request:

```json
{
  "title": "Añadir criterios de aceptación",
  "description": "Permitir documentar criterios verificables"
}
```

Respuesta correcta: `201 Created`, cabecera `Location` apuntando al recurso y:

```json
{
  "id": "bd52c30c-b9c6-4975-b58e-548a3bac634c",
  "projectId": "5fc297e9-cba9-47f4-862f-b984b53c317c",
  "title": "Añadir criterios de aceptación",
  "description": "Permitir documentar criterios verificables",
  "createdAtUtc": "2026-10-05T10:30:00+00:00"
}
```

Errores:

- `400 Bad Request` si `projectId` no es un UUID válido o el body es inválido.
- `404 Not Found` si el proyecto no existe.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

### Consultar una propuesta

`GET /api/projects/{projectId}/proposals/{proposalId}`

- `200 OK` con la propuesta.
- `400 Bad Request` si algún identificador no es un UUID válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.
- `404 Not Found` con título `Feature proposal not found` si la propuesta no
  existe o no pertenece al proyecto indicado.

No se expone una ruta global para consultar propuestas durante esta fase.

### Listar propuestas de un proyecto

`GET /api/projects/{projectId}/proposals`

- `200 OK` con un array de propuestas del proyecto.
- Orden por `CreatedAtUtc` descendente y después por `Id` ascendente.
- Si el proyecto existe pero no tiene propuestas, el resultado es `[]`.
- `400 Bad Request` si `projectId` no es un UUID válido.
- `404 Not Found` si el proyecto no existe.

## Respuestas de error

Los errores utilizan `application/problem+json`. Los errores de validación
incluyen un diccionario `errors` indexado por `projectId`, `proposalId`, `title`
o `description`, según corresponda.

Las respuestas no indican que una propuesta existe bajo otro proyecto y no
exponen stack traces, rutas locales, cadenas de conexión, detalles de Entity
Framework Core ni información interna de SQLite.

## Criterios de aceptación

1. Una petición válida crea una propuesta, responde 201 e incluye `Location`
   con la ruta anidada del recurso.
2. La respuesta de creación contiene `id`, `projectId`, `title`, `description` y
   `createdAtUtc`.
3. La propuesta creada puede recuperarse mediante los identificadores del
   proyecto y de la propuesta.
4. Crear una propuesta para un proyecto inexistente devuelve 404 mediante
   Problem Details.
5. Un `projectId` con formato inválido devuelve 400 mediante Validation Problem
   Details.
6. Un `proposalId` con formato inválido devuelve 400 mediante Validation Problem
   Details.
7. Un título nulo, vacío o compuesto por espacios devuelve 400.
8. Un título de más de 200 caracteres devuelve 400.
9. Una descripción de más de 4.000 caracteres devuelve 400.
10. El título y la descripción se devuelven sin espacios exteriores.
11. Una descripción compuesta por espacios se persiste y devuelve como `null`.
12. Dos propuestas con el mismo título pueden crearse dentro del mismo proyecto
    y reciben identificadores diferentes.
13. Consultar una propuesta utilizando el identificador de otro proyecto
    devuelve 404 con `Feature proposal not found`.
14. Un proyecto existente sin propuestas devuelve 200 y `[]`.
15. El listado contiene únicamente propuestas del proyecto solicitado.
16. Las propuestas se ordenan por fecha de creación descendente y después por
    identificador ascendente.
17. Listar las propuestas de un proyecto inexistente devuelve 404 mediante
    Problem Details.
18. OpenAPI contiene las operaciones de creación, consulta y listado de
    propuestas.
19. La base de datos impide persistir una propuesta cuyo proyecto no existe.
20. Las propuestas permanecen disponibles entre scopes y reinicios usando la
    base SQLite persistente.

## Estrategia de pruebas

### Pruebas unitarias

- Creación válida y normalización.
- Conversión de descripción vacía a `null`.
- Conversión del instante a UTC y precisión de milisegundos.
- Rechazo de `Id` y `ProjectId` vacíos.
- Límites de título y descripción.
- Aceptación de títulos duplicados como ausencia deliberada de una regla de
  unicidad.

### Pruebas de integración

Se reutiliza `WebApplicationFactory` con SQLite relacional en memoria y un
`TimeProvider` controlable. Para comprobar persistencia entre reinicios se usa
un archivo SQLite temporal compartido por dos instancias sucesivas de la
aplicación.

Se comprueban los contratos POST y GET, normalización, validación, Problem
Details, aislamiento entre proyectos, títulos duplicados, orden determinista,
foreign key, persistencia y documento OpenAPI. No se utilizan mocks de EF Core
ni el proveedor EF Core InMemory.

## Impacto en persistencia

Se añadirá una tabla `FeatureProposals` con:

- clave primaria `Id`;
- foreign key obligatoria `ProjectId` hacia `Projects.Id`;
- eliminación restrictiva;
- `Title` requerido con longitud máxima 200;
- `Description` nullable con longitud máxima 4.000;
- `CreatedAtUtc` almacenado como milisegundos Unix;
- índice por `ProjectId` para las consultas del listado.

No se añadirá `NormalizedTitle` ni una restricción única sobre el título. El
cambio requiere una nueva migración, pero no nuevos paquetes o proveedores.

## Requisitos no funcionales

- Compilación sin warnings y nullable reference types habilitado.
- Operaciones asíncronas con `CancellationToken`.
- Consultas de lectura con `AsNoTracking`.
- Fechas ISO 8601 en UTC y precisión de milisegundos.
- Endpoints separados de las entidades de dominio mediante DTOs.
- Pruebas deterministas, aisladas y sin dependencia de la hora real.
- Ningún cambio en los contratos HTTP existentes de proyectos.
- Sin secretos ni archivos SQLite generados en Git.
- Compatibilidad conceptual con una futura sustitución de SQLite por
  PostgreSQL.

## Estructura prevista

- `src/SpecFlow.Domain/FeatureProposals`: entidad y reglas de dominio.
- `src/SpecFlow.Infrastructure/Persistence`: `DbSet`, configuración y migración.
- `src/SpecFlow.Api/Contracts/FeatureProposals`: request y response DTOs.
- `src/SpecFlow.Api/Endpoints/FeatureProposalEndpoints.cs`: rutas y orquestación.
- `tests/SpecFlow.Domain.Tests/FeatureProposals`: pruebas unitarias.
- `tests/SpecFlow.Api.IntegrationTests/FeatureProposals`: pruebas HTTP y de
  persistencia.

Se mantienen los tres proyectos de producción actuales y no se crea una capa
Application.

## Decisiones aprobadas

- `FeatureProposal` es una raíz independiente identificada globalmente.
- La asociación con el proyecto se representa mediante `ProjectId` inmutable.
- Sólo se exponen rutas anidadas bajo `/api/projects/{projectId}/proposals`.
- Los títulos duplicados están permitidos dentro del mismo proyecto.
- El título admite 200 caracteres y la descripción 4.000.
- El listado se ordena por `CreatedAtUtc` descendente y `Id` ascendente.
- Una propuesta inexistente o perteneciente a otro proyecto devuelve 404.
- Listar propuestas de un proyecto inexistente devuelve 404.
- Estados y transiciones se posponen para otra especificación.
- La foreign key utiliza eliminación restrictiva.
- El dominio no incorpora propiedades de navegación entre los dos agregados.

## Plan de implementación

1. Implementar `FeatureProposal` y sus pruebas unitarias.
2. Añadir el `DbSet`, la configuración de EF Core y generar la migración
   `AddFeatureProposals`.
3. Implementar los contratos y el endpoint de creación con sus pruebas.
4. Implementar la consulta anidada y sus respuestas de error.
5. Implementar el listado, aislamiento entre proyectos y orden determinista.
6. Verificar la foreign key y la persistencia entre reinicios.
7. Actualizar OpenAPI y README.
8. Ejecutar restauración, compilación, pruebas y formato.
9. Auditar cada criterio de aceptación antes de cambiar el estado de la
   especificación a implementada y verificada.
