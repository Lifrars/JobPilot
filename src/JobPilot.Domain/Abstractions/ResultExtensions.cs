namespace JobPilot.Domain.Abstractions;

/// <summary>Combinadores genéricos para encadenar Results sin if/else en cascada (estilo "railway").</summary>
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map) =>
        result.IsSuccess ? map(result.Value) : Result.Failure<TOut>(result.Error);

    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind) =>
        result.IsSuccess ? bind(result.Value) : Result.Failure<TOut>(result.Error);

    public static TOut Match<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess() : onFailure(result.Error);

    public static Result<TValue> Ensure<TValue>(this Result<TValue> result, Func<TValue, bool> predicate, Error error) =>
        result.IsFailure || predicate(result.Value) ? result : error;
}
