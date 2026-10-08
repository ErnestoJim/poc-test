# NNNN — Título del incremento

> Plantilla vigente desde la constitución 1.0.0. Eliminar instrucciones y
> secciones opcionales que no aporten información al completar la spec.

## Estado

Borrador para revisión. No aprobado para implementación.

## Base documental

- Constitución: `specs/constitution.md`, versión X.Y.Z.
- Specs anteriores relacionadas: indicar identificadores o `Ninguna`.

Solo se documentan aquí reglas nuevas, especializaciones o excepciones. Las
reglas transversales de arquitectura, HTTP, concurrencia, persistencia, pruebas
y calidad se heredan de la constitución.

## Problema

Describir la necesidad observable y por qué el comportamiento actual no la
resuelve. Evitar proponer la implementación en esta sección.

## Objetivo

Definir el resultado verificable del incremento en uno o dos párrafos.

## Alcance

- Enumerar únicamente las capacidades introducidas o modificadas.
- Mantener el incremento vertical y acotado.

## Fuera de alcance

- Incluir solo capacidades cercanas cuya exclusión pueda resultar ambigua.
- No repetir las exclusiones generales de la constitución.

## Casos de uso

### UC-01 — Nombre

Describir actor, acción, resultado y alternativas observables relevantes.

## Cambios de dominio

Eliminar esta sección si no cambia el dominio.

| Campo o concepto | Cambio | Regla específica |
|---|---|---|
| `Example` | Nuevo | Describir solo la regla propia del incremento |

### Reglas específicas

1. Enumerar invariantes nuevas o modificadas.
2. No repetir UUID, UTC, `TimeProvider` u otras reglas constitucionales.

## Cambios del contrato HTTP

Eliminar esta sección si el incremento no cambia el contrato HTTP.

| Método | Ruta | Resultado correcto | Errores propios |
|---|---|---|---|
| `POST` | `/api/example` | `201 Created` | `409` cuando... |

Documentar requests y responses completos cuando introduzcan o modifiquen un
contrato. Para errores comunes, ETags y Problem Details basta con referenciar la
constitución; detallar aquí solamente títulos, campos o comportamientos nuevos.

## Impacto en persistencia

Indicar una de estas opciones y explicar solo los detalles relevantes:

- Sin cambios de esquema.
- Nueva migración: `<MigrationName>`.
- Transformación de datos o compatibilidad especial.

## Criterios de aceptación

1. Cada criterio describe un comportamiento observable y verificable.
2. Cubrir camino correcto, límites, errores propios, aislamiento y concurrencia
   cuando correspondan.
3. No convertir reglas generales de calidad en criterios repetidos.

## Decisiones

### Pendientes

- Mantener aquí las decisiones que impiden aprobar la spec.

### Acordadas

- Registrar las decisiones propias del incremento y su motivación breve.

## Plan de verificación

- Pruebas de dominio específicas.
- Escenarios de integración específicos.
- Riesgos o regresiones que necesitan comprobación adicional.

## Evidencia de cierre

Completar al finalizar la implementación:

- compilación;
- pruebas de dominio e integración;
- formato;
- estado de migraciones;
- cualquier verificación particular del incremento.
