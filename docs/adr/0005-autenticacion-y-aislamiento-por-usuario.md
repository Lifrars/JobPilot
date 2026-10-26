# ADR 0005: Autenticación con Identity y aislamiento de datos por usuario

- **Estado:** aceptada · **Fecha:** 2026-10-26

## Contexto
JobPilot va a estar publicado en internet y lo van a usar varias personas. Cada usuario debe ver **solo** sus
postulaciones, empresas y CVs. Además, ningún desarrollador debería poder "olvidarse" del filtro al agregar una consulta.

## Decisión
1. **ASP.NET Core Identity API endpoints** (`AddIdentityApiEndpoints` + `MapIdentityApi`) en `/api/v1/auth`:
   registro, login y refresh con bearer tokens opacos, sin escribir controllers ni manejar JWT a mano.
2. Las tablas `AspNet*` viven en el mismo `JobPilotDbContext` (hereda de `IdentityDbContext`): un solo motor y una sola migración.
3. El **dominio solo conoce `UserId`**. La cuenta, el password y los tokens son detalles de infraestructura.
4. Todo aggregate implementa **`IOwnedByUser`**. El `DbContext` registra por reflexión, con un método genérico,
   un **global query filter con nombre** (EF Core 10): `IsSystemContext || OwnerId == CurrentUserId`.
   EF evalúa el usuario actual en cada consulta y lo envía como parámetro SQL.
5. `IUserContext` (puerto de Application) entrega el `UserId` del usuario autenticado a los handlers que crean datos.
6. Toda la API (`/api/v1`) y el servidor MCP exigen autenticación (`RequireAuthorization()`).

## Trampas resueltas (aparecen en entrevistas)
| Riesgo | Solución |
|---|---|
| El SQL crudo (`SqlQuery`) **ignora los query filters** | `SearchSimilarAsync` recibe el `ownerId` y filtra en el `WHERE` |
| Caché con clave global: un usuario vería las métricas de otro | Clave y tag por usuario: `pipeline-stats:{userId}`; los eventos llevan `OwnerId` |
| El Outbox corre sin usuario, así que el filtro le ocultaría todo | `CurrentUser.RunAsSystem()` en el scope del procesador |
| Rate limiting global: un usuario agotaría la cuota de todos | Particionado por `NameIdentifier` |
| Un aggregate nuevo sin filtro | `LayerTests.Aggregates_BelongToAUser` falla si no implementa `IOwnedByUser` |
| Revelar que existe un recurso ajeno | El filtro hace que la respuesta sea 404, no 403 |

## Alternativas
- **JWT propio** (`AddJwtBearer` + emisión manual): más control (claims, expiración), pero más código y más riesgo.
- **Proveedor externo** (Entra ID, Auth0, Keycloak): ideal en empresas; aquí agrega dependencias externas a la demo.
- **Filtro en cada repositorio o spec**: frágil, basta un olvido para tener una fuga de datos.
- **Row-Level Security de PostgreSQL**: es la defensa más fuerte (en la base), pero más compleja de operar. Es una buena mejora futura.

## Consecuencias
- ✅ El aislamiento es automático y está probado end-to-end (`UserIsolationTests`).
- ⚠️ Las filas creadas antes de esta migración quedaron con `OwnerId = 00000000-...` y no son visibles para nadie.
- ⚠️ Los bearer tokens de Identity no son JWT: solo los valida esta API. Para varias APIs convendría JWT o un proveedor externo.
