using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace JobPilot.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}

/// <summary>Usado solo por <c>dotnet ef migrations add</c>: no necesita una base de datos real.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JobPilotDbContext>
{
    public JobPilotDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseNpgsql("Host=localhost;Database=jobpilot", npgsql => npgsql.UseVector())
            .Options);
}
