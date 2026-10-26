using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.JobApplications;

public readonly record struct JobApplicationId(Guid Value) : IStronglyTypedId<JobApplicationId, Guid>
{
    public static JobApplicationId New() => new(Guid.CreateVersion7());

    public static JobApplicationId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public readonly record struct InterviewId(Guid Value) : IStronglyTypedId<InterviewId, Guid>
{
    public static InterviewId New() => new(Guid.CreateVersion7());

    public static InterviewId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public enum WorkMode
{
    Unknown,
    Remote,
    Hybrid,
    OnSite
}

public enum InterviewType
{
    Hr,
    Technical,
    LiveCoding,
    SystemDesign,
    Manager,
    Final
}

/// <summary>Value object: se compara por valor y valida sus invariantes al crearse.</summary>
public sealed record SalaryRange
{
    private SalaryRange(decimal min, decimal max, string currency)
    {
        Min = min;
        Max = max;
        Currency = currency;
    }

    public decimal Min { get; private init; }

    public decimal Max { get; private init; }

    public string Currency { get; private init; }

    public static Result<SalaryRange> Create(decimal min, decimal max, string currency)
    {
        if (min < 0 || max < min)
        {
            return JobApplicationErrors.InvalidSalaryRange;
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            return JobApplicationErrors.InvalidCurrency;
        }

        return new SalaryRange(min, max, currency.Trim().ToUpperInvariant());
    }
}

/// <summary>Historial inmutable de cambios de estado.</summary>
public sealed record StatusChange(ApplicationStatus From, ApplicationStatus To, DateTimeOffset ChangedAt);

/// <summary>Resultado del análisis de la oferta hecho por el LLM (se guarda como JSON).</summary>
public sealed record JobPostingAnalysis(
    IReadOnlyList<string> RequiredSkills,
    IReadOnlyList<string> NiceToHaveSkills,
    string Seniority,
    WorkMode WorkMode,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Currency,
    IReadOnlyList<string> RedFlags,
    string Summary,
    DateTimeOffset AnalyzedAt);
