# Constitución de SpecFlow

## Estado y versión

- Estado: vigente.
- Versión: 1.0.0.
- Ratificada: 8 de octubre de 2026.
- Última modificación: 8 de octubre de 2026.
- Origen: reglas consolidadas durante las especificaciones 0001 a 0010.

## Propósito

Esta constitución recoge las reglas estables compartidas por los incrementos de
SpecFlow. Su objetivo es evitar que cada especificación repita decisiones de
arquitectura, contratos HTTP, persistencia, pruebas y calidad ya acordadas.

Las especificaciones numeradas describen cambios concretos del producto. Deben
ser comprensibles por sí mismas en lo que respecta a su comportamiento, pero
pueden referenciar esta constitución para las reglas transversales.

## Jerarquía documental

1. Esta constitución define las reglas compartidas vigentes.
2. Una especificación numerada y aprobada define el comportamiento particular
   de su incremento.
3. Una especificación posterior puede sustituir comportamiento de una anterior
   cuando lo declare expresamente.
4. Una especificación no puede contradecir silenciosamente la constitución. Si
   necesita hacerlo, debe proponer y justificar primero una enmienda.
5. `AGENTS.md` contiene instrucciones operativas para trabajar en el repositorio;
   no sustituye los contratos de producto.
6. El README orienta al usuario, pero no sustituye la constitución ni las specs.
7. Código y pruebas demuestran la implementación; una discrepancia con la
   documentación aprobada debe resolverse explícitamente, no asumirse como una
   modificación del contrato.

Las specs 0001 a 0010 permanecen intactas como registro histórico del aprendizaje
del proyecto. Esta constitución no cambia retroactivamente su significado.

## Principios de desarrollo

### Flujo spec-driven

- Ningún cambio funcional comienza sin una spec revisada y aprobada.
- Los cambios de alcance, contrato o arquitectura se documentan antes de tocar
  el código afectado.
- Cada incremento debe ser vertical, pequeño y trazable a criterios de
  aceptación observables.
- No se amplía silenciosamente el alcance durante la implementación.
- Un defecto descubierto incorpora una prueba de regresión cuando sea razonable.
- La simplicidad y el uso concreto prevalecen sobre la extensibilidad hipotética.

### Límites de arquitectura

- `SpecFlow.Domain` contiene entidades, invariantes, normalización y
  transiciones. No depende de ASP.NET Core, EF Core ni SQLite.
- `SpecFlow.Infrastructure` contiene EF Core, configuración de persistencia,
  migraciones y comportamiento específico del proveedor.
- `SpecFlow.Api` compone la aplicación y contiene contratos HTTP, endpoints,
  OpenAPI y traducción de errores.
- Las entidades de dominio no se devuelven directamente mediante HTTP.
- Las reglas del dominio no se duplican en endpoints o configuraciones de EF.
- No se crea una capa `Application` hasta que exista lógica de casos de uso o un
  segundo adaptador que justifique extraer la orquestación.
- CQRS, MediatR, event sourcing, repositorios genéricos, una unit of work propia
  y abstracciones equivalentes requieren un caso de uso aprobado.

## Convenciones de dominio

- Los nombres técnicos, tipos, propiedades, rutas internas e identificadores se
  escriben en inglés.
- Los identificadores persistentes son UUID no vacíos generados por la
  aplicación, salvo que una spec apruebe otra estrategia.
- Toda normalización forma parte de una regla explícita del dominio.
- El contenido Markdown se trata como texto opaco. Se conserva exactamente y no
  se interpreta, ejecuta ni renderiza salvo que una spec indique lo contrario.
- El comportamiento temporal utiliza `TimeProvider`.
- Las fechas se normalizan a UTC con precisión de milisegundos.
- La creación establece desde el reloj las fechas que correspondan; el cliente
  no proporciona identificadores, versiones ni fechas gestionadas por el
  sistema.
- Una actualización idéntica no cambia fechas ni versiones, salvo que la spec
  defina expresamente otro comportamiento observable.

## Contratos HTTP

- Los endpoints son asíncronos y propagan `CancellationToken`.
- Las consultas de solo lectura utilizan `AsNoTracking`.
- Las creaciones responden `201 Created` y proporcionan `Location` cuando existe
  una ruta individual para el recurso.
- Los listados responden `200 OK` y devuelven `[]` cuando el contexto existe pero
  no contiene elementos.
- Los identificadores de ruta inválidos responden `400 Bad Request` mediante
  Validation Problem Details y conservan una clave de campo estable.
- Un recurso inexistente responde `404 Not Found` mediante Problem Details.
- Las rutas anidadas respetan el aislamiento del contexto: consultar un recurso
  desde otro proyecto, propuesta o especificación no revela su existencia.
- Los conflictos de negocio responden `409 Conflict` con un título estable y
  documentado por la spec que los introduce.
