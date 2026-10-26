using JobPilot.Domain.Abstractions;
using JobPilot.Domain.JobApplications;

namespace JobPilot.Domain.UnitTests;

public sealed class ApplicationStatusTests
{
    [Theory]
    [InlineData("applied")]
    [InlineData("APPLIED")]
    [InlineData("Applied")]
    public void FromName_IsCaseInsensitive(string name) =>
        ApplicationStatus.FromName(name).ShouldBe(ApplicationStatus.Applied);

    [Fact]
    public void List_ContainsAllStates() =>
        ApplicationStatus.List.Count.ShouldBe(8);

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Rejected")]
    [InlineData("Withdrawn")]
    public void TerminalStates_HaveNoTransitions(string name)
    {
        var status = ApplicationStatus.FromName(name)!;

        status.IsTerminal.ShouldBeTrue();
        status.AllowedTransitions.ShouldBeEmpty();
    }

    [Fact]
    public void Offer_CanOnlyBeAcceptedRejectedOrWithdrawn() =>
        ApplicationStatus.Offer.AllowedTransitions
            .ShouldBe([ApplicationStatus.Accepted, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn], ignoreOrder: true);
}

public sealed class SalaryRangeTests
{
    [Fact]
    public void Create_NormalizesCurrency()
    {
        var result = SalaryRange.Create(2000, 3000, " usd ");

        result.Value.Currency.ShouldBe("USD");
    }

    [Theory]
    [InlineData(-1, 100, "USD")]
    [InlineData(500, 100, "USD")]
    [InlineData(100, 500, "US")]
    public void Create_WithInvalidValues_Fails(decimal min, decimal max, string currency) =>
        SalaryRange.Create(min, max, currency).IsFailure.ShouldBeTrue();

    [Fact]
    public void ValueObjects_AreComparedByValue() =>
        SalaryRange.Create(1, 2, "EUR").Value.ShouldBe(SalaryRange.Create(1, 2, "EUR").Value);
}

public sealed class ResultTests
{
    private static readonly Error SomeError = Error.Failure("Test.Error", "Something failed");

    [Fact]
    public void ImplicitConversions_CreateSuccessAndFailure()
    {
        Result<int> success = 42;
        Result<int> failure = SomeError;

        success.IsSuccess.ShouldBeTrue();
        success.Value.ShouldBe(42);
        failure.Error.ShouldBe(SomeError);
    }

    [Fact]
    public void Value_OnFailure_Throws()
    {
        Result<int> failure = SomeError;

        Should.Throw<InvalidOperationException>(() => failure.Value);
    }

    [Fact]
    public void MapAndBind_ShortCircuitOnFailure()
    {
        Result<int> start = 10;

        var chained = start
            .Map(x => x * 2)
            .Bind(x => x > 100 ? Result.Success(x) : Result.Failure<int>(SomeError))
            .Map(x => x + 1);

        chained.Error.ShouldBe(SomeError);
    }
}
