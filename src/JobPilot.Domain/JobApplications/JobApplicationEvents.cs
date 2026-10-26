using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.JobApplications;

public sealed record JobApplicationCreatedDomainEvent(JobApplicationId JobApplicationId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record JobDescriptionChangedDomainEvent(JobApplicationId JobApplicationId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ApplicationStatusChangedDomainEvent(
    JobApplicationId JobApplicationId,
    string FromStatus,
    string ToStatus,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InterviewScheduledDomainEvent(
    JobApplicationId JobApplicationId,
    InterviewId InterviewId,
    DateTimeOffset ScheduledAt,
    DateTimeOffset OccurredOn) : IDomainEvent;
