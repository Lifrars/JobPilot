# JobPilot

API para gestionar una búsqueda de empleo (postulaciones, entrevistas, CVs) con IA: análisis de ofertas con LLM,
match CV↔oferta, búsqueda semántica (pgvector) y servidor MCP. Proyecto de portafolio: el código debe ser
ejemplar y explicable en una entrevista.

## Stack
.NET 10 / C# 14 · ASP.NET Core Minimal APIs · EF Core 10 + PostgreSQL 17 + pgvector · .NET Aspire 13 ·
Microsoft.Extensions.AI (Anthropic / Ollama / OpenAI) · ModelContextProtocol · FluentValidation · Scrutor ·
xUnit v3 (Microsoft.Testing.Platform) + Shouldly + NSubstitute + Testcontainers + NetArchTest.

## Comandos
```bash
dotnet build JobPilot.slnx                       # 0 warnings: TreatWarningsAsErrors está activo
dotnet test                                      # integración necesita Docker (Testcontainers)
dotnet test --project tests/JobPilot.Domain.UnitTests
dotnet run --project src/JobPilot.AppHost        # Aspire: Postgres + API + dashboard
dotnet ef migrations add <Nombre> --project src/JobPilot.Infrastructure --startup-project src/JobPilot.Infrastructure --output-dir Persistence/Migrations
```

## Capas (regla de dependencias: Api → Infrastructure → Application → Domain)
- `src/JobPilot.Domain` — aggregates, value objects, smart enums, eventos, `Result`, `Specification<T>`, `IRepository<,>`. Sin paquetes.
- `src/JobPilot.Application` — casos de uso CQRS (un archivo por feature con Command/Query + Validator + Handler), decorators, estrategias de match, puertos de IA.
- `src/JobPilot.Infrastructure` — EF Core, converters genéricos, repositorios, Outbox, adaptadores de IA.
- `src/JobPilot.Api` — endpoints (`IEndpoint`), ProblemDetails, rate limiting, servidor MCP. `Program.cs` es el composition root.
- `tests/JobPilot.ArchitectureTests` hace cumplir la regla de dependencias: no la rompas.

## Convenciones (obligatorias)
- Errores de negocio con `Result`/`Error`, nunca excepciones. Cada aggregate tiene su clase `XxxErrors`.
- IDs fuertemente tipados (`readonly record struct XxxId(Guid Value) : IStronglyTypedId<XxxId, Guid>`, `Guid.CreateVersion7()`).
- Aggregates: constructores privados, factory `Create(...)` que devuelve `Result<T>`, colecciones privadas expuestas como `IReadOnlyCollection`.
- Handlers `internal sealed`; nunca llaman a `SaveChanges` (lo hace `UnitOfWorkDecorator`).
- Tiempo vía `TimeProvider`; nunca `DateTime.Now`. Fechas `DateTimeOffset` en UTC.
- Mapeo manual a DTOs (`ToResponse()`). **No** agregar MediatR, AutoMapper ni FluentAssertions (licencias comerciales).
- Versiones de paquetes solo en `Directory.Packages.props`.
- Código en inglés; documentación y comentarios didácticos en español.

## Trampas conocidas
- En specifications, compara smart enums contra **variables locales**, no contra `ApplicationStatus.X` directo (EF no lo traduce). Lo cubre `QueryTranslationTests`.
- IDs generados por el cliente ⇒ `ValueGeneratedNever()` (ya en `EntityConfiguration<,>`), si no EF hace UPDATE en vez de INSERT para hijos nuevos.
- Npgsql solo acepta `DateTimeOffset` con offset 0 → convertir con `ToUniversalTime()` en el dominio.
- Los handlers de eventos corren desde el Outbox (asíncrono, ~5 s): los tests deben esperar, no asumir.

## Más contexto
- Estándares por capa: `.claude/rules/` (se cargan al tocar esos archivos).
- Procedimientos: `.claude/skills/` (`add-feature`, `add-entity`, `explain-pattern`, `interview-prep`).
- Aprendizaje: `docs/arquitectura.md`, `docs/patrones.md`, `docs/genericos.md`, `docs/adr/`, `docs/entrevista.md`.
