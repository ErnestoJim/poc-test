# 0001 — Gestión básica de proyectos

## Estado

Implementada y verificada el 5 de octubre de 2026, tras su aprobación para
implementación el 2 de octubre de 2026.

## Problema

SpecFlow necesita una entidad raíz que permita organizar posteriormente
funcionalidades, especificaciones, tareas y decisiones técnicas. Actualmente no
existe ninguna capacidad para registrar o consultar proyectos.

## Objetivo

Proporcionar una API HTTP que permita crear un proyecto, consultarlo por su
identificador y listar los proyectos existentes. La implementación debe
establecer una base sencilla para el crecimiento posterior del producto sin
introducir abstracciones que todavía no sean necesarias.

## Alcance

- Modelo de dominio `Project`.
- Persistencia mediante Entity Framework Core y SQLite.
- Creación de proyectos.
- Consulta por identificador.
- Listado de proyectos sin paginación.
- Nombres únicos sin distinguir mayúsculas, minúsculas o espacios exteriores.
- Validación de entrada.
- Errores HTTP mediante Problem Details.
- Documento OpenAPI.
- Pruebas unitarias y de integración.

## Fuera de alcance

- Edición o eliminación de proyectos.
- Autenticación, autorización, usuarios, equipos u organizaciones.
- Paginación, búsqueda o filtrado.
- Propuestas de funcionalidades, especificaciones, tareas o decisiones técnicas.
- Frontend Blazor.
- MCP o integración con modelos de IA.
- Docker, Aspire, despliegue o PostgreSQL.

## Casos de uso

### UC-01 — Crear un proyecto

Un consumidor envía un nombre y, opcionalmente, una descripción. El sistema
valida la entrada, comprueba que el nombre no está en uso, crea el proyecto, lo
persiste y devuelve el recurso creado.

### UC-02 — Consultar un proyecto

Un consumidor proporciona el identificador de un proyecto. Si existe, el
sistema devuelve el proyecto. Si no existe, devuelve un Problem Details con
estado 404.

### UC-03 — Listar proyectos

El sistema devuelve todos los proyectos, desde el más reciente al más antiguo.
Si no existe ninguno, devuelve una colección vacía.

## Modelo de dominio

| Campo | Tipo | Requerido | Descripción |
|---|---|---:|---|
| `Id` | UUID | Sí | Identificador generado por la aplicación |
| `Name` | string | Sí | Nombre visible y normalizado exteriormente |
| `NormalizedName` | string | Sí | Nombre interno para garantizar unicidad |
| `Description` | string nullable | No | Descripción normalizada |
| `CreatedAtUtc` | DateTimeOffset | Sí | Instante UTC de creación |

`NormalizedName` es persistente, pero no forma parte del contrato HTTP.

## Reglas de negocio

1. `Id` no puede ser un UUID vacío.
2. `Name` se normaliza eliminando espacios exteriores.
3. `Name` debe contener entre 1 y 100 caracteres después de normalizarlo.
4. `Description` se normaliza eliminando espacios exteriores.
5. Una descripción vacía después de normalizarse se almacena como `null`.
6. `Description` puede contener como máximo 1.000 caracteres.
7. `CreatedAtUtc` se establece desde el reloj del sistema y se almacena en UTC.
8. El cliente no puede proporcionar `Id` ni `CreatedAtUtc`.
9. Dos nombres que sólo difieren en mayúsculas, minúsculas o espacios exteriores
   se consideran el mismo nombre.
10. La unicidad debe protegerse también mediante una restricción en la base de
    datos para evitar condiciones de carrera.

## Contratos HTTP

### Crear un proyecto

`POST /api/projects`

Request:

```json
{
  "name": "SpecFlow",
  "description": "POC de desarrollo asistido por IA"
}
```

Respuesta correcta: `201 Created`, cabecera `Location` apuntando al recurso y:

```json
{
  "id": "5fc297e9-cba9-47f4-862f-b984b53c317c",
  "name": "SpecFlow",
  "description": "POC de desarrollo asistido por IA",
  "createdAtUtc": "2026-10-02T08:30:00+00:00"
}
```

Errores:

- `400 Bad Request` para entrada inválida.
- `409 Conflict` cuando el nombre ya existe.
- `415 Unsupported Media Type` cuando el contenido no es JSON compatible.

### Consultar un proyecto

`GET /api/projects/{id}`

- `200 OK` con el proyecto.
- `400 Bad Request` si el identificador no es un UUID válido.
- `404 Not Found` si no existe.

### Listar proyectos

`GET /api/projects`

- `200 OK` con un array de proyectos.
- Orden por `CreatedAtUtc` descendente y después por `Id`.
- Si no hay proyectos, el resultado es `[]`.

## Respuestas de error

Los errores utilizan `application/problem+json`. Los errores de validación
incluyen un diccionario `errors` indexado por campo. Los errores no deben
exponer stack traces, rutas locales, cadenas de conexión ni detalles internos
de Entity Framework Core.

## Criterios de aceptación

1. Una petición válida crea un proyecto, responde 201 e incluye `Location`.
2. El proyecto creado puede recuperarse mediante el identificador devuelto.
3. Un listado vacío devuelve 200 y `[]`.
4. Varios proyectos se devuelven del más reciente al más antiguo.
5. Un nombre nulo, vacío o compuesto por espacios devuelve 400.
6. Un nombre de más de 100 caracteres devuelve 400.
7. Una descripción de más de 1.000 caracteres devuelve 400.
8. El nombre y la descripción se devuelven sin espacios exteriores.
9. Una descripción compuesta por espacios se almacena como `null`.
10. Crear `SpecFlow` y después ` specflow ` devuelve 409.
11. Un UUID válido inexistente devuelve 404 mediante Problem Details.
12. Un identificador con formato inválido devuelve 400 mediante Validation
    Problem Details.
13. OpenAPI contiene las tres operaciones.
14. Los datos permanecen disponibles entre scopes y reinicios usando la base
    SQLite persistente.

## Estrategia de pruebas

### Pruebas unitarias

- Creación válida y normalización.
- Conversión de descripción vacía a `null`.
- Conversión del instante a UTC.
- Rechazo de identificador vacío.
- Límites de nombre y descripción.

### Pruebas de integración

Se utiliza `WebApplicationFactory` con una conexión SQLite relacional en memoria
que permanece abierta durante cada prueba. No se utiliza el proveedor EF Core
InMemory ni mocks de EF Core.

Se comprueban los contratos POST y GET, la normalización, la unicidad, los
Problem Details, el orden del listado y el documento OpenAPI.

## Requisitos no funcionales

- Compilación sin warnings y nullable reference types habilitado.
- Operaciones asíncronas con `CancellationToken`.
- Consultas de lectura con `AsNoTracking`.
- Fechas en formato ISO 8601 y UTC.
- Sin secretos ni archivos SQLite generados en Git.
- La base local de desarrollo debe ser persistente.
- Los endpoints no devuelven directamente entidades de EF Core.
- Las pruebas no dependen de la hora real ni comparten datos.
- SQLite debe poder sustituirse posteriormente sin cambiar los contratos HTTP
  ni el modelo de dominio.

## Decisiones aprobadas

- Nombre del producto: SpecFlow.
- Tres proyectos iniciales: Domain, Infrastructure y Api.
- Sin capa Application hasta que exista un segundo adaptador.
- Nombres de proyecto únicos sin distinguir mayúsculas y minúsculas.
- Listado sin paginación durante esta fase.
- SQLite persistente para desarrollo y SQLite en memoria para integración.
