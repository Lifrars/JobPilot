using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using JobPilot.Api.Endpoints;
using JobPilot.Api.Mcp;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace JobPilot.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddEndpoints(typeof(DependencyInjection).Assembly);

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            // Por defecto ASP.NET acepta "42" como número; Strict hace que el contrato OpenAPI diga
            // solo "number" y el cliente TypeScript generado no tenga tipos "number | string".
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Una ventana por usuario: si fuera global, un solo usuario agotaría la cuota de todos.
            options.AddPolicy(AiEndpoints.RateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? httpContext.Connection.RemoteIpAddress?.ToString()
                              ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<JobPilotTools>();

        return services;
    }
}

/// <summary>
/// Declara el esquema "Bearer" en el documento OpenAPI: Scalar muestra el campo para pegar el token
/// obtenido en POST /api/v1/auth/login y lo envía en cada request.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "Access token de POST /api/v1/auth/login"
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
        });

        return Task.CompletedTask;
    }
}

/// <summary>Última red de seguridad: cualquier excepción no controlada se convierte en un 500 ProblemDetails sin filtrar detalles.</summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandled(logger, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "Server.Error",
                Detail = "An unexpected error occurred."
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
