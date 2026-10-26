using JobPilot.Domain.Abstractions;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Resumes;

namespace JobPilot.Application.Abstractions.AI;

// La capa Application define QUÉ necesita de la IA; Infrastructure decide CÓMO (Claude, Ollama, OpenAI...).
// Esto es el Principio de Inversión de Dependencias (la "D" de SOLID).

public interface IJobPostingAnalyzer
{
    Task<Result<JobPostingAnalysis>> AnalyzeAsync(string jobDescription, CancellationToken cancellationToken);
}

public interface ICoverLetterWriter
{
    Task<Result<string>> WriteAsync(
        JobApplication application,
        string companyName,
        Resume resume,
        string language,
        CancellationToken cancellationToken);
}

public interface IEmbeddingService
{
    bool IsEnabled { get; }

    Task<Result<float[]>> GenerateAsync(string text, CancellationToken cancellationToken);
}

public static class AiErrors
{
    public static readonly Error ChatNotConfigured = Error.Unavailable(
        "AI.ChatNotConfigured",
        "No chat model is configured. Set AI:Chat:Provider (Anthropic, Ollama or OpenAI).");

    public static readonly Error EmbeddingsNotConfigured = Error.Unavailable(
        "AI.EmbeddingsNotConfigured",
        "No embedding model is configured. Set AI:Embeddings:Provider (Ollama or OpenAI).");

    public static readonly Error ProviderUnavailable = Error.Unavailable(
        "AI.ProviderUnavailable",
        "The AI provider could not be reached. Check the endpoint, API key and model name.");

    public static readonly Error InvalidResponse = Error.Failure(
        "AI.InvalidResponse",
        "The model returned a response that could not be parsed.");
}
