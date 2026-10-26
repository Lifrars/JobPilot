using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Infrastructure.Identity;
using JobPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPilot.Infrastructure.Outbox;

/// <summary>
/// BackgroundService que publica los eventos pendientes del Outbox cada <see cref="OutboxOptions.Interval"/>.
/// Mejora para producción con varias instancias: leer con <c>FOR UPDATE SKIP LOCKED</c>.
/// </summary>
internal sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogBatchFailed(logger, exception);
            }
        }
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // No hay usuario autenticado en segundo plano: el Outbox procesa eventos de todos los usuarios.
        scope.ServiceProvider.GetRequiredService<CurrentUser>().RunAsSystem();
        var context = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedOn == null && m.Attempts < options.Value.MaxAttempts)
            .OrderBy(m => m.OccurredOn)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await dispatcher.DispatchAsync(message.ToDomainEvent(), cancellationToken);
                message.MarkProcessed(timeProvider.GetUtcNow());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.MarkFailed(exception.Message);
                LogMessageFailed(logger, message.Id, message.Type, exception);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({MessageType}) failed")]
    private static partial void LogMessageFailed(ILogger logger, Guid messageId, string messageType, Exception exception);
}
