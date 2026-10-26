---
name: add-feature
description: Crea un caso de uso completo en JobPilot (Command o Query + Validator + Handler + endpoint + tests) siguiendo la arquitectura por capas y CQRS del proyecto. Usar cuando se pida "agregar un endpoint", "nuevo caso de uso", "nueva funcionalidad" o similar.
---

# Agregar un feature (vertical slice)

Sigue estos pasos en orden. Toma como referencia real `src/JobPilot.Application/JobApplications/JobApplicationCommands.cs`
(comandos) y `JobApplicationQueries.cs` (queries).

## 1. Decide: ¿Command o Query?
- Cambia estado → `ICommand<TResponse>` (el `UnitOfWorkDecorator` guardará los cambios).
- Solo lee → `IQuery<TResponse>`.

## 2. Dominio primero (si hay regla de negocio)
- Agrega el comportamiento como método del aggregate en `src/JobPilot.Domain/<Aggregate>/`, que devuelva `Result`.
- Agrega los errores nuevos a `XErrors`.
- Si otros procesos deben reaccionar, emite un `XxxDomainEvent` con `Raise(...)`.
- Escribe el test en `tests/JobPilot.Domain.UnitTests` **antes** de seguir.

## 3. Application
En el archivo del feature (`src/JobPilot.Application/<Feature>/...`):
```csharp
public sealed record DoSomethingCommand(Guid Id, string Value) : ICommand<SomethingResponse>;

internal sealed class DoSomethingCommandValidator : AbstractValidator<DoSomethingCommand>
{
    public DoSomethingCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Value).NotEmpty().MaximumLength(200);
    }
}

internal sealed class DoSomethingCommandHandler(IJobApplicationRepository applications, TimeProvider timeProvider)
    : ICommandHandler<DoSomethingCommand, SomethingResponse>
{
    public async Task<Result<SomethingResponse>> Handle(DoSomethingCommand command, CancellationToken cancellationToken)
    {
        var id = JobApplicationId.From(command.Id);
        var application = await applications.GetByIdAsync(id, cancellationToken);
        if (application is null)
        {
            return JobApplicationErrors.NotFound(id);
        }

        var result = application.DoSomething(command.Value, timeProvider.GetUtcNow());
        return result.IsSuccess ? application.ToSomethingResponse() : result.Error;
    }
}
```
No hace falta registrar nada: Scrutor y FluentValidation escanean el ensamblado.

## 4. Api
Agrega el endpoint en la clase `IEndpoint` del grupo (`src/JobPilot.Api/Endpoints/`):
```csharp
group.MapPost("/{id:guid}/something", async (Guid id, DoSomethingRequest request, ISender sender, CancellationToken ct) =>
        (await sender.SendAsync(new DoSomethingCommand(id, request.Value), ct)).ToOk())
    .WithName("DoSomething")
    .WithSummary("...")
    .Produces<SomethingResponse>()
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound);
```
Si es útil para un agente de IA, expónlo también como tool en `src/JobPilot.Api/Mcp/JobPilotTools.cs`.

## 5. Persistencia (solo si cambió el modelo)
```bash
dotnet ef migrations add <NombreDescriptivo> --project src/JobPilot.Infrastructure --startup-project src/JobPilot.Infrastructure --output-dir Persistence/Migrations
```
Revisa el `.cs` generado. Si agregaste una specification, añade un caso en `QueryTranslationTests`.

## 6. Tests y verificación
- Unit test del handler en `tests/JobPilot.Application.UnitTests` (NSubstitute + `FakeTimeProvider`).
- Flujo HTTP en `tests/JobPilot.Api.IntegrationTests/JobApplicationApiTests.cs` si el endpoint es importante.
- `dotnet build JobPilot.slnx` (0 warnings) y `dotnet test`.
