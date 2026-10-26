using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.JobApplications;
using JobPilot.Application.JobApplications.Matching;
using JobPilot.Domain.JobApplications;

namespace JobPilot.Api.Endpoints;

public sealed record AnalyzePostingRequest(string? JobDescription);

public sealed record CoverLetterRequest(Guid ResumeId, string? Language);

internal sealed class AiEndpoints : IEndpoint
{
    public const string RateLimitPolicy = "ai";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Las llamadas al LLM cuestan dinero: limitamos la tasa por ventana de tiempo.
        var group = app.MapGroup("applications")
            .WithTags("AI")
            .RequireRateLimiting(RateLimitPolicy)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/{id:guid}/analyze", async (Guid id, AnalyzePostingRequest request, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new AnalyzeJobPostingCommand(id, request.JobDescription), ct)).ToOk())
            .WithName("AnalyzeJobPosting")
            .WithSummary("Extrae skills, seniority, salario y red flags de la oferta con un LLM")
            .Produces<JobPostingAnalysis>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/match", async (Guid id, Guid resumeId, string? strategy, ISender sender, CancellationToken ct) =>
                (await sender.QueryAsync(new GetMatchScoreQuery(id, resumeId, strategy ?? "hybrid"), ct)).ToOk())
            .WithName("GetMatchScore")
            .WithSummary("Puntaje de encaje CV vs oferta (strategy: keyword | embedding | hybrid)")
            .Produces<MatchScore>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/search", async (string q, int? take, ISender sender, CancellationToken ct) =>
                (await sender.QueryAsync(new SearchJobApplicationsQuery(q, take ?? 5), ct)).ToOk())
            .WithName("SemanticSearch")
            .WithSummary("Búsqueda semántica sobre tus ofertas (embeddings + pgvector)")
            .Produces<IReadOnlyList<SearchHitResponse>>();

        group.MapPost("/{id:guid}/cover-letter", async (Guid id, CoverLetterRequest request, ISender sender, CancellationToken ct) =>
                (await sender.QueryAsync(new GenerateCoverLetterQuery(id, request.ResumeId, request.Language ?? "es"), ct)).ToOk())
            .WithName("GenerateCoverLetter")
            .WithSummary("Genera un borrador de carta de presentación con tu CV")
            .Produces<CoverLetterResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
