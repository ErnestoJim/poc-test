# 0005 — Gestión de criterios de aceptación

## Estado

Implementada y verificada el 6 de octubre de 2026, tras su aprobación para
implementación el mismo día.

## Problema

SpecFlow permite crear y refinar una especificación en Markdown, pero todavía
no puede expresar de forma separada las condiciones verificables que determinan
si esa especificación está correctamente implementada. Sin criterios de
aceptación no existe una base estructurada para preparar tareas, comprobar
resultados o incorporar verificaciones asistidas en el futuro.

## Objetivo

Proporcionar una API HTTP para crear, consultar, listar, editar, reordenar y
eliminar permanentemente criterios de aceptación asociados a una
especificación. Cada criterio tendrá contenido Markdown libre y una posición
explícita dentro de su especificación.

El incremento debe impedir criterios duplicados y mantener un orden estable sin
introducir todavía estados de ejecución, historial, generación mediante IA ni
un modelo obligatorio `Given / When / Then`.

## Alcance

- Modelo de dominio `AcceptanceCriterion` como raíz independiente.
- Asociación obligatoria con una especificación mediante `SpecificationId`.
- Múltiples criterios por especificación.
- Contenido Markdown libre, obligatorio y conservado sin transformaciones.
- Prevención de contenido exactamente duplicado dentro de una especificación.
- Posición explícita, consecutiva y comenzando en 1.
- Creación al final de la colección.
- Consulta individual y listado ordenado.
- Sustitución completa del contenido mediante `PUT`.
- Reordenación atómica de toda la colección.
- Eliminación física y reajuste de las posiciones restantes.
- Fechas de creación y última actualización.
- Persistencia mediante Entity Framework Core y SQLite.
- Foreign key, restricciones e índices necesarios.
- Errores HTTP mediante Problem Details.
- Actualización del documento OpenAPI.
- Pruebas unitarias y de integración.

## Fuera de alcance

- Estructura obligatoria `Given / When / Then`.
- Estados como pendiente, aprobado, superado o fallido.
- Ejecución manual o automática de criterios.
- Resultados, evidencias o historial de ejecuciones.
- Versionado, auditoría o recuperación después de eliminar.
- Eliminación lógica.
- Edición parcial mediante `PATCH`.
- ETags o versiones expuestas mediante HTTP.
- Generación, reescritura o evaluación mediante IA.
- Prompts, agentes, skills o MCP.
- Asociación de criterios con tareas.
- Listados globales o ajenos al contexto de una especificación.
- Búsqueda, filtrado o paginación.
- Comentarios, adjuntos, etiquetas o notificaciones.
- Autenticación o autorización.
- Frontend, Docker, Aspire, despliegue o PostgreSQL.
- Una capa Application, CQRS, MediatR o repositorios genéricos.

Los ETags deberán reconsiderarse si posteriormente existe edición concurrente
por múltiples consumidores o agentes. Durante esta fase la reordenación se
protege de forma interna y atómica, sin ampliar el contrato HTTP con versiones.

## Casos de uso

### UC-01 — Crear un criterio

Un consumidor proporciona contenido Markdown para una especificación existente.
El sistema valida el contexto, comprueba que no exista un criterio con contenido
idéntico, asigna la siguiente posición disponible y devuelve el criterio creado.

### UC-02 — Consultar un criterio

Un consumidor proporciona el contexto de proyecto, propuesta y especificación,
además del identificador del criterio. El sistema devuelve el criterio solamente
si pertenece a esa especificación.

### UC-03 — Listar criterios

El sistema devuelve todos los criterios de una especificación ordenados por su
posición. Si todavía no existe ninguno, devuelve una colección vacía.

### UC-04 — Editar un criterio

Un consumidor sustituye mediante `PUT` el contenido Markdown completo de un
criterio existente. El nuevo contenido no puede duplicar el de otro criterio de
la misma especificación.

### UC-05 — Reordenar criterios

Un consumidor envía todos los identificadores de la colección en el orden
deseado. El sistema valida que cada criterio aparezca exactamente una vez y
actualiza la colección de forma atómica.

### UC-06 — Eliminar un criterio

