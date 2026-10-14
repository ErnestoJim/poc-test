# 0016 — Primera interfaz web para proyectos y propuestas

## Estado

Aprobada para implementación el 14 de octubre de 2026.

## Base documental

- Constitución: `specs/constitution.md`, versión 1.0.0.
- Specs relacionadas: `specs/0001-projects/spec.md`,
  `specs/0002-feature-proposals/spec.md`,
  `specs/0003-feature-proposal-lifecycle/spec.md` y
  `specs/0015-follow-up-proposals/spec.md`.

Los contratos HTTP y las reglas de dominio de esas specs permanecen vigentes.
Esta spec introduce un consumidor web de las operaciones existentes; no cambia
el comportamiento de la API.

## Problema

SpecFlow solo puede utilizarse mediante peticiones HTTP directas. Una persona
que quiere explorar los proyectos y sus propuestas o registrar trabajo nuevo
necesita conocer las rutas y construir JSON manualmente. Esto dificulta usar y
mostrar el flujo del producto, especialmente cuando la base local está vacía.

## Objetivo

Ofrecer una primera interfaz web en .NET que permita crear, listar y consultar
proyectos y propuestas sin conocer los detalles de las peticiones HTTP. La
interfaz debe conservar el contexto del proyecto al navegar y reflejar las
respuestas de la API, incluidos sus errores, sin duplicar reglas de negocio.

## Alcance

- Nueva aplicación `src/SpecFlow.Web` con Blazor Web App en .NET 10 e
  interactividad en servidor, incorporada a `SpecFlow.slnx`.
- Páginas de listado, creación y detalle de proyectos.
- Páginas de listado, creación y detalle de propuestas dentro de un proyecto.
- Presentación de estado, fecha de decisión, motivo de rechazo y propuesta de
  origen cuando existan en una respuesta de propuesta.
- Cliente HTTP de servidor para consumir exclusivamente los contratos públicos
  de `SpecFlow.Api`, con URL base configurable.
- Estados visibles de carga, listas vacías, errores de validación, conflictos,
  recursos no encontrados y falta de conexión con la API.
- Instrucciones de arranque local y comprobación manual en el README.

## Fuera de alcance

- Aceptar o rechazar propuestas y crear continuaciones con `sourceProposalId`.
- Crear, editar, consultar o representar especificaciones, criterios, tareas y
  decisiones técnicas.
- Renderizar Markdown como HTML o mostrar una vista previa.
- Cambiar rutas, cuerpos, respuestas o validaciones de la API.
- Acceso directo del frontend al dominio, EF Core o SQLite.
- Autenticación, autorización, despliegue, Docker y Aspire.
- Búsqueda, filtros, paginación y actualización automática de listados.

## Casos de uso

### UC-01 — Explorar proyectos

Una persona abre la aplicación y ve los proyectos en el orden devuelto por la
API. Puede abrir el detalle de cualquiera. Si no existen proyectos, ve un
estado vacío con acceso a la creación. Si la API no responde, ve un error con
una acción para reintentar la consulta.

### UC-02 — Crear un proyecto

La persona introduce nombre y descripción opcional. Tras una creación correcta,
la interfaz navega al detalle del proyecto devuelto por la API. Si la API
rechaza la entrada, muestra los errores asociados a los campos; si el nombre ya
existe, muestra el conflicto sin perder los valores introducidos.

### UC-03 — Explorar propuestas de un proyecto

Desde el detalle de un proyecto, la persona ve solo sus propuestas y puede
abrir el detalle de una. Los listados vacíos se presentan como tales. El detalle
muestra el estado y los datos de decisión cuando corresponden. Si una
propuesta tiene `sourceProposalId`, se ofrece un enlace a esa propuesta dentro
del mismo proyecto.

### UC-04 — Crear una propuesta ordinaria

La persona introduce título y descripción opcional desde el contexto de un
proyecto. La interfaz envía la creación sin `sourceProposalId` y navega al
detalle devuelto. Los errores de validación se muestran junto a los campos sin
perder la entrada. Si el proyecto dejó de existir, se informa y se ofrece
volver al listado de proyectos.

## Comportamiento de la interfaz

| Vista | Ruta web propuesta | Datos de la API |
|---|---|---|
| Listado de proyectos | `/projects` | `GET /api/projects` |
| Crear proyecto | `/projects/new` | `POST /api/projects` |
| Detalle de proyecto y sus propuestas | `/projects/{projectId}` | `GET /api/projects/{projectId}` y `GET /api/projects/{projectId}/proposals` |
| Crear propuesta | `/projects/{projectId}/proposals/new` | `POST /api/projects/{projectId}/proposals` |
| Detalle de propuesta | `/projects/{projectId}/proposals/{proposalId}` | `GET /api/projects/{projectId}/proposals/{proposalId}` |

- La raíz `/` conduce al listado de proyectos.
- Las rutas conservan el identificador del proyecto. Una ruta con identificador
  inválido o un recurso inexistente muestra una página comprensible y permite
  volver al listado de proyectos; no presenta un error técnico sin tratar.
