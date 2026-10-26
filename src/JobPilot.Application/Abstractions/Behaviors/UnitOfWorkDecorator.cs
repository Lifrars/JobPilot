using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Abstractions.Persistence;
using JobPilot.Domain.Abstractions;

namespace JobPilot.Application.Abstractions.Behaviors;

/// <summary>
/// Decorator de Unit of Work: si el comando terminó bien, persiste todos los cambios en una sola transacción.
/// Los handlers nunca llaman a SaveChanges: se enfocan solo en la lógica.
/// </summary>
internal sealed class UnitOfWorkDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IUnitOfWork unitOfWork)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var result = await inner.Handle(command, cancellationToken);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
