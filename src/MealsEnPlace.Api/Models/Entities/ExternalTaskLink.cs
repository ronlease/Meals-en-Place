namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Records that a local <see cref="ShoppingListItem"/> or <see cref="MealPlanSlot"/>
/// has been pushed to an external task provider (MEP-028 / MEP-029). Keyed by
/// <see cref="SourceType"/> + <see cref="SourceId"/> + <see cref="Provider"/>,
/// this row is what makes re-push idempotent: the push target compares the
/// current content hash against <see cref="ContentHash"/> to decide whether
/// to no-op, PATCH the remote task, or leave the existing task alone.
/// <para>
/// <see cref="ExternalProjectId"/> captures which project the task was pushed
/// into, enabling MEP-036 to surface previously-used project IDs as quick-pick
/// targets without an extra Todoist API call.
/// </para>
/// </summary>
public class ExternalTaskLink
{
    /// <summary>
    /// SHA-256 hash (hex-lowercase) of the task content at the time of the
    /// most recent push. When re-pushing, the target recomputes the hash and
    /// compares — equal means no-op, different means PATCH the remote task.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Optional external project identifier. Null signals "default project"
    /// (Inbox for Todoist). Populated at push time so MEP-036 can enumerate
    /// previously-used projects without calling the provider's API.
    /// </summary>
    public string? ExternalProjectId { get; set; }

    /// <summary>The identifier of the pushed task in the external provider's system.</summary>
    public string ExternalTaskId { get; set; } = string.Empty;

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// External provider name — e.g., <c>"Todoist"</c>. A string rather than
    /// an enum so additional providers can be added without a schema change.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the most recent push.</summary>
    public DateTime PushedAt { get; set; }

    /// <summary>
    /// Id of the source entity — either <see cref="ShoppingListItem.Id"/>
    /// or <see cref="MealPlanSlot.Id"/> depending on <see cref="SourceType"/>.
    /// Not enforced as a foreign key because the source entity may be
    /// deleted independently; the push target then detects the missing row
    /// via <see cref="SourceScope"/> and closes the remote task.
    /// </summary>
    public Guid SourceId { get; set; }

    /// <summary>
    /// Identifies the push scope this link originally belonged to — the
    /// meal plan id for meal-plan-bound items, or the sentinel
    /// <c>"standalone"</c> for the non-meal-plan shopping list. A re-push
    /// filters by <see cref="SourceScope"/> to detect links whose source
    /// row has been removed from the scope, so the push target can close
    /// those remote tasks without needing to join the (possibly deleted)
    /// source entity.
    /// </summary>
    public string SourceScope { get; set; } = string.Empty;

    /// <summary>Discriminator identifying which local entity <see cref="SourceId"/> refers to.</summary>
    public ExternalTaskSource SourceType { get; set; }
}
