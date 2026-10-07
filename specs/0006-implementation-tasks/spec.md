# 0006 — Planificación de tareas de implementación

## Estado

Aprobada para implementación el 7 de octubre de 2026.

## Problema

SpecFlow permite convertir una propuesta aceptada en una especificación y
describir por separado sus criterios de aceptación, pero todavía no puede
descomponer el trabajo en tareas de implementación ordenadas. La planificación
permanece fuera del sistema y no existe una secuencia persistente que indique
qué trabajo técnico se pretende realizar.

## Objetivo

Proporcionar una API HTTP para crear, consultar, listar, editar, reordenar y
eliminar permanentemente tareas de implementación asociadas a una
especificación. Cada tarea tendrá un título obligatorio, una descripción
Markdown opcional y una posición explícita dentro de su especificación.

El incremento debe impedir títulos duplicados dentro de una misma
especificación y mantener un orden estable. Las tareas representan únicamente
la planificación inicial: todavía no incorporan estados de ejecución,
asignaciones, estimaciones, dependencias ni relaciones con criterios de
aceptación.

## Alcance

- Modelo de dominio `ImplementationTask` como raíz independiente.
- Asociación obligatoria con una especificación mediante `SpecificationId`.
- Múltiples tareas por especificación.
- Título obligatorio, normalizado exteriormente y único sin distinguir
  mayúsculas y minúsculas dentro de una especificación.
- Descripción Markdown opcional y conservada sin transformaciones.
- Posición explícita, consecutiva y comenzando en 1.
- Creación al final de la colección.
- Consulta individual y listado ordenado.
- Sustitución completa de título y descripción mediante `PUT`.
- Reordenación atómica de toda la colección enviando únicamente sus
  identificadores.
- Eliminación física y reajuste de las posiciones restantes.
- Fechas de creación y última actualización.
- Versiones internas para proteger ediciones y cambios concurrentes de la
  colección, sin exponerlas por HTTP.
- Persistencia mediante Entity Framework Core y SQLite.
- Foreign key, restricciones e índices necesarios.
- Errores HTTP mediante Problem Details.
- Actualización del documento OpenAPI.
- Pruebas unitarias y de integración.

## Fuera de alcance

- Estados de tarea como pendiente, en progreso, completada o cancelada.
- Transiciones de estado, reapertura o historial de ejecución.
- Asociación entre tareas y criterios de aceptación.
- Dependencias o bloqueos entre tareas.
- Asignaciones a personas o agentes.
- Prioridades, estimaciones, fechas objetivo o registro de tiempo.
- Subtareas, jerarquías o agrupaciones.
- Resultados, evidencias, comentarios, adjuntos o etiquetas.
- Versionado, auditoría o recuperación después de eliminar.
- Eliminación lógica.
- Edición parcial mediante `PATCH`.
- ETags o versiones expuestas mediante HTTP.
- Generación, división, reescritura o ejecución mediante IA.
- Prompts, agentes, skills o MCP.
- Listados globales o ajenos al contexto de una especificación.
- Búsqueda, filtrado o paginación.
- Autenticación o autorización.
- Frontend, Docker, Aspire, despliegue o PostgreSQL.
- Una capa Application, CQRS, MediatR o repositorios genéricos.

Los estados y la relación con criterios de aceptación se definirán en una
especificación posterior cuando existan sus casos de uso y transiciones
concretas. Los ETags deberán reconsiderarse si la edición concurrente por
múltiples consumidores o agentes requiere una precondición explícita en el
contrato HTTP.

## Casos de uso

### UC-01 — Crear una tarea

Un consumidor proporciona un título y, opcionalmente, una descripción Markdown
para una especificación existente. El sistema valida el contexto, comprueba que
el título no esté utilizado por otra tarea de esa especificación, asigna la
siguiente posición disponible y devuelve la tarea creada.

### UC-02 — Consultar una tarea

