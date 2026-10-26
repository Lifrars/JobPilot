using JobPilot.Application.Abstractions.Persistence;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Resumes;
using JobPilot.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Infrastructure.Persistence;

/// <summary>El DbContext ya es un Unit of Work: agrupa todos los cambios y los guarda en una transacción.</summary>
public sealed class JobPilotDbContext(DbContextOptions<JobPilotDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    public DbSet<Resume> Resumes => Set<Resume>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobPilotDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.AddStronglyTypedIdConversions(Domain.AssemblyReference.Assembly);

        configurationBuilder.Properties<ApplicationStatus>()
            .HaveConversion<EnumerationConverter<ApplicationStatus>>()
            .HaveMaxLength(32);
    }
}
