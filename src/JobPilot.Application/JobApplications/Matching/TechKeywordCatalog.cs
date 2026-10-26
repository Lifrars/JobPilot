namespace JobPilot.Application.JobApplications.Matching;

/// <summary>Catálogo de tecnologías para detectar skills sin IA (modo fallback).</summary>
internal static class TechKeywordCatalog
{
    private static readonly string[] Keywords =
    [
        "C#", ".NET", "ASP.NET Core", "Entity Framework", "LINQ", "Blazor", "SQL", "SQL Server", "PostgreSQL", "MySQL",
        "MongoDB", "Redis", "Elasticsearch", "Docker", "Kubernetes", "Azure", "AWS", "GCP", "Terraform", "CI/CD",
        "GitHub Actions", "Azure DevOps", "Git", "REST", "GraphQL", "gRPC", "Microservices", "RabbitMQ", "Kafka",
        "Python", "Java", "Spring", "JavaScript", "TypeScript", "React", "Angular", "Vue", "Node.js", "Rust",
        "LLM", "RAG", "OpenAI", "Machine Learning", "DDD", "Clean Architecture", "CQRS", "TDD", "xUnit",
        "OpenTelemetry", "Linux", "Scrum"
    ];

    public static IEnumerable<string> FindIn(string text) => Keywords.Where(keyword => Mentions(text, keyword));

    /// <summary>
    /// Búsqueda "por palabra completa" que soporta símbolos (C#, .NET, Node.js):
    /// \b de Regex falla con esos caracteres, así que validamos los bordes a mano.
    /// </summary>
    public static bool Mentions(string text, string keyword)
    {
        var index = 0;
        while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var end = index + keyword.Length;
            var startsClean = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var endsClean = end == text.Length || !char.IsLetterOrDigit(text[end]);

            if (startsClean && endsClean)
            {
                return true;
            }

            index = end;
        }

        return false;
    }
}
