# 0014 — Verificación de criterios de aceptación

## Estado

Implementada y verificada el 14 de octubre de 2026, tras su aprobación para
implementación el mismo día.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Specs ampliadas: `specs/0005-acceptance-criteria/spec.md`,
  `specs/0009-http-concurrency/spec.md` y
  `specs/0013-specification-lifecycle/spec.md`.

Las reglas transversales de arquitectura, HTTP, concurrencia, persistencia,
pruebas y calidad se heredan de la constitución.

## Problema

Una especificación implementada puede pasar a `verified` mediante una acción
manual, aunque no exista un registro de qué se comprobó para cada criterio de
aceptación. Después de la transición tampoco se puede consultar la evidencia
que justificó esa confirmación.

## Objetivo

Registrar una evaluación manual para cada criterio de una especificación
implementada, con evidencia textual opcional. La verificación final seguirá
siendo explícita, pero solo podrá realizarse cuando todos los criterios tengan
un resultado positivo.

## Alcance

- Consulta del estado de verificación de cada criterio: `unverified`, `passed`
  o `failed`.
- Sustitución de la evaluación actual y su evidencia mediante una operación
  condicionada por ETag.
- Invalidación del ETag agregado de la especificación al cambiar una evaluación.
- Nueva condición para la transición de `implemented` a `verified`.
- Migración de datos existentes, OpenAPI, README y pruebas.

## Fuera de alcance

- Ejecución automática, integración con pruebas o evaluación mediante IA.
- Identidad del evaluador, revisores y aprobaciones múltiples.
- Adjuntos, enlaces estructurados e historial de evaluaciones anteriores.
- Reabrir una especificación `verified` o modificar su plan aprobado.
- Versiones sucesivas de una especificación; se tratarán en otra spec.
- Inferir un resultado a partir del estado de las tareas.

## Casos de uso

### UC-01 — Consultar una evaluación

El consumidor consulta la evaluación de un criterio. Un criterio sin evaluación
devuelve `unverified`, sin evidencia ni fecha, y un ETag utilizable para registrar
la primera evaluación.

### UC-02 — Registrar o corregir una evaluación

Mientras la especificación está `implemented`, el consumidor envía el ETag
vigente de la evaluación y sustituye su resultado y evidencia. Puede corregir
un `failed` por un `passed`, cambiar la evidencia o devolverlo a `unverified`.

### UC-03 — Verificar la especificación

El consumidor consulta el ETag agregado de la especificación y solicita la
transición existente `/verify`. La transición solo se completa si cada criterio
tiene `passed`. Un resultado `failed` o `unverified` mantiene la
especificación en `implemented`.

### UC-04 — Consultar una especificación verificada

Una vez verificada, el consumidor puede consultar las evaluaciones finales,
pero no modificarlas.

## Cambios de dominio

| Campo o concepto | Cambio | Regla específica |
|---|---|---|
| Estado de evaluación | Nuevo | `Unverified`, `Passed` o `Failed` por criterio |
| `Evidence` | Nuevo, nullable | Markdown opaco del resultado actual |
| `CheckedAtUtc` | Nuevo, nullable | Fecha del último cambio efectivo de evaluación |
| Versión de evaluación | Nueva, interna | Protege la evaluación sin cambiar el ETag del contenido del criterio |
| ETag de especificación | Ampliado | Cambia con cada evaluación efectiva |

### Reglas específicas

1. Cada criterio comienza en `Unverified`, con evidencia y fecha `null`.
2. `Passed` y `Failed` admiten evidencia opcional. Si se proporciona, debe
   contener al menos un carácter no blanco y un máximo de 10.000 caracteres.
   El Markdown se conserva exactamente.
3. `Unverified` exige evidencia `null` y deja `CheckedAtUtc` en `null`.
4. Una evaluación solo se modifica cuando la especificación está `Implemented`.
   El contenido, posición y trazabilidad del criterio permanecen congelados.
5. Un cambio efectivo sustituye el resultado y la evidencia, registra
   `CheckedAtUtc`, incrementa una vez la versión de evaluación y una vez la
   versión agregada de la especificación. No modifica el ETag del contenido del
   criterio ni sus fechas de edición.
6. Enviar el mismo resultado y la misma evidencia es idempotente: no cambia
   fecha ni versiones.
7. La transición a `Verified` exige que todos los criterios de la especificación
   estén en `Passed`; no exige evidencia. Continúa siendo una acción explícita.
8. Tras la transición a `Verified`, las evaluaciones quedan inmutables.
9. Una evaluación y la transición final deben competir sobre la versión
   agregada de la especificación: ninguna puede confirmar un estado distinto al
   representado por su precondición.

## Cambios del contrato HTTP

La ruta base es
`/api/projects/{projectId}/proposals/{proposalId}/specification/acceptance-criteria`.

| Método | Ruta relativa | Resultado correcto | Errores propios |
|---|---|---|---|
| `GET` | `/{criterionId}/verification` | `200` y ETag de evaluación | Ninguno |
| `PUT` | `/{criterionId}/verification` | `200` y ETag actualizado | `409 Specification is not ready for criterion verification` |

La respuesta del `GET` y el `PUT` tiene esta forma:

```json
{
  "status": "passed",
  "evidence": "Comprobado con el caso de integración X.",
  "checkedAtUtc": "2026-10-14T10:30:00Z"
}
```

