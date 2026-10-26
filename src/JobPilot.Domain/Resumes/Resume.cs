using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.Resumes;

public readonly record struct ResumeId(Guid Value) : IStronglyTypedId<ResumeId, Guid>
{
    public static ResumeId New() => new(Guid.CreateVersion7());

    public static ResumeId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public sealed record ResumeCreatedDomainEvent(ResumeId ResumeId, DateTimeOffset OccurredOn) : IDomainEvent;

/// <summary>Una versión de tu CV (por ejemplo "Backend .NET – ES" o "AI Engineer – EN").</summary>
public sealed class Resume : AggregateRoot<ResumeId>
{
    public const int NameMaxLength = 120;

    private Resume(ResumeId id, string name, string content, DateTimeOffset createdAt) : base(id)
    {
        Name = name;
        Content = content;
        CreatedAt = createdAt;
    }

    private Resume() { }

    public string Name { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public float[]? Embedding { get; private set; }

    public static Result<Resume> Create(string name, string content, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ResumeErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return ResumeErrors.ContentRequired;
        }

        var resume = new Resume(ResumeId.New(), name.Trim(), content, now);
        resume.Raise(new ResumeCreatedDomainEvent(resume.Id, now));
        return resume;
    }

    public void SetEmbedding(float[] embedding) => Embedding = embedding;
}

public static class ResumeErrors
{
    public static readonly Error NameRequired = Error.Validation("Resume.NameRequired", "The resume name is required.");

    public static readonly Error ContentRequired = Error.Validation("Resume.ContentRequired", "The resume content is required.");

    public static Error NotFound(ResumeId id) => Error.NotFound("Resume.NotFound", $"The resume '{id}' was not found.");
}
