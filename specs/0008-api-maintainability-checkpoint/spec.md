# 0008 — Checkpoint de mantenibilidad de la API

## Estado

Implementada y verificada el 7 de octubre de 2026, tras su aprobación para
implementación el mismo día.

## Problema

SpecFlow ha crecido mediante siete incrementos verticales y ya ofrece un flujo
completo desde la creación de un proyecto hasta la ejecución de tareas de una
especificación. El diseño mantiene separados dominio, persistencia y API, pero
la incorporación progresiva de recursos anidados ha producido duplicación y
complejidad accidental en el adaptador HTTP.

En particular:

- los endpoints de criterios de aceptación y tareas de implementación repiten
  la resolución de proyecto, propuesta y especificación;
- varios grupos de endpoints repiten el parseo de identificadores y la creación
  de Problem Details equivalentes;
- la API identifica directamente errores específicos de SQLite para traducir
  violaciones de unicidad;
- los archivos de endpoints de criterios y tareas concentran registro de rutas,
  consultas, comandos, transacciones, traducción de errores y utilidades;
- las pruebas de integración repiten parte de la preparación de escenarios y
  de las comprobaciones de errores HTTP.

Esta duplicación todavía no impide evolucionar el producto, pero aumenta el
riesgo de que futuros recursos anidados respondan de forma inconsistente o que
un cambio de contrato tenga que aplicarse en varios lugares. Antes de añadir
nuevas capacidades conviene consolidar estas responsabilidades sin modificar
el comportamiento observable.

## Objetivo

Mejorar la mantenibilidad del adaptador HTTP y de sus pruebas mediante
refactorizaciones pequeñas y verificables que eliminen duplicación concreta,
conserven todos los contratos existentes y mantengan los límites
arquitectónicos actuales.

El resultado debe seguir siendo la misma aplicación desde el punto de vista de
un consumidor HTTP. Este incremento no añade funcionalidad de producto ni
introduce una arquitectura prevista para necesidades futuras.

## Principios del checkpoint

1. El comportamiento existente actúa como contrato de caracterización.
2. Cada extracción debe responder a duplicación ya presente, no a una necesidad
   hipotética.
3. Las reglas de negocio continúan perteneciendo al dominio.
4. La API continúa siendo responsable de HTTP, rutas, contratos y Problem
   Details.
5. Infrastructure continúa siendo responsable de EF Core, SQLite y la
   interpretación de errores del proveedor de persistencia.
6. El refactor se realizará en pasos pequeños que dejen el repositorio
   verificable después de cada paso.
7. Una abstracción que no reduzca claramente duplicación o riesgo debe
   descartarse.

## Alcance

- Establecer y verificar una línea base de comportamiento antes del refactor.
- Centralizar la construcción de los Problem Details compartidos por varios
  grupos de endpoints.
- Centralizar el parseo y validación de los identificadores utilizados en rutas
  anidadas cuando sus reglas y mensajes sean idénticos.
- Centralizar la resolución del contexto formado por proyecto, propuesta y
  especificación para criterios de aceptación y tareas de implementación.
- Conservar la posibilidad de realizar la resolución con o sin seguimiento de
  EF Core según la operación que la consume.
- Encapsular en Infrastructure la detección de violaciones de restricciones
  específicas de SQLite que actualmente conoce la API.
- Separar físicamente responsabilidades dentro de los endpoints grandes cuando
  la separación mejore su navegación y lectura.
- Consolidar helpers de preparación de escenarios HTTP repetidos en las pruebas
  de integración.
- Consolidar assertions repetidas de Problem Details cuando mantengan visible
  el contrato que verifica cada prueba.
- Añadir pruebas de regresión únicamente si durante el refactor se identifica
  un comportamiento existente relevante que no estaba cubierto.
- Mantener actualizados README y OpenAPI sólo si la reorganización interna
  obliga a corregir documentación inexacta; no se añadirán operaciones.

## Fuera de alcance

- Nuevos endpoints o eliminación de endpoints existentes.
- Cambios en rutas, métodos HTTP, requests, responses, cabeceras o códigos de
  estado.
