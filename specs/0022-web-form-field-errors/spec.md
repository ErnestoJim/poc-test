# 0022 — Errores de campo accesibles en los formularios web

## Estado

Aprobada para implementación el 15 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.1.0.
- Specs relacionadas: `specs/0016-first-web-interface/spec.md`,
  `specs/0017-web-proposal-decisions/spec.md`,
  `specs/0018-web-specification-create-read/spec.md` y
  `specs/0021-web-specification-edit/spec.md`.

Corrección de la presentación de errores de validación en la web. No cambia
contratos HTTP, dominio, persistencia ni los recorridos de las specs
relacionadas.

## Problema

Los formularios web marcan un campo inválido con
`aria-invalid="@(condición == true)"`. Blazor convierte el booleano `true` en el
atributo vacío `aria-invalid=""`, que los lectores de pantalla no interpretan
como «inválido»; con `false` lo omite. Se detectó durante la verificación de la
0021, que ya corrigió su propio campo.

Afecta a seis campos en cuatro formularios:

| Formulario | Campos | Spec de origen |
|---|---|---|
| Nuevo proyecto | `name`, `description` | 0016 |
| Nueva propuesta | `title`, `description` | 0016 |
| Rechazar propuesta | `reason` | 0017 |
| Crear especificación | `content` | 0018 |

Tiene dos consecuencias observables:

1. Un lector de pantalla no anuncia que el campo es inválido.
2. El borde rojo de error, definido en CSS con `.field [aria-invalid="true"]`,
   nunca se aplica en esos campos. Solo se ve el texto de error.

Además, ni el texto de ayuda (`<small>`) ni el mensaje de error del campo están
asociados a su control, así que no se leen al enfocarlo. Esto incluye el campo
de edición de la 0021.

## Objetivo

Todo campo con un error de validación devuelto por la API se marca como
inválido de forma que lo anuncien las tecnologías de asistencia y lo resalte el
estilo existente. Su ayuda y su mensaje de error quedan asociados al control.
La regla queda centralizada para que los formularios futuros no repitan el
defecto.

## Alcance

- Una utilidad única en `SpecFlow.Web` que produce el valor `"true"` o
  `"false"` de `aria-invalid` a partir del error de la API y el nombre del
  campo.
- Uso de esa utilidad en los siete campos con validación: los seis afectados y
  el de edición de la 0021.
- Asociación mediante `aria-describedby` de cada campo con su texto de ayuda y,
  cuando exista, con su mensaje de error, usando identificadores estables por
  campo.
- Pruebas unitarias de la utilidad.

## Fuera de alcance

- Cambios en los textos de los mensajes, el diseño visual o los colores.
- Validación en el cliente antes de llamar a la API.
- Mover el foco automáticamente al primer campo inválido.
- Auditoría de accesibilidad general (contraste, navegación por teclado,
  landmarks u orden de encabezados).
- Pruebas de componentes Blazor (bUnit), por las mismas razones de la 0021.

## Casos de uso

### UC-01 — Enviar un formulario con un campo inválido

La persona envía un formulario y la API responde `400` con error en un campo.
El campo pasa a `aria-invalid="true"`, muestra el borde de error y su
`aria-describedby` incluye el mensaje de error, de modo que un lector de
pantalla anuncia «inválido» y lee el mensaje al enfocarlo.

### UC-02 — Corregir el campo

Al escribir de nuevo, el error desaparece como hasta ahora. El campo vuelve a
`aria-invalid="false"`, sin borde de error, y `aria-describedby` solo
referencia el texto de ayuda.

## Cambios en la web

- Nueva utilidad estática en `SpecFlow.Web/Services` (por ejemplo
  `FieldValidation.AriaInvalid(ApiError?, string field)`) que devuelve
  `"true"` solo cuando el error contiene ese campo y `"false"` en otro caso.
- Cada campo afectado usa la utilidad y define identificadores para su ayuda y
  su mensaje, con la forma `<id-del-campo>-hint` y `<id-del-campo>-error`.

## Impacto en persistencia

- Sin cambios de esquema ni migración.
- Sin cambios de dominio, API u OpenAPI.
- Sin paquetes nuevos.

## Criterios de aceptación

1. Con un `400` asociado a su campo, cada uno de los siete campos se renderiza
   con `aria-invalid="true"` y muestra el borde de error del CSS existente.
2. Sin error en su campo, se renderiza con `aria-invalid="false"`. Ningún campo
   se renderiza con `aria-invalid=""`.
3. Cada campo referencia su texto de ayuda mediante `aria-describedby`. Con
   error, también referencia su mensaje, y todos los identificadores
   referenciados existen en la página.
4. Tras corregir el campo, el estado vuelve a `false` y la referencia al
   mensaje desaparece.
5. Las pruebas unitarias de la utilidad cubren error en el campo, error en otro
   campo, error sin campos y ausencia de error.
6. Los recorridos de las specs 0016, 0017, 0018 y 0021 no cambian.
7. No se modifican la API, OpenAPI, el dominio ni el esquema.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Se centraliza el valor de `aria-invalid` en una utilidad probada en lugar de
  corregir cada expresión por separado, porque el defecto venía de repetir un
  patrón y la 0021 iba a ser solo la primera de varias pantallas con
  formularios.
- Se incluye `aria-describedby` porque responde al mismo problema (un error que
  existe pero no se comunica) y requiere tocar los mismos campos.
- Los errores generales que no pertenecen a un campo ya usan `role="alert"` y no
  cambian.

## Plan de verificación

- Pruebas unitarias de la utilidad.
- Navegador con API real: provocar el `400` de cada uno de los siete campos y
  comprobar en el DOM `aria-invalid`, `aria-describedby`, la existencia de los
  identificadores y el borde aplicado; corregir cada campo y comprobar la
  vuelta al estado válido.
- Búsqueda en el código de que no quedan expresiones booleanas en
  `aria-invalid`.
- Compilación, pruebas, formato y ausencia de cambios del modelo EF Core.

## Evidencia de cierre

Pendiente de implementación y verificación.
