# 0009 — Control de concurrencia HTTP

## Estado

Implementada y verificada el 7 de octubre de 2026.

Verificación final:

- compilación sin warnings ni errores;
- 76 pruebas de dominio superadas;
- 206 pruebas de integración superadas;
- formato verificado sin cambios;
- modelo de EF Core sin migraciones pendientes.

## Problema

SpecFlow protege varias operaciones frente a carreras que ocurren dentro de
una petición. Las propuestas, los criterios de aceptación y las tareas cuentan
con versiones internas, y las colecciones ordenadas utilizan contadores para
detectar modificaciones simultáneas.

Sin embargo, esas versiones no forman parte del contrato HTTP. Un consumidor
puede consultar un recurso, mantener una copia durante un tiempo y enviar
posteriormente una modificación sin indicar qué versión leyó. Si otro
consumidor modificó el recurso durante ese intervalo, la petición tardía puede
sobrescribir el estado más reciente.

Esta limitación es especialmente relevante antes de incorporar frontend, MCP o
múltiples agentes, porque esos adaptadores ampliarán el tiempo entre lectura y
escritura y aumentarán la probabilidad de ediciones concurrentes.

Además, la protección actual de las colecciones no es completamente simétrica:

- `AcceptanceCriteriaVersion` está configurada como concurrency token de toda
  la entidad `Specification`;
- `ImplementationTasksVersion` se avanza mediante una comparación y
  actualización atómica específica de la colección;
- `Specification` no tiene una versión propia para proteger su contenido;
- un cambio en los criterios puede producir un conflicto artificial con una
  edición independiente del contenido de la especificación;
- dos ediciones concurrentes del contenido pueden aplicar silenciosamente la
  última escritura.

La API necesita un contrato explícito y uniforme que permita a los consumidores
evitar escrituras obsoletas sin exponer detalles de EF Core ni convertir las
versiones internas en propiedades JSON.

## Objetivo

Incorporar concurrencia optimista HTTP mediante ETags fuertes y precondiciones
`If-Match` para los recursos editables y las colecciones reordenables.

El sistema debe:

- devolver un validador opaco asociado a la representación leída;
- exigir que una modificación indique el validador sobre el que se basa;
- rechazar una escritura cuando el recurso cambió desde la lectura;
- diferenciar una precondición obsoleta de un conflicto de negocio;
- mantener independientes el contenido de la especificación, la colección de
  criterios y la colección de tareas;
- conservar las protecciones internas frente a carreras que ocurren después de
  evaluar la precondición HTTP.

Este incremento define concurrencia optimista, no historial ni versionado
funcional de documentos.

## Terminología

### Versión de recurso

Contador persistente que cambia cuando cambia la representación observable de
un recurso individual. Durante este incremento existen versiones individuales
para:

- `Specification`;
- `AcceptanceCriterion`;
- `ImplementationTask`.

### Versión de colección

Contador persistente asociado a la composición y orden de una colección. Ya
existen:

- `AcceptanceCriteriaVersion`;
- `ImplementationTasksVersion`.

### ETag de recurso

Validador HTTP fuerte, entre comillas y opaco para el consumidor, derivado de la
identidad y versión interna del recurso. No se incorpora al JSON.

### ETag de colección

Validador HTTP fuerte y opaco asociado a la representación listada. Debe tener
en cuenta la versión de colección y las versiones de sus elementos para cambiar
cuando cambie cualquier propiedad visible del listado.

El algoritmo concreto y el formato interno de ambos tipos de ETag no forman
parte del contrato público. Las pruebas del consumidor deben reutilizar el
valor recibido y no construirlo ni interpretarlo.

## Alcance

- Añadir una versión interna a `Specification`.
- Incrementar la versión de la especificación cuando cambia realmente su
  contenido.
- Mantener la versión cuando un `PUT` conserva contenido idéntico.
- Configurar la versión de la especificación como concurrency token.
- Exponer ETags fuertes en respuestas de recursos individuales.
- Exponer ETags fuertes en los listados de criterios y tareas.
- Exigir una única precondición `If-Match` fuerte en las modificaciones de
  recursos existentes incluidas en esta especificación.
- Exigir el ETag del listado actual al reordenar criterios o tareas.
- Responder `428 Precondition Required` cuando falta una precondición
  obligatoria.