- Cambios en títulos, detalles o estructura de los Problem Details existentes.
- Cambios en las reglas, entidades o excepciones del dominio.
- Cambios de esquema o nuevas migraciones de EF Core.
- Nuevas capacidades de edición, eliminación, búsqueda, filtrado o paginación.
- Cambios en la semántica de concurrencia existente.
- ETags, `If-Match`, versiones públicas o nuevos conflictos HTTP.
- Resolver en este incremento posibles carreras entre la edición de una
  especificación y la modificación de sus colecciones.
- Sustitución de SQLite o incorporación de PostgreSQL.
- Creación de una capa `Application`.
- CQRS, MediatR, event sourcing, repositorios genéricos o unit of work propia.
- Un tipo `Result` transversal o una jerarquía genérica de errores.
- Un framework genérico de endpoints CRUD.
- Reflexión, generación de código o metaprogramación para registrar endpoints.
- Cambios de autenticación, frontend, MCP, skills o integración con modelos de
  IA.
- Nuevos paquetes, salvo que durante la revisión se apruebe explícitamente una
  necesidad concreta.

La concurrencia HTTP se tratará en una especificación posterior porque requiere
decidir nuevo comportamiento observable y no forma parte de un refactor
compatible.

## Casos de mantenimiento

### MC-01 — Resolver un contexto anidado de forma consistente

Los endpoints de criterios de aceptación y tareas de implementación necesitan
comprobar el mismo recorrido: el proyecto debe existir, la propuesta debe
pertenecer al proyecto y la especificación debe pertenecer a la propuesta.

La implementación compartida debe preservar el orden actual de las
comprobaciones, el aislamiento entre proyectos y los Problem Details devueltos
en cada fallo.

### MC-02 — Validar identificadores de ruta de forma consistente

Cuando distintos endpoints reciben los mismos identificadores, deben aplicar la
misma regla de formato y producir el mismo Validation Problem Details. La
extracción no debe impedir que identificadores específicos, como `criterionId`
o `taskId`, mantengan sus mensajes propios.

### MC-03 — Producir errores HTTP comunes desde una única definición

Los errores `Project not found`, `Feature proposal not found` y
`Specification not found` deben conservar exactamente sus títulos, detalles y
códigos de estado, pero no deben mantenerse mediante copias independientes en
cada grupo de endpoints.

Los errores exclusivos de una operación o recurso permanecen junto a su grupo
de endpoints salvo que exista duplicación real que justifique otra extracción.

### MC-04 — Traducir conflictos de persistencia sin filtrar SQLite a los handlers

Los endpoints deben poder preguntar si un `DbUpdateException` representa una
violación de unicidad sin inspeccionar directamente códigos de `SqliteException`.
El conocimiento del proveedor queda encapsulado en Infrastructure.

La API seguirá decidiendo qué Problem Details corresponde al caso de uso; la
infraestructura no producirá respuestas HTTP.

### MC-05 — Reutilizar escenarios de pruebas sin ocultar el contrato

Las pruebas pueden reutilizar la creación de un proyecto, una propuesta
aceptada y una especificación. También pueden reutilizar comprobaciones
mecánicas de Problem Details.

Cada prueba debe continuar mostrando con claridad la petición ejecutada, el
código esperado y el comportamiento específico que protege. No se creará un
DSL de pruebas que oculte el contrato HTTP.

## Responsabilidades resultantes

La siguiente distribución expresa responsabilidades, no nombres obligatorios
de archivos o tipos:

### API

- Registro de rutas y metadatos OpenAPI.
- Binding de contratos HTTP.
- Validación y traducción de identificadores de ruta.
- Invocación de reglas del dominio.
- Orquestación propia del adaptador HTTP mientras sólo exista este adaptador.
- Selección de códigos de estado y Problem Details.
- Resolución reutilizable del contexto anidado requerido por las rutas.

### Infrastructure

