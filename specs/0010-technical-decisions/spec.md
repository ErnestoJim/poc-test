# 0010 — Gestión básica de decisiones técnicas

## Estado

Implementada y verificada el 8 de octubre de 2026, tras su aprobación para
implementación el mismo día.

Verificación final:

- compilación sin warnings ni errores;
- 90 pruebas de dominio superadas;
- 241 pruebas de integración superadas;
- formato verificado sin cambios;
- modelo de EF Core sin migraciones pendientes.

## Problema

SpecFlow permite describir funcionalidades mediante propuestas,
especificaciones, criterios de aceptación y tareas de implementación, pero
todavía no puede registrar las decisiones técnicas tomadas durante el diseño y
desarrollo de un proyecto.

Como resultado, decisiones transversales como la elección de una estrategia de
persistencia, un contrato HTTP o una restricción arquitectónica quedan repartidas
entre especificaciones, tareas, commits o conversaciones externas. El sistema no
ofrece un registro consultable que explique qué se decidió y por qué.

## Objetivo

Proporcionar una API HTTP para crear, consultar, listar y editar decisiones
técnicas asociadas a un proyecto existente.

Cada decisión tendrá un título obligatorio y un contenido Markdown obligatorio.
El contenido será flexible para permitir documentar contexto, alternativas,
decisión y consecuencias sin imponer todavía una plantilla estructurada.

Este incremento establece el registro básico. Los estados, la aprobación, el
rechazo, la supersesión y las posibles reglas de eliminación se definirán en una
especificación posterior.

## Alcance

- Modelo de dominio `TechnicalDecision` como raíz independiente.
- Asociación obligatoria con un proyecto mediante `ProjectId`.
- Múltiples decisiones técnicas por proyecto.
- Título obligatorio y normalizado exteriormente.
- Títulos repetidos permitidos dentro del mismo proyecto.
- Contenido Markdown obligatorio y conservado sin transformaciones.
- Creación, consulta individual y listado cronológico.
- Sustitución completa de título y contenido mediante `PUT`.
- Fechas de creación y última actualización.
- Concurrencia optimista HTTP mediante ETag fuerte e `If-Match` para editar una
  decisión existente.
- Persistencia mediante Entity Framework Core y SQLite.
- Foreign key, restricciones e índices necesarios.
- Errores HTTP mediante Problem Details.
- Actualización del documento OpenAPI y del README.
- Pruebas unitarias y de integración.

## Fuera de alcance

- Estados como borrador, propuesta, aceptada, rechazada o supersedida.
- Transiciones de estado o reglas de aprobación.
- Relación entre una decisión y la decisión a la que reemplaza.
- Historial de cambios, revisiones o restauración de versiones anteriores.
- Numeración humana tipo ADR-001.
- Plantillas o secciones obligatorias para contexto, alternativas, decisión y
  consecuencias.
- Posición explícita o reordenación manual de decisiones.
- Unicidad de títulos.
- Eliminación física o lógica de decisiones.
- Asociación directa con propuestas, especificaciones, criterios de aceptación
  o tareas.
- Etiquetas, categorías, comentarios, adjuntos o enlaces externos estructurados.
- Edición parcial mediante `PATCH`.
- Listados globales o ajenos al contexto de un proyecto.
- Búsqueda, filtrado o paginación.
- Caché condicional mediante `If-None-Match` o respuestas `304 Not Modified`.
- Generación, revisión o clasificación mediante IA.
- Prompts, agentes, skills o MCP.
- Autenticación o autorización.
- Frontend, Docker, Aspire, despliegue o PostgreSQL.
- Una capa Application, CQRS, MediatR, event sourcing, repositorios genéricos o
  una unit of work propia.

## Casos de uso

### UC-01 — Crear una decisión técnica

Un consumidor proporciona un título y contenido Markdown para un proyecto
existente. El sistema valida el contexto y los datos, crea la decisión y la
devuelve con su ETag inicial.

### UC-02 — Consultar una decisión técnica

Un consumidor proporciona el identificador de un proyecto y el de una decisión.
El sistema la devuelve solamente si pertenece a ese proyecto e incluye su ETag
vigente.

### UC-03 — Listar decisiones técnicas

El sistema devuelve todas las decisiones de un proyecto en orden de creación
ascendente. Si todavía no existe ninguna, devuelve una colección vacía.

### UC-04 — Editar una decisión técnica

