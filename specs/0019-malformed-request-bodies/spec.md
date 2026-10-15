# 0019 — Cuerpos de petición malformados

## Estado

Aprobada para implementación el 15 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Specs anteriores relacionadas: `specs/0008-api-maintainability-checkpoint/spec.md`
  y `specs/0009-http-concurrency/spec.md`.

Las reglas transversales de arquitectura, HTTP, concurrencia, persistencia,
pruebas y calidad se heredan de la constitución.

## Problema

Cuando la API se ejecuta en el entorno `Development`, que es el que usa
`dotnet run`, cualquier endpoint con body responde `500 Internal Server Error`
si el JSON no se puede leer: sintaxis inválida (`{bad`), body vacío, un tipo
raíz distinto de objeto (`[]`) o un tipo de propiedad incompatible
(`{"title":123}`).

En `Production` y en el entorno `Testing` de las pruebas de integración, la
misma petición responde `400 Bad Request`. El comportamiento depende por tanto
del entorno y la suite no puede detectarlo. Un `500` comunica un fallo del
servidor ante un error del consumidor y contradice el espíritu de los contratos
de error de la constitución.

## Objetivo

Toda petición cuyo body JSON no pueda leerse como el contrato esperado responde
`400 Bad Request` con Problem Details, de forma idéntica en `Development`,
`Testing` y `Production`, sin exponer detalles internos del deserializador.

## Alcance

- Tratamiento uniforme de los errores de lectura del body JSON en todos los
  endpoints con body existentes.
- Pruebas de regresión HTTP que se ejecuten también con el entorno
  `Development`.
- Documentación de la respuesta en OpenAPI si el contrato observable cambia.

## Fuera de alcance

- Cambiar la validación de campos que ya responde Validation Problem Details.
- Cambiar la respuesta `415 Unsupported Media Type`.
- Cambiar los límites de tamaño del body.
- Cambios en `SpecFlow.Web`.

## Casos de uso

### UC-01 — Enviar JSON con sintaxis inválida

El consumidor envía un body que no es JSON válido. Recibe `400` con Problem
Details y ningún cambio queda persistido.

### UC-02 — Enviar JSON con estructura incompatible

El consumidor envía JSON válido cuya raíz o propiedades no corresponden al
contrato. Recibe `400` con Problem Details y ningún cambio queda persistido.

### UC-03 — Omitir el body requerido

El consumidor declara `Content-Type: application/json` sin enviar body.
Recibe `400` con Problem Details.

## Cambios del contrato HTTP

Ninguna ruta nueva. Se fija un resultado que hasta ahora dependía del entorno:

| Situación | Antes (`Development`) | Antes (`Production`/`Testing`) | Después (todos) |
|---|---|---|---|
| JSON sintácticamente inválido | `500` | `400` | `400` |
| Raíz o tipo de propiedad incompatible | `500` | `400` | `400` |
| Body vacío con `application/json` | `500` | `400` | `400` |

Respuesta: Problem Details con `status: 400` y título estable
`Invalid request body`. El `detail` no incluye mensajes de
`System.Text.Json`, rutas JSON, posiciones de bytes, nombres de tipos .NET ni
stack traces.

El orden de evaluación de la constitución no cambia: un body ilegible se
rechaza en el paso 2 (validación del body), antes de la existencia, el
contexto y `If-Match`.

## Impacto en persistencia

Sin cambios de esquema.

## Criterios de aceptación

1. En `Development`, `POST /api/projects` con `{bad` responde `400` con
   Problem Details y el título `Invalid request body`.
2. El mismo resultado se obtiene con body vacío, con `[]` y con un tipo de
   propiedad incompatible.
3. El comportamiento es idéntico en `Testing` y `Production`.
4. Cada endpoint existente con body responde `400` ante JSON malformado. Una
   prueba parametrizada recorre todas las operaciones con body.
5. En una operación condicionada (por ejemplo `PUT` de especificación), un body
   malformado responde `400` aunque falte `If-Match` o el recurso no exista.
6. La respuesta no contiene texto del deserializador, nombres de tipos .NET,
   rutas locales ni stack traces.
7. Ninguna petición rechazada modifica datos ni ETags.
8. Los bodies válidos, la Validation Problem Details por campo y el `415`
   conservan su comportamiento actual.

## Decisiones

### Acordadas

- El título estable es `Invalid request body`, más específico que el genérico
  `Bad Request` que devolvía `Production`.
- Se corrige en la composición de `SpecFlow.Api`, no endpoint por endpoint,
  para no duplicar la regla.
- Las pruebas fuerzan el entorno `Development` en los escenarios de esta spec
  porque el defecto solo se manifiesta allí.

## Plan de verificación

- Pruebas de integración HTTP parametrizadas por endpoint con body y por tipo
  de malformación, ejecutadas con los entornos `Development` y `Testing`.
- Comprobación de que no se persiste nada tras cada rechazo.
- Prueba manual con `dotnet run --project src/SpecFlow.Api` y `curl`.

## Evidencia de cierre

Pendiente de implementación y verificación.
