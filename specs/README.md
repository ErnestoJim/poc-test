# Especificaciones de SpecFlow

Este directorio contiene la historia funcional y metodológica de SpecFlow. Las
specs no son solo documentación del producto: también muestran cómo ha evolucionado
el uso de spec-driven development durante el proyecto.

## Cómo leer la documentación

1. Leer [`constitution.md`](constitution.md) para conocer las reglas compartidas
   vigentes.
2. Leer la spec numerada relacionada con el comportamiento que se quiere
   comprender o modificar.
3. Consultar specs anteriores cuando la spec actual declare que sustituye o
   amplía su comportamiento.
4. Usar [`spec-template.md`](spec-template.md) para proponer un incremento nuevo.

La constitución define reglas transversales. Las specs numeradas definen cambios
concretos. Una spec posterior solo sustituye comportamiento anterior cuando lo
declara expresamente.

## Evolución del formato

### Specs 0001–0010: formato exhaustivo

Las primeras specs son deliberadamente detalladas y autocontenidas. Repiten
reglas de arquitectura, persistencia, HTTP, pruebas y calidad para hacer visible
el razonamiento completo de cada incremento.

Este formato ayudó a establecer las convenciones del proyecto, pero su crecimiento
mostró dos costes:

- era difícil distinguir una decisión nueva de una regla ya establecida;
- mantener texto repetido aumentaba el riesgo de contradicciones documentales.

Se conservan sin reescribir porque forman parte del registro histórico y permiten
comparar ambas formas de trabajar.

### Desde la spec 0011: constitución y deltas

La constitución 1.0.0 consolida las reglas estables aprendidas durante las specs
0001–0010. Las specs nuevas utilizan una plantilla reducida y documentan
principalmente el delta funcional.

El objetivo no es aplicar DRY de forma extrema. Una spec debe seguir conteniendo
todo lo necesario para comprender su comportamiento propio, especialmente
contratos, invariantes, errores y criterios de aceptación.

## Ciclo de una spec

1. **Borrador:** problema, alcance y decisiones todavía pueden cambiar.
2. **Revisada:** las preguntas importantes están resueltas.
3. **Aprobada para implementación:** el contrato puede convertirse en código.
4. **Implementada y verificada:** los criterios están cubiertos y se registra la
   evidencia de cierre.

La aprobación es explícita. Crear un archivo no autoriza por sí mismo su
implementación.

## Histórico funcional

| Spec | Incremento | Estado |
|---|---|---|
| [0001](0001-projects/spec.md) | Gestión básica de proyectos | Implementada |
| [0002](0002-feature-proposals/spec.md) | Gestión básica de propuestas | Implementada |
| [0003](0003-feature-proposal-lifecycle/spec.md) | Ciclo de vida de propuestas | Implementada |
| [0004](0004-specifications/spec.md) | Gestión básica de especificaciones | Implementada |
| [0005](0005-acceptance-criteria/spec.md) | Criterios de aceptación | Implementada |
| [0006](0006-implementation-tasks/spec.md) | Tareas de implementación | Implementada |
| [0007](0007-implementation-task-lifecycle/spec.md) | Ciclo de vida de tareas | Implementada |
| [0008](0008-api-maintainability-checkpoint/spec.md) | Checkpoint de mantenibilidad | Implementada |
| [0009](0009-http-concurrency/spec.md) | Concurrencia HTTP | Implementada |
| [0010](0010-technical-decisions/spec.md) | Decisiones técnicas | Implementada |
| [0011](0011-technical-decision-lifecycle/spec.md) | Ciclo de vida de decisiones | Implementada |
| [0012](0012-acceptance-criterion-task-traceability/spec.md) | Trazabilidad entre criterios y tareas | Aprobada |

## Crear la siguiente spec

- Copiar `spec-template.md` a `specs/NNNN-slug/spec.md`.
- Indicar la versión de la constitución utilizada.
- Eliminar instrucciones y secciones que no correspondan.
- Documentar solo reglas nuevas, cambios y excepciones.
- Mantener criterios de aceptación observables.
- No cambiar el estado a aprobada hasta resolver las decisiones pendientes.