- Configuración y ejecución de EF Core.
- Transacciones y operaciones específicas del proveedor cuando sean necesarias.
- Clasificación de errores de SQLite sin referencias a ASP.NET Core.
- Persistencia y migraciones existentes sin cambios de esquema.

### Domain

- Entidades, invariantes, normalización y transiciones actuales.
- Ninguna dependencia nueva de API, EF Core o SQLite.

### Pruebas

- Escenarios compartidos para preparar datos.
- Pruebas de contrato que continúan usando HTTP real mediante
  `WebApplicationFactory`.
- Pruebas específicas de persistencia que continúan usando SQLite relacional.

## Reglas de compatibilidad

1. Las 25 operaciones HTTP actuales deben continuar registradas.
2. Los nombres de ruta usados por `CreatedAtRoute` no cambian.
3. Las rutas y métodos HTTP no cambian.
4. Los contratos JSON no ganan, pierden ni renombran propiedades.
5. La serialización de enums y fechas no cambia.
6. Los códigos de estado correctos y de error no cambian.
7. Los títulos y detalles de los Problem Details existentes no cambian.
8. Los errores de validación conservan las claves de campo actuales.
9. La cabecera `Location` de las creaciones continúa apuntando al recurso
   correspondiente.
10. El orden y aislamiento de los listados no cambia.
11. Las transacciones y garantías de concurrencia existentes no se debilitan.
12. Las consultas de sólo lectura continúan usando `AsNoTracking`.
13. Todas las operaciones asíncronas continúan propagando
    `CancellationToken`.
14. El comportamiento temporal continúa usando `TimeProvider` y precisión de
    milisegundos.
15. Ningún detalle de SQLite, EF Core, rutas locales o stack traces se expone al
    consumidor.

## Contratos HTTP

Este checkpoint no define contratos nuevos. La fuente de verdad continúa
siendo el conjunto de especificaciones 0001 a 0007 y el documento OpenAPI
generado por la aplicación.

Como parte de la verificación se compararán, al menos:

- inventario de rutas y métodos;
- nombres de las operaciones;
- esquemas de request y response;
- respuestas documentadas por código de estado;
- tipos de contenido;
- rutas usadas por las cabeceras `Location`.

Una diferencia observada en OpenAPI debe considerarse un cambio de contrato y
detener el refactor hasta determinar su causa. Si la diferencia fuese deseada,
deberá salir de esta especificación y aprobarse como un incremento funcional.

## Diseño orientativo

La implementación puede introducir componentes internos equivalentes a:

- una factoría de Problem Details comunes;
- un parser de identificadores de rutas anidadas;
- un resolvedor del contexto de especificación;
- un clasificador de restricciones SQLite en Infrastructure;
- helpers de escenarios y assertions en las pruebas de integración.

Estos nombres y su organización final se decidirán durante la implementación
según el código resultante. No se crearán interfaces cuando exista una única
implementación y las pruebas no necesiten sustituirla. Los helpers estáticos son
válidos cuando no mantienen estado ni ocultan dependencias relevantes.

La separación física de un archivo grande puede realizarse mediante varios
tipos enfocados o mediante archivos parciales, siempre que el resultado sea más
fácil de navegar. El número de archivos o líneas no constituye por sí mismo un
criterio de aceptación.

## Impacto en persistencia

No existe cambio de modelo ni de datos:

- no se modifican entidades persistentes;
- no se modifican configuraciones de EF Core;
- no se modifica el snapshot;
- no se crea una migración;
- no se transforma la base existente.

Encapsular la identificación de errores SQLite no implica añadir una
abstracción completa de proveedor. El componente puede ser deliberadamente
específico de SQLite y deberá poder sustituirse cuando se apruebe otro
proveedor.

## Estrategia de pruebas

### Línea base

Antes de modificar código se ejecutarán, en un entorno que permita los canales
internos de MSBuild y VSTest:

```bash
dotnet tool restore
dotnet restore SpecFlow.slnx
dotnet build SpecFlow.slnx --no-restore
dotnet test SpecFlow.slnx --no-build
dotnet format SpecFlow.slnx --verify-no-changes --no-restore
```

