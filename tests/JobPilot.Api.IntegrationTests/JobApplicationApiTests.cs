using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobPilot.Application.Abstractions.Paging;
using JobPilot.Application.Companies;
using JobPilot.Application.JobApplications;
using JobPilot.Domain.JobApplications;

namespace JobPilot.Api.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class JobApplicationApiTests(JobPilotApiFactory factory) : IAsyncLifetime
{
    private HttpClient _client = null!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // xUnit crea una instancia por test: cada test trabaja con su propio usuario recién registrado.
    public async ValueTask InitializeAsync() => _client = await factory.CreateAuthenticatedClientAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Lifecycle_FollowsTheStateMachine()
    {
        var company = await CreateCompanyAsync();
        var created = await CreateApplicationAsync(company.Id, "Backend Developer", "C# and PostgreSQL");

        created.Status.ShouldBe("Wishlist");
        created.AllowedTransitions.ShouldBe(["Applied", "Withdrawn"]);

        var applied = await _client.PostAsJsonAsync($"/api/v1/applications/{created.Id}/status", new { Status = "Applied" }, Ct);
        applied.StatusCode.ShouldBe(HttpStatusCode.OK);

        var illegal = await _client.PostAsJsonAsync($"/api/v1/applications/{created.Id}/status", new { Status = "Offer" }, Ct);
        illegal.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadErrorCodeAsync(illegal)).ShouldBe("JobApplication.InvalidTransition");

        var interview = await _client.PostAsJsonAsync(
            $"/api/v1/applications/{created.Id}/interviews",
            new { Type = "Technical", ScheduledAt = DateTimeOffset.UtcNow.AddDays(3), Notes = "System design" },
            JobPilotApiFactory.Json,
            Ct);
        interview.StatusCode.ShouldBe(HttpStatusCode.Created);

        var detail = await _client.GetFromJsonAsync<JobApplicationResponse>($"/api/v1/applications/{created.Id}", JobPilotApiFactory.Json, Ct);
        detail!.Status.ShouldBe("Interviewing");
        detail.Interviews.ShouldHaveSingleItem().Type.ShouldBe("Technical");
        detail.StatusHistory.Select(h => h.To).ShouldBe(["Applied", "Interviewing"]);
    }

    [Fact]
    public async Task Create_WithInvalidBody_ReturnsValidationProblem()
    {
        var company = await CreateCompanyAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/applications", new { CompanyId = company.Id, Title = "" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty("Title", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ForUnknownCompany_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/applications", new { CompanyId = Guid.NewGuid(), Title = "Dev" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadErrorCodeAsync(response)).ShouldBe("Company.NotFound");
    }

    [Fact]
    public async Task List_FiltersByCompanyAndStatus()
    {
        var company = await CreateCompanyAsync();
        var first = await CreateApplicationAsync(company.Id, "Platform Engineer", "Kubernetes");
        await CreateApplicationAsync(company.Id, "Data Engineer", "SQL");
        await _client.PostAsJsonAsync($"/api/v1/applications/{first.Id}/status", new { Status = "Applied" }, Ct);

        var page = await _client.GetFromJsonAsync<PagedList<JobApplicationSummary>>(
            $"/api/v1/applications?companyId={company.Id}&status=applied", JobPilotApiFactory.Json, Ct);

        page!.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task Stats_ReturnsEveryStatus()
    {
        var stats = await _client.GetFromJsonAsync<PipelineStatsResponse>("/api/v1/applications/stats", Ct);

        stats!.ByStatus.Keys.ShouldBe(ApplicationStatus.List.Select(s => s.Name), ignoreOrder: true);
    }

    [Fact]
    public async Task SemanticSearch_UsesEmbeddingsGeneratedThroughTheOutbox()
    {
        var company = await CreateCompanyAsync();
        var python = await CreateApplicationAsync(company.Id, "ML Engineer", "Python, PyTorch, Docker");
        var dotnet = await CreateApplicationAsync(company.Id, "Backend .NET", "C#, .NET, SQL");

        // Los embeddings se generan en segundo plano (Outbox -> handler), así que esperamos a que existan.
        await WaitUntilAsync(async () =>
            (await GetAsync(python.Id)).HasEmbedding && (await GetAsync(dotnet.Id)).HasEmbedding);

        var hits = await _client.GetFromJsonAsync<List<SearchHitResponse>>("/api/v1/applications/search?q=python developer&take=3", Ct);

        hits!.First().Id.ShouldBe(python.Id);
    }

    [Fact]
    public async Task Analyze_WithoutAiConfigured_Returns503()
    {
        var company = await CreateCompanyAsync();
        var application = await CreateApplicationAsync(company.Id, "Dev", "Some description");

        var response = await _client.PostAsJsonAsync($"/api/v1/applications/{application.Id}/analyze", new { }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await ReadErrorCodeAsync(response)).ShouldBe("AI.ChatNotConfigured");
    }

    private async Task<CompanyResponse> CreateCompanyAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/companies", new { Name = $"Company {Guid.NewGuid():N}" }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CompanyResponse>(Ct))!;
    }

    private async Task<JobApplicationResponse> CreateApplicationAsync(Guid companyId, string title, string description)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/applications",
            new { CompanyId = companyId, Title = title, JobDescription = description, WorkMode = "Remote" },
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobApplicationResponse>(JobPilotApiFactory.Json, Ct))!;
    }

    private async Task<JobApplicationResponse> GetAsync(Guid id) =>
        (await _client.GetFromJsonAsync<JobApplicationResponse>($"/api/v1/applications/{id}", JobPilotApiFactory.Json, Ct))!;

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("errorCode").GetString();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met within 15 seconds.");
            }

            await Task.Delay(250, Ct);
        }
    }
}
