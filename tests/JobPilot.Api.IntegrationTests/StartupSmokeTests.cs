using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobPilot.Api.IntegrationTests;

/// <summary>
/// Arranca la API sin base de datos (no requiere Docker). En Development, ASP.NET Core valida el
/// contenedor de DI al construirlo (ValidateOnBuild + ValidateScopes): un registro roto falla aquí.
/// </summary>
public sealed class StartupSmokeTests : IClassFixture<StartupSmokeTests.NoDatabaseFactory>
{
    private readonly HttpClient _client;

    public StartupSmokeTests(NoDatabaseFactory factory) => _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpenApiDocument_ListsTheEndpoints()
    {
        var document = await _client.GetStringAsync("/openapi/v1.json", Ct);

        document.ShouldContain("/api/v1/applications/{id}/status");
        document.ShouldContain("/api/v1/applications/{id}/analyze");
        document.ShouldContain("AnalyzeJobPosting");
    }

    [Fact]
    public async Task InvalidCommand_IsRejectedByTheValidationDecoratorBeforeTouchingTheDatabase()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/companies", new { Name = "" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty("Name", out _).ShouldBeTrue();
        problem.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UnknownMatchStrategy_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync($"/api/v1/applications/{Guid.NewGuid()}/match?resumeId={Guid.NewGuid()}&strategy=magic", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errorCode").GetString().ShouldBe("Match.UnknownStrategy");
    }

    [Fact]
    public async Task McpServer_ExposesTheJobPilotTools()
    {
        var initialize = await SendMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "smoke-test", version = "1.0" } }
        });
        initialize.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessionId = initialize.Headers.TryGetValues("Mcp-Session-Id", out var values) ? values.First() : null;

        var tools = await SendMcpAsync(new { jsonrpc = "2.0", id = 2, method = "tools/list" }, sessionId);
        var body = await tools.Content.ReadAsStringAsync(Ct);

        body.ShouldContain("list_applications");
        body.ShouldContain("get_pipeline_stats");
        body.ShouldContain("change_application_status");
    }

    private Task<HttpResponseMessage> SendMcpAsync(object payload, string? sessionId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = JsonContent.Create(payload) };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (sessionId is not null)
        {
            request.Headers.Add("Mcp-Session-Id", sessionId);
        }

        return _client.SendAsync(request, Ct);
    }

    public sealed class NoDatabaseFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("ConnectionStrings:jobpilot", "Host=127.0.0.1;Port=1;Database=none;Timeout=1");
            builder.UseSetting("Outbox:Interval", "01:00:00");
        }
    }
}
