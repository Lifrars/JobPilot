using JobPilot.Application.Abstractions.AI;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Resumes;
using Microsoft.Extensions.AI;

namespace JobPilot.Infrastructure.AI;

/// <summary>
/// Extrae datos estructurados de una oferta. <c>GetResponseAsync&lt;T&gt;</c> genera un JSON Schema a partir
/// de <see cref="AnalysisPayload"/> y obliga al modelo a responder con ese formato (structured output).
/// </summary>
internal sealed class LlmJobPostingAnalyzer(IChatClient chatClient, TimeProvider timeProvider) : IJobPostingAnalyzer
{
    private const string SystemPrompt = """
        You are an expert technical recruiter. Extract structured data from the job posting the user sends.
        - RequiredSkills / NiceToHaveSkills: concrete technologies or practices with short canonical names
          (e.g. "C#", "ASP.NET Core", "PostgreSQL", "Docker", "Clean Architecture").
        - Seniority: one of Intern, Junior, SemiSenior, Senior, Lead, Unknown.
        - WorkMode: one of Remote, Hybrid, OnSite, Unknown.
        - SalaryMin / SalaryMax / Currency (ISO 4217): only if explicitly stated, otherwise null.
        - RedFlags: warning signs for a candidate (unpaid take-home projects, unrealistic requirements,
          vague compensation, "rockstar/ninja", etc.). Empty list if none.
        - Summary: two sentences, in the same language as the posting.
        """;

    public async Task<Result<JobPostingAnalysis>> AnalyzeAsync(string jobDescription, CancellationToken cancellationToken)
    {
        ChatResponse<AnalysisPayload> response;
        try
        {
            response = await chatClient.GetResponseAsync<AnalysisPayload>(
                [new ChatMessage(ChatRole.System, SystemPrompt), new ChatMessage(ChatRole.User, jobDescription)],
                cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return AiErrors.ProviderUnavailable;
        }

        if (!response.TryGetResult(out var payload))
        {
            return AiErrors.InvalidResponse;
        }

        return new JobPostingAnalysis(
            payload.RequiredSkills ?? [],
            payload.NiceToHaveSkills ?? [],
            payload.Seniority ?? "Unknown",
            Enum.TryParse<WorkMode>(payload.WorkMode, ignoreCase: true, out var workMode) ? workMode : WorkMode.Unknown,
            payload.SalaryMin,
            payload.SalaryMax,
            payload.Currency,
            payload.RedFlags ?? [],
            payload.Summary ?? string.Empty,
            timeProvider.GetUtcNow());
    }
}

internal sealed record AnalysisPayload(
    string[]? RequiredSkills,
    string[]? NiceToHaveSkills,
    string? Seniority,
    string? WorkMode,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Currency,
    string[]? RedFlags,
    string? Summary);

internal sealed class LlmCoverLetterWriter(IChatClient chatClient) : ICoverLetterWriter
{
    public async Task<Result<string>> WriteAsync(
        JobApplication application,
        string companyName,
        Resume resume,
        string language,
        CancellationToken cancellationToken)
    {
        var skills = application.Analysis is { } analysis ? string.Join(", ", analysis.RequiredSkills) : "not analyzed";

        var prompt = $"""
            Write a concise cover letter (max 250 words) in language "{language}".
            Be specific: connect real experience from the resume with the role's requirements. Do not invent experience.

            ## Role
            {application.Title} at {companyName}
            Key skills: {skills}

            ## Job description
            {application.JobDescription}

            ## Candidate resume
            {resume.Content}
            """;

        try
        {
            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            return response.Text;
        }
        catch (HttpRequestException)
        {
            return AiErrors.ProviderUnavailable;
        }
    }
}

internal sealed class EmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator) : IEmbeddingService
{
    public bool IsEnabled => true;

    public async Task<Result<float[]>> GenerateAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var vector = await generator.GenerateVectorAsync(text, cancellationToken: cancellationToken);
            return vector.ToArray();
        }
        catch (HttpRequestException)
        {
            return AiErrors.ProviderUnavailable;
        }
    }
}

/// <summary>
/// Null Object pattern: cuando no hay IA configurada, la app sigue funcionando y los endpoints de IA
/// responden 503 con un mensaje claro, sin <c>if (ai is null)</c> repartidos por el código.
/// </summary>
internal sealed class DisabledAiServices : IJobPostingAnalyzer, ICoverLetterWriter, IEmbeddingService
{
    public bool IsEnabled => false;

    public Task<Result<JobPostingAnalysis>> AnalyzeAsync(string jobDescription, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<JobPostingAnalysis>(AiErrors.ChatNotConfigured));

    public Task<Result<string>> WriteAsync(JobApplication application, string companyName, Resume resume, string language, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<string>(AiErrors.ChatNotConfigured));

    public Task<Result<float[]>> GenerateAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<float[]>(AiErrors.EmbeddingsNotConfigured));
}
