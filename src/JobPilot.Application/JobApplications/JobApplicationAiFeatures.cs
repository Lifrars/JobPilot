using FluentValidation;
using JobPilot.Application.Abstractions.AI;
using JobPilot.Application.Abstractions.Authentication;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.JobApplications.Matching;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Resumes;

namespace JobPilot.Application.JobApplications;

// ---------- Analyze posting (LLM con salida estructurada) ----------

public sealed record AnalyzeJobPostingCommand(Guid JobApplicationId, string? JobDescription) : ICommand<JobPostingAnalysis>;

internal sealed class AnalyzeJobPostingCommandValidator : AbstractValidator<AnalyzeJobPostingCommand>
{
    public AnalyzeJobPostingCommandValidator()
    {
        RuleFor(c => c.JobApplicationId).NotEmpty();
        RuleFor(c => c.JobDescription).MaximumLength(30_000);
    }
}

internal sealed class AnalyzeJobPostingCommandHandler(
    IJobApplicationRepository applications,
    IJobPostingAnalyzer analyzer,
    TimeProvider timeProvider)
    : ICommandHandler<AnalyzeJobPostingCommand, JobPostingAnalysis>
{
    public async Task<Result<JobPostingAnalysis>> Handle(AnalyzeJobPostingCommand command, CancellationToken cancellationToken)
    {
        var id = JobApplicationId.From(command.JobApplicationId);
        var application = await applications.GetByIdAsync(id, cancellationToken);
        if (application is null)
        {
            return JobApplicationErrors.NotFound(id);
        }

        var description = command.JobDescription ?? application.JobDescription;
        if (string.IsNullOrWhiteSpace(description))
        {
            return JobApplicationErrors.DescriptionRequired;
        }

        var analysis = await analyzer.AnalyzeAsync(description, cancellationToken);
        if (analysis.IsFailure)
        {
            return analysis.Error;
        }

        var now = timeProvider.GetUtcNow();
        application.UpdateJobDescription(description, now);
        application.AttachAnalysis(analysis.Value, now);

        return analysis.Value;
    }
}

// ---------- Semantic search (embeddings + pgvector) ----------

public sealed record SearchHitResponse(Guid Id, string Title, string Status, double Similarity);

public sealed record SearchJobApplicationsQuery(string Text, int Take = 5) : IQuery<IReadOnlyList<SearchHitResponse>>;

internal sealed class SearchJobApplicationsQueryValidator : AbstractValidator<SearchJobApplicationsQuery>
{
    public SearchJobApplicationsQueryValidator()
    {
        RuleFor(q => q.Text).NotEmpty().MaximumLength(2_000);
        RuleFor(q => q.Take).InclusiveBetween(1, 50);
    }
}

internal sealed class SearchJobApplicationsQueryHandler(
    IJobApplicationRepository applications,
    IEmbeddingService embeddings,
    IUserContext userContext)
    : IQueryHandler<SearchJobApplicationsQuery, IReadOnlyList<SearchHitResponse>>
{
    public async Task<Result<IReadOnlyList<SearchHitResponse>>> Handle(SearchJobApplicationsQuery query, CancellationToken cancellationToken)
    {
        var vector = await embeddings.GenerateAsync(query.Text, cancellationToken);
        if (vector.IsFailure)
        {
            return vector.Error;
        }

        var hits = await applications.SearchSimilarAsync(userContext.UserId, vector.Value, query.Take, cancellationToken);

        return Result.Success<IReadOnlyList<SearchHitResponse>>(
            [.. hits.Select(hit => new SearchHitResponse(hit.Item.Id.Value, hit.Item.Title, hit.Item.Status.Name, Math.Round(hit.Score, 4)))]);
    }
}

// ---------- Match score CV vs oferta (Strategy) ----------

public sealed record GetMatchScoreQuery(Guid JobApplicationId, Guid ResumeId, string Strategy = "hybrid") : IQuery<MatchScore>;

internal sealed class GetMatchScoreQueryHandler(
    IJobApplicationRepository applications,
    IRepository<Resume, ResumeId> resumes,
    IEnumerable<IMatchScoringStrategy> strategies)
    : IQueryHandler<GetMatchScoreQuery, MatchScore>
{
    public async Task<Result<MatchScore>> Handle(GetMatchScoreQuery query, CancellationToken cancellationToken)
    {
        var strategy = strategies.FirstOrDefault(s => string.Equals(s.Name, query.Strategy, StringComparison.OrdinalIgnoreCase));
        if (strategy is null)
        {
            return MatchErrors.UnknownStrategy(query.Strategy);
        }

        var applicationId = JobApplicationId.From(query.JobApplicationId);
        var application = await applications.GetByIdAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return JobApplicationErrors.NotFound(applicationId);
        }

        var resumeId = ResumeId.From(query.ResumeId);
        var resume = await resumes.GetByIdAsync(resumeId, cancellationToken);
        if (resume is null)
        {
            return ResumeErrors.NotFound(resumeId);
        }

        return strategy.Score(application, resume);
    }
}

// ---------- Cover letter ----------

public sealed record CoverLetterResponse(string Content);

public sealed record GenerateCoverLetterQuery(Guid JobApplicationId, Guid ResumeId, string Language = "es") : IQuery<CoverLetterResponse>;

internal sealed class GenerateCoverLetterQueryValidator : AbstractValidator<GenerateCoverLetterQuery>
{
    public GenerateCoverLetterQueryValidator()
    {
        RuleFor(q => q.JobApplicationId).NotEmpty();
        RuleFor(q => q.ResumeId).NotEmpty();
        RuleFor(q => q.Language).NotEmpty().MaximumLength(20);
    }
}

internal sealed class GenerateCoverLetterQueryHandler(
    IJobApplicationRepository applications,
    IRepository<Resume, ResumeId> resumes,
    IRepository<Company, CompanyId> companies,
    ICoverLetterWriter writer)
    : IQueryHandler<GenerateCoverLetterQuery, CoverLetterResponse>
{
    public async Task<Result<CoverLetterResponse>> Handle(GenerateCoverLetterQuery query, CancellationToken cancellationToken)
    {
        var applicationId = JobApplicationId.From(query.JobApplicationId);
        var application = await applications.GetByIdAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return JobApplicationErrors.NotFound(applicationId);
        }

        var resumeId = ResumeId.From(query.ResumeId);
        var resume = await resumes.GetByIdAsync(resumeId, cancellationToken);
        if (resume is null)
        {
            return ResumeErrors.NotFound(resumeId);
        }

        var company = await companies.GetByIdAsync(application.CompanyId, cancellationToken);
        var letter = await writer.WriteAsync(application, company?.Name ?? "the company", resume, query.Language, cancellationToken);

        return letter.Map(content => new CoverLetterResponse(content));
    }
}
