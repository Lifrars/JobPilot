using System.Reflection;
using JobPilot.Domain.Abstractions;
using NetArchTest.Rules;

namespace JobPilot.ArchitectureTests;

/// <summary>
/// La arquitectura se documenta con tests: si alguien hace que Domain dependa de EF Core,
/// el build de CI falla. Las reglas dejan de ser "acuerdos" y pasan a ser verificables.
/// </summary>
public sealed class LayerTests
{
    private static readonly Assembly Domain = JobPilot.Domain.AssemblyReference.Assembly;
    private static readonly Assembly Application = JobPilot.Application.AssemblyReference.Assembly;
    private static readonly Assembly Infrastructure = typeof(JobPilot.Infrastructure.DependencyInjection).Assembly;

    private const string ApplicationNs = "JobPilot.Application";
    private const string InfrastructureNs = "JobPilot.Infrastructure";
    private const string ApiNs = "JobPilot.Api";

    [Fact]
    public void Domain_DependsOnNothingElse()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions.AI")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn(ApiNs).GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Handlers_AreSealedAndInternal()
    {
        var result = Types.InAssembly(Application)
            .That().HaveNameEndingWith("Handler")
            .Should().BeSealed().And().NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void DomainEvents_AreSealedAndNamedInPastTense()
    {
        var result = Types.InAssembly(Domain)
            .That().ImplementInterface(typeof(IDomainEvent))
            .Should().BeSealed().And().HaveNameEndingWith("DomainEvent")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Aggregates_DoNotExposePublicConstructors()
    {
        var aggregates = Domain.GetTypes()
            .Where(t => t is { IsAbstract: false, BaseType.IsGenericType: true } &&
                        t.BaseType.GetGenericTypeDefinition() == typeof(AggregateRoot<>));

        aggregates.SelectMany(t => t.GetConstructors()).ShouldBeEmpty("aggregates must be created through factory methods");
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
