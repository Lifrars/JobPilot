using System.Reflection;
using JobPilot.Application.Abstractions.Persistence;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Resumes;
using JobPilot.Domain.Users;
using JobPilot.Infrastructure.Identity;
using JobPilot.Infrastructure.Outbox;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Infrastructure.Persistence;

/// <summary>
/// El DbContext ya es un Unit of Work: agrupa todos los cambios y los guarda en una transacción.
/// Hereda de IdentityDbContext para que usuarios y datos de negocio vivan en la misma base.
/// </summary>
public sealed class JobPilotDbContext(DbContextOptions<JobPilotDbContext> options, CurrentUser currentUser)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IUnitOfWork
{
    public const string OwnerFilter = "OwnerFilter";

    private static readonly MethodInfo ApplyOwnerFilterMethod =
        typeof(JobPilotDbContext).GetMethod(nameof(ApplyOwnerFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    public DbSet<Resume> Resumes => Set<Resume>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // EF reevalúa estas propiedades en CADA consulta y las envía como parámetros SQL:
    // un mismo modelo compilado sirve para todos los usuarios.
    private bool IsSystemContext => currentUser.IsSystem;

    private UserId CurrentUserId => currentUser.TryGetUserId() ?? default;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobPilotDbContext).Assembly);

        // Aislamiento multiusuario: todo aggregate IOwnedByUser recibe el filtro "solo mis datos".
        // Un tipo nuevo queda protegido automáticamente; no depende de que alguien se acuerde de filtrar.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IOwnedByUser).IsAssignableFrom(t.ClrType) && !t.IsOwned()))
        {
            ApplyOwnerFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.AddStronglyTypedIdConversions(Domain.AssemblyReference.Assembly);

        configurationBuilder.Properties<ApplicationStatus>()
            .HaveConversion<EnumerationConverter<ApplicationStatus>>()
            .HaveMaxLength(32);
    }

    /// <summary>Método genérico invocado por reflexión: la restricción garantiza que TEntity tiene OwnerId.</summary>
    private void ApplyOwnerFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IOwnedByUser =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            OwnerFilter,
            entity => IsSystemContext || entity.OwnerId == CurrentUserId);
}
