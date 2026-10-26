using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.JobApplications;

/// <summary>Entidad interna del agregado JobApplication: solo se crea y modifica a través de su raíz.</summary>
public sealed class Interview : Entity<InterviewId>
{
    internal Interview(InterviewId id, InterviewType type, DateTimeOffset scheduledAt, string? notes) : base(id)
    {
        Type = type;
        ScheduledAt = scheduledAt;
        Notes = notes;
    }

    private Interview() { }

    public InterviewType Type { get; private set; }

    public DateTimeOffset ScheduledAt { get; private set; }

    public string? Notes { get; private set; }
}