También se conservará una representación del OpenAPI inicial adecuada para
detectar diferencias de contrato durante el checkpoint. No es obligatorio
versionar un snapshot generado si una comparación determinista puede realizarse
durante las pruebas.

### Durante el refactor

- Ejecutar las pruebas afectadas después de cada extracción.
- Ejecutar la suite completa antes de integrar cada paso independiente.
- Mantener las pruebas de dominio sin cambios salvo correcciones justificadas.
- Mantener las expectativas de las pruebas de integración.
- Añadir una prueba de regresión antes de corregir cualquier defecto descubierto.
- No cambiar una expectativa sólo para acomodar involuntariamente el nuevo
  diseño.

### Verificación final

- Compilación sin warnings.
- Suite completa satisfactoria.
- Formato sin cambios pendientes.
- OpenAPI compatible con la línea base.
- Base SQLite existente utilizable sin migración.
- `git diff` limitado a reorganización interna, pruebas auxiliares y esta
  documentación.

## Criterios de aceptación

1. Las 25 operaciones HTTP existentes conservan sus rutas y métodos.
2. Requests, responses, cabeceras y códigos de estado mantienen el contrato de
   las especificaciones 0001 a 0007.
3. OpenAPI conserva todas las operaciones, nombres y esquemas existentes.
4. Los nombres usados por `CreatedAtRoute` y las cabeceras `Location` no cambian.
5. Los Problem Details comunes se producen desde una única definición
   compartida por los grupos que actualmente los duplican.
6. El parseo compartido de identificadores conserva las claves y mensajes de
   validación actuales.
7. La resolución proyecto-propuesta-especificación utilizada por criterios y
   tareas tiene una única implementación compartida.
8. La resolución compartida conserva el orden de comprobación y no revela
   recursos pertenecientes a otro proyecto o propuesta.
9. Las lecturas realizadas mediante la resolución compartida pueden conservar
   `AsNoTracking` cuando no necesitan modificar la especificación.
10. Ningún endpoint inspecciona directamente `SqliteException` ni códigos de
    error numéricos de SQLite.
11. La clasificación específica de restricciones SQLite reside en
    Infrastructure y no depende de ASP.NET Core.
12. La traducción final de un conflicto de persistencia a Problem Details sigue
    siendo responsabilidad de la API.
13. Las transacciones y protecciones de concurrencia existentes para criterios
    y tareas conservan su comportamiento.
14. Los helpers compartidos de pruebas reducen preparación o assertions
    repetidas sin ocultar la petición y la respuesta protegidas por cada prueba.
15. Cualquier defecto detectado durante el refactor cuenta con una prueba de
    regresión antes de su corrección.
16. Todas las pruebas unitarias y de integración existentes pasan.
17. La solución compila sin warnings y `dotnet format` no detecta cambios.
18. No se añade ningún paquete ni proyecto.
19. No se modifica el esquema, el snapshot ni las migraciones existentes y no
    se genera una migración nueva.
20. No se crea una capa `Application`, un repositorio genérico ni un framework
    CRUD.
21. `SpecFlow.Domain` permanece independiente de ASP.NET Core, EF Core y SQLite.
22. El repositorio queda sin artefactos generados o cambios ajenos al
    checkpoint.

## Riesgos y mitigaciones

### Abstracción excesivamente genérica

Una factoría o resolvedor demasiado configurable puede resultar más difícil de
entender que la duplicación original. Se mitigará extrayendo únicamente casos
idénticos y manteniendo los errores específicos junto a sus endpoints.

### Cambio accidental de contrato

Mover la creación de errores o el registro de rutas puede alterar títulos,
detalles, metadatos OpenAPI o nombres de ruta. Se mitigará mediante las pruebas
existentes y una comparación explícita de OpenAPI.

### Cambio accidental del tracking de EF Core

Compartir la resolución del contexto puede hacer que una operación obtenga una
entidad sin tracking cuando necesita modificarla, o al contrario. La API del
resolvedor debe hacer visible esta necesidad y las pruebas de colección y
concurrencia deben ejecutarse después de cada cambio.

