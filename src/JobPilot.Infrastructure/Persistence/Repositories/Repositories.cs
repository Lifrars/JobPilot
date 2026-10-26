using JobPilot.Domain.Abstractions;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace JobPilot.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación genérica: se registra UNA vez como genérico abierto
/// (<c>IRepository&lt;,&gt;</c> -> <c>EfRepository&lt;,&gt;</c>) y sirve para cualquier agregado.
/// </summary>
internal class EfRepository<TAggregate, TId>(JobPilotDbContext context) : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : struct, IEquatable<TId>
{
    protected JobPilotDbContext Context { get; } = context;

    protected DbSet<TAggregate> Set => Context.Set<TAggregate>();

    public virtual async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default) =>
        await Set.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<TAggregate>> ListAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default) =>
        await SpecificationEvaluator.Apply(Set, specification).ToListAsync(cancellationToken);

    public Task<int> CountAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default) =>
        SpecificationEvaluator.Apply(Set, specification, forCount: true).CountAsync(cancellationToken);

    public Task<bool> AnyAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default) =>
        SpecificationEvaluator.Apply(Set, specification, forCount: true).AnyAsync(cancellationToken);

    public void Add(TAggregate aggregate) => Set.Add(aggregate);

    public void Remove(TAggregate aggregate) => Set.Remove(aggregate);
}

/// <summary>Repositorio específico: reutiliza la base genérica y agrega consultas propias del agregado.</summary>
internal sealed class JobApplicationRepository(JobPilotDbContext context)
    : EfRepository<JobApplication, JobApplicationId>(context), IJobApplicationRepository
{
    public override Task<JobApplication?> GetByIdAsync(JobApplicationId id, CancellationToken cancellationToken = default) =>
        Set.Include(a => a.Interviews).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Set
            .GroupBy(a => a.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Status, row => row.Count);
    }

    public async Task<IReadOnlyList<Scored<JobApplication>>> SearchSimilarAsync(
        UserId ownerId,
        float[] embedding,
        int take,
        CancellationToken cancellationToken = default)
    {
        var vector = new Vector(embedding);

        // <=> es el operador de distancia coseno de pgvector (0 = idéntico, 2 = opuesto).
        // SQL interpolado = parámetros seguros, sin inyección SQL.
        // ¡Ojo! El SQL crudo NO aplica los query filters globales: el filtro por dueño va explícito.
        var hits = await Context.Database
            .SqlQuery<SimilarityHit>($"""
                SELECT "Id", "Embedding" <=> {vector} AS "Distance"
                FROM "JobApplications"
                WHERE "OwnerId" = {ownerId.Value} AND "Embedding" IS NOT NULL
                ORDER BY "Embedding" <=> {vector}
                LIMIT {take}
                """)
            .ToListAsync(cancellationToken);

        var ids = hits.Select(hit => JobApplicationId.From(hit.Id)).ToList();
        var applications = await Set.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken);

        return
        [
            .. hits
                .OrderBy(hit => hit.Distance)
                .Join(applications, hit => hit.Id, a => a.Id.Value, (hit, a) => new Scored<JobApplication>(a, 1 - hit.Distance))
        ];
    }
}

internal sealed record SimilarityHit(Guid Id, double Distance);

internal static class SpecificationEvaluator
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, Specification<T> specification, bool forCount = false)
        where T : class
    {
        query = specification.Criteria.Aggregate(query, (current, criteria) => current.Where(criteria));

        if (forCount)
        {
            return query;
        }

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));

        if (specification.OrderBy is not null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending is not null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        if (specification.Skip is { } skip)
        {
            query = query.Skip(skip);
        }

        if (specification.Take is { } take)
        {
            query = query.Take(take);
        }

        return specification.IsReadOnly ? query.AsNoTracking() : query;
    }
}
