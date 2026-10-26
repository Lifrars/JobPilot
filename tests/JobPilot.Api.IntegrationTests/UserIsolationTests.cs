using System.Net;
using System.Net.Http.Json;
using JobPilot.Application.Abstractions.Paging;
using JobPilot.Application.Companies;
using JobPilot.Application.JobApplications;

namespace JobPilot.Api.IntegrationTests;

/// <summary>
/// Multi-tenancy: Alice y Bob usan la misma API y la misma base. Bob nunca debe poder ver,
/// modificar ni referenciar los datos de Alice, ni siquiera conociendo sus IDs.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class UserIsolationTests(JobPilotApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Anonymous_RequestsAreRejected()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/applications", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Users_CanNotSeeOrTouchEachOthersData()
    {
        using var alice = await factory.CreateAuthenticatedClientAsync();
        using var bob = await factory.CreateAuthenticatedClientAsync();

        var aliceCompany = await (await alice.PostAsJsonAsync("/api/v1/companies", new { Name = "Shared Corp" }, Ct))
            .Content.ReadFromJsonAsync<CompanyResponse>(Ct);
        var aliceApp = await (await alice.PostAsJsonAsync(
                "/api/v1/applications",
                new { CompanyId = aliceCompany!.Id, Title = "Python Engineer", JobDescription = "Python, Docker" },
                Ct))
            .Content.ReadFromJsonAsync<JobApplicationResponse>(JobPilotApiFactory.Json, Ct);

        // Leer por ID: 404 (no 403), para no revelar siquiera que el recurso existe.
        (await bob.GetAsync($"/api/v1/applications/{aliceApp!.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Modificar: tampoco.
        (await bob.PostAsJsonAsync($"/api/v1/applications/{aliceApp.Id}/status", new { Status = "Applied" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Referenciar la empresa de Alice desde una postulación propia: no existe para Bob.
        (await bob.PostAsJsonAsync("/api/v1/applications", new { CompanyId = aliceCompany.Id, Title = "Sneaky" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Mismo nombre de empresa: permitido, la unicidad es por usuario.
        (await bob.PostAsJsonAsync("/api/v1/companies", new { Name = "Shared Corp" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Listados y métricas: solo lo propio.
        var bobList = await bob.GetFromJsonAsync<PagedList<JobApplicationSummary>>("/api/v1/applications", JobPilotApiFactory.Json, Ct);
        bobList!.TotalCount.ShouldBe(0);
        var bobStats = await bob.GetFromJsonAsync<PipelineStatsResponse>("/api/v1/applications/stats", Ct);
        bobStats!.Total.ShouldBe(0);

        // La búsqueda semántica usa SQL crudo (sin query filters): debe filtrar por dueño explícitamente.
        await WaitUntilAsync(async () =>
            (await alice.GetFromJsonAsync<JobApplicationResponse>($"/api/v1/applications/{aliceApp.Id}", JobPilotApiFactory.Json, Ct))!.HasEmbedding);
        var bobHits = await bob.GetFromJsonAsync<List<SearchHitResponse>>("/api/v1/applications/search?q=python", Ct);
        bobHits!.ShouldBeEmpty();
        var aliceHits = await alice.GetFromJsonAsync<List<SearchHitResponse>>("/api/v1/applications/search?q=python", Ct);
        aliceHits!.ShouldHaveSingleItem().Id.ShouldBe(aliceApp.Id);
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