### Ocultación de comportamiento en pruebas

Helpers demasiado amplios pueden convertir las pruebas en secuencias opacas. Se
mantendrán helpers sólo para preparación repetitiva y assertions mecánicas; la
acción y la expectativa principal permanecerán en cada prueba.

## Evidencias de cierre

- Los Problem Details compartidos se centralizaron dentro del adaptador API.
- El parseo de identificadores y sus mensajes se centralizó sin modificar las
  claves de validación.
- Criterios y tareas reutilizan una única resolución del contexto formado por
  proyecto, propuesta y especificación.
- La clasificación de violaciones de unicidad SQLite reside en Infrastructure y
  ningún endpoint inspecciona tipos o códigos de SQLite.
- La preparación del escenario proyecto-propuesta-especificación tiene una
  única implementación compartida por las pruebas de criterios y tareas.
- OpenAPI cuenta con una prueba que protege el inventario exacto de las 25
  operaciones y sus nombres.
- No se dividieron físicamente los handlers de criterios y tareas porque, tras
  las extracciones, hacerlo sólo habría trasladado código sin reducir más
  responsabilidades o acoplamiento.
- `dotnet build SpecFlow.slnx --no-restore` terminó con cero warnings y cero
  errores.
- `dotnet test SpecFlow.slnx --no-build --no-restore` superó 178 pruebas de
  integración y 77 pruebas de dominio, sin errores ni omisiones.
- `dotnet format SpecFlow.slnx --verify-no-changes --no-restore` terminó
  correctamente.
- No se añadieron paquetes, proyectos, migraciones ni cambios de esquema.

## Decisiones propuestas para aprobación

- Tratar este trabajo como una especificación técnica numerada porque atraviesa
  varios grupos de endpoints, Infrastructure y pruebas.
- Mantener compatibilidad HTTP completa como condición principal.
- Compartir únicamente los errores y recorridos realmente duplicados.
- Encapsular SQLite en Infrastructure sin diseñar todavía una abstracción
  multiproveedor.
- No crear una capa `Application` mientras HTTP siga siendo el único adaptador
  de casos de uso.
- Reservar la revisión del contrato de concurrencia para la siguiente
  especificación funcional.
- No utilizar objetivos de número de líneas como criterio de terminado.

## Decisiones de implementación aprobadas

1. La compatibilidad de OpenAPI se protegerá ampliando las pruebas automáticas
   actuales; no se versionará un documento generado completo.
2. La separación física de los dos archivos de endpoints grandes queda
   condicionada a que siga aportando claridad después de las extracciones
   compartidas.
3. Los helpers de Problem Details y rutas serán internos y permanecerán dentro
   del adaptador API.
4. La revisión de concurrencia se realizará en una especificación posterior y
   cubrirá conjuntamente especificaciones, criterios y tareas.

## Plan de implementación

1. Ejecutar y registrar la línea base de compilación, pruebas, formato y
   OpenAPI.
2. Añadir o ajustar pruebas de caracterización sólo donde exista una laguna
   relevante para mover código con seguridad.
3. Extraer Problem Details comunes sin cambiar los handlers.
4. Extraer el parseo común de identificadores manteniendo los mensajes actuales.
5. Extraer la resolución del contexto de especificación y aplicarla primero a
   criterios de aceptación.
6. Aplicar la misma resolución a tareas de implementación.
7. Encapsular la clasificación de restricciones SQLite en Infrastructure y
   actualizar progresivamente los endpoints que la utilizan.
8. Reorganizar físicamente los endpoints grandes únicamente si sigue aportando
   claridad después de las extracciones.
9. Consolidar helpers de escenarios y assertions repetidas en las pruebas.
10. Ejecutar la verificación completa y comparar OpenAPI con la línea base.
11. Auditar cada criterio de aceptación y registrar cualquier decisión tomada
    durante la implementación.
12. Cambiar el estado de la especificación sólo después de aprobarla, completar
    el trabajo y verificar todos los criterios.