Un consumidor proporciona el contexto de proyecto, propuesta y especificación,
además del identificador de la tarea. El sistema devuelve la tarea solamente si
pertenece a esa especificación.

### UC-03 — Listar tareas

El sistema devuelve todas las tareas de una especificación ordenadas por su
posición. Si todavía no existe ninguna, devuelve una colección vacía.

### UC-04 — Editar una tarea

Un consumidor sustituye mediante `PUT` el título y la descripción completos de
una tarea existente. El nuevo título no puede duplicar el de otra tarea de la
misma especificación.

### UC-05 — Reordenar tareas

Un consumidor envía todos los identificadores de la colección en el orden
deseado. El sistema valida que cada tarea aparezca exactamente una vez y
actualiza la colección de forma atómica.

### UC-06 — Eliminar una tarea

Un consumidor elimina permanentemente una tarea. Las tareas posteriores se
desplazan para que las posiciones continúen siendo consecutivas.

## Modelo de dominio

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Id` | UUID | Sí | Identificador global generado por la aplicación |
| `SpecificationId` | UUID | Sí | Especificación a la que pertenece |
| `Title` | string | Sí | Título visible sin espacios exteriores |
| `Description` | string nullable | No | Descripción Markdown conservada exactamente |
| `Position` | int | Sí | Posición dentro de la especificación, comenzando en 1 |
| `CreatedAtUtc` | DateTimeOffset | Sí | Instante UTC de creación |
| `UpdatedAtUtc` | DateTimeOffset | Sí | Instante UTC del último cambio real |

Persistencia incorpora además `NormalizedTitle`, calculado aplicando
`ToUpperInvariant` al título después de eliminar sus espacios exteriores. No
forma parte del contrato HTTP y se utiliza junto con `SpecificationId` para
proteger la unicidad incluso ante operaciones simultáneas. El consumidor sigue
recibiendo el título con las mayúsculas y minúsculas que proporcionó.

Cada tarea mantiene también una versión de concurrencia interna. La
especificación incorpora un contador interno independiente para la colección de
tareas. Estos valores no forman parte del contrato HTTP.

`ImplementationTask` es una raíz independiente. El dominio no expone
navegaciones hacia `Specification`, y `Specification` no mantiene una colección
de tareas. La relación se representa mediante `SpecificationId` y se protege en
persistencia.

## Reglas de negocio

1. `Id` no puede ser un UUID vacío.
2. `SpecificationId` no puede ser un UUID vacío y es inmutable.
3. La especificación debe existir antes de crear, consultar, listar, editar,
   reordenar o eliminar tareas.
4. `Title` se normaliza eliminando espacios exteriores.
5. `Title` debe contener entre 1 y 200 caracteres después de normalizarlo.
6. Dos tareas de la misma especificación no pueden compartir
   `NormalizedTitle`; por tanto, la unicidad ignora diferencias de mayúsculas y
   minúsculas después de eliminar espacios exteriores.
7. El mismo título está permitido en especificaciones diferentes.
8. `Description` puede ser `null`. Si se proporciona, debe contener al menos un
   carácter distinto de espacios en blanco.
9. `Description` puede contener como máximo 10.000 caracteres.
10. La descripción se conserva exactamente como la proporciona el consumidor.
    No se eliminan espacios exteriores, no se normalizan saltos de línea y no
    se interpreta ni renderiza Markdown.
11. `Position` debe ser mayor que cero.
12. La primera tarea de una especificación recibe la posición 1.
13. Cada nueva tarea se añade al final con la posición máxima existente más
    uno.
14. Dentro de una especificación las posiciones son únicas y consecutivas.
15. `CreatedAtUtc` se obtiene mediante `TimeProvider`, se convierte a UTC y se
    normaliza a precisión de milisegundos.
16. Al crear, `UpdatedAtUtc` tiene el mismo valor que `CreatedAtUtc`.
17. Un `PUT` válido sustituye conjuntamente el título y la descripción.
18. Si el título normalizado, las mayúsculas o minúsculas visibles del título o
    la descripción cambian, `UpdatedAtUtc` se actualiza desde `TimeProvider`.
19. Si el título y la descripción enviados producen exactamente el mismo estado
    almacenado, `UpdatedAtUtc` no cambia.
20. Enviar `description: null` mediante `PUT` elimina una descripción existente.
21. Reordenar modifica únicamente `Position` y `UpdatedAtUtc` de las tareas cuya
    posición cambia.
22. Todas las tareas movidas por la misma reordenación comparten el mismo
    `UpdatedAtUtc`.
23. La reordenación debe incluir todos los identificadores actuales exactamente
    una vez; no permite añadir, omitir ni duplicar identificadores.
24. Una reordenación idéntica al orden almacenado no modifica ninguna fecha.
25. Al eliminar una tarea, las posiciones posteriores disminuyen en uno y sus
    fechas `UpdatedAtUtc` registran el instante de compactación.
26. El cliente no puede proporcionar `Id`, `SpecificationId`, `Position`,
    títulos normalizados, versiones ni fechas al crear o editar.
27. Las operaciones que modifican toda la colección se realizan en una
    transacción para evitar estados parcialmente actualizados.
28. Crear, reordenar o eliminar incrementa la versión interna de la colección,
    incluso cuando una reordenación válida conserva el mismo orden.
29. Cambiar el título, la descripción o la posición de una tarea incrementa su
    versión de concurrencia interna.

## Contratos HTTP

Todas las rutas se anidan bajo:

`/api/projects/{projectId}/proposals/{proposalId}/specification/tasks`

Antes de operar sobre tareas, el sistema aplica las reglas existentes de
existencia y aislamiento para proyecto, propuesta y especificación. No vuelve a
validar el estado actual de la propuesta: una especificación existente continúa
siendo utilizable si el ciclo de vida de propuestas se amplía posteriormente.

### Crear una tarea

`POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks`

Request:

```json
{
  "title": "Implementar la entidad de dominio",
  "description": "Crear `ImplementationTask` y cubrir sus invariantes con pruebas."
}
```

Respuesta correcta: `201 Created`, cabecera `Location` apuntando a la tarea y:

```json
{
  "id": "d92ed73f-9225-40ac-99b8-cc36ac87afff",
  "specificationId": "5d346b9e-3449-4f79-ab86-a17af587689b",
  "title": "Implementar la entidad de dominio",
  "description": "Crear `ImplementationTask` y cubrir sus invariantes con pruebas.",
  "position": 1,
  "createdAtUtc": "2026-10-08T08:30:00+00:00",
  "updatedAtUtc": "2026-10-08T08:30:00+00:00"
}
```

Errores específicos:

- `400 Bad Request` si el título o la descripción no son válidos.
- `409 Conflict` con título `Implementation task already exists` si otra tarea
  de la especificación tiene el mismo título normalizado, incluso ante
  creaciones simultáneas.
- `409 Conflict` con título `Implementation tasks collection changed` si una
  creación simultánea con otro título modifica posición o versión.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

### Consultar una tarea

`GET /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`

- `200 OK` con la tarea.
- `400 Bad Request` si `taskId` no es un UUID válido.
- `404 Not Found` con título `Implementation task not found` si no existe o no
  pertenece a la especificación indicada.

### Listar tareas

`GET /api/projects/{projectId}/proposals/{proposalId}/specification/tasks`

- `200 OK` con un array ordenado por `position` ascendente y después por `id`.
- Si la especificación no tiene tareas, devuelve `[]`.

### Editar una tarea

`PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`

El request tiene el mismo contrato que la creación y representa el título y la
descripción completos deseados.

- `200 OK` con la tarea actualizada.
- `400 Bad Request` si el título, la descripción o `taskId` no son válidos.
- `404 Not Found` con título `Implementation task not found` si no existe o no
  pertenece a la especificación.
- `409 Conflict` con título `Implementation task already exists` si el nuevo
  título duplica el de otra tarea de la especificación.
- `409 Conflict` con título `Implementation task update conflict` si la tarea
  cambia concurrentemente durante la edición.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

### Reordenar tareas

`PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/order`

Request:

```json
{
  "taskIds": [
    "22831d74-f8bd-44f6-a53b-d4aa06e4d786",
    "d92ed73f-9225-40ac-99b8-cc36ac87afff"
  ]
}
```

Respuesta correcta: `204 No Content`.

Errores específicos:

- `400 Bad Request` mediante Validation Problem Details si la colección contiene
  UUID vacíos o repetidos.
- `409 Conflict` con título `Implementation tasks collection changed` si los
  identificadores no coinciden exactamente con la colección actual o si esta
  cambia concurrentemente durante la operación.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

El request contiene únicamente los identificadores. No requiere enviar títulos,
descripciones, posiciones, fechas ni versiones, y la respuesta no devuelve la
colección. Si un consumidor necesita obtener el estado actualizado puede usar
el endpoint de listado de forma independiente.

Enviar una colección vacía es válido únicamente si la especificación no contiene
tareas. En ese caso también se devuelve `204 No Content`.

### Eliminar una tarea

`DELETE /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`

- `204 No Content` después de eliminar y compactar las posiciones.
- `400 Bad Request` si `taskId` no es un UUID válido.
- `404 Not Found` con título `Implementation task not found` si no existe o no
  pertenece a la especificación.
- `409 Conflict` con título `Implementation tasks collection changed` si la
  colección cambia concurrentemente durante la eliminación.

La eliminación es permanente y no puede deshacerse mediante esta API.

## Errores del contexto anidado

Todos los endpoints anteriores comparten además:

- `400 Bad Request` si `projectId` o `proposalId` no es un UUID válido.
- `404 Not Found` con título `Project not found` si el proyecto no existe.
- `404 Not Found` con título `Feature proposal not found` si la propuesta no
  existe o pertenece a otro proyecto.
- `404 Not Found` con título `Specification not found` si la propuesta existe
  pero todavía no tiene una especificación.

Las respuestas usan `application/problem+json`. Los errores de validación
incluyen un diccionario `errors` indexado por `projectId`, `proposalId`,
`taskId`, `title`, `description` o `taskIds`, según corresponda.

Las respuestas no revelan recursos pertenecientes a otro contexto ni exponen
stack traces, rutas locales, cadenas de conexión, títulos normalizados, versiones
internas, detalles de Entity Framework Core o información interna de SQLite.

## Criterios de aceptación

1. Crear una tarea válida responde 201, incluye `Location` y la añade al final
   de la colección.
2. La respuesta contiene `id`, `specificationId`, `title`, `description`,
   `position`, `createdAtUtc` y `updatedAtUtc`.
3. El primer elemento recibe posición 1 y los siguientes posiciones
   consecutivas.
4. Al crear, las dos fechas coinciden en UTC con precisión de milisegundos.
5. Un título nulo, vacío o compuesto por espacios devuelve 400.
6. Un título de más de 200 caracteres después de eliminar espacios exteriores
   devuelve 400.
7. El título se persiste y devuelve sin espacios exteriores, conservando el
   resto de su escritura.
8. Una descripción nula es válida y se devuelve como `null`.
9. Una descripción no nula compuesta únicamente por espacios devuelve 400.
10. Una descripción de más de 10.000 caracteres devuelve 400.
11. El Markdown de la descripción se persiste y devuelve sin normalizaciones ni
    cambios.
12. Crear un título ya utilizado dentro de la misma especificación devuelve 409,
    incluso si solo difiere en espacios exteriores o mayúsculas y minúsculas.
13. Dos creaciones simultáneas con el mismo título normalizado no pueden
    completarse ambas; una devuelve 409.
14. El mismo título puede utilizarse en especificaciones diferentes.
15. Una tarea puede consultarse mediante su ruta anidada.
16. Consultar una tarea mediante otra especificación devuelve 404 con
    `Implementation task not found`.
17. Un listado vacío devuelve 200 y `[]`.
18. El listado devuelve todas las tareas por posición ascendente.
19. Un `PUT` válido sustituye título y descripción, conserva identificadores,
    posición y fecha de creación, y actualiza `updatedAtUtc`.
20. Un `PUT` idéntico al estado almacenado no modifica `updatedAtUtc`.
21. Un `PUT` puede eliminar una descripción enviando `description: null`.
22. Editar una tarea para duplicar el título de otra devuelve 409 y conserva el
    estado anterior.
23. Una reordenación válida responde 204; una consulta posterior muestra
    posiciones consecutivas y las fechas de los elementos movidos actualizadas
    al mismo instante.
24. Repetir el orden actual no modifica `updatedAtUtc`.
25. Una reordenación con identificadores omitidos, desconocidos o adicionales
    devuelve 409 y no cambia ninguna posición.
26. Una reordenación con identificadores repetidos o vacíos devuelve 400.
27. Una modificación concurrente de la colección durante la reordenación no
    deja un orden parcial y devuelve 409.
28. Eliminar una tarea devuelve 204 y el recurso deja de estar disponible.
29. Después de eliminar, las posiciones restantes vuelven a ser consecutivas.
30. La eliminación actualiza `updatedAtUtc` únicamente en tareas desplazadas.
31. Los identificadores con formato inválido devuelven 400 mediante Validation
    Problem Details.
32. Un proyecto, propuesta o especificación inexistente produce el Problem
    Details correspondiente.
33. Utilizar otro proyecto o propuesta no revela la existencia de la tarea.
34. Los endpoints que reciben body devuelven 415 para contenido no JSON.
35. La base de datos impide tareas huérfanas, títulos normalizados duplicados y
    posiciones duplicadas dentro de una especificación.
36. OpenAPI documenta los seis endpoints y sus contratos.
37. Las tareas y su orden permanecen disponibles entre scopes y reinicios.
38. Los contratos HTTP existentes no cambian.

## Estrategia de pruebas

### Pruebas unitarias

- Creación válida, normalización del título, conservación de Markdown y
  normalización temporal.
- Rechazo de identificadores, posición, título o descripción inválidos.
- Cálculo determinista del título normalizado.
- Sustitución de título y descripción y actualización condicional de fecha.
- Cambio de posición y actualización condicional de fecha.
- Ausencia de cambios ante contenido y posición idénticos.

### Pruebas de integración

Se reutiliza `WebApplicationFactory` con SQLite relacional en memoria y un
`TimeProvider` controlable. Los escenarios concurrentes y la persistencia entre
reinicios utilizan archivos SQLite temporales.

Se comprueban los contratos HTTP, validación, Problem Details, contexto anidado,
normalización y unicidad del título, preservación de Markdown, orden,
reordenación atómica, compactación al eliminar, foreign key, concurrencia,
persistencia y OpenAPI. No se utilizan mocks de EF Core ni el proveedor EF Core
InMemory.

## Impacto en persistencia

Se añadirá una tabla `ImplementationTasks` con:

- clave primaria `Id`;
- foreign key obligatoria `SpecificationId` hacia `Specifications.Id`;
- eliminación restrictiva;
- `Title` requerido con longitud máxima de 200 caracteres;
- `NormalizedTitle` requerido con longitud máxima de 200 caracteres;
- `Description` nullable con longitud máxima de 10.000 caracteres;
- `Position` requerido y mayor que cero;
- `CreatedAtUtc` y `UpdatedAtUtc` almacenados como milisegundos Unix;
- `Version` requerido como token de concurrencia interno;
- índice único compuesto por `SpecificationId` y `NormalizedTitle`;
- índice único compuesto por `SpecificationId` y `Position`.

La tabla `Specifications` incorporará `ImplementationTasksVersion`, requerido y
utilizado como token de concurrencia para los cambios en la colección. Este
contador es independiente de `AcceptanceCriteriaVersion` para que modificar una
colección no produzca conflictos artificiales en la otra.

No se duplican `ProjectId` ni `FeatureProposalId`. La pertenencia se obtiene a
través de `Specification` y `FeatureProposal` y se valida en los endpoints.

La reordenación y la compactación utilizan una transacción y posiciones
temporales positivas fuera del rango actual antes de asignar las posiciones
finales. Esto permite conservar la restricción única durante los intercambios.

El cambio requiere una nueva migración, pero no nuevos paquetes o proveedores.

## Requisitos no funcionales

- Compilación sin warnings y nullable reference types habilitado.
- Operaciones asíncronas con `CancellationToken`.
- Consultas de lectura con `AsNoTracking`.
- Fechas ISO 8601 en UTC y precisión de milisegundos.
- Reordenaciones y eliminaciones atómicas.
- Endpoints separados de las entidades de dominio mediante DTOs.
- Markdown tratado como contenido opaco, sin ejecución ni renderizado.
- Pruebas deterministas, aisladas y sin dependencia de la hora real.
- Ningún cambio en los contratos HTTP existentes.
- Sin secretos ni archivos SQLite generados en Git.
- Compatibilidad conceptual con una futura sustitución de SQLite por
  PostgreSQL.

## Estructura prevista

- `src/SpecFlow.Domain/ImplementationTasks`: entidad y reglas de dominio.
- `src/SpecFlow.Infrastructure/Persistence`: `DbSet`, configuración y migración.
- `src/SpecFlow.Api/Contracts/ImplementationTasks`: requests y response DTOs.
- `src/SpecFlow.Api/Endpoints/ImplementationTaskEndpoints.cs`: rutas y
  orquestación.
- `tests/SpecFlow.Domain.Tests/ImplementationTasks`: pruebas unitarias.
- `tests/SpecFlow.Api.IntegrationTests/ImplementationTasks`: pruebas HTTP,
  persistencia y concurrencia.

Se mantienen los tres proyectos de producción actuales y no se crea una capa
Application.

## Decisiones acordadas

- Una especificación puede contener múltiples tareas de implementación.
- Cada tarea tiene un título obligatorio y una descripción Markdown opcional.
- El título admite 200 caracteres y se eliminan sus espacios exteriores.
- Los títulos son únicos por especificación sin distinguir mayúsculas y
  minúsculas; pueden repetirse en especificaciones diferentes.
- La descripción admite como máximo 10.000 caracteres y se conserva
  exactamente.
- Las tareas tienen un orden explícito y los nuevos elementos se añaden al
  final.
- La edición sustituye título y descripción completos mediante `PUT`.
- La reordenación recibe todos los identificadores en el orden deseado, no
  recibe ningún otro dato y responde `204 No Content`.
- Las tareas pueden eliminarse física y permanentemente; eliminar compacta las
  posiciones restantes.
- `UpdatedAtUtc` cambia al modificar título, descripción o posición, pero no
  ante una operación idéntica.
- La colección y cada tarea utilizan versiones de concurrencia internas que no
  se exponen mediante HTTP.
- Estados, relación con criterios, dependencias, asignaciones, estimaciones e IA
  quedan fuera del incremento.
- No se incorporan ETags ni versiones expuestas en esta fase.

## Plan de implementación

1. Implementar `ImplementationTask` y sus pruebas unitarias.
2. Añadir el `DbSet`, configuración, índices y generar la migración
   `AddImplementationTasks`.
3. Implementar contratos, creación y prevención de títulos duplicados.
4. Implementar consulta individual y listado ordenado.
5. Implementar edición y conflictos de título.
6. Implementar reordenación transaccional y protección concurrente.
7. Implementar eliminación y compactación transaccional.
8. Verificar foreign key, restricciones, concurrencia y persistencia.
9. Actualizar OpenAPI y README.
10. Ejecutar restauración, compilación, pruebas y formato.
11. Auditar cada criterio de aceptación antes de cambiar el estado de la
    especificación a implementada y verificada.
