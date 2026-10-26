using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.JobApplications;

/// <summary>
/// Repositorio específico: hereda las operaciones genéricas y agrega las propias del agregado.
/// </summary>
public interface IJobApplicationRepository : IRepository<JobApplication, JobApplicationId>
{
    Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Búsqueda semántica: devuelve las postulaciones más cercanas al vector (similitud coseno).</summary>
    Task<IReadOnlyList<Scored<JobApplication>>> SearchSimilarAsync(
        float[] embedding,
        int take,
        CancellationToken cancellationToken = default);
}
