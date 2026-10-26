# Arquitectura de JobPilot

## Clean Architecture en 4 capas

```mermaid
flowchart LR
    Api["JobPilot.Api<br/>Minimal APIs · MCP · ProblemDetails"] --> Infra["JobPilot.Infrastructure<br/>EF Core · pgvector · Outbox · IA"]
    Api --> App
    Infra --> App["JobPilot.Application<br/>CQRS · Decorators · Strategies"]
    App --> Domain["JobPilot.Domain<br/>Aggregates · Value Objects · Result"]
    AppHost["JobPilot.AppHost (Aspire)"] -. orquesta .-> Api
    AppHost -. contenedor .-> PG[(PostgreSQL + pgvector)]
```

**Regla de dependencias:** las flechas apuntan hacia adentro. El dominio no sabe que existe una base de datos,
una API HTTP o un LLM. Esto se verifica con `tests/JobPilot.ArchitectureTests/LayerTests.cs`.

| Capa | Responsabilidad | Ejemplos |
|---|---|---|
| Domain | Reglas de negocio puras | `JobApplication.ChangeStatus`, `ApplicationStatus`, `SalaryRange` |
| Application | Orquestar casos de uso; definir *puertos* (interfaces) | `ChangeJobApplicationStatusCommandHandler`, `IEmbeddingService` |
| Infrastructure | Implementar los puertos con tecnología concreta (*adaptadores*) | `JobPilotDbContext`, `EfRepository<,>`, `LlmJobPostingAnalyzer` |
| Api | Entrada HTTP/MCP y composición (composition root) | `JobApplicationEndpoints`, `Program.cs` |

### ¿Y el "proyecto IoC" o la "capa Repository"?
- **IoC/CrossCutting separado:** innecesario. Cada capa expone `AddApplication()` / `AddInfrastructure()` y
  `Program.cs` las compone. Es más simple y cada capa es dueña de sus registros.
- **Repository como capa:** el repositorio es un *patrón*, no una capa. La interfaz (`IRepository<,>`) vive en
  Domain y la implementación en Infrastructure. Un proyecto "Repository" aparte es herencia del N-tier clásico (DAL).

## Recorrido de una request

`POST /api/v1/applications/{id}/status` con `{ "status": "Interviewing" }`:

```mermaid
sequenceDiagram
    participant C as Cliente
    participant E as Endpoint (Api)
    participant S as ISender (Mediator)
    participant L as LoggingDecorator
    participant V as ValidationDecorator
    participant U as UnitOfWorkDecorator
    participant H as Handler
    participant D as JobApplication (Domain)
    participant DB as PostgreSQL
    C->>E: HTTP POST
    E->>S: SendAsync(ChangeJobApplicationStatusCommand)
    S->>L: Handle
    L->>V: Handle
    V->>V: FluentValidation (¿status existe?)
    V->>U: Handle
    U->>H: Handle
    H->>DB: GetByIdAsync (repositorio)
    H->>D: ChangeStatus(next, now)
    D->>D: State pattern valida la transición y emite ApplicationStatusChangedDomainEvent
    H-->>U: Result.Success(response)
    U->>DB: SaveChanges (interceptor guarda el evento en OutboxMessages, misma transacción)
    U-->>E: Result
    E-->>C: 200 OK / 400 ProblemDetails
    Note over DB: OutboxProcessor (cada 5 s) publica el evento → invalida caché de stats
```

## Flujo de la IA

1. **Crear postulación** → evento `JobApplicationCreatedDomainEvent` → Outbox → `GenerateJobApplicationEmbeddingHandler`
   calcula el embedding con `IEmbeddingGenerator` y lo guarda en una columna `vector` (pgvector).
2. **Analizar oferta** (`POST /{id}/analyze`) → `IChatClient.GetResponseAsync<AnalysisPayload>()` fuerza al LLM
   a responder con un JSON Schema (structured output) → se guarda en `jsonb`.
3. **Match** (`GET /{id}/match?resumeId=&strategy=`) → Strategy pattern: keyword, embedding (coseno con SIMD) o hybrid.
4. **Búsqueda semántica** (`GET /search?q=`) → embedding de la consulta + operador `<=>` de pgvector.
5. **MCP** (`/mcp`) → los mismos casos de uso expuestos como tools para agentes (Claude Code, Claude Desktop).

Si no hay proveedor configurado, el patrón **Null Object** (`DisabledAiServices`) hace que los endpoints de IA
respondan `503` con un mensaje claro, y todo lo demás sigue funcionando.

## Observabilidad
`JobPilot.ServiceDefaults` configura OpenTelemetry: trazas de ASP.NET Core, HttpClient, **Npgsql (SQL)** y
**Microsoft.Extensions.AI (llamadas al LLM, tokens)**, más métricas y logs. Con Aspire se ven en el dashboard.
