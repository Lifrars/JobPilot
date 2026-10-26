---
paths:
  - "src/JobPilot.Application/**/*.cs"
---

# Estándares de la capa Application

- Organización por feature (vertical slice), no por tipo técnico: `JobApplications/JobApplicationCommands.cs` agrupa Command + Validator + Handler.
- Comandos: `public sealed record XxxCommand(...) : ICommand<TResponse>`; consultas: `IQuery<TResponse>`.
- Handlers: `internal sealed class XxxCommandHandler(dependencias...) : ICommandHandler<XxxCommand, TResponse>`, con primary constructor.
  - No llamar a `SaveChanges`: lo hace `UnitOfWorkDecorator` si el resultado es exitoso.
  - No validar formato de entrada en el handler: eso va en `AbstractValidator<T>` (lo ejecuta `ValidationDecorator`).
  - Devolver errores del dominio (`XErrors.NotFound(id)`), nunca lanzar excepciones de negocio.
- Validators `internal sealed`; se registran solos (`AddValidatorsFromAssembly`).
- DTOs de salida: `sealed record XxxResponse` + método de mapeo manual (`ToResponse()` / `From(...)`).
- Specifications: subclases de `Specification<T>`. Comparar smart enums/IDs contra **variables locales** (parámetros SQL).
- Dependencias permitidas: Domain, abstracciones de `Microsoft.Extensions.*`, FluentValidation, Scrutor. Prohibido: EF Core, ASP.NET Core, Microsoft.Extensions.AI (los puertos de IA están en `Abstractions/AI`).
- Nuevas estrategias de match: implementar `IMatchScoringStrategy` y registrarla en `DependencyInjection.cs`.
- Handlers de eventos de dominio: `IDomainEventHandler<TEvent>`; corren desde el Outbox, así que deben ser idempotentes y sí llaman a `IUnitOfWork.SaveChangesAsync` si modifican datos.
