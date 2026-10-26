using FluentValidation;
using JobPilot.Application.Abstractions.Authentication;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Abstractions.Paging;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Users;
using Microsoft.Extensions.Caching.Hybrid;

namespace JobPilot.Application.JobApplications;

// ---------- Get by id ----------

public sealed record GetJobApplicationQuery(Guid JobApplicationId) : IQuery<JobApplicationResponse>;

internal sealed class GetJobApplicationQueryHandler(IJobApplicationRepository applications)
    : IQueryHandler<GetJobApplicationQuery, JobApplicationResponse>
{
    public async Task<Result<JobApplicationResponse>> Handle(GetJobApplicationQuery query, CancellationToken cancellationToken)
    {
        var id = JobApplicationId.From(query.JobApplicationId);
        var application = await applications.GetByIdAsync(id, cancellationToken);

        return application is null ? JobApplicationErrors.NotFound(id) : application.ToResponse();
    }
}

// ---------- List (paged + filters) ----------

public sealed record ListJobApplicationsQuery(
    string? Status,
    Guid? CompanyId,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedList<JobApplicationSummary>>;

internal sealed class ListJobApplicationsQueryValidator : AbstractValidator<ListJobApplicationsQuery>
{
    public ListJobApplicationsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
        RuleFor(q => q.Status)
            .Must(status => ApplicationStatus.FromName(status!) is not null)
            .When(q => !string.IsNullOrWhiteSpace(q.Status))
            .WithMessage(q => $"'{q.Status}' is not a valid status.");
    }
}

internal sealed class ListJobApplicationsQueryHandler(IJobApplicationRepository applications)
    : IQueryHandler<ListJobApplicationsQuery, PagedList<JobApplicationSummary>>
{
    public async Task<Result<PagedList<JobApplicationSummary>>> Handle(ListJobApplicationsQuery query, CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(query.Status) ? null : ApplicationStatus.FromName(query.Status);
        var companyId = query.CompanyId is { } id ? CompanyId.From(id) : (CompanyId?)null;

        var total = await applications.CountAsync(
            new JobApplicationsFilterSpec(status, companyId, query.Search), cancellationToken);

        var items = await applications.ListAsync(
            new JobApplicationsFilterSpec(status, companyId, query.Search, query.Page, query.PageSize), cancellationToken);

        return new PagedList<JobApplicationSummary>([.. items.Select(a => a.ToSummary())], query.Page, query.PageSize, total);
    }
}

// ---------- Pipeline stats (dashboard, cacheado con HybridCache) ----------

public sealed record PipelineStatsResponse(
    int Total,
    int Active,
    double ResponseRate,
    IReadOnlyDictionary<string, int> ByStatus);

public sealed record GetPipelineStatsQuery : IQuery<PipelineStatsResponse>;

internal sealed class GetPipelineStatsQueryHandler(IJobApplicationRepository applications, HybridCache cache, IUserContext userContext)
    : IQueryHandler<GetPipelineStatsQuery, PipelineStatsResponse>
{
    // La clave y el tag incluyen el usuario: una clave global le mostraría a todos las métricas del primero que consulte.
    public static string CacheKey(UserId ownerId) => $"pipeline-stats:{ownerId}";

    public static string CacheTag(UserId ownerId) => $"pipeline-stats:{ownerId}";

    public async Task<Result<PipelineStatsResponse>> Handle(GetPipelineStatsQuery query, CancellationToken cancellationToken)
    {
        // HybridCache: caché L1 (memoria) + L2 opcional (Redis), con protección contra "stampede".
        return await cache.GetOrCreateAsync(
            CacheKey(userContext.UserId),
            async ct => Calculate(await applications.CountByStatusAsync(ct)),
            tags: [CacheTag(userContext.UserId)],
            cancellationToken: cancellationToken);
    }

    internal static PipelineStatsResponse Calculate(IReadOnlyDictionary<ApplicationStatus, int> counts)
    {
        int Count(ApplicationStatus status) => counts.GetValueOrDefault(status);

        var total = counts.Values.Sum();
        var active = counts.Where(c => !c.Key.IsTerminal && c.Key != ApplicationStatus.Wishlist).Sum(c => c.Value);

        // Tasa de respuesta: de lo que enviaste, ¿cuánto avanzó más allá de "Applied"?
        var sent = total - Count(ApplicationStatus.Wishlist) - Count(ApplicationStatus.Withdrawn);
        var responded = Count(ApplicationStatus.Screening) + Count(ApplicationStatus.Interviewing) +
                        Count(ApplicationStatus.Offer) + Count(ApplicationStatus.Accepted) + Count(ApplicationStatus.Rejected);
        var responseRate = sent == 0 ? 0 : Math.Round(responded * 100.0 / sent, 1);

        var byStatus = ApplicationStatus.List
            .OrderBy(s => s.Id)
            .ToDictionary(s => s.Name, Count);

        return new PipelineStatsResponse(total, active, responseRate, byStatus);
    }
}