Un consumidor elimina permanentemente un criterio. Los criterios posteriores
se desplazan para que las posiciones continúen siendo consecutivas.

## Modelo de dominio

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Id` | UUID | Sí | Identificador global generado por la aplicación |
| `SpecificationId` | UUID | Sí | Especificación a la que pertenece |
| `Content` | string | Sí | Criterio expresado como Markdown libre |
| `Position` | int | Sí | Posición dentro de la especificación, comenzando en 1 |
| `CreatedAtUtc` | DateTimeOffset | Sí | Instante UTC de creación |
| `UpdatedAtUtc` | DateTimeOffset | Sí | Instante UTC del último cambio real |

Persistencia incorpora además `ContentHash`, un hash SHA-256 hexadecimal del
contenido exacto. No forma parte del contrato HTTP y se utiliza junto con
`SpecificationId` para proteger la unicidad frente a condiciones de carrera sin
crear un índice sobre una columna Markdown de gran tamaño.

SHA-256 es la estrategia interna aprobada para esta fase, no una parte del
contrato público ni una decisión permanente. Podrá sustituirse mediante una
migración posterior sin modificar los contratos HTTP.

Cada criterio mantiene también una versión de concurrencia interna. La
especificación incorpora un contador interno de versión de su colección de
criterios. Ninguno de los dos valores forma parte del contrato HTTP; permiten
detectar cambios simultáneos durante creación, reordenación y eliminación.

`AcceptanceCriterion` es una raíz independiente. El dominio no expone
navegaciones hacia `Specification`, y `Specification` no mantiene una colección
de criterios. La relación se representa mediante `SpecificationId` y se protege
en persistencia.

## Reglas de negocio

1. `Id` no puede ser un UUID vacío.
2. `SpecificationId` no puede ser un UUID vacío y es inmutable.
3. La especificación debe existir antes de crear, consultar, listar, editar,
   reordenar o eliminar criterios.
4. `Content` es obligatorio y debe contener al menos un carácter distinto de
   espacios en blanco.
5. `Content` puede contener como máximo 10.000 caracteres.
6. El contenido se conserva exactamente como lo proporciona el consumidor. No
   se eliminan espacios exteriores, no se normalizan saltos de línea y no se
   interpreta ni renderiza Markdown.
7. Dos criterios de la misma especificación no pueden tener contenido idéntico,
   utilizando comparación ordinal carácter por carácter.
8. El mismo contenido está permitido en especificaciones diferentes.
9. `ContentHash` se calcula a partir de los bytes UTF-8 del contenido exacto y
   se representa como SHA-256 hexadecimal en minúsculas.
10. `Position` debe ser mayor que cero.
11. El primer criterio de una especificación recibe la posición 1.
12. Cada nuevo criterio se añade al final con la posición máxima existente más
    uno.
13. Dentro de una especificación las posiciones son únicas y consecutivas.
14. `CreatedAtUtc` se obtiene mediante `TimeProvider`, se convierte a UTC y se
    normaliza a precisión de milisegundos.
15. Al crear, `UpdatedAtUtc` tiene el mismo valor que `CreatedAtUtc`.
16. Un `PUT` válido sustituye todo el contenido existente y recalcula
    `ContentHash`.
17. Si el contenido cambia, `UpdatedAtUtc` se actualiza desde `TimeProvider`.
18. Si el contenido enviado es idéntico al almacenado, no cambia
    `UpdatedAtUtc`.
19. Reordenar modifica únicamente `Position` y `UpdatedAtUtc` de los criterios
    cuya posición cambia.
20. Todos los criterios movidos por la misma reordenación comparten el mismo
    `UpdatedAtUtc`.
21. La reordenación debe incluir todos los identificadores actuales exactamente
    una vez; no permite añadir, omitir ni duplicar identificadores.
22. Una reordenación idéntica al orden almacenado no modifica ninguna fecha.
23. Al eliminar un criterio, las posiciones posteriores disminuyen en uno y sus
    fechas `UpdatedAtUtc` registran el instante de compactación.
24. El cliente no puede proporcionar `Id`, `SpecificationId`, `Position`, hashes
    ni fechas al crear o editar contenido.
25. Las operaciones que modifican toda la colección se realizan en una
    transacción para evitar estados parcialmente actualizados.
26. Crear, reordenar o eliminar incrementa la versión interna de la colección,
    incluso cuando una reordenación válida conserva el mismo orden.
27. Cambiar el contenido o la posición de un criterio incrementa su versión de
    concurrencia interna.

## Contratos HTTP

Todas las rutas se anidan bajo:

`/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`

Antes de operar sobre criterios, el sistema aplica las reglas existentes de
existencia y aislamiento para proyecto, propuesta y especificación.

### Crear un criterio

`POST /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`

Request:

```json
{
  "content": "Given a valid project, creating a proposal returns `201 Created`."
}
```

Respuesta correcta: `201 Created`, cabecera `Location` apuntando al criterio y:

```json
{
  "id": "225ceb34-721c-44ea-aa87-97d679028b1a",
  "specificationId": "5d346b9e-3449-4f79-ab86-a17af587689b",
  "content": "Given a valid project, creating a proposal returns `201 Created`.",
  "position": 1,
  "createdAtUtc": "2026-10-08T08:30:00+00:00",
  "updatedAtUtc": "2026-10-08T08:30:00+00:00"
}
```

Errores específicos:

- `400 Bad Request` si el contenido no es válido.
- `409 Conflict` con título `Acceptance criterion already exists` si otro
  criterio de la especificación tiene contenido idéntico, incluso ante
  creaciones simultáneas.
- `409 Conflict` con título `Acceptance criteria collection changed` si una
  creación simultánea con otro contenido modifica posición o versión.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

### Consultar un criterio

`GET /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`

- `200 OK` con el criterio.
- `400 Bad Request` si `criterionId` no es un UUID válido.
- `404 Not Found` con título `Acceptance criterion not found` si no existe o no
  pertenece a la especificación indicada.

### Listar criterios

`GET /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`

- `200 OK` con un array ordenado por `position` ascendente y después por `id`.
- Si la especificación no tiene criterios, devuelve `[]`.

### Editar un criterio

`PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`

El request tiene el mismo contrato que la creación y representa el contenido
completo deseado.

- `200 OK` con el criterio actualizado.
- `400 Bad Request` si el contenido o `criterionId` no es válido.
- `404 Not Found` con título `Acceptance criterion not found` si no existe o no
  pertenece a la especificación.
- `409 Conflict` con título `Acceptance criterion already exists` si el nuevo
  contenido duplica el de otro criterio de la especificación.
- `409 Conflict` con título `Acceptance criterion update conflict` si el
  criterio cambia concurrentemente durante la edición.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

### Reordenar criterios

`PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/order`

Request:

```json
{
  "criterionIds": [
    "e7748e2a-94cc-43ed-a9f8-449160b34bbc",
    "225ceb34-721c-44ea-aa87-97d679028b1a"
  ]
}
```

Respuesta correcta: `204 No Content`.

Errores específicos:

- `400 Bad Request` mediante Validation Problem Details si la colección contiene
  UUID vacíos o repetidos.
- `409 Conflict` con título `Acceptance criteria collection changed` si los
  identificadores no coinciden exactamente con la colección actual o si esta
  cambia concurrentemente durante la operación.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

El request contiene únicamente los identificadores. No requiere enviar
contenido, posiciones, fechas ni versiones, y la respuesta no devuelve la
colección. Si un consumidor necesita obtener el estado actualizado puede usar
el endpoint de listado de forma independiente.

Enviar una colección vacía es válido únicamente si la especificación no contiene
criterios. En ese caso también se devuelve `204 No Content`.

### Eliminar un criterio

`DELETE /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`

- `204 No Content` después de eliminar y compactar las posiciones.
- `400 Bad Request` si `criterionId` no es un UUID válido.
- `404 Not Found` con título `Acceptance criterion not found` si no existe o no
  pertenece a la especificación.
- `409 Conflict` con título `Acceptance criteria collection changed` si la
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
`criterionId`, `content` o `criterionIds`, según corresponda.

Las respuestas no revelan recursos pertenecientes a otro contexto ni exponen
stack traces, rutas locales, cadenas de conexión, hashes internos, detalles de
Entity Framework Core o información interna de SQLite.

## Criterios de aceptación

1. Crear un criterio válido responde 201, incluye `Location` y lo añade al final
   de la colección.
2. La respuesta contiene `id`, `specificationId`, `content`, `position`,
   `createdAtUtc` y `updatedAtUtc`.
3. El primer criterio recibe posición 1 y los siguientes posiciones consecutivas.
4. Al crear, las dos fechas coinciden en UTC con precisión de milisegundos.
5. Un contenido nulo, vacío o compuesto por espacios devuelve 400.
6. Un contenido de más de 10.000 caracteres devuelve 400.
7. El Markdown se persiste y devuelve sin normalizaciones ni cambios.
8. Crear contenido idéntico dentro de la misma especificación devuelve 409.
9. Dos creaciones simultáneas con contenido idéntico no pueden completarse
   ambas; una devuelve 409.
10. El mismo contenido puede utilizarse en especificaciones diferentes.
11. Un criterio puede consultarse mediante su ruta anidada.
12. Consultar un criterio mediante otra especificación devuelve 404 con
    `Acceptance criterion not found`.
13. Un listado vacío devuelve 200 y `[]`.
14. El listado devuelve todos los criterios por posición ascendente.
15. Un `PUT` válido sustituye el contenido completo, conserva identificadores,
    posición y fecha de creación, y actualiza `updatedAtUtc`.
16. Un `PUT` con contenido idéntico no modifica `updatedAtUtc`.
17. Editar un criterio para duplicar otro devuelve 409 y conserva el contenido
    anterior.
18. Una reordenación válida responde 204; una consulta posterior muestra
    posiciones consecutivas y las fechas de los elementos movidos actualizadas
    al mismo instante.
19. Repetir el orden actual no modifica `updatedAtUtc`.
20. Una reordenación con identificadores omitidos, desconocidos o adicionales
    devuelve 409 y no cambia ninguna posición.
21. Una reordenación con identificadores repetidos o vacíos devuelve 400.
22. Una modificación concurrente de la colección durante la reordenación no
    deja un orden parcial y devuelve 409.
23. Eliminar un criterio devuelve 204 y el recurso deja de estar disponible.
24. Después de eliminar, las posiciones restantes vuelven a ser consecutivas.
25. La eliminación actualiza `updatedAtUtc` únicamente en criterios desplazados.
26. Los identificadores con formato inválido devuelven 400 mediante Validation
    Problem Details.
27. Un proyecto, propuesta o especificación inexistente produce el Problem
    Details correspondiente.
28. Utilizar otro proyecto o propuesta no revela la existencia del criterio.
29. Los endpoints que reciben body devuelven 415 para contenido no JSON.
30. La base de datos impide criterios huérfanos, contenidos duplicados y
    posiciones duplicadas dentro de una especificación.
31. OpenAPI documenta los seis endpoints y sus contratos.
32. Los criterios y su orden permanecen disponibles entre scopes y reinicios.
33. Los contratos HTTP existentes no cambian.

## Estrategia de pruebas

### Pruebas unitarias

- Creación válida, preservación de Markdown y normalización temporal.
- Rechazo de identificadores, posición o contenido inválidos.
- Cálculo determinista del hash a partir del contenido exacto.
- Sustitución de contenido y actualización condicional de fecha y hash.
- Cambio de posición y actualización condicional de fecha.
- Ausencia de cambios ante contenido o posición idénticos.

### Pruebas de integración

Se reutiliza `WebApplicationFactory` con SQLite relacional en memoria y un
`TimeProvider` controlable. Los escenarios concurrentes y la persistencia entre
reinicios utilizan archivos SQLite temporales.

Se comprueban los contratos HTTP, validación, Problem Details, contexto anidado,
preservación de Markdown, unicidad, orden, reordenación atómica, compactación al
eliminar, foreign key, concurrencia, persistencia y OpenAPI. No se utilizan mocks
de EF Core ni el proveedor EF Core InMemory.

## Impacto en persistencia

Se añadirá una tabla `AcceptanceCriteria` con:

- clave primaria `Id`;
- foreign key obligatoria `SpecificationId` hacia `Specifications.Id`;
- eliminación restrictiva;
- `Content` requerido con longitud máxima de 10.000 caracteres;
- `ContentHash` requerido con longitud fija de 64 caracteres;
- `Position` requerido y mayor que cero;
- `CreatedAtUtc` y `UpdatedAtUtc` almacenados como milisegundos Unix;
- `Version` requerido como token de concurrencia interno;
- índice único compuesto por `SpecificationId` y `ContentHash`;
- índice único compuesto por `SpecificationId` y `Position`.

La tabla `Specifications` incorporará `AcceptanceCriteriaVersion`, requerido y
utilizado como token de concurrencia para los cambios en la colección.

No se duplican `ProjectId` ni `FeatureProposalId`. La pertenencia se obtiene a
través de `Specification` y `FeatureProposal` y se valida en los endpoints.

La reordenación y la compactación utilizan una transacción y posiciones
temporales positivas fuera del rango actual antes de asignar las posiciones
finales. Esto permite conservar la restricción única durante los intercambios.

El cambio requiere una nueva migración, pero no nuevos paquetes o proveedores.

SHA-256 podrá sustituirse en el futuro mediante una migración de datos. Los
consumidores no observan ni proporcionan el hash, por lo que ese cambio no
requiere modificar la API.

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

- `src/SpecFlow.Domain/AcceptanceCriteria`: entidad y reglas de dominio.
- `src/SpecFlow.Infrastructure/Persistence`: `DbSet`, configuración y migración.
- `src/SpecFlow.Api/Contracts/AcceptanceCriteria`: requests y response DTOs.
- `src/SpecFlow.Api/Endpoints/AcceptanceCriterionEndpoints.cs`: rutas y
  orquestación.
- `tests/SpecFlow.Domain.Tests/AcceptanceCriteria`: pruebas unitarias.
- `tests/SpecFlow.Api.IntegrationTests/AcceptanceCriteria`: pruebas HTTP,
  persistencia y concurrencia.

Se mantienen los tres proyectos de producción actuales y no se crea una capa
Application.

## Decisiones acordadas

- Los criterios utilizan texto Markdown libre, sin estructura obligatoria
  `Given / When / Then`.
- Una especificación puede contener múltiples criterios.
- Se impiden contenidos exactamente duplicados dentro de una especificación.
- Los criterios tienen un orden explícito y pueden reordenarse.
- La edición sustituye el contenido completo mediante `PUT`.
- Los criterios pueden eliminarse.
- La eliminación es física y permanente durante esta fase.
- Estados de ejecución, historial e IA quedan fuera del incremento.
- El contenido admite como máximo 10.000 caracteres y se conserva exactamente.
- La unicidad se protege con un hash SHA-256 interno del contenido exacto.
- SHA-256 es sustituible mediante una futura migración y no forma parte del
  contrato HTTP.
- Las posiciones comienzan en 1, son consecutivas y están protegidas mediante
  una restricción única por especificación.
- Los nuevos criterios se añaden al final.
- La reordenación recibe todos los identificadores en el orden deseado y es
  atómica.
- La reordenación no recibe ningún otro dato y responde `204 No Content`.
- Eliminar un criterio compacta las posiciones restantes.
- `UpdatedAtUtc` cambia al modificar contenido o posición, pero no ante una
  operación idéntica.
- La colección y cada criterio utilizan versiones de concurrencia internas que
  no se exponen mediante HTTP.
- No se incorporan ETags ni versiones expuestas en esta fase.

## Plan de implementación

1. Implementar `AcceptanceCriterion` y sus pruebas unitarias.
2. Añadir el `DbSet`, configuración e índice de unicidad y generar la migración
   `AddAcceptanceCriteria`.
3. Implementar contratos, creación y prevención de duplicados.
4. Implementar consulta individual y listado ordenado.
5. Implementar edición y conflictos de contenido.
6. Implementar reordenación transaccional y protección concurrente.
7. Implementar eliminación y compactación transaccional.
8. Verificar foreign key, restricciones, concurrencia y persistencia.
9. Actualizar OpenAPI y README.
10. Ejecutar restauración, compilación, pruebas y formato.
11. Auditar cada criterio de aceptación antes de cambiar el estado de la
    especificación a implementada y verificada.
