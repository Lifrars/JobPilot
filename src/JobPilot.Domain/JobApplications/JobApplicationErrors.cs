using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.JobApplications;

public static class JobApplicationErrors
{
    public static readonly Error TitleRequired =
        Error.Validation("JobApplication.TitleRequired", "The job title is required.");

    public static readonly Error InvalidUrl =
        Error.Validation("JobApplication.InvalidUrl", "The posting URL must be an absolute URL.");

    public static readonly Error InvalidSalaryRange =
        Error.Validation("JobApplication.InvalidSalaryRange", "The salary range must be positive and Min <= Max.");

    public static readonly Error InvalidCurrency =
        Error.Validation("JobApplication.InvalidCurrency", "The currency must be a 3-letter ISO code (e.g. USD).");

    public static readonly Error DescriptionRequired =
        Error.Validation("JobApplication.DescriptionRequired", "A job description is required for this operation.");

    public static Error NotFound(JobApplicationId id) =>
        Error.NotFound("JobApplication.NotFound", $"The job application '{id}' was not found.");

    public static Error UnknownStatus(string status) =>
        Error.Validation("JobApplication.UnknownStatus", $"'{status}' is not a valid status.");

    public static Error AlreadyInStatus(ApplicationStatus status) =>
        Error.Conflict("JobApplication.AlreadyInStatus", $"The application is already in status '{status}'.");

    public static Error InvalidTransition(ApplicationStatus from, ApplicationStatus to) =>
        Error.Validation(
            "JobApplication.InvalidTransition",
            $"Can not move from '{from}' to '{to}'. Allowed: [{string.Join(", ", from.AllowedTransitions)}].");

    public static Error InterviewsNotAllowed(ApplicationStatus status) =>
        Error.Validation("JobApplication.InterviewsNotAllowed", $"Interviews can not be scheduled while in status '{status}'.");
}
