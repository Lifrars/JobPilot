using JobPilot.Application.JobApplications.Matching;
using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Resumes;

namespace JobPilot.Application.UnitTests;

public sealed class MatchStrategyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static JobApplication Job(string description) =>
        JobApplication.Create(CompanyId.New(), "Backend Developer", null, description, null, WorkMode.Remote, Now).Value;

    private static Resume Cv(string content) => Resume.Create("CV", content, Now).Value;

    [Theory]
    [InlineData("I work with C# daily", "C#", true)]
    [InlineData("Experience with .NET 10", ".NET", true)]
    [InlineData("PostgreSQL and Redis", "SQL", false)]
    [InlineData("JavaScript developer", "Java", false)]
    [InlineData("Node.js, React", "Node.js", true)]
    public void Mentions_RespectsWordBoundariesWithSymbols(string text, string keyword, bool expected) =>
        TechKeywordCatalog.Mentions(text, keyword).ShouldBe(expected);

    [Fact]
    public void Keyword_WithoutAnalysis_UsesCatalogAndReportsMissingSkills()
    {
        var job = Job("We need C#, PostgreSQL, Docker and Kubernetes.");
        var cv = Cv("Senior dev: C#, PostgreSQL, Docker.");

        var score = new KeywordMatchStrategy().Score(job, cv).Value;

        score.Score.ShouldBe(75);
        score.MissingSkills.ShouldBe(["Kubernetes"]);
    }

    [Fact]
    public void Keyword_WithAnalysis_WeighsNiceToHaveAtHalf()
    {
        var job = Job("irrelevant");
        job.AttachAnalysis(
            new JobPostingAnalysis(["C#", "Azure"], ["Kafka"], "Senior", WorkMode.Remote, null, null, null, [], "", Now),
            Now);

        var score = new KeywordMatchStrategy().Score(job, Cv("C# and Kafka")).Value;

        // (1 + 0.5) / (1 + 1 + 0.5) = 60 %
        score.Score.ShouldBe(60);
    }

    [Fact]
    public void Keyword_WithNoDetectableSkills_Fails() =>
        new KeywordMatchStrategy().Score(Job("Great team, nice office."), Cv("C#")).Error.ShouldBe(MatchErrors.NoSkillsFound);

    [Fact]
    public void Embedding_ComputesCosineSimilarity()
    {
        var job = Job("C#");
        var cv = Cv("C#");
        job.SetEmbedding([1, 0, 0]);
        cv.SetEmbedding([1, 0, 0]);

        new EmbeddingMatchStrategy().Score(job, cv).Value.Score.ShouldBe(100);
    }

    [Fact]
    public void Hybrid_WithoutEmbeddings_FallsBackToKeywords()
    {
        var hybrid = new HybridMatchStrategy(new KeywordMatchStrategy(), new EmbeddingMatchStrategy());

        var score = hybrid.Score(Job("C# and Docker"), Cv("C#")).Value;

        score.Strategy.ShouldBe("hybrid");
        score.Score.ShouldBe(50);
    }

    [Fact]
    public void Hybrid_WithBoth_WeighsSixtyForty()
    {
        var job = Job("C# and Docker");
        var cv = Cv("C#");
        job.SetEmbedding([1, 0]);
        cv.SetEmbedding([1, 0]);

        var score = new HybridMatchStrategy(new KeywordMatchStrategy(), new EmbeddingMatchStrategy()).Score(job, cv).Value;

        // 0.6 * 50 + 0.4 * 100 = 70
        score.Score.ShouldBe(70);
    }
}
