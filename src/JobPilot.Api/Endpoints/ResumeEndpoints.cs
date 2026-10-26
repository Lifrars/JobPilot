using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Resumes;

namespace JobPilot.Api.Endpoints;

internal sealed class ResumeEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("resumes").WithTags("Resumes");

        group.MapPost("/", async (CreateResumeCommand command, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(command, ct)).ToCreated(r => $"/api/v1/resumes/{r.Id}"))
            .WithName("CreateResume")
            .WithSummary("Guarda una versión de tu CV (texto plano o Markdown)")
            .Produces<ResumeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.QueryAsync(new ListResumesQuery(), ct)).ToOk())
            .WithName("ListResumes")
            .Produces<IReadOnlyList<ResumeResponse>>();
    }
}
