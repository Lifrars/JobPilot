using JobPilot.Application.JobApplications;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Users;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPilot.Application.UnitTests;

public sealed class JobApplicationHandlerTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero));
    private readonly IJobApplicationRepository _applications = Substitute.For<IJobApplicationRepository>();

    [Fact]
    public async Task ChangeStatus_UsesTimeProviderAndReturnsAllowedTransitions()
    {
        var application = JobApplication.Create(UserId.From(Guid.NewGuid()), CompanyId.New(), "Dev", null, null, null, WorkMode.Remote, _time.GetUtcNow()).Value;
        _applications.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        var handler = new ChangeJobApplicationStatusCommandHandler(_applications, _time);
        _time.Advance(TimeSpan.FromDays(1));

        var result = await handler.Handle(new ChangeJobApplicationStatusCommand(application.Id.Value, "applied"), TestContext.Current.CancellationToken);

        result.Value.Status.ShouldBe("Applied");
        result.Value.AppliedAt.ShouldBe(_time.GetUtcNow());
        result.Value.AllowedTransitions.ShouldBe(["Screening", "Interviewing", "Rejected", "Withdrawn"]);
    }

    [Fact]
    public async Task ChangeStatus_UnknownApplication_ReturnsNotFound()
    {
        var handler = new ChangeJobApplicationStatusCommandHandler(_applications, _time);

        var result = await handler.Handle(new ChangeJobApplicationStatusCommand(Guid.NewGuid(), "Applied"), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("JobApplication.NotFound");
    }

    [Fact]
    public void PipelineStats_CalculatesResponseRateOverSentApplications()
    {
        var counts = new Dictionary<ApplicationStatus, int>
        {
            [ApplicationStatus.Wishlist] = 5,      // no enviadas: no cuentan
            [ApplicationStatus.Applied] = 6,       // sin respuesta
            [ApplicationStatus.Interviewing] = 2,  // respondieron
            [ApplicationStatus.Rejected] = 2       // respondieron
        };

        var stats = GetPipelineStatsQueryHandler.Calculate(counts);

        stats.Total.ShouldBe(15);
        stats.Active.ShouldBe(8);
        stats.ResponseRate.ShouldBe(40.0);
        stats.ByStatus["Offer"].ShouldBe(0);
    }
}