El `PUT` recibe `status` y, opcionalmente, `evidence`. `status` admite `passed`,
`failed` y `unverified`. Para los dos primeros, `evidence` puede omitirse, ser
`null` o contener Markdown válido. Para `unverified`, debe omitirse o ser
`null`. Un valor desconocido, una evidencia incompatible o una longitud
inválida devuelve `400 Validation Problem Details` bajo las claves `status` o
`evidence`.

`PUT` exige el ETag de `GET /{criterionId}/verification` mediante `If-Match`.
El ETag de la evaluación es distinto del ETag individual del criterio y del
ETag agregado de la especificación. La ausencia, el formato inválido y la
obsolescencia siguen las reglas de la constitución. La ruta mantiene el
aislamiento existente de proyecto, propuesta, especificación y criterio.

`POST /specification/verify` conserva ruta, request y respuesta de la 0013.
Si la especificación está `implemented` pero no todos los criterios tienen
`passed`, devuelve `409 Specification verification incomplete`
sin modificar estado ni fechas. El conflicto de transición existente se
conserva para los demás estados. El ETag agregado cambia al modificar una
evaluación, por lo que debe consultarse de nuevo antes de llamar a `/verify`.

Los endpoints existentes para editar contenido, posición y trazabilidad de
criterios siguen restringidos a `draft`. Las respuestas existentes de criterios
no añaden campos; la evaluación se consulta mediante su ruta propia.

## Impacto en persistencia

Nueva migración: `AddAcceptanceCriterionVerification`.

- Añade a `AcceptanceCriteria` el estado, la evidencia, la fecha y la versión
  interna de evaluación, con valores iniciales `Unverified`, `null`, `null` y
  cero. No modifica el contenido, las fechas ni la versión de edición.
- Las especificaciones `draft`, `approved` e `implemented` existentes conservan
  su estado. Sus criterios comienzan sin evaluación.
- Las especificaciones ya `verified` conservan ese estado y `VerifiedAtUtc`,
  aunque sus criterios no tengan evidencia histórica. No se fabrican resultados
  ni se reabre una especificación terminal. La nueva condición se aplica a las
  transiciones posteriores a la migración.

## Criterios de aceptación

1. Un criterio nuevo o migrado devuelve `unverified`, evidencia y fecha `null`,
   y un ETag de evaluación válido.
2. Registrar `passed` o `failed`, con o sin evidencia, en una especificación
   `implemented` devuelve el resultado, la fecha y un ETag nuevo.
3. Corregir un resultado o su evidencia sustituye el valor anterior; repetir
   exactamente la petición conserva fecha y ETags.
4. Devolver la evaluación a `unverified` elimina evidencia y fecha, y cambia
   los ETags correspondientes si existía un resultado.
5. Una evidencia presente pero vacía, demasiado larga o incompatible con el
   estado devuelve `400` sin cambios.
6. Registrar evaluaciones en `draft`, `approved` o `verified` devuelve `409` y
   no modifica ningún dato.
7. Un ETag ausente, inválido u obsoleto conserva la evaluación y devuelve el
   error constitucional correspondiente.
8. Una evaluación efectiva invalida el ETag agregado de la especificación,
   pero conserva el ETag y las fechas de edición del criterio.
9. `/verify` devuelve `409 Specification verification incomplete` si algún
   criterio está `unverified` o `failed`, y conserva el estado y las fechas.
10. `/verify` con todos los criterios en `passed`, aunque no tengan evidencia,
    devuelve `verified` y el nuevo ETag sin transicionar automáticamente.
11. La evaluación y `/verify`, o dos evaluaciones basadas en el mismo ETag, no
    pueden sobrescribirse silenciosamente en carreras concurrentes.
12. Las consultas respetan el aislamiento y las evaluaciones persisten entre
    scopes y reinicios.
13. La migración conserva especificaciones ya verificadas sin inventar
    evidencias; OpenAPI y README explican esta excepción histórica.
14. OpenAPI documenta las rutas nuevas, sus cuerpos, respuestas, ETags y el
    conflicto nuevo de `/verify`.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- `Passed` y `Failed` admiten evidencia textual opcional; `/verify` solo exige
  que todos los criterios estén en `Passed`.
- Las especificaciones ya `Verified` conservan su estado sin atribuirles
  resultados ni evidencias históricas.

- Una sola evaluación actual por criterio; no se mantiene historial.
- La evaluación tiene su propio ETag y no modifica el ETag del contenido del
  criterio. Sí invalida el ETag agregado de la especificación.
- Solo `Implemented` admite evaluaciones; `Verified` congela el resultado.
- Se permite volver a `unverified` para corregir una evaluación registrada por
  error antes de verificar la especificación.

## Plan de verificación

- Pruebas de dominio para validación, cambios efectivos, idempotencia y fechas.
- Pruebas HTTP para estados permitidos, Problem Details, ETags y aislamiento.
- Pruebas de carreras entre evaluaciones y transición `/verify`.
- Prueba de migración desde `AddSpecificationLifecycle`, incluidos datos ya
  `verified`.
- Pruebas de persistencia, OpenAPI y ausencia de migraciones pendientes.

## Evidencia de cierre

- Compilación sin warnings ni errores.
- 125 pruebas de dominio superadas.
- 314 pruebas de integración superadas.
- Formato verificado sin cambios.
- Modelo de EF Core sin migraciones pendientes.
