using System.Text.Json.Serialization;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Paginated envelope returned by Todoist API v1 list endpoints such as
/// <c>GET /api/v1/projects</c>. When <see cref="NextCursor"/> is non-null,
/// pass it as the <c>cursor</c> query-string parameter to fetch the next page.
/// A null <see cref="NextCursor"/> signals the final page.
/// </summary>
internal sealed class TodoistProjectPageEnvelope
{
    /// <summary>Cursor value for the next page. Null on the last page.</summary>
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }

    /// <summary>Items on this page.</summary>
    [JsonPropertyName("results")]
    public List<TodoistProjectEnvelopeItem>? Results { get; init; }
}

/// <summary>A single project entry within a <see cref="TodoistProjectPageEnvelope"/>.</summary>
internal sealed class TodoistProjectEnvelopeItem
{
    /// <summary>Todoist-assigned project ID.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>User-visible project name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
