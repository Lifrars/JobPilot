using FluentValidation;
using JobPilot.Application.Abstractions.Behaviors;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.JobApplications.Matching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobPilot.Application;

/// <summary>
/// Cada capa registra sus propios servicios; Api/Program.cs solo compone (composition root).
/// No hace falta un proyecto "IoC" separado.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ISender, Sender>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Scrutor escanea el ensamblado y registra cada handler por sus interfaces genéricas cerradas.
        services.Scan(scan => scan
            .FromAssemblies(AssemblyReference.Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
                .AsImplementedInterfaces().WithScopedLifetime());

        // Decorators como genéricos abiertos. El último registrado queda más afuera:
        // Logging -> Validation -> UnitOfWork -> Handler
        services.Decorate(typeof(ICommandHandler<,>), typeof(UnitOfWorkDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(ValidationDecorator.QueryHandler<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(LoggingDecorator.CommandHandler<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator.QueryHandler<,>));

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        // Strategy: todas las estrategias se inyectan como IEnumerable<IMatchScoringStrategy>.
        services.AddSingleton<KeywordMatchStrategy>();
        services.AddSingleton<EmbeddingMatchStrategy>();
        services.AddSingleton<HybridMatchStrategy>();
        services.AddSingleton<IMatchScoringStrategy>(sp => sp.GetRequiredService<KeywordMatchStrategy>());
        services.AddSingleton<IMatchScoringStrategy>(sp => sp.GetRequiredService<EmbeddingMatchStrategy>());
        services.AddSingleton<IMatchScoringStrategy>(sp => sp.GetRequiredService<HybridMatchStrategy>());

        return services;
    }
}
