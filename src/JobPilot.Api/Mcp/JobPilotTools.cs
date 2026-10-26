using System.ComponentModel;
using JobPilot.Application.Abstractions.Messaging;
using JobPilot.Application.JobApplications;
using JobPilot.Domain.Abstractions;
using ModelContextProtocol.Server;

namespace JobPilot.Api.Mcp;

/// <summary>
/// Servidor MCP (Model Context Protocol): expone JobPilot como "herramientas" para agentes de IA
/// (Claude Code, Claude Desktop, etc.). Reutiliza los mismos casos de uso que la API REST vía ISender:
/// es otra "puerta de entrada" a la capa Application, igual que los endpoints HTTP.
/// </summary>
[McpServerToolType]
public sealed class JobPilotTools
{
    [McpServerTool(Name = "list_applications")]
    [Description("Lists the user's job applications, optionally filtered by status.")]
    public static async Task<object> ListApplications(
        ISender sender,
        [Description("Optional status: Wishlist, Applied, Screening, Interviewing, Offer, Accepted, Rejected, Withdrawn.")]
        string? status = null,
        CancellationToken cancellationToken = default) =>
        ToToolResult(await sender.QueryAsync(new ListJobApplicationsQuery(status, null, null, 1, 50), cancellationToken));

    [McpServerTool(Name = "get_application")]
    [Description("Gets the full detail of a job application: status history, interviews and AI analysis.")]
    public static async Task<object> GetApplication(
        ISender sender,
        [Description("Job application id (GUID).")] Guid id,
        CancellationToken cancellationToken = default) =>
        ToToolResult(await sender.QueryAsync(new GetJobApplicationQuery(id), cancellationToken));

    [McpServerTool(Name = "get_pipeline_stats")]
    [Description("Returns job search metrics: totals per status, active applications and response rate.")]
    public static async Task<object> GetPipelineStats(ISender sender, CancellationToken cancellationToken = default) =>
        ToToolResult(await sender.QueryAsync(new GetPipelineStatsQuery(), cancellationToken));

    [McpServerTool(Name = "search_applications")]
    [Description("Semantic search over saved job postings (requires embeddings to be configured).")]
    public static async Task<object> SearchApplications(
        ISender sender,
        [Description("Natural language query, e.g. 'remote backend roles with Kubernetes'.")] string query,
        CancellationToken cancellationToken = default) =>
        ToToolResult(await sender.QueryAsync(new SearchJobApplicationsQuery(query), cancellationToken));

    [McpServerTool(Name = "change_application_status")]
    [Description("Moves a job application to a new status. Only valid transitions are accepted.")]
    public static async Task<object> ChangeStatus(
        ISender sender,
        [Description("Job application id (GUID).")] Guid id,
        [Description("Target status.")] string status,
        CancellationToken cancellationToken = default) =>
        ToToolResult(await sender.SendAsync(new ChangeJobApplicationStatusCommand(id, status), cancellationToken));

    private static object ToToolResult<T>(Result<T> result) =>
        result.IsSuccess ? result.Value! : $"Error {result.Error.Code}: {result.Error.Description}";
}
