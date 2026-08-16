namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Response shape for <c>POST /api/v1/settings/todoist/test</c>. Carries the
/// outcome of the live <c>GET /rest/v2/projects</c> probe plus an optional
/// error message when the call failed.
/// </summary>
public sealed class TodoistTokenTestResponse
{
    /// <summary>Todoist-reported error message on failure. Null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when the test call to Todoist succeeded.</summary>
    public bool Success { get; init; }
}
