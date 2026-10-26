using System.Security.Claims;
using JobPilot.Application.JobApplications;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Infrastructure.Identity;
using JobPilot.Infrastructure.Persistence;
using JobPilot.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace JobPilot.Api.IntegrationTests;

/// <summary>
/// Verifica que EF Core traduce a SQL nuestras specifications con tipos personalizados
/// (IDs fuertemente tipados, smart enums, ordenamiento con boxing, filtro por dueño). No necesita base de datos ni Docker.
/// </summary>
public sealed class QueryTranslationTests : IDisposable
{
    private static readonly Guid UserGuid = Guid.NewGuid();

    private readonly JobPilotDbContext _context = new(
        new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseNpgsql("Host=localhost;Database=translation-only", npgsql => npgsql.UseVector())
            .Options,
        new CurrentUser(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserGuid.ToString())], "Test"))
            }
        }));

    [Fact]
    public void EveryOwnedAggregate_IsFilteredByCurrentUser()
    {
        var applications = _context.JobApplications.ToQueryString();
        var companies = _context.Companies.ToQueryString();
        var resumes = _context.Resumes.ToQueryString();

        foreach (var sql in new[] { applications, companies, resumes })
        {
            sql.ShouldContain("\"OwnerId\" = @");
            sql.ShouldContain(UserGuid.ToString());
        }
    }

    [Fact]
    public void IgnoreQueryFilters_RemovesTheOwnerFilter() =>
        _context.JobApplications.IgnoreQueryFilters([JobPilotDbContext.OwnerFilter]).ToQueryString().ShouldNotContain("\"OwnerId\" = @");

    [Fact]
    public void FilterSpec_TranslatesStatusCompanyAndSearchToSql()
    {
        var spec = new JobApplicationsFilterSpec(ApplicationStatus.Interviewing, CompanyId.New(), "Backend", page: 2, pageSize: 10);

        var sql = SpecificationEvaluator.Apply(_context.JobApplications, spec).ToQueryString();

        sql.ShouldContain("-- @status='Interviewing'");
        sql.ShouldContain("\"Status\" = @status");
        sql.ShouldContain("\"CompanyId\" = @");
        sql.ShouldContain("lower(");
        sql.ShouldContain("ORDER BY j.\"UpdatedAt\" DESC");
        sql.ShouldContain("LIMIT @");
        sql.ShouldContain("OFFSET @");
    }

    [Fact]
    public void StaleSpec_TranslatesMultipleStatusesAndDates()
    {
        var spec = new StaleApplicationsSpec(DateTimeOffset.UtcNow.AddDays(-14));

        var sql = SpecificationEvaluator.Apply(_context.JobApplications, spec).ToQueryString();

        sql.ShouldContain("@applied='Applied'");
        sql.ShouldContain("@screening='Screening'");
        sql.ShouldContain("\"UpdatedAt\" <");
    }

    [Fact]
    public void ContainsOverStronglyTypedIds_TranslatesToArrayParameter()
    {
        var ids = new List<JobApplicationId> { JobApplicationId.New(), JobApplicationId.New() };

        var sql = _context.JobApplications.Where(a => ids.Contains(a.Id)).ToQueryString();

        sql.ShouldContain("= ANY (@");
    }

    [Fact]
    public void GroupByStatus_TranslatesToSqlGroupBy()
    {
        var sql = _context.JobApplications
            .GroupBy(a => a.Status)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToQueryString();

        sql.ShouldContain("GROUP BY j.\"Status\"");
    }

    [Fact]
    public void FindByStronglyTypedId_UsesParameterAndJoinsInterviews()
    {
        var id = JobApplicationId.New();

        var sql = _context.JobApplications.Include(a => a.Interviews).Where(a => a.Id == id).ToQueryString();

        sql.ShouldContain("\"Id\" = @id");
        sql.ShouldContain("\"Interviews\"");
    }

    public void Dispose() => _context.Dispose();
}
