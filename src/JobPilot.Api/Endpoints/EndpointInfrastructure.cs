using System.Reflection;
using JobPilot.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobPilot.Api.Endpoints;

/// <summary>Cada grupo de endpoints implementa esta interfaz y se descubre automáticamente.</summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var descriptors = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type));

        services.TryAddEnumerable(descriptors);
        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app, RouteGroupBuilder group)
    {
        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(group);
        }

        return app;
    }
}

/// <summary>Traduce un <see cref="Error"/> del dominio a una respuesta RFC 9457 (ProblemDetails).</summary>
public static class ApiResults
{
    public static IResult Problem(Error error) => error switch
    {
        ValidationError validation => Results.ValidationProblem(
            validation.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(group => group.Key, group => group.Select(e => e.Description).ToArray()),
            title: "One or more validation errors occurred.",
            extensions: new Dictionary<string, object?> { ["errorCode"] = validation.Code }),

        _ => Results.Problem(
            title: error.Code,
            detail: error.Description,
            statusCode: StatusCodeFor(error.Type),
            extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code })
    };

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };
}

/// <summary>Atajos genéricos para no repetir el Match en cada endpoint.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToOk<T>(this Result<T> result) => result.Match(value => Results.Ok(value), ApiResults.Problem);

    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.Match(value => Results.Created(location(value), value), ApiResults.Problem);
}
