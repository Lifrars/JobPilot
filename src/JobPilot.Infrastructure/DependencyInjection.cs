using JobPilot.Application.Abstractions.Persistence;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.JobApplications;
using JobPilot.Infrastructure.AI;
using JobPilot.Infrastructure.Outbox;
using JobPilot.Infrastructure.Persistence;
using JobPilot.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace JobPilot.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseName = "jobpilot";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<InsertOutboxMessagesInterceptor>();

        // La cadena de conexión se lee al resolver el DbContext (no al registrarlo), así Aspire o
        // los tests de integración pueden inyectarla por configuración.
        services.AddDbContext<JobPilotDbContext>((serviceProvider, options) => options
            .UseNpgsql(
                serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString(DatabaseName),
                npgsql => npgsql.UseVector())
            .AddInterceptors(serviceProvider.GetRequiredService<InsertOutboxMessagesInterceptor>()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<JobPilotDbContext>());

        // Genérico abierto: cualquier IRepository<TAggregate, TId> se resuelve con EfRepository<TAggregate, TId>.
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();

        services.AddHybridCache();

        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxProcessor>();

        services.AddAi(configuration);

        return services;
    }
}
