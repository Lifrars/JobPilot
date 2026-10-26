using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Users;

namespace JobPilot.Domain.UnitTests;

public sealed class JobApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly UserId Owner = UserId.From(Guid.NewGuid());

    private static JobApplication NewApplication() =>
        JobApplication.Create(Owner, CompanyId.New(), "Backend Developer .NET", null, "C#, PostgreSQL", null, WorkMode.Remote, Now).Value;

    private static JobApplication InStatus(params ApplicationStatus[] path)
    {
        var application = NewApplication();
        foreach (var status in path)
        {
            application.ChangeStatus(status, Now).IsSuccess.ShouldBeTrue($"transition to {status} should be valid");
        }

        return application;
    }

    [Fact]
    public void Create_WithEmptyTitle_ReturnsTitleRequired()
    {
        var result = JobApplication.Create(Owner, CompanyId.New(), "  ", null, null, null, WorkMode.Unknown, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(JobApplicationErrors.TitleRequired);
    }

    [Theory]
    [InlineData("/jobs/1")]                 // en Linux, Uri lo interpreta como file:///jobs/1
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/job")]
    [InlineData("not a url")]
    public void Create_WithNonHttpUrl_ReturnsInvalidUrl(string url)
    {
        var result = JobApplication.Create(Owner, CompanyId.New(), "Dev", url, null, null, WorkMode.Unknown, Now);

        result.Error.ShouldBe(JobApplicationErrors.InvalidUrl);
    }

    [Fact]
    public void Create_WithHttpsUrl_Succeeds() =>
        JobApplication.Create(Owner, CompanyId.New(), "Dev", "https://jobs.example.com/1", null, null, WorkMode.Unknown, Now)
            .IsSuccess.ShouldBeTrue();

    [Fact]
    public void Create_Valid_StartsInWishlistAndRaisesCreatedEvent()
    {
        var application = NewApplication();

        application.Status.ShouldBe(ApplicationStatus.Wishlist);
        application.OwnerId.ShouldBe(Owner);
        application.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<JobApplicationCreatedDomainEvent>().OwnerId.ShouldBe(Owner);
    }

    [Fact]
    public void ChangeStatus_ValidTransition_UpdatesStatusHistoryAndRaisesEvent()
    {
        var application = NewApplication();
        application.ClearDomainEvents();

        var result = application.ChangeStatus(ApplicationStatus.Applied, Now);

        result.IsSuccess.ShouldBeTrue();
        application.Status.ShouldBe(ApplicationStatus.Applied);
        application.AppliedAt.ShouldBe(Now);
        application.StatusHistory.ShouldHaveSingleItem().ShouldBe(new StatusChange(ApplicationStatus.Wishlist, ApplicationStatus.Applied, Now));
        var raised = application.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ApplicationStatusChangedDomainEvent>();
        raised.FromStatus.ShouldBe("Wishlist");
        raised.ToStatus.ShouldBe("Applied");
    }

    [Fact]
    public void ChangeStatus_InvalidTransition_ReturnsErrorAndKeepsStatus()
    {
        var application = NewApplication();

        var result = application.ChangeStatus(ApplicationStatus.Offer, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("JobApplication.InvalidTransition");
        application.Status.ShouldBe(ApplicationStatus.Wishlist);
        application.StatusHistory.ShouldBeEmpty();
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_ReturnsConflict()
    {
        var application = InStatus(ApplicationStatus.Applied);

        var result = application.ChangeStatus(ApplicationStatus.Applied, Now);

        result.Error.Code.ShouldBe("JobApplication.AlreadyInStatus");
    }

    [Fact]
    public void ChangeStatus_FromTerminalStatus_IsRejected()
    {
        var application = InStatus(ApplicationStatus.Applied, ApplicationStatus.Rejected);

        application.ChangeStatus(ApplicationStatus.Interviewing, Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ScheduleInterview_WhenApplied_MovesToInterviewingAndStoresUtc()
    {
        var application = InStatus(ApplicationStatus.Applied);
        var localTime = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(-5));

        var result = application.ScheduleInterview(InterviewType.Technical, localTime, "Live coding", Now);

        result.IsSuccess.ShouldBeTrue();
        application.Status.ShouldBe(ApplicationStatus.Interviewing);
        var interview = application.Interviews.ShouldHaveSingleItem();
        interview.ScheduledAt.Offset.ShouldBe(TimeSpan.Zero);
        interview.ScheduledAt.ShouldBe(localTime);
    }

    [Fact]
    public void ScheduleInterview_WhenInWishlist_IsNotAllowed()
    {
        var application = NewApplication();

        var result = application.ScheduleInterview(InterviewType.Hr, Now.AddDays(2), null, Now);

        result.Error.Code.ShouldBe("JobApplication.InterviewsNotAllowed");
        application.Interviews.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateJobDescription_WhenChanged_RaisesEvent()
    {
        var application = NewApplication();
        application.ClearDomainEvents();

        application.UpdateJobDescription("New description", Now);
        application.UpdateJobDescription("New description", Now);

        application.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<JobDescriptionChangedDomainEvent>();
    }
}