- Responder `400 Bad Request` cuando `If-Match` tiene un formato no admitido.
- Responder `412 Precondition Failed` cuando el ETag ya no corresponde al estado
  actual o una carrera posterior invalida la precondición.
- Conservar `409 Conflict` para transiciones inválidas, duplicados y conflictos
  de colección que no representan una precondición obsoleta.
- Separar la concurrencia del contenido de `Specification` de los contadores de
  sus dos colecciones.
- Sustituir el concurrency token global de `AcceptanceCriteriaVersion` por una
  actualización atómica específica, equivalente conceptualmente a la utilizada
  por las tareas.
- Añadir una migración de EF Core para la versión de especificación.
- Actualizar OpenAPI y README.
- Añadir pruebas unitarias, de integración, concurrencia, migración y
  persistencia.

## Fuera de alcance

- Historial de versiones o revisiones consultables.
- Recuperación o comparación de versiones anteriores.
- Auditoría de quién realizó una modificación.
- Bloqueos pesimistas o reservas de edición.
- WebSockets, Server-Sent Events o notificaciones de cambios.
- Idempotency keys para operaciones de creación.
- ETags para proyectos, porque actualmente no son editables.
- ETags para propuestas, porque su decisión ya es una transición final y no
  existe edición general de la propuesta.
- Precondiciones para crear especificaciones, criterios o tareas.
- Precondiciones `If-None-Match` y caché condicional mediante `304 Not Modified`.
- Fechas `If-Unmodified-Since`.
- Soporte de `If-Match: *`.
- Soporte de múltiples entity-tags en una misma cabecera `If-Match`.
- ETags débiles con prefijo `W/`.
- Exponer versiones internas en los contratos JSON.
- Cambiar los estados o transiciones de propuestas y tareas.
- Crear una capa `Application`.
- CQRS, MediatR, event sourcing, repositorios genéricos o una unit of work
  propia.
- Frontend, MCP, skills o integración con modelos de IA.
- PostgreSQL, Docker, Aspire o despliegue.

La caché condicional de lecturas podrá añadirse posteriormente. Los ETags de
este incremento se utilizan para concurrencia optimista y no obligan a
incorporar ahora respuestas `304 Not Modified`.

## Casos de uso

### UC-01 — Consultar y editar una especificación

Un consumidor consulta una especificación y recibe su representación junto con
un ETag. Para sustituir el contenido envía ese ETag mediante `If-Match`.

Si la especificación conserva la versión leída, la API realiza la actualización
y devuelve la representación con su ETag actual. Si cambió, devuelve `412` sin
modificarla.

### UC-02 — Editar un criterio de aceptación

Un consumidor consulta un criterio y recibe su ETag. El `PUT` del criterio debe
incluirlo. Una edición con un ETag actual conserva el comportamiento de
validación y unicidad existente; una edición basada en una versión anterior
devuelve `412`.

### UC-03 — Eliminar un criterio de aceptación

Un consumidor elimina un criterio indicando el ETag individual obtenido al
consultarlo. La eliminación continúa siendo transaccional y compacta las
posiciones. Si el criterio cambió desde la lectura, no se elimina y se devuelve
`412`.

### UC-04 — Reordenar criterios de aceptación

El listado de criterios devuelve un ETag de colección. La reordenación debe
enviar ese mismo valor mediante `If-Match`.

Si el listado cambió desde la lectura, la API devuelve `412`. Si el ETag es
actual pero el request omite, repite o añade identificadores, se mantienen los
errores de validación o conflicto definidos previamente.

### UC-05 — Editar una tarea de implementación

Un consumidor consulta una tarea, conserva su ETag y lo proporciona al editar
título o descripción. La API rechaza con `412` una edición basada en una versión
anterior.

### UC-06 — Transicionar una tarea

Iniciar o completar una tarea requiere el ETag de la tarea. Si el ETag es
obsoleto se devuelve `412`. Si el ETag es actual pero el estado no permite la
transición, se devuelve `409`.

### UC-07 — Eliminar una tarea de implementación

Eliminar una tarea requiere su ETag individual. La compactación de posiciones y
la versión de colección continúan protegidas transaccionalmente. Una tarea
modificada desde la lectura no se elimina.

### UC-08 — Reordenar tareas de implementación

