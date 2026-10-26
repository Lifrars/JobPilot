namespace JobPilot.Infrastructure.AI;

public enum AiProvider
{
    None,
    Ollama,
    Anthropic,
    OpenAI
}

/// <summary>Options pattern: la sección "AI" de appsettings / user-secrets se enlaza a esta clase tipada.</summary>
public sealed class AiOptions
{
    public const string SectionName = "AI";

    public AiProviderOptions Chat { get; set; } = new();

    public AiProviderOptions Embeddings { get; set; } = new();
}

public sealed class AiProviderOptions
{
    public AiProvider Provider { get; set; } = AiProvider.None;

    public string? Model { get; set; }

    public Uri? Endpoint { get; set; }

    /// <summary>Nunca en appsettings.json: usar <c>dotnet user-secrets</c> o variables de entorno.</summary>
    public string? ApiKey { get; set; }
}
