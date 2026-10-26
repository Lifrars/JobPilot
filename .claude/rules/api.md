---
paths:
  - "src/JobPilot.Api/**/*.cs"
---

# Estándares de la capa Api

- Un grupo de endpoints = una clase `internal sealed class XEndpoints : IEndpoint` (se descubre sola). Rutas relativas a `/api/v1`, que ya exige `RequireAuthorization()`: no agregar endpoints anónimos fuera de `/api/v1/auth`.
- Recursos de otro usuario responden **404**, no 403 (no revelar que existen). Sale solo gracias al query filter.
- Los endpoints son delgados: arman el Command/Query, llaman a `ISender` y convierten con `.ToOk()` / `.ToCreated(...)`. Sin lógica de negocio.
- Si el body coincide con el comando, se puede bindear el comando directamente; si hay ID en la ruta, usar un `XxxRequest` record.
- Documentar cada endpoint para OpenAPI: `.WithName()`, `.WithSummary()`, `.Produces<T>()`, `.ProducesProblem(...)` / `.ProducesValidationProblem()`.
- Errores siempre como ProblemDetails vía `ApiResults.Problem(error)`. Mapeo: Validation→400, NotFound→404, Conflict→409, Unavailable→503.
- Endpoints que llaman al LLM: `.RequireRateLimiting(AiEndpoints.RateLimitPolicy)`.
- Tools MCP: métodos estáticos en `Mcp/JobPilotTools.cs` con `[McpServerTool(Name = "snake_case")]` + `[Description]` en inglés; reutilizan `ISender`, nunca acceden a la BD directamente.
