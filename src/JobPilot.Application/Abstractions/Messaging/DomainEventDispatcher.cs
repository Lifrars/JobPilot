using System.Collections.Concurrent;
using JobPilot.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Application.Abstractions.Messaging;

/// <summary>
/// Publica un evento a todos sus handlers (0..N). Lo usa el procesador del Outbox,
/// que recibe el evento como <see cref="IDomainEvent"/> y necesita volver al tipo concreto.
/// </summary>
internal sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, EventWrapper> Wrappers = new();

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var wrapper = Wrappers.GetOrAdd(
            domainEvent.GetType(),
            static eventType => (EventWrapper)Activator.CreateInstance(typeof(EventWrapper<>).MakeGenericType(eventType))!);

        return wrapper.Handle(domainEvent, serviceProvider, cancellationToken);
    }

    private abstract class EventWrapper
    {
        public abstract Task Handle(IDomainEvent domainEvent, IServiceProvider provider, CancellationToken cancellationToken);
    }

    private sealed class EventWrapper<TEvent> : EventWrapper
        where TEvent : IDomainEvent
    {
        public override async Task Handle(IDomainEvent domainEvent, IServiceProvider provider, CancellationToken cancellationToken)
        {
            foreach (var handler in provider.GetServices<IDomainEventHandler<TEvent>>())
            {
                await handler.Handle((TEvent)domainEvent, cancellationToken);
            }
        }
    }
}