El listado de tareas devuelve un ETag de colección. El consumidor debe enviarlo
al reordenar. Un listado obsoleto produce `412`; un request incompatible con la
colección actual y basado en el ETag actual conserva `409`.

### UC-09 — Repetir una actualización sin cambios

Un consumidor envía un `PUT` válido cuyo contenido coincide exactamente con el
estado actual. La operación devuelve `200`, no incrementa la versión y devuelve
el mismo ETag.

### UC-10 — Detectar una carrera después de validar `If-Match`

Dos peticiones parten del mismo ETag y alcanzan simultáneamente la persistencia.
Como máximo una modificación puede completarse. La otra devuelve `412`, incluso
si ambas superaron inicialmente la comparación del header.

## Modelo de dominio

### Specification

Se añade:

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Version` | int | Sí | Versión interna del contenido de la especificación |

Reglas:

1. Una especificación nueva comienza con versión cero.
2. Cambiar realmente `Content` incrementa `Version` una vez.
3. Enviar contenido idéntico no cambia `Version` ni `UpdatedAtUtc`.
4. Modificar criterios de aceptación no cambia `Version`.
5. Modificar tareas de implementación no cambia `Version`.
6. `Version` no forma parte de `SpecificationResponse`.

### AcceptanceCriterion

Se reutiliza `Version`:

- cambiar el contenido incrementa la versión;
- cambiar la posición incrementa la versión;
- una operación idéntica no la incrementa;
- la versión no se expone en JSON.

### ImplementationTask

Se reutiliza `Version`:

- cambiar título, descripción o posición incrementa la versión;
- iniciar o completar incrementa la versión;
- una edición idéntica no la incrementa;
- la versión no se expone en JSON.

### Colecciones

`AcceptanceCriteriaVersion` e `ImplementationTasksVersion` continúan siendo
contadores independientes para composición y orden. Crear, reordenar o eliminar
continúa avanzando el contador según las reglas existentes.

El ETag de un listado también tiene en cuenta las versiones individuales de los
elementos porque editar contenido, título, descripción o estado cambia la
representación listada aunque no cambie composición ni orden.

## Reglas de negocio y concurrencia

1. Las reglas de dominio se evalúan únicamente después de resolver el recurso y
   validar la precondición HTTP.
2. Una precondición fallida no modifica entidades, fechas, posiciones ni
   versiones.
3. Una validación de body fallida no modifica el recurso.
4. Una actualización idéntica mantiene fecha y ETag.
5. Una actualización efectiva cambia fecha, versión y ETag del recurso.
6. Mover un criterio o tarea cambia su ETag individual.
7. Una transición de tarea cambia su ETag individual.
8. Un cambio observable en un elemento cambia el ETag del listado que lo
   contiene.
9. Cambiar criterios no invalida el ETag individual de la especificación.
10. Cambiar tareas no invalida el ETag individual de la especificación.
11. Cambiar criterios no invalida el ETag del listado de tareas.
12. Cambiar tareas no invalida el ETag del listado de criterios.
13. Crear recursos continúa protegido por restricciones y concurrencia interna,
    sin requerir `If-Match`.
14. Las versiones continúan siendo enteros internos y no son proporcionadas por
    el cliente en el body.
15. Los ETags se comparan mediante comparación fuerte y exacta.
16. Los ETags son opacos: un consumidor no puede depender de su estructura.
17. Una carrera detectada por EF Core después de validar `If-Match` se presenta
    como precondición fallida, no como error interno.
18. Una transición de dominio inválida con ETag actual continúa siendo un
    conflicto `409`.
19. Una violación de unicidad con ETag actual continúa siendo un conflicto
    `409`.
20. Las operaciones sobre colecciones continúan siendo transaccionales.

## Orden de evaluación

Las operaciones condicionadas siguen este orden observable:

1. Validar el formato de identificadores de ruta.
2. Validar el body cuando exista.
3. Resolver proyecto, propuesta, especificación y recurso sin revelar recursos
   pertenecientes a otro contexto.
4. Comprobar la presencia de `If-Match`.
5. Comprobar que el header tiene un formato admitido.
6. Comparar el ETag proporcionado con el estado actual.
7. Aplicar reglas de dominio y validaciones de unicidad.
8. Persistir usando concurrencia optimista.

Este orden conserva la validación de entrada existente antes de consultar la
base de datos. Por tanto, un body inválido continúa devolviendo `400` aunque
falte `If-Match`.

La API no debe utilizar la presencia o ausencia de un ETag para revelar si un
recurso existe bajo otro proyecto, propuesta o especificación.

## Contrato de headers

### ETag de respuesta

Las respuestas siguientes incorporan `ETag`:

- `201 Created` al crear una especificación, criterio o tarea;
- `200 OK` al consultar una especificación, criterio o tarea;
- `200 OK` al editar una especificación, criterio o tarea;
- `200 OK` al iniciar o completar una tarea;
- `200 OK` al listar criterios o tareas;
- `204 No Content` después de reordenar, con el nuevo ETag de la colección.

El valor cumple la sintaxis de entity-tag fuerte de HTTP y está entre comillas.
No utiliza el prefijo `W/`.

### If-Match de petición

Las operaciones siguientes requieren exactamente un ETag fuerte:

- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification`;
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`;
- `DELETE /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/{criterionId}`;
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria/order`;
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`;
- `DELETE /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}`;
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/start`;
- `POST /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/{taskId}/complete`;
- `PUT /api/projects/{projectId}/proposals/{proposalId}/specification/tasks/order`.

