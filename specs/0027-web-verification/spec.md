# 0027 — Verificar criterios y la especificación desde la interfaz web

## Estado

Implementada y verificada el 16 de octubre de 2026, tras su aprobación.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0009-http-concurrency/spec.md`,
  `specs/0013-specification-lifecycle/spec.md`,
  `specs/0014-acceptance-criterion-verification/spec.md`,
  `specs/0022-web-form-field-errors/spec.md`,
  `specs/0023-web-acceptance-criteria/spec.md` y
  `specs/0026-web-specification-lifecycle/spec.md`.

Este incremento amplía la web para consumir la verificación de criterios y la
transición a `verified` ya disponibles en la API. No cambia contratos HTTP,
dominio ni persistencia.

## Problema

Una especificación implementada solo se cierra cuando cada criterio de
aceptación se ha comprobado y cumple. La web llega hasta `implemented`, pero
registrar el resultado de cada criterio, con su evidencia, y marcar verificada
la especificación todavía requiere la API, con un ETag propio para la
verificación de cada criterio.

## Objetivo

Mientras la especificación está en `implemented`, la persona puede registrar
desde la sección de criterios si cada uno cumple o no, con una evidencia
opcional, y marcar verificada la especificación cuando todos cumplen. Los
conflictos se resuelven de forma explícita, sin perder la evidencia escrita.

## Alcance

- En `implemented` y `verified`, cada criterio muestra su resultado («Sin
  verificar», «Cumple» o «No cumple»), su evidencia si la tiene y la fecha de
  comprobación.
- En `implemented`: un resumen en la sección de criterios («Todos los criterios
  cumplen» o «Criterios pendientes de cumplir: 2 y 3»).
- En `implemented`: «Registrar verificación» en cada criterio, con el resultado
  y la evidencia, que guarda con el ETag de la verificación de ese criterio.
- En `implemented`: «Marcar como verificada» en la vista, con el mismo
  comportamiento de confirmación y recuperación que las transiciones de la
  0026.
- Ampliación del cliente HTTP web con la consulta y el registro de la
  verificación de un criterio y con la transición a `verified`.
- Actualización del README con el recorrido disponible.

## Fuera de alcance

- Crear propuestas de seguimiento a partir de una especificación verificada
  (0015), que tendrá su propio incremento.
- Verificar criterios en estados distintos de `implemented`, que la API no
  permite.
- Volver de `verified` a `implemented`, que la API no permite.
- Renderizar la evidencia como Markdown; se muestra como texto.
- Cambios en la API, OpenAPI, dominio o esquema.

## Casos de uso

### UC-01 — Ver el resultado de cada criterio

En `implemented` y `verified`, la sección de criterios consulta la
verificación de cada criterio y la muestra junto a su texto. Si una consulta
falla, los criterios siguen visibles, la verificación se marca como no
disponible y se puede reintentar. En `draft` y `approved` no se consulta,
porque todos los criterios están sin verificar.

### UC-02 — Registrar la verificación de un criterio

En `implemented`, «Registrar verificación» consulta la verificación actual para
obtener su ETag y muestra un grupo de opciones («Sin verificar», «Cumple», «No
cumple») y un campo «Evidencia (opcional)». «Guardar verificación» envía `PUT`
con el resultado, la evidencia e `If-Match`, y está deshabilitado mientras nada
cambie. «Cancelar» con cambios pide confirmación en la página.

- Una evidencia vacía se envía como ausente (`null`); cualquier otro texto se
  envía exacto. Una evidencia de solo espacios o de más de 10.000 caracteres
  devuelve `400`, que se muestra junto al campo con la utilidad de la 0022.
- «Sin verificar» no admite evidencia: al elegirlo, el campo se deshabilita y
  se indica que la evidencia escrita no se guardará. Se envía `null`.

### UC-03 — Marcar verificada la especificación

En `implemented`, la vista muestra «Marcar como verificada». Al confirmar,
envía `POST .../specification/verify` con el ETag de la especificación. Con
`200`, muestra «Verificada» con su fecha y los criterios quedan en modo de
lectura con su resultado. Ante `409` con la especificación aún en
`implemented`, explica que todos los criterios deben cumplir.

### UC-04 — Conflictos y errores al registrar

- `412`: la web no reintenta. Relee la verificación del criterio, muestra la
  guardada junto a la de la persona y ofrece «Guardar mi verificación» con el
  ETag recién leído o «Descartar mi verificación».
- `409`: la web relee la especificación. Si ya no está en `implemented`, la
  sección pasa a modo de lectura y la verificación no guardada se muestra como
  texto.
- `404`: como en la 0023.
- Pérdida de conexión: no se reenvía y se exige consultar el estado. Si la
  verificación guardada coincide con la enviada, se confirma; si no, se
  presenta la resolución de `412`.
- `428` o `400` sin campo conocido: error genérico y se exige consultar el
  estado.

## Comportamiento de la interfaz

- Registrar una verificación cuenta como edición activa y respeta el bloqueo de
  toda la vista acordado en las specs 0023 a 0026.
- Tras guardar una verificación, la vista relee la especificación para
  conservar su ETag vigente, que cubre también las verificaciones.
- El grupo de opciones es un `fieldset` con leyenda; la evidencia aplica
  `aria-invalid` y `aria-describedby` con la utilidad de la 0022.
- La web no transforma la evidencia, salvo enviar como ausente una vacía.
- Mensajes en español y sin JSON bruto, ETags ni detalles internos.

## Consumo del contrato HTTP existente

`...` corresponde a `/api/projects/{projectId}/proposals/{proposalId}/specification`.

| Acción web | Petición | Respuesta correcta | Errores relevantes |
|---|---|---|---|
| Consultar verificación | `GET .../acceptance-criteria/{criterionId}/verification` | `200`, `{"status","evidence","checkedAtUtc"}` y ETag | `400`, `404` |
| Registrar verificación | `PUT .../acceptance-criteria/{criterionId}/verification` con `{"status","evidence"}` e `If-Match` | `200`, verificación y ETag | `400`, `404`, `409`, `412`, `428` |
| Marcar verificada | `POST .../verify` con `If-Match` | `200`, especificación y ETag | `400`, `404`, `409`, `412`, `428` |

`status` toma los valores `unverified`, `passed` y `failed`. El ETag de la
verificación es distinto del ETag del criterio. La API comprueba `If-Match`
antes que el estado de la especificación, así que un `409` con el ETag vigente
significa que la especificación ya no está en `implemented`. Registrar el mismo
resultado y la misma evidencia responde `200` sin cambios.

## Cambios en la web

- Nuevo `AcceptanceCriterionVerificationDto` y tres métodos en
  `SpecFlowApiClient`.
- `AcceptanceCriteriaSection` muestra los resultados, el resumen y el
  formulario de verificación.
- `SpecificationDetail` añade la transición a `verified` a las de la 0026.

Son cambios internos de `SpecFlow.Web`; no afectan a la API.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes, servicios ni herramientas nuevos.

## Criterios de aceptación

1. En `implemented` y `verified`, cada criterio muestra su resultado, su
   evidencia si la tiene y su fecha de comprobación. En `draft` y `approved` no
   se muestra ni se consulta.
2. En `implemented`, el resumen indica qué criterios no cumplen todavía, o que
   todos cumplen.
3. «Registrar verificación» solo aparece en `implemented` y muestra el
   resultado y la evidencia actuales.
4. Guardar envía un único `PUT` con el resultado, la evidencia exacta o `null`
   si está vacía, y el ETag de la verificación. «Sin verificar» envía siempre
   `null` como evidencia.
5. «Guardar verificación» está deshabilitado sin cambios y «Cancelar» con
   cambios pide confirmación en la página.
6. Errores `400` de evidencia se muestran junto al campo, con
   `aria-invalid="true"`, y conservan el texto.
7. Ante `412` la web no reintenta y muestra la verificación guardada y la propia
   hasta que la persona decide.
8. Ante `409` con la especificación fuera de `implemented`, la sección queda en
   modo de lectura y la verificación no guardada sigue visible.
9. Ante pérdida de conexión, la web no reenvía y exige consultar el estado.
10. «Marcar como verificada» aparece solo en `implemented`, pide confirmación y,
    con todos los criterios cumpliendo, deja la especificación en `verified`.
    Si alguno no cumple, la web explica el requisito.
11. Tras registrar verificaciones, marcar verificada la especificación no
    responde `412` por esos cambios.
12. Si la consulta de verificaciones falla, los criterios siguen visibles y se
    puede reintentar.
13. Las pruebas del cliente HTTP cubren rutas, cuerpos con evidencia y con
    `null`, `If-Match`, ETags, y la clasificación de `400`, `404`, `409`,
    `412`, `428` y fallos de conexión.
14. Los recorridos de las specs 0021 y 0023 a 0026 no cambian.
15. La implementación no modifica el contrato HTTP, OpenAPI, el dominio ni el
    esquema de SQLite.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- La verificación se registra desde la sección de criterios, porque es una
  propiedad de cada criterio y comparte su numeración y su texto.
- Se consulta una verificación por criterio, porque el listado de criterios no
  la incluye. Es aceptable para el tamaño de plan del proyecto, como en la
  0025; una consulta agregada requeriría cambiar la API.
- Una evidencia vacía se envía como `null`, igual que la descripción de una
  tarea en la 0024, porque la API rechaza una evidencia presente sin contenido.
- Al elegir «Sin verificar», la web envía `null` aunque el campo tenga texto,
  porque la API no admite evidencia en ese resultado, y lo indica antes de
  guardar en lugar de devolver un error.
- La transición a `verified` reutiliza la acción, la confirmación y la
  recuperación de la 0026.
- Las propuestas de seguimiento quedan fuera para mantener el incremento
  centrado en cerrar el ciclo de vida de la especificación.
- La sección de criterios crece con este incremento. La duplicación con la de
  tareas sigue registrada para el checkpoint de mantenibilidad.

## Plan de verificación

- Pruebas del cliente HTTP para consultar y registrar verificaciones y para
  marcar verificada la especificación.
- Navegador con API real y SQLite temporal, con una especificación
  `implemented` preparada desde la web:
  - Resultados y resumen sin verificar, con alguno que no cumple y con todos
    cumpliendo.
  - Registrar «Cumple» con y sin evidencia, «No cumple» con evidencia y volver
    a «Sin verificar»; evidencia de solo espacios y de 10.001 caracteres;
    guardar deshabilitado sin cambios y cancelar con cambios.
  - `412` con la verificación cambiada por la API; `409` marcando verificada
    la especificación por la API durante un registro.
  - API detenida al guardar: bloqueo y recuperación en los dos resultados.
  - «Marcar como verificada» con un criterio que no cumple (`409` y requisito)
    y con todos cumpliendo (`200`, fecha y modo de lectura).
  - Bloqueo mutuo con las demás acciones de la vista.
- Compilación, pruebas, formato y ausencia de cambios en el modelo EF Core y en
  OpenAPI.

## Evidencia de cierre

- `SpecFlowApiClient` añade la consulta y el registro de la verificación de un
  criterio, con el nuevo `AcceptanceCriterionVerificationDto`, y la transición
  a `verified`. `AcceptanceCriteriaSection` muestra resultados, resumen y
  formulario; `SpecificationDetail` añade «Marcar como verificada» a las
  transiciones de la 0026. No cambian la API, OpenAPI, el dominio ni el
  esquema.
- Cuatro pruebas nuevas del cliente HTTP (ocho casos): rutas, `GET` sin cuerpo,
  `PUT` con evidencia exacta y con `null` explícito, `If-Match` literal, ETag
  propio de la verificación, transición sin cuerpo con fecha y ETag,
  clasificación de `400` (`evidence`), `404`, `409`, `412` y `428`, y fallos
  de conexión.
- Navegador con API real y SQLite temporal, con especificaciones llevadas
  desde la web de `draft` a `implemented`:
  - Sin verificar: «Criterios pendientes de cumplir: 1, 2 y 3» y «Marcar como
    verificada» respondió `409` con el requisito explicado.
  - Al abrir el registro, «Guardar verificación» está deshabilitado sin
    cambios, la evidencia está deshabilitada con «Sin verificar» y solo queda
    habilitado «Cancelar» en toda la vista. Evidencia de solo espacios y de
    10.001 caracteres: error junto al campo con `aria-invalid="true"` y texto
    conservado. «Cancelar» con cambios pidió confirmación.
  - «Cumple» con evidencia, «Cumple» sin evidencia y «No cumple» con evidencia;
    la API guardó exactamente lo enviado, con `null` cuando no había
    evidencia. Volver a «Sin verificar» con texto en el campo avisó de que no
    se guardaría y la API guardó `null`. El resumen pasó por «Criterio
    pendiente de cumplir: 3» hasta «Todos los criterios cumplen».
  - `412` con la verificación cambiada por la API: ambas verificaciones a la
    vista; «Guardar mi verificación» guardó la propia y «Descartar mi
    verificación» adoptó la guardada.
  - Especificación verificada por la API durante un registro: `409`, estado
    «Verificada», criterios en modo de lectura y la verificación no guardada
    visible como texto.
  - API detenida al guardar: formulario y botones deshabilitados con
    «Consultar estado actual» como única acción; tras reiniciar, «no se
    guardó» con la comparación de `412`, y «se guardó correctamente» cuando la
    API ya tenía la verificación enviada.
  - Recorrido completo desde la web: registrar todos los criterios como
    «Cumple» y «Marcar como verificada» respondió `200`, con «Verificada», su
    fecha y criterios en modo de lectura. Ningún paso respondió `412`.
- Corrección durante la verificación: tras un `412`, aparecía un bloque vacío
  de «Texto de criterio no guardado», porque la verificación local se había
  añadido al texto de edición que la sección traslada fuera de `draft` en cada
  renderizado. La verificación tiene ahora su propio texto no guardado y solo
  se incluye al perder el contexto.
- Desviación del plan: como en la 0025, no se reprodujo en navegador un fallo
  aislado de la consulta de verificaciones con el resto de la vista
  disponible; la carga se hace justo después del listado de criterios.
- Revisión de idioma: los textos nuevos están en español, incluidos los
  resultados, las etiquetas accesibles y los avisos.
- `dotnet build SpecFlow.slnx --no-restore`: correcto, 0 warnings.
- `dotnet test SpecFlow.slnx --no-build`: 531 pruebas correctas (127 de dominio,
  339 de integración y 65 web).
- `dotnet format SpecFlow.slnx --verify-no-changes --no-restore` y
  `git diff --check`: correctos.
- `dotnet ef migrations has-pending-model-changes`: sin cambios del modelo.
