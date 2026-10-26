using JobPilot.Domain.Abstractions;

namespace JobPilot.Application.Abstractions.Messaging;

/// <summary>Comando: intención de cambiar el estado del sistema. <typeparamref name="TResponse"/> es lo que devuelve.</summary>
public interface ICommand<TResponse>;

/// <summary>Consulta: solo lee, nunca modifica estado (CQRS).</summary>
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Handler de eventos de dominio. El <c>in</c> (contravarianza) permite que un handler de
/// <c>IDomainEvent</c> reciba cualquier evento concreto.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Mediator: los endpoints solo conocen este punto de entrada, no los handlers concretos.</summary>
public interface ISender
{
    Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
