using FluentValidation;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;

namespace JobPilot.Application.JobApplications;

// ---------- Create ----------

public sealed record CreateJobApplicationCommand(
    Guid CompanyId,
    string Title,
    string? PostingUrl,
    string? JobDescription,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Currency,
    WorkMode WorkMode = WorkMode.Unknown) : ICommand<JobApplicationResponse>;

internal sealed class CreateJobApplicationCommandValidator : AbstractValidator<CreateJobApplicationCommand>
{
    public CreateJobApplicationCommandValidator()
    {
        RuleFor(c => c.CompanyId).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(JobApplication.TitleMaxLength);
        RuleFor(c => c.WorkMode).IsInEnum();
        RuleFor(c => c.SalaryMax).GreaterThanOrEqualTo(c => c.SalaryMin).When(c => c.SalaryMin is not null && c.SalaryMax is not null);
        RuleFor(c => c.Currency).NotEmpty().Length(3).When(c => c.SalaryMin is not null || c.SalaryMax is not null);
    }
}

internal sealed class CreateJobApplicationCommandHandler(
    IJobApplicationRepository applications,
    IRepository<Company, CompanyId> companies,
    TimeProvider timeProvider)
    : ICommandHandler<CreateJobApplicationCommand, JobApplicationResponse>
{
    public async Task<Result<JobApplicationResponse>> Handle(CreateJobApplicationCommand command, CancellationToken cancellationToken)
    {
        var companyId = CompanyId.From(command.CompanyId);
        if (await companies.GetByIdAsync(companyId, cancellationToken) is null)
        {
            return CompanyErrors.NotFound(companyId);
        }

        SalaryRange? salary = null;
        if (command.SalaryMin is { } min && command.SalaryMax is { } max)
        {
            var salaryResult = SalaryRange.Create(min, max, command.Currency ?? string.Empty);
            if (salaryResult.IsFailure)
            {
                return salaryResult.Error;
            }

            salary = salaryResult.Value;
        }

        var result = JobApplication.Create(
            companyId,
            command.Title,
            command.PostingUrl,
            command.JobDescription,
            salary,
            command.WorkMode,
            timeProvider.GetUtcNow());

        if (result.IsFailure)
        {
            return result.Error;
        }

        applications.Add(result.Value);
        return result.Value.ToResponse();
    }
}

// ---------- Change status ----------

public sealed record ChangeJobApplicationStatusCommand(Guid JobApplicationId, string Status) : ICommand<JobApplicationResponse>;

internal sealed class ChangeJobApplicationStatusCommandValidator : AbstractValidator<ChangeJobApplicationStatusCommand>
{
    public ChangeJobApplicationStatusCommandValidator()
    {
        RuleFor(c => c.JobApplicationId).NotEmpty();
        RuleFor(c => c.Status)
            .NotEmpty()
            .Must(status => ApplicationStatus.FromName(status) is not null)
            .WithMessage(c => $"'{c.Status}' is not a valid status. Valid: {string.Join(", ", ApplicationStatus.List)}.");
    }
}

internal sealed class ChangeJobApplicationStatusCommandHandler(
    IJobApplicationRepository applications,
    TimeProvider timeProvider)
    : ICommandHandler<ChangeJobApplicationStatusCommand, JobApplicationResponse>
{
    public async Task<Result<JobApplicationResponse>> Handle(ChangeJobApplicationStatusCommand command, CancellationToken cancellationToken)
    {
        var id = JobApplicationId.From(command.JobApplicationId);
        var application = await applications.GetByIdAsync(id, cancellationToken);
        if (application is null)
        {
            return JobApplicationErrors.NotFound(id);
        }

        var status = ApplicationStatus.FromName(command.Status);
        if (status is null)
        {
            return JobApplicationErrors.UnknownStatus(command.Status);
        }

        var result = application.ChangeStatus(status, timeProvider.GetUtcNow());

        return result.IsSuccess ? application.ToResponse() : result.Error;
    }
}

// ---------- Schedule interview ----------

public sealed record ScheduleInterviewCommand(
    Guid JobApplicationId,
    InterviewType Type,
    DateTimeOffset ScheduledAt,
    string? Notes) : ICommand<Guid>;

internal sealed class ScheduleInterviewCommandValidator : AbstractValidator<ScheduleInterviewCommand>
{
    public ScheduleInterviewCommandValidator()
    {
        RuleFor(c => c.JobApplicationId).NotEmpty();
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.ScheduledAt).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

internal sealed class ScheduleInterviewCommandHandler(
    IJobApplicationRepository applications,
    TimeProvider timeProvider)
    : ICommandHandler<ScheduleInterviewCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ScheduleInterviewCommand command, CancellationToken cancellationToken)
    {
        var id = JobApplicationId.From(command.JobApplicationId);
        var application = await applications.GetByIdAsync(id, cancellationToken);
        if (application is null)
        {
            return JobApplicationErrors.NotFound(id);
        }

        return application
            .ScheduleInterview(command.Type, command.ScheduledAt, command.Notes, timeProvider.GetUtcNow())
            .Map(interviewId => interviewId.Value);
    }
}
