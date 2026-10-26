using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;

namespace JobPilot.Application.JobApplications;

/// <summary>Filtro combinable del listado. Cada criterio opcional se agrega solo si viene informado.</summary>
public sealed class JobApplicationsFilterSpec : Specification<JobApplication>
{
    public JobApplicationsFilterSpec(ApplicationStatus? status, CompanyId? companyId, string? search, int? page = null, int pageSize = 20)
    {
        if (status is not null)
        {
            Where(a => a.Status == status);
        }

        if (companyId is not null)
        {
            var id = companyId.Value;
            Where(a => a.CompanyId == id);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() se traduce a SQL lower().
            Where(a => a.Title.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        ApplyOrderByDescending(a => a.UpdatedAt);
        AsReadOnly();

        if (page is not null)
        {
            ApplyPaging(page.Value, pageSize);
        }
    }
}

/// <summary>Postulaciones activas sin movimiento desde <paramref name="olderThan"/> (para hacer follow-up).</summary>
public sealed class StaleApplicationsSpec : Specification<JobApplication>
{
    public StaleApplicationsSpec(DateTimeOffset olderThan)
    {
        // Variables locales => EF las envía como parámetros SQL y aplica el converter del smart enum.
        // Comparar directamente contra el campo estático (ApplicationStatus.Applied) no se traduce.
        var applied = ApplicationStatus.Applied;
        var screening = ApplicationStatus.Screening;
        Where(a => a.Status == applied || a.Status == screening);
        Where(a => a.UpdatedAt < olderThan);
        ApplyOrderBy(a => a.UpdatedAt);
        AsReadOnly();
    }
}
