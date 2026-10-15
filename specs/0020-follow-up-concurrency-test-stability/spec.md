# 0020 — Estabilidad de la prueba de continuaciones concurrentes

## Estado

Aprobada para implementación el 15 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0. Esta spec propone
  la enmienda 1.1.0 descrita en «Alcance».
- Specs anteriores relacionadas: `specs/0015-follow-up-proposals/spec.md`.

Las reglas transversales de arquitectura, HTTP, concurrencia, persistencia,
pruebas y calidad se heredan de la constitución.

## Problema

La prueba de integración `ConcurrentFollowUps_CanShareVerifiedSource`, añadida
en la 0015, falla de forma intermitente: el segundo `POST` simultáneo responde
`500 Internal Server Error` en lugar de `201 Created`. Las evidencias de cierre
de la 0016 y la 0018 lo registran, y volvió a ocurrir al verificar el historial
de la fase 0 y en 1 de 20 ejecuciones completas de la suite de integración. En
40 ejecuciones aisladas no falló.

La causa está en la prueba, no en el producto. La prueba lanza dos peticiones
HTTP simultáneas con la configuración por defecto de `SpecFlowApiFactory`, que
comparte una única `SqliteConnection` en memoria entre todos los `DbContext`.
Esa conexión no admite uso concurrente. El resto de pruebas con peticiones HTTP
simultáneas (`SpecificationConcurrencyTests`,
`AcceptanceCriterionConcurrencyTests`, `ImplementationTaskConcurrencyTests` y la
verificación de criterios) ya usan un archivo SQLite temporal, con una conexión
por contexto, como ocurre en ejecución real.

Un experimento temporal, no integrado, con 200 creaciones simultáneas de
continuaciones sobre la misma fuente dio:

| Configuración | Respuestas distintas de `201` |
|---|---|
| Conexión en memoria compartida | 128 de 200, todas `500` |
| Archivo SQLite temporal | 0 de 200 |

Una suite intermitente erosiona la definición de terminado: obliga a repetir
ejecuciones y puede ocultar regresiones reales.

## Objetivo

La prueba de continuaciones concurrentes comprueba el mismo comportamiento de
la 0015 de forma determinista, con el patrón de base de datos que el proyecto
ya usa para concurrencia HTTP.

## Alcance

- Ejecutar `ConcurrentFollowUps_CanShareVerifiedSource` sobre un archivo SQLite
  temporal propio, eliminado al terminar la prueba.
- Conservar sus aserciones actuales: ambas creaciones responden `201`, la ruta
  `follow-ups` devuelve las dos continuaciones y la propuesta de origen no
  cambia.
- Enmienda menor de la constitución (1.0.0 → 1.1.0) en «Persistencia y
  migraciones»: «Las pruebas que envían peticiones HTTP simultáneas usan un
  archivo SQLite temporal; la conexión en memoria compartida solo se usa en
  escenarios secuenciales».

## Fuera de alcance

- Cambios en `SpecFlow.Api`, el dominio, la infraestructura o el contrato HTTP.
- Cambiar el modo en memoria por defecto de `SpecFlowApiFactory` para el resto
  de pruebas.
- Extraer un helper común para bases temporales; las pruebas existentes
  repiten ese patrón y unificarlo sería una refactorización aparte.
- Reintentos automáticos de pruebas o desactivar el paralelismo de xUnit, que
  ocultarían el problema en vez de resolverlo.

## Casos de uso

### UC-01 — Ejecutar la suite

Quien ejecuta `dotnet test` obtiene el mismo resultado en ejecuciones repetidas
sin cambios en el código; la prueba de continuaciones concurrentes no falla por
compartir la conexión de pruebas.

## Impacto en persistencia

Sin cambios de esquema.

## Criterios de aceptación

1. `ConcurrentFollowUps_CanShareVerifiedSource` usa un archivo SQLite temporal
   propio, con el mismo patrón que las demás pruebas de concurrencia HTTP, y lo
   elimina al terminar.
2. La prueba mantiene sus aserciones: dos `201 Created`, dos continuaciones
   directas en `follow-ups` y la propuesta de origen idéntica antes y después.
3. La prueba pasa en 50 ejecuciones aisladas consecutivas.
4. La suite de integración completa pasa en 20 ejecuciones consecutivas sin
   fallos de esta prueba.
5. El código de producción no cambia.
6. La constitución pasa a la versión 1.1.0, con fecha de última modificación
   actualizada, e incluye la regla de pruebas concurrentes anterior.

## Decisiones

### Acordadas

- Se incorpora la enmienda 1.1.0. Aunque el uso concurrente real de SpecFlow es
  improbable, la regla evita que futuras pruebas repitan el mismo patrón. Es una
  regla nueva compatible, por lo que incrementa la versión menor.
- Se corrige la prueba y no el producto, porque en ejecución real cada
  `DbContext` abre su propia conexión al archivo SQLite y el experimento con
  archivo no produjo errores.
- No se añaden reintentos ni se reduce el paralelismo de la suite.

## Plan de verificación

- 50 ejecuciones aisladas de la prueba.
- 20 ejecuciones completas de `SpecFlow.Api.IntegrationTests`.
- Suite completa de la solución, formato y migraciones.

## Evidencia de cierre

Pendiente de implementación y verificación.
