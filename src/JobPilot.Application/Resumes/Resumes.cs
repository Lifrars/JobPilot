using FluentValidation;
using JobPilot.Application.Abstractions.AI;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Abstractions.Persistence;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Resumes;

namespace JobPilot.Application.Resumes;

public sealed record ResumeResponse(Guid Id, string Name, int Characters, bool HasEmbedding, DateTimeOffset CreatedAt)
{
    public static ResumeResponse From(Resume resume) =>
        new(resume.Id.Value, resume.Name, resume.Content.Length, resume.Embedding is not null, resume.CreatedAt);
}

// ---------- Create ----------

public sealed record CreateResumeCommand(string Name, string Content) : ICommand<ResumeResponse>;

internal sealed class CreateResumeCommandValidator : AbstractValidator<CreateResumeCommand>
{
    public CreateResumeCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Resume.NameMaxLength);
        RuleFor(c => c.Content).NotEmpty().MaximumLength(50_000);
    }
}

internal sealed class CreateResumeCommandHandler(IRepository<Resume, ResumeId> resumes, TimeProvider timeProvider)
    : ICommandHandler<CreateResumeCommand, ResumeResponse>
{
    public Task<Result<ResumeResponse>> Handle(CreateResumeCommand command, CancellationToken cancellationToken)
    {
        var result = Resume.Create(command.Name, command.Content, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return Task.FromResult(Result.Failure<ResumeResponse>(result.Error));
        }

        resumes.Add(result.Value);
        return Task.FromResult(Result.Success(ResumeResponse.From(result.Value)));
    }
}

// ---------- List ----------

public sealed record ListResumesQuery : IQuery<IReadOnlyList<ResumeResponse>>;

internal sealed class ListResumesQueryHandler(IRepository<Resume, ResumeId> resumes)
    : IQueryHandler<ListResumesQuery, IReadOnlyList<ResumeResponse>>
{
    public async Task<Result<IReadOnlyList<ResumeResponse>>> Handle(ListResumesQuery query, CancellationToken cancellationToken)
    {
        var items = await resumes.ListAsync(new AllResumesSpec(), cancellationToken);
        return Result.Success<IReadOnlyList<ResumeResponse>>([.. items.Select(ResumeResponse.From)]);
    }
}

internal sealed class AllResumesSpec : Specification<Resume>
{
    public AllResumesSpec()
    {
        ApplyOrderByDescending(r => r.CreatedAt);
        AsReadOnly();
    }
}

// ---------- Event handlers ----------

internal sealed class GenerateResumeEmbeddingHandler(
    IRepository<Resume, ResumeId> resumes,
    IEmbeddingService embeddings,
    IUnitOfWork unitOfWork)
    : IDomainEventHandler<ResumeCreatedDomainEvent>
{
    public async Task Handle(ResumeCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (!embeddings.IsEnabled)
        {
            return;
        }

        var resume = await resumes.GetByIdAsync(domainEvent.ResumeId, cancellationToken);
        if (resume is null)
        {
            return;
        }

        var vector = await embeddings.GenerateAsync(resume.Content, cancellationToken);
        if (vector.IsSuccess)
        {
            resume.SetEmbedding(vector.Value);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
