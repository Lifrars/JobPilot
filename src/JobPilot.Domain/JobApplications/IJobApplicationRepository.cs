using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Users;

namespace JobPilot.Domain.JobApplications;

/// <summary>
/// Repositorio específico: hereda las operaciones genéricas y agrega las propias del agregado.
/// </summary>
public interface IJobApplicationRepository : IRepository<JobApplication, JobApplicationId>
{
    Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Búsqueda semántica: devuelve las postulaciones de <paramref name="ownerId"/> más cercanas al vector.
    /// El dueño es explícito porque la consulta usa SQL crudo, que NO pasa por los query filters de EF.
    /// </summary>
    Task<IReadOnlyList<Scored<JobApplication>>> SearchSimilarAsync(
        UserId ownerId,
        float[] embedding,
        int take,
        CancellationToken cancellationToken = default);
}
