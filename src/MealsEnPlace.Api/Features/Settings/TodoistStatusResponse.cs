namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Response shape for <c>GET /api/v1/settings/todoist/status</c>. MEP-028
/// scope: `configured` reflects whether the <c>Todoist:Token</c> user secret
/// is populated. The raw token is never exposed.
/// </summary>
public sealed class TodoistStatusResponse
{
    /// <summary>True when the Todoist integration has a non-empty token available.</summary>
    public bool Configured { get; init; }
}
