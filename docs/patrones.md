# Patrones de diseño en JobPilot

Cada patrón resuelve un problema real del proyecto. Para cada uno: **problema → dónde está → trade-off**.

## De comportamiento

### State — `src/JobPilot.Domain/JobApplications/ApplicationStatus.cs`
- **Problema:** una postulación no puede pasar de *Wishlist* a *Offer*. Con `enum` + `switch` esa regla termina repetida en varios lugares.
- **Solución:** cada estado es una subclase privada que declara sus transiciones (`NextStates()`) y capacidades (`AllowsInterviews`). `JobApplication.ChangeStatus` delega en `Status.CanTransitionTo(next)`.
- **Trade-off:** más clases que un enum. Vale la pena cuando los estados tienen *comportamiento*, no solo un nombre.

### Strategy — `src/JobPilot.Application/JobApplications/Matching/MatchStrategies.cs`
- **Problema:** hay varias formas de calcular el match CV↔oferta, y se eligen en tiempo de ejecución (`?strategy=`).
- **Solución:** `IMatchScoringStrategy` con `KeywordMatchStrategy`, `EmbeddingMatchStrategy` y `HybridMatchStrategy`. El handler recibe `IEnumerable<IMatchScoringStrategy>` y elige por `Name`.
- **Trade-off:** para dos variantes fijas un `if` bastaría. Aquí, agregar una estrategia nueva no toca código existente (principio Open/Closed).

### Mediator + CQRS — `src/JobPilot.Application/Abstractions/Messaging/`
- **Problema:** los endpoints no deberían conocer cada handler concreto ni sus dependencias.
- **Solución:** `ISender.SendAsync(command)` / `QueryAsync(query)` resuelve el handler por tipo. Los comandos escriben y las queries solo leen.
- **Trade-off:** agrega una indirección (más difícil de navegar con F12). Lo compensan los decorators transversales.

### Template Method — `src/JobPilot.Infrastructure/Persistence/Configurations/EntityConfiguration.cs`
- **Problema:** toda entidad necesita clave, IDs sin auto-generar e ignorar `DomainEvents`.
- **Solución:** la base fija los pasos comunes en `Configure()` y las subclases implementan solo `ConfigureEntity()`.

## Estructurales

### Decorator — `src/JobPilot.Application/Abstractions/Behaviors/`
- **Problema:** logging, validación y guardado son transversales; copiarlos en cada handler es duplicación.
- **Solución:** `LoggingDecorator` → `ValidationDecorator` → `UnitOfWorkDecorator` → handler, registrados como **genéricos abiertos** con Scrutor (`DependencyInjection.cs`). `Microsoft.Extensions.AI` usa la misma idea: `AddChatClient(...).UseOpenTelemetry().UseLogging()`.
- **Trade-off:** el orden de registro importa (el último queda más afuera).

### Composite — `HybridMatchStrategy`
Una estrategia compuesta por otras dos, que también degrada con elegancia si falta una.

### Null Object — `src/JobPilot.Infrastructure/AI/AiServices.cs` (`DisabledAiServices`)
Sin IA configurada se inyecta un objeto que devuelve `AiErrors.ChatNotConfigured`. Así no hay `if (ai is null)` por todo el código.

## Creacionales

### Factory Method — `Company.Create`, `JobApplication.Create`, `Resume.Create`, `SalaryRange.Create`
Los constructores son privados. La única forma de crear un objeto es a través de una factory que valida invariantes y devuelve `Result<T>`, así que un objeto inválido no puede existir. `LayerTests.Aggregates_DoNotExposePublicConstructors` lo verifica.

### Factory (configuración) — `AiServiceCollectionExtensions.CreateChatClient`
Elige el proveedor (Anthropic, Ollama u OpenAI) según la configuración y devuelve siempre `IChatClient`.

## De persistencia y arquitectura

### Repository + Unit of Work — `src/JobPilot.Infrastructure/Persistence/Repositories/Repositories.cs`
- `IRepository<TAggregate, TId>` genérico, implementado una sola vez (`EfRepository<,>`) y registrado como genérico abierto.
- `JobApplicationRepository` hereda del genérico y agrega consultas propias (`CountByStatusAsync`, `SearchSimilarAsync`).
- `JobPilotDbContext` *es* el Unit of Work (`IUnitOfWork`).
- **Debate en entrevistas:** "EF Core ya es Repository + UoW". Es cierto. Aquí el repositorio aporta tres cosas: aísla Application de EF, restringe la persistencia a aggregate roots (por la restricción genérica) y facilita los tests.

### Specification — `src/JobPilot.Domain/Abstractions/Specification.cs` + `SpecificationEvaluator`
Los filtros reutilizables (`JobApplicationsFilterSpec`, `StaleApplicationsSpec`) son objetos, y el evaluador genérico los traduce a `IQueryable`. Trampa real: comparar contra el campo estático `ApplicationStatus.Applied` no se traduce a SQL; se usan variables locales. `QueryTranslationTests` lo cubre.

### Outbox — `src/JobPilot.Infrastructure/Outbox/`
- **Problema:** guardar un cambio y publicar su evento en dos pasos puede dejar uno sin el otro.
- **Solución:** `InsertOutboxMessagesInterceptor` guarda los eventos en la **misma transacción** y `OutboxProcessor` (un BackgroundService) los publica después.
- **Trade-off:** consistencia eventual (unos segundos de retraso). Con varias instancias hay que leer con `FOR UPDATE SKIP LOCKED`, y los handlers deben ser idempotentes.

### Result pattern — `src/JobPilot.Domain/Abstractions/Result.cs`
Los errores de negocio son valores (`Error`) y no excepciones: hace explícito qué puede fallar y es más barato. `ApiResults.Problem` los traduce a ProblemDetails (RFC 9457).

### Options pattern — `AiOptions`, `OutboxOptions`
La configuración es tipada y se enlaza desde `appsettings.json` o user-secrets.

### Smart Enum + Strongly-typed IDs
- `Enumeration<TEnum>`: un enum con comportamiento que se guarda por nombre en la base (`EnumerationConverter<TEnum>`).
- `JobApplicationId`, `CompanyId`...: el compilador impide pasar un `CompanyId` donde va un `JobApplicationId`.