- Los formularios muestran los límites conocidos de longitud como ayuda, pero
  la respuesta de la API decide si la entrada es válida. El frontend no
  normaliza los valores antes de enviarlos.
- Los errores `application/problem+json` con `errors` se presentan por campo
  cuando existe una correspondencia; los demás se muestran como mensaje
  general. No se exponen detalles internos ni se muestra el JSON bruto.
- Durante una creación se impide un segundo envío desde el mismo formulario.
  Un fallo de red en un `POST` no desencadena un reintento automático.
- Los estados visibles de propuesta reflejan `pending`, `accepted` y
  `rejected`. Un estado desconocido se muestra de forma neutral sin habilitar
  acciones de transición.
- Los textos visibles de esta primera interfaz están en español; nombres de
  código, rutas y contratos permanecen en inglés según la constitución.

## Decisiones técnicas propuestas

- `SpecFlow.Web` es un proceso ASP.NET Core separado. Sus componentes llaman a
  un servicio de cliente HTTP registrado con `IHttpClientFactory`; no hacen
  consultas a la base de datos ni referencian `SpecFlow.Domain` o
  `SpecFlow.Infrastructure`.
- La URL base de la API se configura en el servidor web y se documenta para
  desarrollo local. El navegador habla con `SpecFlow.Web`; las peticiones a la
  API salen del servidor web, por lo que este incremento no requiere CORS en
  `SpecFlow.Api`.
- Los modelos usados por la interfaz representan los JSON públicos necesarios.
  No se comparten entidades de dominio con la web ni se añade una capa
  `Application` por anticipado.
- Se utiliza el estilo básico del proyecto Blazor, con diseño adaptable a
  móvil y escritorio y navegación mediante enlaces ordinarios. Una biblioteca
  adicional de componentes solo se valorará ante una necesidad concreta.

## Impacto en contratos y persistencia

- Sin cambios en los contratos HTTP, OpenAPI, dominio ni esquema de SQLite.
- Sin migración de EF Core.
- La aplicación web necesita la API disponible para consultar y guardar datos.

## Criterios de aceptación

1. Con la API y la web en ejecución y una base vacía, la raíz conduce a un
   listado vacío desde el que se puede crear un proyecto sin usar otra
   herramienta.
2. Crear un proyecto válido muestra su detalle; al volver al listado, el
   proyecto aparece con el nombre y la descripción devueltos por la API.
3. Un nombre de proyecto inválido muestra el error del campo y permite
   corregirlo; un nombre duplicado muestra el conflicto y conserva la entrada.
4. El detalle de un proyecto existente muestra exclusivamente sus propuestas
   en el orden recibido de la API y presenta un estado vacío cuando no hay
   ninguna.
5. Crear una propuesta válida desde un proyecto muestra su detalle en estado
   `pending`; al regresar al proyecto aparece en el listado.
6. Una propuesta inválida muestra los errores de sus campos y permite
   corregirlos sin perder la entrada.
7. El detalle de propuestas existentes muestra `pending`, `accepted` o
   `rejected` según la API, junto con fecha y motivo de decisión cuando
   existan; si hay origen, enlaza a su detalle en el mismo proyecto.
8. Un identificador inválido, un proyecto inexistente o una propuesta ajena al
   proyecto no muestran datos de otro contexto y ofrecen una salida clara hacia
   el listado de proyectos.
9. Cuando la API no responde, las consultas muestran un error y permiten
   reintentar; los formularios conservan la entrada tras el fallo y no
   reenvían automáticamente un `POST`.
10. Un envío en curso no puede iniciarse dos veces desde el mismo formulario.
11. La web se inicia con los comandos documentados, obtiene la URL de la API de
    configuración y se puede abrir en un navegador local.
12. La implementación de esta spec no modifica los contratos ni el esquema de
    datos existentes y no incorpora acciones fuera de alcance.

## Decisiones

### Pendientes

- Ninguna.

### Acordadas

- Incluir creación de proyectos hace posible recorrer el flujo completo desde
  una base vacía.
- La creación de continuaciones y las transiciones se abordarán en otro
  incremento para mantener esta primera entrega acotada.

## Plan de verificación

- Pruebas automatizadas del cliente HTTP para las seis operaciones utilizadas,
  la deserialización de respuestas y la traducción de errores de validación,
  conflicto, no encontrado y falta de conexión.
- Comprobación de navegación y formularios en navegador con la API real y
  SQLite local: base vacía, creación de proyecto y propuesta, error de campo,
  nombre duplicado y API no disponible.
- Comprobación de aislamiento mediante una ruta de propuesta bajo otro
  proyecto y de presentación de una propuesta con origen.
- Compilación, pruebas y formato de la solución. Confirmar que no hay cambios
  pendientes del modelo de EF Core ni del documento OpenAPI.

## Evidencia de cierre

Pendiente de implementación y verificación.
