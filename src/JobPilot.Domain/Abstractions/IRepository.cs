namespace JobPilot.Domain.Abstractions;

/// <summary>
/// Repositorio genérico por agregado. La restricción <c>where TAggregate : AggregateRoot&lt;TId&gt;</c>
/// garantiza en compilación que solo se persisten raíces de agregado, nunca entidades internas.
/// </summary>
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : struct, IEquatable<TId>
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TAggregate>> ListAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default);

    void Add(TAggregate aggregate);

    void Remove(TAggregate aggregate);
}

/// <summary>Un elemento con su puntaje (por ejemplo, similitud semántica).</summary>
public sealed record Scored<T>(T Item, double Score);