- Los endpoints con body aceptan JSON compatible y responden
  `415 Unsupported Media Type` para tipos no admitidos.
- Las respuestas no exponen stack traces, rutas locales, cadenas de conexión,
  versiones internas ni detalles de EF Core o SQLite.
- OpenAPI documenta rutas, operaciones, cuerpos, respuestas y headers
  observables.
- Un contrato existente no cambia salvo que una spec aprobada identifique el
  comportamiento sustituido y sus consecuencias de compatibilidad.

## Concurrencia HTTP

- Todo recurso editable expone un ETag fuerte y opaco derivado de su identidad y
  versión interna. La versión no forma parte del JSON.
- Modificar un recurso existente exige su ETag vigente mediante `If-Match`.
- La ausencia de la precondición responde `428 Precondition Required`.
- Un `If-Match` no admitido responde `400 Bad Request`.
- Un ETag obsoleto o una carrera posterior responde
  `412 Precondition Failed` sin aplicar cambios parciales.
- No se admiten `If-Match: *`, ETags débiles ni múltiples entity-tags salvo que
  una spec amplíe expresamente el contrato.
- Crear un recurso no exige `If-Match` porque todavía no existe el recurso
  individual.
- Un listado solo necesita ETag de colección cuando una operación condicionada
  actúa sobre la colección completa.
- Las precondiciones HTTP no sustituyen restricciones, transacciones ni tokens
  de concurrencia en persistencia.

Para una operación condicionada se evalúan, en este orden observable:

1. formato de identificadores;
2. validación del body;
3. existencia y pertenencia al contexto;
4. presencia, formato y coincidencia de `If-Match`;
5. reglas de dominio;
6. persistencia protegida frente a carreras.

## Persistencia y migraciones

- SQLite es el proveedor local y de pruebas de integración.
- La configuración debe conservar la posibilidad conceptual de migrar a
  PostgreSQL sin incorporar todavía ese proveedor.
- Las relaciones y reglas que protegen integridad o unicidad se refuerzan con
  foreign keys, restricciones e índices cuando corresponda.
- Las eliminaciones de relaciones actuales son restrictivas salvo decisión
  expresa de una spec.
- Todo cambio de esquema incluye una migración de EF Core y pruebas relevantes.
- Una migración ya integrada no se modifica salvo razón explícita y revisada.
- Los datos temporales se almacenan con precisión de milisegundos y se exponen
  como fechas ISO 8601 en UTC.
- Las pruebas de integración utilizan SQLite real, normalmente en memoria con
  una conexión abierta durante cada prueba. No utilizan EF Core InMemory ni mocks
  de EF Core.

## Calidad y pruebas

- El proyecto utiliza la versión de .NET y C# declarada en `global.json` y
  `Directory.Build.props`.
- Nullable reference types e implicit usings permanecen habilitados.
- Los warnings se tratan como errores y se respetan los analizadores
  configurados.
- Las invariantes, normalización y transiciones se prueban en el dominio.
- Los contratos, validación, Problem Details, aislamiento, persistencia,
  concurrencia y OpenAPI se prueban mediante HTTP real cuando corresponda.
- Las pruebas son deterministas, no dependen de la hora real y no comparten
  estado.
- Los escenarios de persistencia entre reinicios utilizan archivos SQLite
  temporales.
- No se cambian expectativas de pruebas para acomodar involuntariamente una
  implementación incompatible.

## Definición de terminado

Un incremento está terminado cuando:

- satisface todos sus criterios de aceptación;
- mantiene el aislamiento entre contextos;
- incluye pruebas proporcionales y todas pasan;
- compila sin warnings;
- el formato no presenta cambios pendientes;
- el modelo de EF Core no tiene migraciones pendientes;
- incluye la migración requerida por cualquier cambio de esquema;
- actualiza OpenAPI, README y la propia spec cuando corresponde;
- no incorpora comportamiento fuera de alcance;
- registra en la spec la evidencia final de verificación.

## Alcance que requiere aprobación explícita

No se incorporan sin una spec aprobada:

- autenticación, autorización, usuarios, equipos u organizaciones;
- frontend;
- MCP, skills, agentes o integración con modelos de IA;
- Docker, Aspire, despliegue o PostgreSQL;
- nuevos paquetes, herramientas o servicios;
- cambios significativos de arquitectura.

Que una capacidad quede fuera de una spec no implica que esté prohibida para
siempre; significa que necesita su propio problema, alcance, decisiones y
criterios de aceptación.

## Enmiendas

- Toda enmienda debe explicar la motivación y el impacto sobre specs y contratos
  existentes.
- Una corrección editorial incrementa la versión de parche.
- Una regla nueva compatible incrementa la versión menor.
- Una redefinición incompatible de principios o jerarquía incrementa la versión
  mayor.
- La fecha y versión de la constitución se actualizan en el mismo cambio que la
  enmienda.
