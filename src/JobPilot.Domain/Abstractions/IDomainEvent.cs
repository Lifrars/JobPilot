namespace JobPilot.Domain.Abstractions;

/// <summary>Algo relevante que ya ocurrió en el dominio (se nombra en pasado).</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}
