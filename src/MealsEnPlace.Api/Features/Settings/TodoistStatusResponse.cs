namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Response shape for <c>GET /api/v1/settings/todoist/status</c>. The flag
/// reflects whether the Todoist token resolver finds a non-whitespace token in
/// either the encrypted Settings-page store (MEP-035) or the legacy
/// <c>Todoist:Token</c> user-secret fallback. The raw token is never exposed.
/// </summary>
public sealed class TodoistStatusResponse
{
    /// <summary>True when the Todoist integration has a non-empty token available.</summary>
    public bool Configured { get; init; }
}