No requieren `If-Match`:

- operaciones de lectura;
- creación de proyectos, propuestas, especificaciones, criterios o tareas;
- aceptación o rechazo de propuestas.

## Respuestas de error

### Precondición ausente

`428 Precondition Required` con:

- título `Precondition required`;
- detalle que indique que la operación requiere el ETag actual mediante
  `If-Match`.

### If-Match no admitido

`400 Bad Request` mediante Problem Details con:

- título `Invalid If-Match header`;
- detalle que indique que se requiere exactamente un ETag fuerte;
- no se acepta `*`, un ETag débil ni una lista de ETags.

### ETag obsoleto

`412 Precondition Failed` con:

- título `Precondition failed`;
- detalle que indique que el recurso cambió desde que fue leído;
- sin incluir la versión actual ni el ETag esperado.

La respuesta no revela versiones internas, SQL, detalles de EF Core ni la causa
concreta de una carrera.

### Conflictos existentes

Se conserva `409 Conflict` para:

- propuestas o tareas en un estado que no permite la transición solicitada;
- especificaciones, criterios o tareas duplicadas;
- reordenaciones cuyo conjunto de identificadores no coincide con la colección
  aunque el ETag proporcionado sea actual;
- creaciones simultáneas, porque no utilizan precondición HTTP.

## Comportamiento por operación

### Especificaciones

- `POST` crea con versión cero y devuelve su ETag.
- `GET` devuelve el ETag actual.
- `PUT` requiere `If-Match`.
- Un cambio real incrementa `Version` y devuelve un ETag nuevo.
- Un `PUT` idéntico devuelve el mismo ETag.
- Una precondición obsoleta devuelve `412`.

### Criterios de aceptación

- `POST` devuelve el ETag individual del criterio nuevo.
- `GET /{criterionId}` devuelve el ETag individual.
- `GET` de la colección devuelve el ETag del listado.
- `PUT /{criterionId}` requiere el ETag individual.
- `DELETE /{criterionId}` requiere el ETag individual.
- `PUT /order` requiere el ETag del listado y devuelve el nuevo ETag del listado.
- Los ETags individuales de los criterios movidos cambian.
- Los criterios no movidos conservan su ETag individual.

### Tareas de implementación

- `POST` devuelve el ETag individual de la tarea nueva.
- `GET /{taskId}` devuelve el ETag individual.
- `GET` de la colección devuelve el ETag del listado.
- `PUT /{taskId}` requiere el ETag individual.
- `DELETE /{taskId}` requiere el ETag individual.
- `POST /start` y `POST /complete` requieren el ETag individual.
- `PUT /order` requiere el ETag del listado y devuelve el nuevo ETag del listado.
- Las tareas movidas o transicionadas reciben un ETag individual nuevo.
- Las tareas no modificadas conservan su ETag individual.

## Carreras y traducción de errores

Comparar `If-Match` antes de modificar no sustituye el concurrency token de la
base de datos. Entre la comparación y `SaveChangesAsync` otro consumidor puede
persistir una modificación.

Cuando una operación condicionada encuentra `DbUpdateConcurrencyException`
después de haber aceptado `If-Match`, devuelve `412 Precondition Failed`.

