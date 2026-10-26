using JobPilot.Domain.JobApplications;

namespace JobPilot.Application.JobApplications;

// DTOs de salida. El mapeo es manual (sin AutoMapper): explícito, rápido y fácil de depurar.

public sealed record SalaryResponse(decimal Min, decimal Max, string Currency);

public sealed record InterviewResponse(Guid Id, string Type, DateTimeOffset ScheduledAt, string? Notes);

public sealed record StatusChangeResponse(string From, string To, DateTimeOffset ChangedAt);

public sealed record JobApplicationResponse(
    Guid Id,
    Guid CompanyId,
    string Title,
    string? PostingUrl,
    string? JobDescription,
    string Status,
    IReadOnlyList<string> AllowedTransitions,
    WorkMode WorkMode,
    SalaryResponse? Salary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? AppliedAt,
    bool HasEmbedding,
    JobPostingAnalysis? Analysis,
    IReadOnlyList<InterviewResponse> Interviews,
    IReadOnlyList<StatusChangeResponse> StatusHistory);

public sealed record JobApplicationSummary(
    Guid Id,
    Guid CompanyId,
    string Title,
    string Status,
    WorkMode WorkMode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal static class JobApplicationMappings
{
    public static JobApplicationResponse ToResponse(this JobApplication application) => new(
        application.Id.Value,
        application.CompanyId.Value,
        application.Title,
        application.PostingUrl,
        application.JobDescription,
        application.Status.Name,
        [.. application.Status.AllowedTransitions.Select(s => s.Name)],
        application.WorkMode,
        application.Salary is { } salary ? new SalaryResponse(salary.Min, salary.Max, salary.Currency) : null,
        application.CreatedAt,
        application.UpdatedAt,
        application.AppliedAt,
        application.Embedding is not null,
        application.Analysis,
        [.. application.Interviews
            .OrderBy(i => i.ScheduledAt)
            .Select(i => new InterviewResponse(i.Id.Value, i.Type.ToString(), i.ScheduledAt, i.Notes))],
        [.. application.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new StatusChangeResponse(h.From.Name, h.To.Name, h.ChangedAt))]);

    public static JobApplicationSummary ToSummary(this JobApplication application) => new(
        application.Id.Value,
        application.CompanyId.Value,
        application.Title,
        application.Status.Name,
        application.WorkMode,
        application.CreatedAt,
        application.UpdatedAt);
}
