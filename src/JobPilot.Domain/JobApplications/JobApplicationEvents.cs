using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Users;

namespace JobPilot.Domain.JobApplications;

// Los eventos que afectan datos agregados por usuario (estadísticas) llevan el OwnerId:
// los handlers corren en segundo plano, sin usuario autenticado, y necesitan saber a quién afectan.

public sealed record JobApplicationCreatedDomainEvent(
    JobApplicationId JobApplicationId,
    UserId OwnerId,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record JobDescriptionChangedDomainEvent(JobApplicationId JobApplicationId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ApplicationStatusChangedDomainEvent(
    JobApplicationId JobApplicationId,
    UserId OwnerId,
    string FromStatus,
    string ToStatus,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InterviewScheduledDomainEvent(
    JobApplicationId JobApplicationId,
    InterviewId InterviewId,
    DateTimeOffset ScheduledAt,
    DateTimeOffset OccurredOn) : IDomainEvent;
