# 0004 — Gestión básica de especificaciones

## Estado

Aprobada para implementación el 6 de octubre de 2026.

## Problema

SpecFlow permite registrar propuestas y decidir cuáles se aceptan, pero una
propuesta aceptada todavía no puede avanzar hacia una especificación que
describa con detalle el comportamiento que se quiere construir. El contenido
permanece fuera del sistema y no puede consultarse ni refinarse mediante la API.

## Objetivo

Proporcionar una API HTTP para crear, consultar y editar la especificación de
una propuesta aceptada. Cada propuesta podrá tener como máximo una
especificación y su contenido se almacenará como Markdown para admitir
documentos flexibles sin imponer prematuramente una estructura fija.

La creación seguirá siendo explícita: aceptar una propuesta no generará una
especificación automáticamente. El formato permitirá una futura generación o
edición asistida por IA, pero este incremento no incorporará modelos, prompts ni
automatizaciones.

La relación uno a uno responde a la necesidad actual y no se considera una
limitación permanente del producto. Una especificación posterior podrá permitir
múltiples especificaciones o variantes por propuesta mediante cambios explícitos
en el dominio, la persistencia y el contrato HTTP.

## Alcance

- Modelo de dominio `Specification` como raíz independiente.
- Asociación obligatoria con una propuesta mediante `FeatureProposalId`.
- Relación uno a uno: una propuesta puede tener como máximo una especificación.
- Creación únicamente para propuestas en estado `Accepted`.
- Consulta mediante una ruta singular anidada bajo la propuesta.
- Sustitución completa del contenido mediante `PUT`.
- Contenido Markdown obligatorio y conservado sin transformaciones.
- Registro de las fechas de creación y última actualización.
- Persistencia mediante Entity Framework Core y SQLite.
- Foreign key con eliminación restrictiva e índice único.
- Errores HTTP mediante Problem Details.
- Actualización del documento OpenAPI.
- Pruebas unitarias y de integración.

## Fuera de alcance

- Creación automática al aceptar una propuesta.
- Más de una especificación por propuesta.
- Especificaciones para propuestas pendientes o rechazadas.
- Estados, aprobación o rechazo de especificaciones.
- Versionado, historial de cambios, revisiones o restauración de versiones.
- Edición parcial mediante `PATCH`.
- Control de edición mediante ETags o versiones expuestas por HTTP.
- Listados globales o por proyecto de especificaciones.
- Búsqueda, filtrado o paginación.
- Plantillas o secciones estructuradas obligatorias.
- Renderizado de Markdown o conversión a HTML.
- Generación, revisión o modificación mediante IA.
- Prompts, agentes, skills o MCP.
- Tareas, criterios de aceptación o decisiones técnicas asociadas.
- Comentarios, adjuntos, etiquetas o notificaciones.
- Eliminación de especificaciones.
- Autenticación o autorización.
- Frontend, Docker, Aspire, despliegue o PostgreSQL.
- Una capa Application, CQRS, MediatR o repositorios genéricos.

Los ETags deberán reconsiderarse cuando exista un caso de uso concreto de
edición concurrente, versionado o modificación por múltiples consumidores o
agentes.

## Casos de uso

### UC-01 — Crear una especificación

Un consumidor proporciona contenido Markdown para una propuesta aceptada. El
sistema valida el proyecto, la propuesta, su estado y que todavía no exista una
especificación asociada. Después crea y persiste la especificación y devuelve el
recurso creado.

### UC-02 — Consultar una especificación

Un consumidor consulta la especificación de una propuesta concreta. El sistema
la devuelve solamente si el proyecto y la propuesta corresponden con la ruta y
la especificación existe.

### UC-03 — Editar una especificación

Un consumidor sustituye mediante `PUT` el contenido Markdown completo de una
especificación existente. Si el contenido cambia, el sistema registra un nuevo
instante de actualización y devuelve el recurso actualizado.

Enviar exactamente el mismo contenido no modifica `UpdatedAtUtc`. De este modo,
repetir una misma petición `PUT` produce el mismo estado observable.

