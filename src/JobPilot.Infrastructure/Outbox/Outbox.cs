using System.Text.Json;
using JobPilot.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPilot.Infrastructure.Outbox;

/// <summary>
/// Outbox pattern: el evento se guarda en la MISMA transacción que el cambio de negocio.
/// Así nunca queda un cambio guardado con su evento perdido (o un evento sin cambio).
/// Un proceso en segundo plano lo publica después.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    public DateTimeOffset OccurredOn { get; private set; }

    public DateTimeOffset? ProcessedOn { get; private set; }

    public int Attempts { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage From(IDomainEvent domainEvent) => new()
    {
        Id = Guid.CreateVersion7(),
        Type = domainEvent.GetType().FullName!,
        Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
        OccurredOn = domainEvent.OccurredOn
    };

    public IDomainEvent ToDomainEvent()
    {
        var type = Domain.AssemblyReference.Assembly.GetType(Type)
                   ?? throw new InvalidOperationException($"Unknown domain event type '{Type}'.");

        return (IDomainEvent)JsonSerializer.Deserialize(Content, type)!;
    }

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedOn = now;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        Error = error;
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).HasMaxLength(300);
        builder.Property(m => m.Content).HasColumnType("jsonb");
        builder.HasIndex(m => m.OccurredOn).HasFilter("\"ProcessedOn\" IS NULL");
    }
}

/// <summary>Antes de cada SaveChanges, convierte los eventos de los agregados en filas del Outbox.</summary>
internal sealed class InsertOutboxMessagesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        InsertOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        InsertOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void InsertOutboxMessages(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var messages = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .SelectMany(aggregate =>
            {
                var events = aggregate.DomainEvents.ToList();
                aggregate.ClearDomainEvents();
                return events;
            })
            .Select(OutboxMessage.From)
            .ToList();

        context.Set<OutboxMessage>().AddRange(messages);
    }
}

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 20;

    public int MaxAttempts { get; set; } = 5;
}
