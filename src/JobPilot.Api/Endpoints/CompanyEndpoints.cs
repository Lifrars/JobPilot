using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Abstractions.Paging;
using JobPilot.Application.Companies;

namespace JobPilot.Api.Endpoints;

internal sealed class CompanyEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("companies").WithTags("Companies");

        group.MapPost("/", async (CreateCompanyCommand command, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(command, ct)).ToCreated(c => $"/api/v1/companies/{c.Id}"))
            .WithName("CreateCompany")
            .WithSummary("Registra una empresa")
            .Produces<CompanyResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (string? search, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                (await sender.QueryAsync(new ListCompaniesQuery(search, page ?? 1, pageSize ?? 20), ct)).ToOk())
            .WithName("ListCompanies")
            .WithSummary("Lista empresas (paginado)")
            .Produces<PagedList<CompanyResponse>>();
    }
}
