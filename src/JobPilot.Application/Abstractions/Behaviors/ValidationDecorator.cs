using FluentValidation;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Domain.Abstractions;

namespace JobPilot.Application.Abstractions.Behaviors;

/// <summary>
/// Decorator: envuelve a cualquier handler y valida la request con FluentValidation antes de ejecutarlo.
/// Se registra como genérico abierto, así aplica a todos los comandos y queries sin tocarlos.
/// </summary>
internal static class ValidationDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> inner,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var error = await ValidateAsync(command, validators, cancellationToken);
            return error is null ? await inner.Handle(command, cancellationToken) : Result.Failure<TResponse>(error);
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> inner,
        IEnumerable<IValidator<TQuery>> validators)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            var error = await ValidateAsync(query, validators, cancellationToken);
            return error is null ? await inner.Handle(query, cancellationToken) : Result.Failure<TResponse>(error);
        }
    }

    private static async Task<ValidationError?> ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = new List<Error>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors.Select(f => Error.Validation(f.PropertyName, f.ErrorMessage)));
        }

        return failures.Count == 0 ? null : new ValidationError(failures);
    }
}