Un consumidor sustituye mediante `PUT` el título y el contenido completos de una
decisión. La petición debe incluir mediante `If-Match` el ETag obtenido al leer el
recurso.

## Modelo de dominio

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Id` | UUID | Sí | Identificador global generado por la aplicación |
| `ProjectId` | UUID | Sí | Proyecto al que pertenece |
| `Title` | string | Sí | Título visible sin espacios exteriores |
| `Content` | string | Sí | Documento Markdown conservado exactamente |
| `CreatedAtUtc` | DateTimeOffset | Sí | Instante UTC de creación |
| `UpdatedAtUtc` | DateTimeOffset | Sí | Instante UTC del último cambio real |
| `Version` | int | Sí | Versión interna utilizada para concurrencia |

`Version` no forma parte del JSON. Se representa ante el consumidor mediante un
ETag fuerte y opaco, conforme al contrato establecido en la especificación
0009.

`TechnicalDecision` es una raíz independiente. El dominio no expone una
navegación hacia `Project`, y `Project` no mantiene una colección de decisiones.
La relación se representa mediante `ProjectId` y se protege en persistencia.

## Reglas de negocio

1. `Id` no puede ser un UUID vacío.
2. `ProjectId` no puede ser un UUID vacío y es inmutable.
3. El proyecto debe existir antes de crear, consultar, listar o editar
   decisiones técnicas.
4. `Title` se normaliza eliminando espacios exteriores.
5. `Title` debe contener entre 1 y 200 caracteres después de normalizarlo.
6. Dos o más decisiones del mismo proyecto pueden compartir el mismo título,
   incluso si solo difieren en mayúsculas, minúsculas o espacios exteriores.
7. `Content` es obligatorio y debe contener al menos un carácter distinto de
   espacios en blanco.
8. `Content` puede contener como máximo 50.000 caracteres.
9. El contenido se conserva exactamente como lo proporciona el consumidor. No
   se eliminan espacios exteriores, no se normalizan saltos de línea y no se
   interpreta ni renderiza Markdown.
10. `CreatedAtUtc` se obtiene mediante `TimeProvider`, se convierte a UTC y se
    normaliza a precisión de milisegundos.
11. Al crear, `UpdatedAtUtc` tiene el mismo valor que `CreatedAtUtc` y `Version`
    comienza en cero.
12. Un `PUT` válido sustituye conjuntamente el título y el contenido.
13. Si el título visible o el contenido cambian, `UpdatedAtUtc` se actualiza y
    `Version` se incrementa una vez.
14. Si el título y el contenido enviados producen exactamente el mismo estado
    almacenado, `UpdatedAtUtc` y `Version` no cambian.
15. Editar requiere el ETag individual vigente de la decisión.
16. Una precondición ausente, no admitida u obsoleta no modifica el recurso.
17. El cliente no puede proporcionar identificadores, versiones ni fechas.
18. Las reglas de título, contenido y actualización pertenecen al dominio y no se
    duplican en los endpoints ni en persistencia.

## Contratos HTTP

Todas las rutas se anidan bajo:

`/api/projects/{projectId}/technical-decisions`

### Crear una decisión técnica

`POST /api/projects/{projectId}/technical-decisions`

Request:

```json
{
  "title": "Persistir con SQLite durante el POC",
  "content": "# Contexto\n\nNecesitamos persistencia relacional local.\n\n# Decisión\n\nUsar SQLite mediante EF Core."
}
```

Respuesta correcta: `201 Created`, cabecera `Location` apuntando al recurso,
cabecera `ETag` y:

```json
{
  "id": "2ff749e4-65ef-49c2-a557-9c8bb5300913",
  "projectId": "5fc297e9-cba9-47f4-862f-b984b53c317c",
  "title": "Persistir con SQLite durante el POC",
  "content": "# Contexto\n\nNecesitamos persistencia relacional local.\n\n# Decisión\n\nUsar SQLite mediante EF Core.",
  "createdAtUtc": "2026-10-08T08:30:00+00:00",
  "updatedAtUtc": "2026-10-08T08:30:00+00:00"
}
```

Errores específicos:

- `400 Bad Request` si el título o el contenido no son válidos.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

La creación no requiere `If-Match` porque todavía no existe un recurso
individual sobre el que expresar una precondición. Dos peticiones válidas con el
mismo título crean dos decisiones independientes.

### Consultar una decisión técnica

`GET /api/projects/{projectId}/technical-decisions/{decisionId}`

- `200 OK` con la decisión y su cabecera `ETag`.
- `400 Bad Request` si `decisionId` no es un UUID válido.
- `404 Not Found` con título `Technical decision not found` si no existe o no
  pertenece al proyecto indicado.

### Listar decisiones técnicas

`GET /api/projects/{projectId}/technical-decisions`

- `200 OK` con un array ordenado por `createdAtUtc` ascendente y después por
  `id`.
- Si el proyecto no tiene decisiones, devuelve `[]`.

El listado no expone un ETag de colección porque este incremento no incorpora
una operación condicionada sobre la colección completa.

### Editar una decisión técnica

`PUT /api/projects/{projectId}/technical-decisions/{decisionId}`

El request tiene el mismo contrato que la creación y representa el título y el
contenido completos deseados.

- `200 OK` con la decisión y su ETag vigente.
- `400 Bad Request` si el body, `decisionId` o `If-Match` no son válidos.
- `404 Not Found` con título `Technical decision not found` si no existe o no
  pertenece al proyecto.
- `412 Precondition Failed` si el ETag no corresponde al estado actual o una
  carrera posterior invalida la precondición.
- `428 Precondition Required` si falta `If-Match`.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

## Errores del contexto

Todos los endpoints comparten además:

- `400 Bad Request` si `projectId` no es un UUID válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.

Las respuestas usan `application/problem+json`. Los errores de validación
incluyen un diccionario `errors` indexado por `projectId`, `decisionId`, `title`
o `content`, según corresponda.

Las respuestas no revelan decisiones pertenecientes a otro proyecto ni exponen
stack traces, rutas locales, cadenas de conexión, versiones internas, detalles de
Entity Framework Core o información interna de SQLite.

## Criterios de aceptación

1. Crear una decisión válida responde 201 e incluye `Location` y un ETag fuerte.
2. La respuesta contiene `id`, `projectId`, `title`, `content`, `createdAtUtc` y
   `updatedAtUtc`, pero no expone la versión interna.
3. Al crear, las dos fechas coinciden en UTC con precisión de milisegundos.
4. Un título nulo, vacío o compuesto por espacios devuelve 400.
5. Un título de más de 200 caracteres después de eliminar espacios exteriores
   devuelve 400.
6. El título se persiste y devuelve sin espacios exteriores.
7. Dos decisiones del mismo proyecto pueden tener el mismo título.
8. Un contenido nulo, vacío o compuesto únicamente por espacios devuelve 400.
9. Un contenido de más de 50.000 caracteres devuelve 400.
10. El Markdown se persiste y devuelve sin normalizaciones ni cambios.
11. Una decisión puede consultarse mediante su ruta anidada y la respuesta
    incluye su ETag vigente.
12. Consultar una decisión mediante otro proyecto devuelve 404 con
    `Technical decision not found`.
13. Un listado vacío devuelve 200 y `[]`.
14. El listado devuelve todas las decisiones por fecha de creación ascendente y
    después por identificador.
15. Un `PUT` válido con el ETag vigente sustituye título y contenido, conserva
    identificadores y fecha de creación, actualiza `updatedAtUtc` y devuelve un
    nuevo ETag.
16. Un `PUT` idéntico conserva `updatedAtUtc` y devuelve el mismo ETag.
17. Editar sin `If-Match` devuelve 428 y no modifica el recurso.
18. Editar con un `If-Match` no admitido devuelve 400 y no modifica el recurso.
19. Editar con un ETag obsoleto devuelve 412 y no modifica el recurso.
20. Dos ediciones concurrentes basadas en el mismo ETag no pueden completarse
    ambas; una devuelve 412.
21. Un `projectId` o `decisionId` con formato inválido devuelve 400 mediante
    Validation Problem Details.
22. Un proyecto inexistente devuelve 404 con `Project not found`.
23. Utilizar otro proyecto no revela la existencia de la decisión.
24. Los endpoints que reciben body devuelven 415 para contenido no JSON.
25. La base de datos impide decisiones asociadas a proyectos inexistentes.
26. OpenAPI documenta las cuatro operaciones, sus ETags, precondiciones y
    respuestas.
27. Las decisiones y sus actualizaciones permanecen disponibles entre scopes y
    reinicios.
28. Los 25 contratos HTTP existentes no cambian.

## Estrategia de pruebas

### Pruebas unitarias

- Creación válida, normalización del título y conservación exacta del Markdown.
- Rechazo de identificadores, título o contenido inválidos.
- Normalización temporal y coincidencia inicial de fechas.
- Sustitución completa de título y contenido.
- Actualización condicional de fecha y versión.
- Ausencia de cambios ante un `PUT` idéntico.

### Pruebas de integración

Se reutiliza `WebApplicationFactory` con SQLite relacional en memoria y un
`TimeProvider` controlable. Los escenarios concurrentes y la persistencia entre
reinicios utilizan archivos SQLite temporales.

Se comprueban los contratos HTTP, validación, Problem Details, contexto de
proyecto, títulos repetidos, preservación de Markdown, orden del listado, ETags,
precondiciones, carreras, foreign key, persistencia y OpenAPI. No se utilizan
mocks de EF Core ni el proveedor EF Core InMemory.

## Impacto en persistencia

Se añadirá una tabla `TechnicalDecisions` con:

- clave primaria `Id`;
- foreign key obligatoria `ProjectId` hacia `Projects.Id`;
- eliminación restrictiva;
- `Title` requerido con longitud máxima de 200 caracteres;
- `Content` requerido con longitud máxima de 50.000 caracteres;
- `CreatedAtUtc` y `UpdatedAtUtc` almacenados como milisegundos Unix;
- `Version` requerido y configurado como concurrency token;
- índice para el listado por `ProjectId`, `CreatedAtUtc` e `Id`.

No existe un índice único sobre el título. El cambio requiere una migración
`AddTechnicalDecisions`, pero no nuevos paquetes o proveedores.

No se duplican `FeatureProposalId` ni `SpecificationId`. Las futuras relaciones
de trazabilidad se definirán mediante una especificación independiente si
aparecen casos de uso que las necesiten.

## Requisitos no funcionales

- Compilación sin warnings y nullable reference types habilitado.
- Operaciones asíncronas con `CancellationToken`.
- Consultas de lectura con `AsNoTracking`.
- Fechas ISO 8601 en UTC y precisión de milisegundos.
- Endpoints separados de las entidades de dominio mediante DTOs.
- Markdown tratado como contenido opaco, sin ejecución ni renderizado.
- ETags fuertes, opacos y no incluidos en el JSON.
- Pruebas deterministas, aisladas y sin dependencia de la hora real.
- Ningún cambio en los contratos HTTP existentes.
- Sin secretos ni archivos SQLite generados en Git.
- Compatibilidad conceptual con una futura sustitución de SQLite por
  PostgreSQL.

## Estructura prevista

- `src/SpecFlow.Domain/TechnicalDecisions`: entidad y reglas de dominio.
- `src/SpecFlow.Infrastructure/Persistence`: `DbSet`, configuración y migración.
- `src/SpecFlow.Api/Contracts/TechnicalDecisions`: contratos de request y
  response.
- `src/SpecFlow.Api/Endpoints/TechnicalDecisionEndpoints.cs`: rutas y
  orquestación HTTP.
- `tests/SpecFlow.Domain.Tests/TechnicalDecisions`: pruebas unitarias.
- `tests/SpecFlow.Api.IntegrationTests/TechnicalDecisions`: pruebas HTTP,
  persistencia y concurrencia.

Se mantienen los tres proyectos de producción actuales y no se crea una capa
Application.

## Decisiones acordadas

- Cada decisión pertenece a un proyecto porque puede afectar a varias
  funcionalidades y sobrevivir a una propuesta o especificación concreta.
- Una decisión contiene un título y un único cuerpo Markdown flexible.
- Los títulos pueden repetirse; el UUID es la identidad del recurso.
- El listado utiliza orden cronológico y no incorpora posiciones, reordenación
  ni numeración ADR.
- Este incremento no permite eliminar decisiones. Las reglas de eliminación se
  decidirán junto con su ciclo de vida.
- La edición utiliza ETags e `If-Match` desde el primer incremento.
- Estados, aprobación, rechazo y supersesión quedan para una especificación
  posterior.

## Plan de implementación

1. Implementar `TechnicalDecision` y sus pruebas unitarias.
2. Añadir el `DbSet`, la configuración de EF Core y generar la migración
   `AddTechnicalDecisions`.
3. Implementar contratos y creación.
4. Implementar consulta individual y listado cronológico.
5. Implementar edición condicionada mediante ETag.
6. Verificar foreign key, aislamiento entre proyectos, carreras y persistencia.
7. Actualizar OpenAPI y README.
8. Ejecutar restauración, compilación, pruebas y formato.
9. Auditar cada criterio de aceptación antes de cambiar el estado de la
   especificación a implementada y verificada.
