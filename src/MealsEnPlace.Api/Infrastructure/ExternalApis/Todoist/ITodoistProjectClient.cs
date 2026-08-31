namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Fetches the list of Todoist projects for a given API token.
/// Used by the MEP-036 project-history endpoint to resolve project IDs to display names.
/// Distinct from <see cref="ITodoistTestClient"/> (which exists only for token validation)
/// and <see cref="ITodoistClient"/> (which handles task CRUD using the resolved token).
/// </summary>
public interface ITodoistProjectClient
{
    /// <summary>
    /// Returns all Todoist projects accessible by <paramref name="token"/>.
    /// On network error, non-success HTTP status, or parse failure the result has
    /// <see cref="TodoistProjectListResult.Succeeded"/> = false and an
    /// <see cref="TodoistProjectListResult.ErrorMessage"/> — it never throws.
    /// </summary>
    Task<TodoistProjectListResult> GetProjectsAsync(
        string token, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="ITodoistProjectClient.GetProjectsAsync"/>.</summary>
public sealed class TodoistProjectListResult
{
    /// <summary>Error message when the call failed; null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Projects returned by Todoist. Empty when <see cref="Succeeded"/> is false.</summary>
    public IReadOnlyList<TodoistProject> Projects { get; init; } = [];

    /// <summary>True when the call completed with a 2xx status and the body was parsed.</summary>
    public bool Succeeded { get; init; }
}

/// <summary>A Todoist project name and ID pair.</summary>
public sealed class TodoistProject
{
    /// <summary>The Todoist-assigned project ID (e.g., <c>"2331547980"</c>).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>User-visible project name (e.g., <c>"Groceries"</c>).</summary>
    public string Name { get; init; } = string.Empty;
}
