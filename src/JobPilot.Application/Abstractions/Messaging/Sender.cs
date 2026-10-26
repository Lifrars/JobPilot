using System.Collections.Concurrent;
using JobPilot.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Application.Abstractions.Messaging;

/// <summary>
/// Implementación propia del patrón Mediator (sustituye a MediatR, que ahora es de pago).
///
/// El problema: en <c>SendAsync&lt;TResponse&gt;</c> conocemos TResponse en compilación, pero el tipo concreto
/// del comando solo en ejecución. La solución clásica: un "wrapper" genérico cerrado con
/// <c>MakeGenericType</c> una sola vez por tipo y cacheado; desde ahí, todo es tipado y sin reflexión.
/// </summary>
internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wrapper = (RequestWrapper<TResponse>)Wrappers.GetOrAdd(
            command.GetType(),
            static commandType => Activator.CreateInstance(
                typeof(CommandWrapper<,>).MakeGenericType(commandType, typeof(TResponse)))!);

        return wrapper.Handle(command, serviceProvider, cancellationToken);
    }

    public Task<Result<TResponse>> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var wrapper = (RequestWrapper<TResponse>)Wrappers.GetOrAdd(
            query.GetType(),
            static queryType => Activator.CreateInstance(
                typeof(QueryWrapper<,>).MakeGenericType(queryType, typeof(TResponse)))!);

        return wrapper.Handle(query, serviceProvider, cancellationToken);
    }

    private abstract class RequestWrapper<TResponse>
    {
        public abstract Task<Result<TResponse>> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken);
    }

    private sealed class CommandWrapper<TCommand, TResponse> : RequestWrapper<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public override Task<Result<TResponse>> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken) =>
            provider.GetRequiredService<ICommandHandler<TCommand, TResponse>>().Handle((TCommand)request, cancellationToken);
    }

    private sealed class QueryWrapper<TQuery, TResponse> : RequestWrapper<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public override Task<Result<TResponse>> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken) =>
            provider.GetRequiredService<IQueryHandler<TQuery, TResponse>>().Handle((TQuery)request, cancellationToken);
    }
}
