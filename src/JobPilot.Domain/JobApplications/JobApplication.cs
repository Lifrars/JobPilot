using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Companies;

namespace JobPilot.Domain.JobApplications;

/// <summary>
/// Raíz de agregado: una postulación a un puesto. Todas sus reglas (transiciones de estado,
/// entrevistas, análisis de IA) se aplican aquí; nadie modifica sus colecciones desde fuera.
/// </summary>
public sealed class JobApplication : AggregateRoot<JobApplicationId>
{
    public const int TitleMaxLength = 200;

    private readonly List<StatusChange> _statusHistory = [];
    private readonly List<Interview> _interviews = [];

    private JobApplication(
        JobApplicationId id,
        CompanyId companyId,
        string title,
        string? postingUrl,
        string? jobDescription,
        SalaryRange? salary,
        WorkMode workMode,
        DateTimeOffset now) : base(id)
    {
        CompanyId = companyId;
        Title = title;
        PostingUrl = postingUrl;
        JobDescription = jobDescription;
        Salary = salary;
        WorkMode = workMode;
        Status = ApplicationStatus.Wishlist;
        CreatedAt = now;
        UpdatedAt = now;
    }

    private JobApplication() { }

    public CompanyId CompanyId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? PostingUrl { get; private set; }

    public string? JobDescription { get; private set; }

    public SalaryRange? Salary { get; private set; }

    public WorkMode WorkMode { get; private set; }

    public ApplicationStatus Status { get; private set; } = ApplicationStatus.Wishlist;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? AppliedAt { get; private set; }

    public JobPostingAnalysis? Analysis { get; private set; }

    /// <summary>Vector semántico de título + descripción (lo genera un handler asíncrono vía Outbox).</summary>
    public float[]? Embedding { get; private set; }

    public IReadOnlyCollection<StatusChange> StatusHistory => _statusHistory.AsReadOnly();

    public IReadOnlyCollection<Interview> Interviews => _interviews.AsReadOnly();

    public static Result<JobApplication> Create(
        CompanyId companyId,
        string title,
        string? postingUrl,
        string? jobDescription,
        SalaryRange? salary,
        WorkMode workMode,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return JobApplicationErrors.TitleRequired;
        }

        if (postingUrl is not null && !HttpUrl.IsValid(postingUrl))
        {
            return JobApplicationErrors.InvalidUrl;
        }

        var application = new JobApplication(
            JobApplicationId.New(), companyId, title.Trim(), postingUrl, jobDescription, salary, workMode, now);

        application.Raise(new JobApplicationCreatedDomainEvent(application.Id, now));

        return application;
    }

    /// <summary>Delegamos la decisión al estado actual (patrón State).</summary>
    public Result ChangeStatus(ApplicationStatus next, DateTimeOffset now)
    {
        if (next == Status)
        {
            return JobApplicationErrors.AlreadyInStatus(Status);
        }

        if (!Status.CanTransitionTo(next))
        {
            return JobApplicationErrors.InvalidTransition(Status, next);
        }

        var previous = Status;
        Status = next;
        UpdatedAt = now;
        _statusHistory.Add(new StatusChange(previous, next, now));

        if (next == ApplicationStatus.Applied)
        {
            AppliedAt ??= now;
        }

        Raise(new ApplicationStatusChangedDomainEvent(Id, previous.Name, next.Name, now));

        return Result.Success();
    }

    public Result<InterviewId> ScheduleInterview(InterviewType type, DateTimeOffset scheduledAt, string? notes, DateTimeOffset now)
    {
        if (!Status.AllowsInterviews)
        {
            return JobApplicationErrors.InterviewsNotAllowed(Status);
        }

        // Agendar una entrevista implica que el proceso avanzó.
        if (Status != ApplicationStatus.Interviewing)
        {
            var moved = ChangeStatus(ApplicationStatus.Interviewing, now);
            if (moved.IsFailure)
            {
                return moved.Error;
            }
        }

        // PostgreSQL (timestamptz) solo acepta offsets UTC.
        var interview = new Interview(InterviewId.New(), type, scheduledAt.ToUniversalTime(), notes);
        _interviews.Add(interview);
        UpdatedAt = now;

        Raise(new InterviewScheduledDomainEvent(Id, interview.Id, interview.ScheduledAt, now));

        return interview.Id;
    }

    public void UpdateJobDescription(string jobDescription, DateTimeOffset now)
    {
        if (string.Equals(JobDescription, jobDescription, StringComparison.Ordinal))
        {
            return;
        }

        JobDescription = jobDescription;
        UpdatedAt = now;
        Raise(new JobDescriptionChangedDomainEvent(Id, now));
    }

    public void AttachAnalysis(JobPostingAnalysis analysis, DateTimeOffset now)
    {
        Analysis = analysis;
        UpdatedAt = now;

        if (WorkMode == WorkMode.Unknown)
        {
            WorkMode = analysis.WorkMode;
        }
    }

    public void SetEmbedding(float[] embedding) => Embedding = embedding;

    /// <summary>Texto que representa semánticamente la oferta.</summary>
    public string ToEmbeddingText() => $"{Title}\n{JobDescription}";
}
