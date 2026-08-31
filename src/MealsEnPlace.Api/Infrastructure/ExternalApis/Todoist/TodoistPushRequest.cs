namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Optional request body for Todoist push endpoints (MEP-036). When supplied,
/// <see cref="ProjectId"/> overrides the static <c>Todoist:ProjectId</c> configuration
/// for this push only — the user secret is never modified.
/// </summary>
public sealed class TodoistPushRequest
{
    /// <summary>
    /// Todoist project ID to target for this push. Null or absent pushes to Inbox
    /// (Todoist's default). When omitted from the request body entirely, the push
    /// target falls back to <c>Todoist:ProjectId</c> in configuration.
    /// </summary>
    public string? ProjectId { get; init; }
}
