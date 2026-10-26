# JobPilot

Gestor de búsqueda de empleo (postulaciones, entrevistas, CVs) con IA: análisis de ofertas con LLM,
match CV↔oferta, búsqueda semántica (pgvector), servidor MCP y frontend Next.js con tablero kanban.
Proyecto de portafolio: el código debe ser ejemplar y explicable en una entrevista.

## Stack
- **Backend** (`src/`): .NET 10 / C# 14 · ASP.NET Core Minimal APIs · EF Core 10 + PostgreSQL 17 + pgvector · .NET Aspire 13 ·
  Microsoft.Extensions.AI (Anthropic / Ollama / OpenAI) · ModelContextProtocol · Identity · FluentValidation · Scrutor ·
  xUnit v3 (Microsoft.Testing.Platform) + Shouldly + NSubstitute + Testcontainers + NetArchTest.
- **Frontend** (`web/`): Next.js 16 (App Router, Cache Components, React Compiler) · React 19 · TypeScript · Tailwind 4 ·
  TanStack Query · openapi-fetch + openapi-typescript · zod · dnd-kit · Vitest + Testing Library + MSW.
  **Leer `web/AGENTS.md`**: esta versión de Next difiere de lo conocido; consultar `web/node_modules/next/dist/docs/` antes de usar una API.

## Comandos
```bash
dotnet build JobPilot.slnx                       # 0 warnings: TreatWarningsAsErrors está activo
dotnet test                                      # integración necesita Docker (Testcontainers)
dotnet test --project tests/JobPilot.Domain.UnitTests
dotnet run --project src/JobPilot.AppHost        # Aspire: Postgres + API + Next.js + dashboard
dotnet ef migrations add <Nombre> --project src/JobPilot.Infrastructure --startup-project src/JobPilot.Infrastructure --output-dir Persistence/Migrations

cd web && npm run lint && npm run typecheck && npm test && npm run build
cd web && npm run api:types                      # regenera los tipos TS desde el OpenAPI (API corriendo en :5002)
```

## Capas (regla de dependencias: Api → Infrastructure → Application → Domain)
- `src/JobPilot.Domain` — aggregates, value objects, smart enums, eventos, `Result`, `Specification<T>`, `IRepository<,>`. Sin paquetes.
- `src/JobPilot.Application` — casos de uso CQRS (un archivo por feature con Command/Query + Validator + Handler), decorators, estrategias de match, puertos de IA.
- `src/JobPilot.Infrastructure` — EF Core, converters genéricos, repositorios, Outbox, adaptadores de IA.
- `src/JobPilot.Api` — endpoints (`IEndpoint`), ProblemDetails, rate limiting, servidor MCP. `Program.cs` es el composition root.
- `tests/JobPilot.ArchitectureTests` hace cumplir la regla de dependencias: no la rompas.
- `web/` — Next.js como BFF: `src/proxy.ts` reenvía `/api/v1/*` a .NET (cookie HttpOnly de Identity, mismo origen).
  `src/lib` (API tipada, forms, estados), `src/features/<feature>` (UI por funcionalidad), `src/app` (rutas, delgadas).

## Convenciones (obligatorias)
- Errores de negocio con `Result`/`Error`, nunca excepciones. Cada aggregate tiene su clase `XxxErrors`.
- IDs fuertemente tipados (`readonly record struct XxxId(Guid Value) : IStronglyTypedId<XxxId, Guid>`, `Guid.CreateVersion7()`).
- Aggregates: constructores privados, factory `Create(...)` que devuelve `Result<T>`, colecciones privadas expuestas como `IReadOnlyCollection`.
- Handlers `internal sealed`; nunca llaman a `SaveChanges` (lo hace `UnitOfWorkDecorator`).
- Tiempo vía `TimeProvider`; nunca `DateTime.Now`. Fechas `DateTimeOffset` en UTC.
- Mapeo manual a DTOs (`ToResponse()`). **No** agregar MediatR, AutoMapper ni FluentAssertions (licencias comerciales).
- **Multiusuario:** todo aggregate implementa `IOwnedByUser` y recibe `UserId ownerId` en su `Create` (desde `IUserContext`).
  El query filter `OwnerFilter` se aplica solo; `LayerTests.Aggregates_BelongToAUser` lo exige.
- Versiones de paquetes solo en `Directory.Packages.props`.
- Commits: Conventional Commits en inglés, sin trailer `Co-Authored-By`.
- Código en inglés; documentación y comentarios didácticos en español.

## Trampas conocidas
- En specifications, compara smart enums contra **variables locales**, no contra `ApplicationStatus.X` directo (EF no lo traduce). Lo cubre `QueryTranslationTests`.
- IDs generados por el cliente ⇒ `ValueGeneratedNever()` (ya en `EntityConfiguration<,>`), si no EF hace UPDATE en vez de INSERT para hijos nuevos.
- Npgsql solo acepta `DateTimeOffset` con offset 0 → convertir con `ToUniversalTime()` en el dominio.
- Los handlers de eventos corren desde el Outbox (asíncrono, ~5 s): los tests deben esperar, no asumir.
- **SQL crudo (`SqlQuery`/`FromSql`) NO aplica query filters**: filtrar por `OwnerId` explícitamente (ver `SearchSimilarAsync`).
- **Caché**: claves y tags siempre por usuario (`pipeline-stats:{userId}`); una clave global filtra datos entre usuarios.
- El Outbox corre sin usuario: usa `CurrentUser.RunAsSystem()` para ver todos los datos. Nunca llamarlo desde código de request.
- `CurrentUser` captura el objeto `HttpContext` al crearse el scope y lee `User` de forma diferida. Ni leer `IHttpContextAccessor`
  en código diferido (la factory de HybridCache corre sin ExecutionContext: `HttpContext` es null) ni fijar el usuario en el
  constructor (con cookie, Identity lo construye durante la autenticación, cuando aún es anónimo). Ambos casos tienen test.
- Aspire exige el perfil `https` (el `http` falla sin `ASPIRE_ALLOW_UNSECURED_TRANSPORT`).
- **Cambiar un DTO o endpoint ⇒ `npm run api:types`** y commitear `web/src/lib/api/schema.d.ts`; el typecheck del frontend marcará los usos rotos.
- La API usa `JsonNumberHandling.Strict` para que OpenAPI no genere `number | string` en TypeScript.
- Next 16 + Cache Components: leer `cookies()`/`searchParams`/`params` o usar TanStack Query fuera de `<Suspense>` rompe `next build`.

## Más contexto
- Estándares por capa: `.claude/rules/` (se cargan al tocar esos archivos).
- Procedimientos: `.claude/skills/` (`add-feature`, `add-entity`, `explain-pattern`, `interview-prep`).
- Aprendizaje: `docs/arquitectura.md`, `docs/patrones.md`, `docs/genericos.md`, `docs/adr/`, `docs/entrevista.md`.