Las operaciones de colección deben evaluar el ETag y ejecutar sus cambios
dentro de una frontera transaccional que impida aceptar silenciosamente una
representación obsoleta. Las comparaciones y actualizaciones atómicas de los
contadores internos continúan siendo la última protección ante carreras.

## Comportamientos anteriores sustituidos

Esta especificación amplía o sustituye los siguientes comportamientos:

- el `PUT` de especificación deja de ser una escritura incondicional;
- editar o eliminar criterios y tareas requiere `If-Match`;
- iniciar o completar tareas requiere `If-Match`;
- reordenar criterios o tareas requiere el ETag del listado;
- una carrera en una operación condicionada devuelve `412` en lugar del `409`
  utilizado anteriormente para el conflicto interno equivalente;
- `AcceptanceCriteriaVersion` deja de ser concurrency token global de
  `Specification` y pasa a proteger exclusivamente su colección mediante una
  comparación atómica;
- las operaciones de creación conservan los conflictos `409` existentes.

Las rutas, bodies y representaciones JSON actuales no cambian.

## Impacto en persistencia

La tabla `Specifications` incorpora:

- `Version` entero, requerido, con valor inicial cero;
- configuración como concurrency token para actualizaciones del contenido.

`AcceptanceCriteriaVersion` continúa persistido y requerido, pero deja de estar
configurado como concurrency token global de la entidad. Las operaciones de
creación, reordenación y eliminación de criterios lo avanzan mediante una
actualización condicional atómica.

`ImplementationTasksVersion` conserva su mecanismo de actualización condicional
atómica.

Las columnas `Version` existentes de criterios y tareas continúan como
concurrency tokens.

Se generará una migración de EF Core. Las especificaciones existentes recibirán
versión cero. No se modifican claves, foreign keys, índices ni datos funcionales.

Los ETags no se almacenan. Se generan a partir de identificadores y versiones
persistentes, por lo que permanecen estables entre scopes y reinicios mientras
el estado no cambie.

## Estrategia de pruebas

### Pruebas unitarias

- Versión cero al crear una especificación.
- Incremento de versión al cambiar contenido.
- Conservación de versión y fecha ante contenido idéntico.
- Independencia entre versión de contenido y versiones de colección.
- Generación de ETags fuertes y deterministas a partir del mismo estado.
- Cambio de ETag cuando cambia identidad o versión.
- Cálculo determinista del ETag de colección.
- Rechazo de ETags débiles, wildcard, listas y formatos inválidos.

### Pruebas de integración

- Presencia de ETag en cada respuesta indicada.
- Ausencia de versiones en JSON.
- `428` cuando falta `If-Match`.
- `400` para `If-Match` no admitido.
- Éxito con el ETag actual.
- `412` con un ETag obsoleto.
- Un `PUT` idéntico conserva el ETag.
- Una actualización efectiva devuelve un ETag nuevo.
- Dos escrituras con el mismo ETag no pueden completarse ambas.
- Una carrera posterior a la comparación devuelve `412`.
- Una transición inválida con ETag actual devuelve `409`.
- Un ETag obsoleto se evalúa antes que la transición de dominio.
- Edición y eliminación condicionadas de criterios y tareas.
- Inicio y finalización condicionados de tareas.
- ETag de listado para criterios y tareas.
- Reordenación condicionada y nuevo ETag en la respuesta.
- Cambio del ETag de listado al editar o transicionar un elemento.
- Independencia de los tres ámbitos de versión.
- Aislamiento entre proyectos, propuestas y especificaciones.
- Persistencia de las versiones entre reinicios.
- Migración de especificaciones existentes con versión cero.
- OpenAPI documenta `ETag`, `If-Match`, `400`, `412` y `428` donde corresponde.

Se mantiene `WebApplicationFactory` con SQLite relacional. Las carreras,
migraciones y persistencia entre reinicios utilizan archivos SQLite temporales.
No se utilizan mocks de EF Core ni el proveedor EF Core InMemory.

## Criterios de aceptación

1. Crear una especificación devuelve un ETag fuerte y no expone `Version` en el
   JSON.
2. Consultar una especificación devuelve el mismo ETag mientras su contenido no
   cambie.
3. Editar una especificación sin `If-Match` devuelve `428` y no la modifica.
4. Editar una especificación con un header no admitido devuelve `400` y no la
   modifica.
