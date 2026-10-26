using JobPilot.Application.Abstractions.AI;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Abstractions.Persistence;
using JobPilot.Domain.JobApplications;
using Microsoft.Extensions.Caching.Hybrid;

namespace JobPilot.Application.JobApplications;

// Estos handlers corren en segundo plano, desde el Outbox (consistencia eventual):
// crear una postulación responde rápido y el embedding se calcula después.

internal sealed class GenerateJobApplicationEmbeddingHandler(
    IJobApplicationRepository applications,
    IEmbeddingService embeddings,
    IUnitOfWork unitOfWork)
    : IDomainEventHandler<JobApplicationCreatedDomainEvent>,
      IDomainEventHandler<JobDescriptionChangedDomainEvent>
{
    public Task Handle(JobApplicationCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        GenerateAsync(domainEvent.JobApplicationId, cancellationToken);

    public Task Handle(JobDescriptionChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        GenerateAsync(domainEvent.JobApplicationId, cancellationToken);

    private async Task GenerateAsync(JobApplicationId id, CancellationToken cancellationToken)
    {
        if (!embeddings.IsEnabled)
        {
            return;
        }

        var application = await applications.GetByIdAsync(id, cancellationToken);
        if (application is null)
        {
            return;
        }

        var vector = await embeddings.GenerateAsync(application.ToEmbeddingText(), cancellationToken);
        if (vector.IsSuccess)
        {
            application.SetEmbedding(vector.Value);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

internal sealed class InvalidatePipelineStatsHandler(HybridCache cache)
    : IDomainEventHandler<JobApplicationCreatedDomainEvent>,
      IDomainEventHandler<ApplicationStatusChangedDomainEvent>
{
    public Task Handle(JobApplicationCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(GetPipelineStatsQueryHandler.CacheTag(domainEvent.OwnerId), cancellationToken).AsTask();

    public Task Handle(ApplicationStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(GetPipelineStatsQueryHandler.CacheTag(domainEvent.OwnerId), cancellationToken).AsTask();
}
