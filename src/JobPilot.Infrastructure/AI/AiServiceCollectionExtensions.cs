using Anthropic;
using JobPilot.Application.Abstractions.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OpenAI;

namespace JobPilot.Infrastructure.AI;

internal static class AiServiceCollectionExtensions
{
    private static readonly Uri DefaultOllamaEndpoint = new("http://localhost:11434");

    /// <summary>
    /// Factory: según la configuración se elige el proveedor. El resto de la app solo ve <see cref="IChatClient"/>
    /// e <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> (abstracciones de Microsoft.Extensions.AI).
    /// </summary>
    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        var disabled = new DisabledAiServices();

        var chatClient = CreateChatClient(options.Chat);
        if (chatClient is null)
        {
            services.AddSingleton<IJobPostingAnalyzer>(disabled);
            services.AddSingleton<ICoverLetterWriter>(disabled);
        }
        else
        {
            // ChatClientBuilder = Decorator pattern: cada Use* envuelve al cliente con una capa extra.
            services.AddChatClient(chatClient)
                .UseOpenTelemetry()
                .UseLogging();

            services.AddScoped<IJobPostingAnalyzer, LlmJobPostingAnalyzer>();
            services.AddScoped<ICoverLetterWriter, LlmCoverLetterWriter>();
        }

        var embeddingGenerator = CreateEmbeddingGenerator(options.Embeddings);
        if (embeddingGenerator is null)
        {
            services.AddSingleton<IEmbeddingService>(disabled);
        }
        else
        {
            services.AddEmbeddingGenerator(embeddingGenerator)
                .UseOpenTelemetry()
                .UseLogging();

            services.AddScoped<IEmbeddingService, EmbeddingService>();
        }

        return services;
    }

    private static IChatClient? CreateChatClient(AiProviderOptions options) => options.Provider switch
    {
        AiProvider.Ollama => new OllamaApiClient(options.Endpoint ?? DefaultOllamaEndpoint, options.Model ?? "llama3.2"),
        AiProvider.Anthropic => new AnthropicClient { ApiKey = RequireApiKey(options) }
            .AsIChatClient(options.Model ?? "claude-haiku-5-5"),
        AiProvider.OpenAI => new OpenAIClient(RequireApiKey(options))
            .GetChatClient(options.Model ?? "gpt-5-mini")
            .AsIChatClient(),
        _ => null
    };

    private static IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(AiProviderOptions options) => options.Provider switch
    {
        AiProvider.Ollama => new OllamaApiClient(options.Endpoint ?? DefaultOllamaEndpoint, options.Model ?? "nomic-embed-text"),
        AiProvider.OpenAI => new OpenAIClient(RequireApiKey(options))
            .GetEmbeddingClient(options.Model ?? "text-embedding-3-small")
            .AsIEmbeddingGenerator(),
        AiProvider.Anthropic => throw new InvalidOperationException(
            "Anthropic does not provide an embeddings API. Use Ollama or OpenAI for AI:Embeddings."),
        _ => null
    };

    private static string RequireApiKey(AiProviderOptions options) =>
        options.ApiKey ?? throw new InvalidOperationException(
            $"AI provider '{options.Provider}' requires an ApiKey (use: dotnet user-secrets set \"AI:Chat:ApiKey\" \"...\").");
}