5. Editar una especificación con su ETag actual responde `200`.
6. Un cambio real de contenido incrementa la versión y devuelve un ETag nuevo.
7. Un `PUT` idéntico conserva `updatedAtUtc`, versión y ETag.
8. Editar una especificación con un ETag obsoleto devuelve `412` y conserva el
   contenido más reciente.
9. Dos ediciones simultáneas basadas en el mismo ETag no pueden completarse
   ambas.
10. Modificar criterios o tareas no invalida el ETag individual de la
    especificación.
11. Crear y consultar un criterio devuelve su ETag individual.
12. Editar un criterio sin `If-Match` devuelve `428`.
13. Editar un criterio con su ETag actual conserva las reglas de validación y
    unicidad existentes.
14. Editar un criterio con un ETag obsoleto devuelve `412` y no lo modifica.
15. Eliminar un criterio requiere su ETag actual y un ETag obsoleto devuelve
    `412` sin eliminar ni compactar posiciones.
16. Listar criterios devuelve un ETag fuerte de colección.
17. Editar contenido, crear, eliminar o mover un criterio cambia el ETag del
    listado.
18. Reordenar criterios sin `If-Match` devuelve `428`.
19. Reordenar criterios con un ETag obsoleto devuelve `412`.
20. Una reordenación válida devuelve `204` y el nuevo ETag del listado.
21. Una reordenación con ETag actual pero identificadores incompatibles conserva
    los errores `400` o `409` existentes.
22. Crear y consultar una tarea devuelve su ETag individual.
23. Editar una tarea sin `If-Match` devuelve `428`.
24. Editar una tarea con ETag obsoleto devuelve `412` y no la modifica.
25. Un `PUT` de tarea idéntico conserva su ETag.
26. Iniciar o completar una tarea sin `If-Match` devuelve `428`.
27. Iniciar o completar con un ETag obsoleto devuelve `412` antes de evaluar la
    transición.
28. Una transición no permitida con ETag actual devuelve `409`.
29. Una transición válida devuelve el ETag individual nuevo.
30. Eliminar una tarea requiere su ETag actual y un ETag obsoleto devuelve `412`
    sin eliminar ni compactar posiciones.
31. Listar tareas devuelve un ETag fuerte de colección.
32. Editar, crear, eliminar, mover o transicionar una tarea cambia el ETag del
    listado.
33. Reordenar tareas sin `If-Match` devuelve `428`.
34. Reordenar tareas con ETag obsoleto devuelve `412`.
35. Una reordenación válida devuelve `204` y el nuevo ETag del listado.
36. Cambiar criterios no invalida el ETag del listado de tareas y cambiar tareas
    no invalida el ETag del listado de criterios.
37. `If-Match: *`, un ETag débil, una lista de ETags o un valor mal formado
    devuelve `400`.
38. Los errores `412` y `428` utilizan `application/problem+json` y no exponen
    versiones internas.
39. Una carrera de persistencia posterior a validar `If-Match` devuelve `412` y
    no sobrescribe cambios.
40. Las versiones y los ETags permanecen estables entre scopes y reinicios si
    el estado no cambia.
41. Las especificaciones anteriores a la migración quedan con versión cero.
42. OpenAPI documenta los headers y respuestas de concurrencia de todas las
    operaciones afectadas.
43. Las rutas y representaciones JSON existentes no cambian.
44. La solución compila sin warnings, todas las pruebas pasan y el formato es
    correcto.

## Requisitos no funcionales

- Las comparaciones de ETag no dependen de cultura ni mayúsculas.
- Los ETags no contienen secretos ni información sensible.
- El cálculo de ETags de listado es determinista y proporcional al número de
  elementos ya recuperados para producir la respuesta.
- No se realizan consultas adicionales por elemento.
- Las operaciones condicionadas siguen siendo asíncronas y propagan
  `CancellationToken`.
- Las consultas de lectura continúan usando `AsNoTracking`.
- Las fechas continúan almacenándose en UTC con precisión de milisegundos.
- Las transacciones de colección conservan atomicidad en SQLite.
- El diseño mantiene compatibilidad conceptual con PostgreSQL.
- La API no depende de tipos ni códigos específicos de SQLite.
- Las respuestas no exponen stack traces, SQL, rutas locales ni detalles de EF
  Core.

