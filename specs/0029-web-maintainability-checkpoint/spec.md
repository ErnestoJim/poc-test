# 0029 — Checkpoint de mantenibilidad de la interfaz web

## Estado

Implementada y verificada el 16 de octubre de 2026, tras su aprobación.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0008-api-maintainability-checkpoint/spec.md` y
  las specs web de la 0016 a la 0028.

Este incremento reorganiza código de `SpecFlow.Web` y de sus pruebas sin
cambiar el comportamiento observable. No cambia contratos HTTP, dominio ni
persistencia.

## Problema

La interfaz web ha crecido en trece incrementos verticales y ya cubre el ciclo
completo de proyectos, propuestas, especificaciones y decisiones técnicas. Las
specs 0024, 0025 y 0027 registraron de forma deliberada la duplicación entre
las secciones de criterios y de tareas para resolverla en este checkpoint.

En concreto:

- `ImplementationTasksSection` (1.445 líneas) y `AcceptanceCriteriaSection`
  (1.235 líneas) repiten la misma coordinación con la vista: bloqueo, aviso de
  edición activa, relectura de la especificación, avisos, pérdida de contexto y
  el esqueleto de recuperación (`BeginSend`, `AfterChangeAsync`,
  `HandleFailureAsync`, `ClassifyConflictAsync`, `HandleMissingAsync`,
  `HandleSpecificationErrorAsync`, `RecoverAsync`, `ReloadAllAsync`,
  `MoveLocalTextToUnsaved`, `OnAfterRenderAsync`).
- El bloque de aviso con «Consultar estado actual» aparece en 8 lugares, el de
  texto no guardado en 8 y la confirmación de descarte en 6, con el mismo
  marcado.
- Hay 9 funciones de etiqueta o frase de estado repartidas en 6 componentes,
  `FormatDate` está definido o repetido en 7 lugares y la enumeración de
  números en español («1, 2 y 3») está implementada dos veces.
- El registro `ContextLost`, que comparten ambas secciones, vive dentro de
  `AcceptanceCriteriaSection`.
- La validación de identificadores de ruta se repite en 9 lugares y los
  errores locales se construyen a mano en 12.
- Varias clases CSS con nombre de criterio (`criterion-row`, `task-links`,
  `coverage-summary`) se usan ya en tareas, verificaciones y decisiones.
- `SpecFlowApiClientTests` (974 líneas) repite por recurso la construcción de
  respuestas de error y las pruebas de fallo de conexión.

Esta duplicación aún no impide evolucionar el producto, pero un cambio en el
tratamiento de conflictos tendría que aplicarse en varios sitios y es fácil que
las secciones diverjan. Antes de entregar el producto al equipo de pruebas y de
añadir MCP y Docker conviene consolidarla.

## Objetivo

Reducir la duplicación concreta de la interfaz web y de sus pruebas con
refactorizaciones pequeñas y verificables, de modo que la web se comporte
exactamente igual para la persona usuaria.

## Principios del checkpoint

1. El comportamiento existente, recogido en la evidencia de las specs 0016 a
   0028, actúa como contrato de caracterización.
2. Cada extracción responde a duplicación ya presente, no a una necesidad
   hipotética.
3. Una abstracción que no reduzca claramente duplicación o riesgo se descarta.
4. Los textos visibles, el marcado accesible y el orden de las peticiones HTTP
   no cambian.
5. El refactor se hace en pasos pequeños que dejan el repositorio compilando y
   con las pruebas en verde después de cada uno.

## Alcance

### C1 — Base común de las secciones del plan

Clase base `PlanSectionBase` para `AcceptanceCriteriaSection` e
`ImplementationTasksSection` con los parámetros de coordinación (`Locked`,
`OnActiveChanged`, `ReloadSpecification`, `OnContextLost`,
`SpecificationEntityTag`), el aviso de edición activa, el estado de avisos y el
esqueleto de envío, relectura y recuperación. Cada sección conserva solo su
lógica propia: textos, validaciones, verificación de criterios, vínculos y
transiciones de tareas. `ContextLost` pasa a un archivo propio.

### C2 — Componentes de bloques repetidos

Componentes `StatusNotice` (aviso, error y «Consultar estado actual»),
`UnsavedTextBlock` (texto no guardado) y `DiscardConfirmation` (confirmación de
descarte), usados por las secciones y las páginas de detalle.

### C3 — Presentación compartida

Clase estática `DisplayText` con el formato de fechas, las etiquetas y frases
de estado de propuestas, especificaciones, tareas, verificaciones y decisiones,
y la enumeración de números en español.

### C4 — Identificadores de ruta y errores locales

Utilidad para validar los identificadores de ruta de las páginas y fábrica de
`ApiError` locales, con los mismos mensajes actuales.

### C5 — Nombres CSS neutros

Renombrar las clases compartidas a nombres neutros (por ejemplo `plan-row`,
`inline-form`, `summary-banner`) sin cambiar su aspecto.

### C6 — Pruebas del cliente HTTP

Separar `SpecFlowApiClientTests` por recurso con utilidades comunes para
respuestas con ETag, respuestas de error y fallos de conexión, sin perder
ningún caso existente.

### C7 — Pruebas de componentes con bUnit

Añadir el paquete de pruebas `bunit` a `SpecFlow.Web.Tests` para cubrir con
pruebas automáticas las ramas de recuperación que no se pudieron reproducir en
navegador (fallo aislado de la cobertura y de las verificaciones, transiciones
de tareas con resultado incierto) y el bloqueo mutuo de la vista. Sustituye la
decisión de la 0021 de no usar bUnit. La dependencia se autorizó al aprobar
esta spec y solo afecta al proyecto de pruebas.

## Fuera de alcance

- Cualquier cambio de comportamiento, texto visible o diseño.
- Traducir los Problem Details de la API, que cambia un contrato y se valorará
  con MCP.
- Refactorizar la API, el dominio o la infraestructura.
- Fusionar `AcceptanceCriteriaSection` e `ImplementationTasksSection` en un
  componente genérico: sus reglas difieren demasiado y el resultado sería más
  difícil de leer.
- Cambiar el cliente HTTP más allá de lo que requieran C3 y C4.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Un paquete nuevo, `bunit`, solo en el proyecto de pruebas (C7).

## Criterios de aceptación

1. Las secciones de criterios y tareas heredan de una base común y ya no
   repiten la coordinación ni el esqueleto de recuperación.
2. Los bloques de aviso con recuperación, texto no guardado y confirmación de
   descarte se renderizan desde componentes compartidos en todos sus usos.
3. Etiquetas de estado, formato de fechas y enumeración de números tienen una
   sola implementación.
4. La validación de identificadores de ruta y los errores locales tienen una
   sola implementación.
5. Ninguna clase CSS con nombre de un recurso concreto se usa para otro.
6. Las pruebas del cliente HTTP conservan todos sus casos, separadas por
   recurso y sin repetir la construcción de respuestas.
7. Los textos visibles, los atributos accesibles y la secuencia de peticiones
   HTTP de cada recorrido son idénticos a los de antes del checkpoint.
8. Compilación sin warnings, todas las pruebas en verde y formato correcto
   después de cada paso.
9. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
   esquema de SQLite.
10. Las pruebas de componentes cubren las ramas de recuperación citadas y el
    bloqueo mutuo de la vista.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Se extrae una base común en lugar de un componente genérico, para que cada
  sección siga leyéndose de arriba abajo con sus reglas propias.
- Los componentes compartidos reciben el texto por parámetro; no generan
  mensajes propios.
- Se incluye C7: bUnit cubre ramas que el navegador no permite reproducir y
  solo se usa en pruebas.
- Este checkpoint cierra la fase web; después el producto pasa al equipo de
  pruebas antes de MCP y Docker.

## Plan de verificación

- Compilación, pruebas y formato después de cada paso (C1 a C7).
- Comparación del texto visible y de los atributos accesibles antes y después
  del checkpoint en todas las páginas y estados, con el mismo recorrido
  automatizado en navegador sobre una base de datos sembrada igual.
- Repetición en navegador de un recorrido completo, de borrador a verificada,
  con un conflicto `412` y una pérdida de conexión en cada sección, y del
  recorrido de decisiones técnicas.
- Ausencia de cambios en el modelo EF Core y en OpenAPI.

## Evidencia de cierre

- C1: `PlanSectionBase<TOperation>` reúne los parámetros de coordinación, el
  aviso de edición activa, los avisos y el esqueleto de envío, relectura y
  recuperación (`AfterChangeAsync`, `HandleFailureAsync`,
  `HandleSpecificationErrorAsync`, `ReloadForMissingAsync`, `RecoverAsync`,
  `ReloadAllAsync`). Cada sección implementa solo `LoadListAsync`,
  `ClassifyConflictAsync`, `HandleMissingAsync`, `RecoverWithAsync`, su texto
  no guardado y su aviso de especificación desactualizada.
  `AcceptanceCriteriaSection` pasó de 1.235 a 1.063 líneas e
  `ImplementationTasksSection` de 1.445 a 1.273. `ContextLost` y las
  operaciones pendientes tienen archivo propio. Los campos compartidos son
  propiedades protegidas, porque el analizador (CA1051) no admite campos
  visibles.
- C2: `StatusNotice`, `UnsavedTextBlock` y `DiscardConfirmation` sustituyen
  los 7 avisos con recuperación, los 8 bloques de texto no guardado y las 6
  confirmaciones de descarte.
- C3: `DisplayText` concentra formato de fechas, etiquetas y frases de estado
  y la enumeración de números; ya no queda ninguna copia en los componentes.
  Las fechas se formatean con cultura invariable, que produce el mismo texto.
- C4: `RouteIds` y `ApiError.Local` sustituyen las 9 validaciones de
  identificadores y los 12 errores construidos a mano.
- C5: las clases compartidas se renombraron (`item-row`, `item-content`,
  `inline-form`, `choice-form`, `summary-banner`, `entity-row`, entre otras);
  solo quedan con nombre de recurso las que usa ese recurso. Capturas en
  navegador de vínculos, verificación y lista de proyecto confirmaron el mismo
  aspecto.
- C6: las pruebas del cliente se separaron en seis archivos por recurso con
  `ClientTestSupport` (respuestas con ETag, `Problem(...)` y los manejadores
  falsos). Las nueve pruebas de pérdida de conexión se unificaron en una teoría
  con una fila por operación (31), que cubre todas las operaciones antes
  repartidas. Las pruebas del cliente pasaron de 974 a 915 líneas.
- C7: `bunit` 2.11.3 en `SpecFlow.Web.Tests` con un API simulada por ruta.
  Siete pruebas de componentes: fallo aislado de la cobertura con reintento,
  fallo aislado de las verificaciones con reintento, inicio de tarea con
  resultado incierto en sus dos desenlaces sin reenvío, `412` al iniciar una
  tarea, y bloqueo de la vista con texto en el alta de criterios y al editar el
  contenido. Una mutación temporal que quitaba el bloqueo de tareas por
  criterios hizo fallar la prueba de bloqueo; se restauró.
- Caracterización: antes del refactor se sembró una base con todos los estados
  (propuestas pendiente, rechazada y aceptada; especificaciones en borrador,
  aprobada, implementada y verificada; decisiones en los cuatro estados) y se
  congeló. Un recorrido automatizado de 21 páginas con formularios y
  confirmaciones abiertos recogió 43 instantáneas de título, texto visible y
  atributos (`id`, `role`, `aria-*`, `disabled`, `checked`, `for`, `href`,
  longitud de los valores). Repetido sobre una copia de la misma base tras
  C2-C5, tras C1 y al final, no hubo ninguna diferencia.
- Navegador con la versión final: `412` editando un criterio con «Guardar mi
  versión»; pérdida de conexión al añadir un criterio («no se añadió», texto
  conservado y tareas bloqueadas) y al añadir una tarea («se añadió
  correctamente» con la cobertura recargada); `412` editando una decisión
  técnica con ambas versiones.
- Desviación del plan: no se repitió en navegador el recorrido completo de
  borrador a verificada; las transiciones no cambiaron y sus recuperaciones
  están cubiertas por la caracterización y las pruebas de componentes.
- `dotnet build SpecFlow.slnx --no-restore`: correcto, 0 warnings, después de
  cada paso.
- `dotnet test SpecFlow.slnx --no-build`: 567 pruebas correctas (127 de
  dominio, 339 de integración y 101 web).
- `dotnet format SpecFlow.slnx --verify-no-changes --no-restore` y
  `git diff --check`: correctos.
- `dotnet ef migrations has-pending-model-changes`: sin cambios del modelo; sin
  cambios en la API ni en OpenAPI.
