namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// One row in the unresolved container-reference review queue, grouped by
/// canonical ingredient and notes phrase.
/// </summary>
public sealed class UnresolvedGroupResponse
{
    /// <summary>The canonical ingredient id shared by every occurrence.</summary>
    public Guid CanonicalIngredientId { get; init; }

    /// <summary>Display name for <see cref="CanonicalIngredientId"/>.</summary>
    public string CanonicalIngredientName { get; init; } = string.Empty;

    /// <summary>The notes phrase that identifies the group (trimmed display form).</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>How many unresolved ingredients share this group key.</summary>
    public int OccurrenceCount { get; init; }
}
