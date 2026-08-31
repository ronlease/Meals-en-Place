namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Assembles the Todoist project quick-pick history list (MEP-036). Reads
/// previously-used project IDs from <c>ExternalTaskLink</c>, resolves display names
/// via a single <c>GET /api/v1/projects</c> call, and merges them into a single
/// response. Always includes the Inbox sentinel. Name resolution is best-effort —
/// a failed or unconfigured Todoist call degrades gracefully to raw IDs.
/// </summary>
public interface ITodoistProjectHistoryService
{
    /// <summary>Returns the merged project history response.</summary>
    Task<TodoistProjectHistoryResponse> GetProjectHistoryAsync(
        CancellationToken cancellationToken = default);
}
