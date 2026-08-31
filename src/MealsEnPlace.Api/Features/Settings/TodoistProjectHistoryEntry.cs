namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// A single push-target entry in the Todoist project quick-pick list (MEP-036).
/// </summary>
public sealed class TodoistProjectHistoryEntry
{
    /// <summary>
    /// Human-readable project name. Always <c>"Inbox (default)"</c> when
    /// <see cref="IsInbox"/> is true. Null for history entries when
    /// <see cref="TodoistProjectHistoryResponse.NamesResolved"/> is false.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>True only for the special Inbox sentinel entry.</summary>
    public bool IsInbox { get; init; }

    /// <summary>
    /// Todoist project ID (e.g., <c>"2331547980"</c>). Null for the Inbox entry —
    /// a push with a null project ID lands in Todoist's default Inbox.
    /// </summary>
    public string? ProjectId { get; init; }
}
