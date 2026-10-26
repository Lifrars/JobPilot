using FluentValidation;
using JobPilot.Application.Abstractions.Authentication;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Abstractions.Paging;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Companies;

namespace JobPilot.Application.Companies;

public sealed record CompanyResponse(Guid Id, string Name, string? Website, string? Industry, DateTimeOffset CreatedAt)
{
    public static CompanyResponse From(Company company) =>
        new(company.Id.Value, company.Name, company.Website, company.Industry, company.CreatedAt);
}

// ---------- Create ----------

public sealed record CreateCompanyCommand(string Name, string? Website, string? Industry) : ICommand<CompanyResponse>;

internal sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Company.NameMaxLength);
        RuleFor(c => c.Website).Must(HttpUrl.IsValid).When(c => c.Website is not null).WithMessage("Website must be an absolute http(s) URL.");
    }
}

internal sealed class CreateCompanyCommandHandler(
    IRepository<Company, CompanyId> companies,
    IUserContext userContext,
    TimeProvider timeProvider)
    : ICommandHandler<CreateCompanyCommand, CompanyResponse>
{
    public async Task<Result<CompanyResponse>> Handle(CreateCompanyCommand command, CancellationToken cancellationToken)
    {
        if (await companies.AnyAsync(new CompanyByNameSpec(command.Name), cancellationToken))
        {
            return CompanyErrors.NameAlreadyExists(command.Name);
        }

        var result = Company.Create(userContext.UserId, command.Name, command.Website, command.Industry, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error;
        }

        companies.Add(result.Value);
        return CompanyResponse.From(result.Value);
    }
}

// ---------- List ----------

public sealed record ListCompaniesQuery(string? Search, int Page = 1, int PageSize = 20) : IQuery<PagedList<CompanyResponse>>;

internal sealed class ListCompaniesQueryValidator : AbstractValidator<ListCompaniesQuery>
{
    public ListCompaniesQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}

internal sealed class ListCompaniesQueryHandler(IRepository<Company, CompanyId> companies)
    : IQueryHandler<ListCompaniesQuery, PagedList<CompanyResponse>>
{
    public async Task<Result<PagedList<CompanyResponse>>> Handle(ListCompaniesQuery query, CancellationToken cancellationToken)
    {
        var total = await companies.CountAsync(new CompaniesSpec(query.Search), cancellationToken);
        var items = await companies.ListAsync(new CompaniesSpec(query.Search, query.Page, query.PageSize), cancellationToken);

        return new PagedList<CompanyResponse>([.. items.Select(CompanyResponse.From)], query.Page, query.PageSize, total);
    }
}

// ---------- Specifications ----------

internal sealed class CompanyByNameSpec : Specification<Company>
{
    public CompanyByNameSpec(string name)
    {
        var normalized = name.Trim().ToLower();
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() se traduce a SQL lower(); no aplica cultura en el cliente.
        Where(c => c.Name.ToLower() == normalized);
#pragma warning restore CA1304, CA1311, CA1862
        AsReadOnly();
    }
}

internal sealed class CompaniesSpec : Specification<Company>
{
    public CompaniesSpec(string? search, int? page = null, int pageSize = 20)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            Where(c => c.Name.Contains(search));
        }

        ApplyOrderBy(c => c.Name);
        AsReadOnly();

        if (page is not null)
        {
            ApplyPaging(page.Value, pageSize);
        }
    }
}
