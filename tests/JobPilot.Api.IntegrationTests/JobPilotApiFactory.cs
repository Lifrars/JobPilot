using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobPilot.Application.Abstractions.AI;
using JobPilot.Domain.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace JobPilot.Api.IntegrationTests;

/// <summary>
/// Levanta la API real en memoria contra un PostgreSQL real (con pgvector) en Docker.
/// Nada de bases en memoria: probamos el mismo motor que en producción.
/// </summary>
public sealed class JobPilotApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public async ValueTask InitializeAsync() => await _database.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:jobpilot", _database.GetConnectionString());
        builder.UseSetting("Outbox:Interval", "00:00:00.200");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();
        });
    }

    /// <summary>Registra un usuario nuevo (único por test) e inicia sesión: devuelve un cliente con su bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var credentials = new { Email = $"user-{Guid.NewGuid():N}@jobpilot.test", Password = "Test-Passw0rd!" };

        (await client.PostAsJsonAsync("/api/v1/auth/register", credentials)).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", credentials);
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Igual que el frontend: login con <c>?useCookies=true</c> y la sesión viaja en la cookie HttpOnly.</summary>
    public async Task<HttpClient> CreateCookieClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var credentials = new { Email = $"cookie-{Guid.NewGuid():N}@jobpilot.test", Password = "Test-Passw0rd!" };

        (await client.PostAsJsonAsync("/api/v1/auth/register", credentials)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/auth/login?useCookies=true", credentials)).EnsureSuccessStatusCode();
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<JobPilotApiFactory>;

/// <summary>
/// Embeddings deterministas para tests: una dimensión por tecnología + una de "bias" (evita vectores nulos).
/// Permite probar pgvector y la búsqueda semántica sin llamar a un modelo real.
/// </summary>
internal sealed class FakeEmbeddingService : IEmbeddingService
{
    private static readonly string[] Vocabulary = ["python", "c#", ".net", "react", "java", "sql", "docker", "kubernetes"];

    public bool IsEnabled => true;

    public Task<Result<float[]>> GenerateAsync(string text, CancellationToken cancellationToken)
    {
        var lower = text.ToLowerInvariant();
        float[] vector = [0.1f, .. Vocabulary.Select(word => lower.Contains(word, StringComparison.Ordinal) ? 1f : 0f)];
        return Task.FromResult(Result.Success(vector));
    }
}
