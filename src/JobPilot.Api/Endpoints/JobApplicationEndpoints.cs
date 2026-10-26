using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Abstractions.Paging;
using JobPilot.Application.JobApplications;
using JobPilot.Domain.JobApplications;

namespace JobPilot.Api.Endpoints;

public sealed record ChangeStatusRequest(string Status);

public sealed record ScheduleInterviewRequest(InterviewType Type, DateTimeOffset ScheduledAt, string? Notes);

internal sealed class JobApplicationEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("applications").WithTags("Job applications");

        group.MapPost("/", async (CreateJobApplicationCommand command, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(command, ct)).ToCreated(a => $"/api/v1/applications/{a.Id}"))
            .WithName("CreateJobApplication")
            .WithSummary("Registra una postulación (empieza en Wishlist)")
            .Produces<JobApplicationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", async (string? status, Guid? companyId, string? search, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                (await sender.QueryAsync(new ListJobApplicationsQuery(status, companyId, search, page ?? 1, pageSize ?? 20), ct)).ToOk())
            .WithName("ListJobApplications")
            .WithSummary("Lista postulaciones con filtros y paginación")
            .Produces<PagedList<JobApplicationSummary>>();

        group.MapGet("/stats", async (ISender sender, CancellationToken ct) =>
                (await sender.QueryAsync(new GetPipelineStatsQuery(), ct)).ToOk())
            .WithName("GetPipelineStats")
            .WithSummary("Métricas del pipeline (cacheadas con HybridCache)")
            .Produces<PipelineStatsResponse>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.QueryAsync(new GetJobApplicationQuery(id), ct)).ToOk())
            .WithName("GetJobApplication")
            .Produces<JobApplicationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/status", async (Guid id, ChangeStatusRequest request, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new ChangeJobApplicationStatusCommand(id, request.Status), ct)).ToOk())
            .WithName("ChangeJobApplicationStatus")
            .WithSummary("Mueve la postulación a otro estado (State pattern valida la transición)")
            .Produces<JobApplicationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/interviews", async (Guid id, ScheduleInterviewRequest request, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new ScheduleInterviewCommand(id, request.Type, request.ScheduledAt, request.Notes), ct))
                .ToCreated(interviewId => $"/api/v1/applications/{id}"))
            .WithName("ScheduleInterview")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
