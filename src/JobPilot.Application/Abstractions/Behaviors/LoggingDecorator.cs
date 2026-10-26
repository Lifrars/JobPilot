using System.Diagnostics;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobPilot.Application.Abstractions.Behaviors;

/// <summary>Decorator de logging estructurado: nombre de la request, duración y error si falló.</summary>
internal static partial class LoggingDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> inner,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
            LogAsync(typeof(TCommand).Name, logger, () => inner.Handle(command, cancellationToken));
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> inner,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken) =>
            LogAsync(typeof(TQuery).Name, logger, () => inner.Handle(query, cancellationToken));
    }

    private static async Task<Result<TResponse>> LogAsync<TResponse>(
        string requestName,
        ILogger logger,
        Func<Task<Result<TResponse>>> next)
    {
        var start = Stopwatch.GetTimestamp();
        var result = await next();
        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        if (result.IsSuccess)
        {
            LogCompleted(logger, requestName, elapsedMs);
        }
        else
        {
            LogFailed(logger, requestName, elapsedMs, result.Error.Code);
        }

        return result;
    }

    // LoggerMessage genera el código de logging en compilación (sin boxing ni parsing en runtime).
    [LoggerMessage(Level = LogLevel.Information, Message = "{RequestName} completed in {ElapsedMs:0.0} ms")]
    private static partial void LogCompleted(ILogger logger, string requestName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} failed in {ElapsedMs:0.0} ms with {ErrorCode}")]
    private static partial void LogFailed(ILogger logger, string requestName, double elapsedMs, string errorCode);
}
