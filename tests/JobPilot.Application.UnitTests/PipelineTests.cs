using JobPilot.Application.Abstractions.Authentication;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.Abstractions.Persistence;
using JobPilot.Application.Companies;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.Companies;
using JobPilot.Domain.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JobPilot.Application.UnitTests;

/// <summary>
/// Prueba el pipeline completo con el contenedor de DI real: Sender -> Logging -> Validation -> UnitOfWork -> Handler.
/// Solo se reemplazan los puertos de infraestructura (repositorio y unit of work).
/// </summary>
public sealed class PipelineTests
{
    private readonly IRepository<Company, CompanyId> _companies = Substitute.For<IRepository<Company, CompanyId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly UserId _user = UserId.From(Guid.NewGuid());
    private readonly ISender _sender;

    public PipelineTests()
    {
        _userContext.UserId.Returns(_user);

        var services = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddApplication()
            .AddSingleton(_companies)
            .AddSingleton(_unitOfWork)
            .AddSingleton(_userContext);

        _sender = services.BuildServiceProvider(validateScopes: true).CreateScope().ServiceProvider.GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Send_InvalidCommand_ReturnsValidationErrorsAndDoesNotSave()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sender.SendAsync(new CreateCompanyCommand("", "not-a-url", null), ct);

        var validation = result.Error.ShouldBeOfType<ValidationError>();
        validation.Errors.Select(e => e.Code).ShouldBe(["Name", "Website"], ignoreOrder: true);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _companies.DidNotReceive().Add(Arg.Any<Company>());
    }

    [Fact]
    public async Task Send_ValidCommand_RunsHandlerAndSavesOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        _companies.AnyAsync(Arg.Any<Specification<Company>>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sender.SendAsync(new CreateCompanyCommand("Contoso", "https://contoso.com", "Software"), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Contoso");
        _companies.Received(1).Add(Arg.Is<Company>(c => c.Name == "Contoso" && c.OwnerId == _user));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WhenHandlerFails_DoesNotSave()
    {
        var ct = TestContext.Current.CancellationToken;
        _companies.AnyAsync(Arg.Any<Specification<Company>>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sender.SendAsync(new CreateCompanyCommand("Contoso", null, null), ct);

        result.Error.Code.ShouldBe("Company.NameAlreadyExists");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