## Modelo de dominio

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Id` | UUID | Sí | Identificador global generado por la aplicación |
| `FeatureProposalId` | UUID | Sí | Propuesta aceptada a la que pertenece |
| `Content` | string | Sí | Documento Markdown |
| `CreatedAtUtc` | DateTimeOffset | Sí | Instante UTC de creación |
| `UpdatedAtUtc` | DateTimeOffset | Sí | Instante UTC de la última modificación real |

`Specification` es una raíz independiente. El modelo de dominio no expone una
navegación hacia `FeatureProposal`, y `FeatureProposal` no mantiene una
propiedad de navegación hacia la especificación. La relación se representa
mediante `FeatureProposalId` y se protege en persistencia.

No se almacena un título independiente: durante este incremento, la propuesta y
su título identifican el propósito de la especificación.

## Reglas de negocio

1. `Id` no puede ser un UUID vacío.
2. `FeatureProposalId` no puede ser un UUID vacío y es inmutable.
3. Una propuesta puede tener como máximo una especificación.
4. La propuesta debe existir y estar en estado `Accepted` al crear la
   especificación.
5. Aceptar una propuesta no crea automáticamente una especificación.
6. `Content` es obligatorio y debe contener al menos un carácter distinto de
   espacios en blanco.
7. `Content` puede contener como máximo 100.000 caracteres.
8. El contenido se conserva exactamente como lo proporciona el consumidor. No
   se eliminan espacios exteriores, no se normalizan saltos de línea y no se
   interpreta ni renderiza Markdown.
9. `CreatedAtUtc` se obtiene mediante `TimeProvider`, se convierte a UTC y se
   normaliza a precisión de milisegundos.
10. Al crear, `UpdatedAtUtc` tiene el mismo valor que `CreatedAtUtc`.
11. Un `PUT` válido sustituye todo el contenido existente.
12. Cuando el contenido cambia, `UpdatedAtUtc` se obtiene mediante
    `TimeProvider`, se convierte a UTC y se normaliza a precisión de
    milisegundos.
13. `UpdatedAtUtc` no cambia si el nuevo contenido es idéntico al almacenado,
    utilizando comparación ordinal.
14. Una actualización no modifica `Id`, `FeatureProposalId` ni `CreatedAtUtc`.
15. El cliente no puede proporcionar identificadores ni fechas en el body.
16. Las reglas de contenido y actualización pertenecen al dominio y no se
    duplican en los endpoints ni en la persistencia.

## Contratos HTTP

### Crear una especificación

`POST /api/projects/{projectId}/proposals/{proposalId}/specification`

Request:

```json
{
  "content": "# Añadir criterios de aceptación\n\n## Objetivo\n\nPermitir criterios verificables."
}
```

Respuesta correcta: `201 Created`, cabecera `Location` apuntando al recurso y:

```json
{
  "id": "5d346b9e-3449-4f79-ab86-a17af587689b",
  "featureProposalId": "bd52c30c-b9c6-4975-b58e-548a3bac634c",
  "content": "# Añadir criterios de aceptación\n\n## Objetivo\n\nPermitir criterios verificables.",
  "createdAtUtc": "2026-10-07T08:30:00+00:00",
  "updatedAtUtc": "2026-10-07T08:30:00+00:00"
}
```

Errores:

- `400 Bad Request` si algún identificador no es un UUID válido o el contenido
  no es válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.
- `404 Not Found` con título `Feature proposal not found` si la propuesta no
  existe o pertenece a otro proyecto.
- `409 Conflict` con título `Feature proposal is not accepted` si la propuesta
  está pendiente o rechazada.
- `409 Conflict` con título `Specification already exists` si la propuesta ya
  tiene una especificación, incluso ante creaciones simultáneas.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

### Consultar una especificación

`GET /api/projects/{projectId}/proposals/{proposalId}/specification`

- `200 OK` con la especificación.
- `400 Bad Request` si algún identificador no es un UUID válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.
- `404 Not Found` con título `Feature proposal not found` si la propuesta no
  existe o pertenece a otro proyecto.
- `404 Not Found` con título `Specification not found` si la propuesta existe
  pero todavía no tiene una especificación.

Consultar una especificación no vuelve a validar el estado actual de la
propuesta. Durante este incremento `Accepted` es final, pero la especificación
debe seguir siendo consultable si el ciclo de vida se amplía posteriormente.

### Editar una especificación

`PUT /api/projects/{projectId}/proposals/{proposalId}/specification`

El request tiene el mismo contrato que la creación y representa el contenido
completo deseado.

Respuesta correcta: `200 OK` con la especificación actualizada.

Errores:

- `400 Bad Request` si algún identificador no es un UUID válido o el contenido
  no es válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.
- `404 Not Found` con título `Feature proposal not found` si la propuesta no
  existe o pertenece a otro proyecto.
- `404 Not Found` con título `Specification not found` si todavía no existe.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

La actualización no vuelve a validar que la propuesta esté aceptada. La regla
de estado se aplica a la creación; una especificación existente sigue siendo
editable aunque futuras especificaciones amplíen el ciclo de vida de la
propuesta.

## Respuestas de error

Los errores utilizan `application/problem+json`. Los errores de validación
incluyen un diccionario `errors` indexado por `projectId`, `proposalId` o
`content`, según corresponda.

Las rutas mantienen el aislamiento entre proyectos: una propuesta consultada
mediante otro proyecto se presenta como no encontrada y no se revela si tiene
una especificación.

Las respuestas no exponen stack traces, rutas locales, cadenas de conexión,
detalles de Entity Framework Core ni información interna de SQLite.

## Criterios de aceptación

1. Una petición válida para una propuesta aceptada crea una especificación,
   responde 201 e incluye `Location`.
2. La respuesta de creación contiene `id`, `featureProposalId`, `content`,
   `createdAtUtc` y `updatedAtUtc`.
3. Al crear, `createdAtUtc` y `updatedAtUtc` tienen el mismo valor UTC con
   precisión de milisegundos.
4. Aceptar una propuesta no crea automáticamente una especificación.
5. Crear una especificación para una propuesta pendiente devuelve 409 con
   `Feature proposal is not accepted`.
6. Crear una especificación para una propuesta rechazada devuelve 409 con
   `Feature proposal is not accepted`.
7. Crear una segunda especificación para la misma propuesta devuelve 409 con
   `Specification already exists`.
8. Dos creaciones simultáneas para la misma propuesta no pueden completarse
   ambas; una devuelve 409.
9. La especificación creada puede recuperarse mediante la ruta anidada.
10. Consultar una propuesta existente sin especificación devuelve 404 con
    `Specification not found`.
11. Crear, consultar o editar utilizando otro proyecto devuelve 404 con
    `Feature proposal not found`.
12. Un `projectId` o `proposalId` con formato inválido devuelve 400 mediante
    Validation Problem Details.
13. Un contenido nulo, vacío o compuesto únicamente por espacios devuelve 400.
14. Un contenido de más de 100.000 caracteres devuelve 400.
15. El contenido válido se persiste y devuelve sin normalizaciones ni cambios.
16. Un `PUT` válido sustituye completamente el contenido y responde 200.
17. Un `PUT` que cambia el contenido conserva `createdAtUtc` y actualiza
    `updatedAtUtc` mediante el reloj controlable.
18. Repetir un `PUT` con contenido idéntico no modifica `updatedAtUtc`.
19. Editar una propuesta sin especificación devuelve 404 con
    `Specification not found`.
20. Los endpoints que reciben body devuelven 415 para un contenido no JSON
    compatible.
21. OpenAPI contiene las operaciones `POST`, `GET` y `PUT` y sus contratos.
22. La base de datos impide especificaciones duplicadas o asociadas a propuestas
    inexistentes.
23. La especificación y sus actualizaciones permanecen disponibles entre scopes
    y reinicios usando la base SQLite persistente.
24. Los contratos HTTP existentes de proyectos y propuestas no cambian.

## Estrategia de pruebas

### Pruebas unitarias

- Creación válida y conservación exacta del Markdown.
- Rechazo de identificadores vacíos.
- Rechazo de contenido ausente o demasiado largo.
- Normalización temporal y coincidencia inicial de fechas.
- Sustitución completa del contenido.
- Actualización de `UpdatedAtUtc` solamente cuando cambia el contenido.
- Inmutabilidad de identificadores y fecha de creación.

### Pruebas de integración

Se reutiliza `WebApplicationFactory` con SQLite relacional en memoria y un
`TimeProvider` controlable. Para comprobar persistencia entre reinicios se usa
un archivo SQLite temporal compartido por dos instancias sucesivas de la
aplicación.

Se comprueban los contratos `POST`, `GET` y `PUT`, la validación, Problem
Details, el requisito de propuesta aceptada, la relación uno a uno, las
creaciones simultáneas, el aislamiento entre proyectos, la foreign key, la
persistencia y el documento OpenAPI. No se utilizan mocks de EF Core ni el
proveedor EF Core InMemory.

## Impacto en persistencia

Se añadirá una tabla `Specifications` con:

- clave primaria `Id`;
- foreign key obligatoria `FeatureProposalId` hacia `FeatureProposals.Id`;
- índice único sobre `FeatureProposalId`;
- eliminación restrictiva;
- `Content` requerido con longitud máxima de 100.000 caracteres;
- `CreatedAtUtc` y `UpdatedAtUtc` almacenados como milisegundos Unix.

No se duplicará `ProjectId` en `Specifications`. La pertenencia al proyecto se
obtiene a través de la propuesta y se valida en las consultas HTTP.

El cambio requiere una nueva migración, pero no nuevos paquetes o proveedores.

La restricción única representa la cardinalidad aprobada para esta fase. No se
introducen abstracciones para una cardinalidad futura; si el producto necesita
múltiples especificaciones por propuesta, la restricción y las rutas se
revisarán mediante una nueva especificación y migración.

## Requisitos no funcionales

- Compilación sin warnings y nullable reference types habilitado.
- Operaciones asíncronas con `CancellationToken`.
- Consultas de lectura con `AsNoTracking`.
- Fechas ISO 8601 en UTC y precisión de milisegundos.
- Endpoints separados de las entidades de dominio mediante DTOs.
- Markdown tratado como contenido opaco, sin ejecución ni renderizado.
- Pruebas deterministas, aisladas y sin dependencia de la hora real.
- Ningún cambio en los contratos HTTP existentes.
- Sin secretos ni archivos SQLite generados en Git.
- Compatibilidad conceptual con una futura sustitución de SQLite por
  PostgreSQL.

## Estructura prevista

- `src/SpecFlow.Domain/Specifications`: entidad y reglas de dominio.
- `src/SpecFlow.Infrastructure/Persistence`: `DbSet`, configuración y migración.
- `src/SpecFlow.Api/Contracts/Specifications`: request y response DTOs.
- `src/SpecFlow.Api/Endpoints/SpecificationEndpoints.cs`: rutas y orquestación.
- `tests/SpecFlow.Domain.Tests/Specifications`: pruebas unitarias.
- `tests/SpecFlow.Api.IntegrationTests/Specifications`: pruebas HTTP y de
  persistencia.

Se mantienen los tres proyectos de producción actuales y no se crea una capa
Application.

## Decisiones acordadas

- Una especificación se almacena como un documento Markdown flexible.
- La API permite editar el contenido mediante `PUT`.
- `PUT` reemplaza el contenido completo; no se incorpora `PATCH`.
- La futura asistencia mediante IA queda fuera de este incremento.
- Aceptar una propuesta no crea automáticamente su especificación.
- Cada propuesta puede tener como máximo una especificación.
- La relación uno a uno puede ampliarse mediante una especificación futura; no
  se implementa ahora una abstracción para múltiples especificaciones.
- Solo las propuestas aceptadas permiten crear una especificación.
- Se utiliza una ruta singular `/specification` anidada bajo la propuesta.
- No se almacena un título separado; se utiliza el título de la propuesta.
- El contenido admite como máximo 100.000 caracteres y se conserva exactamente.
- `CreatedAtUtc` y `UpdatedAtUtc` se exponen en el contrato.
- Un `PUT` con contenido idéntico conserva `UpdatedAtUtc`.
- No se incorporan ETags, historial ni control de versiones durante esta fase.
- Los ETags se reconsiderarán cuando exista edición concurrente, versionado o
  participación de múltiples consumidores o agentes.

## Plan de implementación

1. Implementar `Specification` y sus pruebas unitarias.
2. Añadir el `DbSet`, la configuración de EF Core y generar la migración
   `AddSpecifications`.
3. Implementar contratos y creación para propuestas aceptadas con sus pruebas.
4. Implementar la consulta singular y el aislamiento entre proyectos.
5. Implementar la sustitución mediante `PUT` y las reglas temporales.
6. Verificar unicidad, foreign key y creaciones simultáneas.
7. Verificar persistencia entre reinicios.
8. Actualizar OpenAPI y README.
9. Ejecutar restauración, compilación, pruebas y formato.
10. Auditar cada criterio de aceptación antes de cambiar el estado de la
    especificación a implementada y verificada.
