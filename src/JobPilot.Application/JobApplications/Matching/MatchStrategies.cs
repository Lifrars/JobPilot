using System.Numerics.Tensors;
using JobPilot.Domain.Abstractions;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Resumes;

namespace JobPilot.Application.JobApplications.Matching;

public sealed record MatchScore(
    string Strategy,
    double Score,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    string Explanation);

/// <summary>
/// Strategy pattern: distintas formas de calcular qué tan bien encaja tu CV con una oferta.
/// El handler elige la estrategia por nombre en tiempo de ejecución, sin if/else.
/// </summary>
public interface IMatchScoringStrategy
{
    string Name { get; }

    Result<MatchScore> Score(JobApplication application, Resume resume);
}

public static class MatchErrors
{
    public static readonly Error NoSkillsFound = Error.Validation(
        "Match.NoSkillsFound",
        "No skills could be detected in the job posting. Run the AI analysis first or add a description.");

    public static readonly Error EmbeddingsMissing = Error.Validation(
        "Match.EmbeddingsMissing",
        "The job application and the resume both need embeddings (configure AI:Embeddings and wait for processing).");

    public static Error UnknownStrategy(string name) =>
        Error.Validation("Match.UnknownStrategy", $"Unknown strategy '{name}'. Use keyword, embedding or hybrid.");
}

/// <summary>Coincidencia de skills: usa el análisis de IA si existe; si no, un catálogo de keywords técnicas.</summary>
public sealed class KeywordMatchStrategy : IMatchScoringStrategy
{
    private const double NiceToHaveWeight = 0.5;

    public string Name => "keyword";

    public Result<MatchScore> Score(JobApplication application, Resume resume)
    {
        var weighted = application.Analysis is { } analysis
            ? analysis.RequiredSkills.Select(s => (Skill: s, Weight: 1.0))
                .Concat(analysis.NiceToHaveSkills.Select(s => (Skill: s, Weight: NiceToHaveWeight)))
            : TechKeywordCatalog.FindIn(application.ToEmbeddingText()).Select(s => (Skill: s, Weight: 1.0));

        var skills = weighted
            .DistinctBy(s => s.Skill, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (skills.Count == 0)
        {
            return MatchErrors.NoSkillsFound;
        }

        var matched = skills.Where(s => TechKeywordCatalog.Mentions(resume.Content, s.Skill)).ToList();
        var score = matched.Sum(s => s.Weight) / skills.Sum(s => s.Weight) * 100;

        return new MatchScore(
            Name,
            Math.Round(score, 1),
            [.. matched.Select(s => s.Skill)],
            [.. skills.Except(matched).Select(s => s.Skill)],
            application.Analysis is null
                ? "Skills detected with the built-in keyword catalog (run /analyze for better results)."
                : "Skills taken from the AI analysis (nice-to-have skills weigh half).");
    }
}

/// <summary>Similitud semántica (coseno) entre los embeddings de la oferta y del CV.</summary>
public sealed class EmbeddingMatchStrategy : IMatchScoringStrategy
{
    public string Name => "embedding";

    public Result<MatchScore> Score(JobApplication application, Resume resume)
    {
        if (application.Embedding is not { } jobVector || resume.Embedding is not { } resumeVector ||
            jobVector.Length != resumeVector.Length)
        {
            return MatchErrors.EmbeddingsMissing;
        }

        // TensorPrimitives usa SIMD (AVX/NEON): mucho más rápido que un bucle manual.
        var similarity = TensorPrimitives.CosineSimilarity(jobVector, resumeVector);
        var score = Math.Round(Math.Clamp(similarity, 0f, 1f) * 100, 1);

        return new MatchScore(Name, score, [], [], $"Cosine similarity between embeddings: {similarity:0.000}.");
    }
}

/// <summary>Composite + Strategy: combina las otras dos estrategias y degrada con elegancia si falta una.</summary>
public sealed class HybridMatchStrategy(KeywordMatchStrategy keyword, EmbeddingMatchStrategy embedding) : IMatchScoringStrategy
{
    private const double KeywordWeight = 0.6;
    private const double EmbeddingWeight = 0.4;

    public string Name => "hybrid";

    public Result<MatchScore> Score(JobApplication application, Resume resume)
    {
        var byKeyword = keyword.Score(application, resume);
        var byEmbedding = embedding.Score(application, resume);

        return (byKeyword.IsSuccess, byEmbedding.IsSuccess) switch
        {
            (true, true) => new MatchScore(
                Name,
                Math.Round(byKeyword.Value.Score * KeywordWeight + byEmbedding.Value.Score * EmbeddingWeight, 1),
                byKeyword.Value.MatchedSkills,
                byKeyword.Value.MissingSkills,
                $"{KeywordWeight:P0} keywords ({byKeyword.Value.Score}) + {EmbeddingWeight:P0} semantic ({byEmbedding.Value.Score})."),
            (true, false) => byKeyword.Value with { Strategy = Name, Explanation = "Only keywords (no embeddings available)." },
            (false, true) => byEmbedding.Value with { Strategy = Name, Explanation = "Only semantic similarity (no skills detected)." },
            _ => byKeyword.Error
        };
    }
}