## Estructura prevista

- `src/SpecFlow.Domain/Specifications`: versión y reglas de actualización de la
  especificación.
- `src/SpecFlow.Infrastructure/Persistence`: configuración de versiones,
  comparación atómica y migración.
- `src/SpecFlow.Api`: lectura de `If-Match`, generación de ETags y Problem
  Details de precondición.
- `src/SpecFlow.Api/Endpoints`: aplicación consistente de precondiciones en las
  operaciones afectadas.
- `tests/SpecFlow.Domain.Tests`: versión de especificación.
- `tests/SpecFlow.Api.IntegrationTests`: headers, errores, carreras, migración,
  persistencia y OpenAPI.

No se crea una capa `Application`. Los componentes HTTP de ETag permanecen en
la API y las operaciones de persistencia permanecen en Infrastructure.

## Riesgos y mitigaciones

### Cambio incompatible para consumidores actuales

Los `PUT`, `DELETE`, transiciones y reordenaciones afectadas empezarán a exigir
`If-Match`. Es un cambio deliberado del contrato. README y OpenAPI deben mostrar
el flujo lectura-modificación completo y las pruebas deben verificar `428`.

### ETag de colección incorrecto

Un ETag basado sólo en composición u orden no detectaría cambios de contenido o
estado visibles en el listado. Se incluirán las versiones individuales de todos
los elementos en el cálculo y se cubrirá cada tipo de cambio con pruebas.

### Conflictos artificiales entre ámbitos independientes

Usar un único concurrency token para toda `Specification` haría competir
cambios independientes. Se mantendrán tres ámbitos: contenido, criterios y
tareas.

### Carrera entre la precondición y la escritura

La comparación HTTP por sí sola no es suficiente. Las versiones de EF Core y
las actualizaciones condicionales internas seguirán protegiendo la escritura,
traduciendo el conflicto posterior a `412`.

### Conversión de errores de negocio en precondiciones

Una tarea puede tener un ETag actual y no admitir una transición. El orden de
evaluación y las pruebas conservarán `409` para reglas de dominio y `412` para
estado obsoleto.

## Decisiones aprobadas

- `If-Match` será obligatorio en todas las modificaciones de recursos
  existentes incluidas en el alcance.
- La ausencia de `If-Match` devolverá `428 Precondition Required`.
- Un ETag obsoleto devolverá `412 Precondition Failed`.
- Un header no admitido devolverá `400 Bad Request`.
- Sólo se aceptará exactamente un ETag fuerte; no se aceptarán wildcard, ETags
  débiles ni listas.
- Los ETags serán headers opacos y no propiedades JSON.
- Las creaciones no requerirán precondición.
- Las reordenaciones utilizarán el ETag del listado completo.
- Los ETags de listado incluirán las versiones individuales de los elementos.
- Las tres versiones de especificación, criterios y tareas permanecerán
  independientes.
- Las carreras posteriores a validar `If-Match` devolverán `412`.
- Las transiciones inválidas y duplicados conservarán `409`.
- No se incorporará todavía caché condicional mediante `If-None-Match`.

## Plan de implementación

1. Añadir `Version` a `Specification` y cubrir sus reglas con pruebas unitarias.
2. Configurar la concurrencia de contenido y generar la migración.
3. Separar `AcceptanceCriteriaVersion` del concurrency token global y aplicar
   actualización condicional atómica a su colección.
4. Implementar generación y validación común de ETags fuertes en la API.
5. Implementar Problem Details comunes para `400`, `412` y `428`.
6. Incorporar ETag en creación, consulta y edición de especificaciones.
7. Incorporar ETags individuales y precondiciones en criterios.
8. Incorporar el ETag de listado y la reordenación condicionada de criterios.
9. Incorporar ETags individuales y precondiciones en tareas y transiciones.
10. Incorporar el ETag de listado y la reordenación condicionada de tareas.
11. Verificar eliminaciones, compactación y carreras posteriores a `If-Match`.
12. Verificar independencia entre los tres ámbitos de versión.
13. Verificar migración y persistencia entre reinicios.
14. Actualizar OpenAPI y README con ejemplos de lectura y escritura condicionada.
15. Ejecutar restauración, compilación, pruebas y formato.
16. Auditar cada criterio de aceptación antes de cambiar el estado de la
    especificación.
