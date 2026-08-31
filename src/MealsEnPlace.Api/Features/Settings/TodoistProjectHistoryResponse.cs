namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Response shape for <c>GET /api/v1/settings/todoist/projects/history</c> (MEP-036).
/// Merges locally-recorded project IDs from <c>ExternalTaskLink</c> with live display
/// names resolved in a single <c>GET /rest/v2/projects</c> call. The Inbox sentinel is
/// always present as the first entry regardless of push history.
/// </summary>
public sealed class TodoistProjectHistoryResponse
{
    /// <summary>
    /// Project ID used in the most recent meal-plan push, or null when no meal-plan
    /// push has been recorded or when the last push targeted Inbox.
    /// Derived from the most recent <c>ExternalTaskLink</c> row for
    /// <c>SourceType = MealPlanSlot</c> — no separate state is stored.
    /// </summary>
    public string? LastUsedMealPlanProjectId { get; init; }

    /// <summary>
    /// Project ID used in the most recent shopping-list push, or null when no
    /// shopping-list push has been recorded or when the last push targeted Inbox.
    /// Derived from the most recent <c>ExternalTaskLink</c> row for
    /// <c>SourceType = ShoppingListItem</c>.
    /// </summary>
    public string? LastUsedShoppingListProjectId { get; init; }

    /// <summary>
    /// Error message describing why name resolution failed. Null when
    /// <see cref="NamesResolved"/> is true.
    /// </summary>
    public string? NameResolutionError { get; init; }

    /// <summary>
    /// True when the <c>GET /rest/v2/projects</c> call succeeded and
    /// <see cref="TodoistProjectHistoryEntry.DisplayName"/> is populated for all
    /// history entries. False when the call failed or no token is configured —
    /// the UI should show raw IDs and a hint that names could not be loaded.
    /// </summary>
    public bool NamesResolved { get; init; }

    /// <summary>
    /// Ordered push-target list. The Inbox sentinel is always first, followed by
    /// previously-used projects in the order Todoist returns them (or ID-only when
    /// <see cref="NamesResolved"/> is false).
    /// </summary>
    public IReadOnlyList<TodoistProjectHistoryEntry> Projects { get; init; } = [];
}
