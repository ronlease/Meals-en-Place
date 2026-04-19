namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// One group of unresolved container references sharing a canonical ingredient
/// and notes phrase. Returned by
/// <see cref="IContainerResolutionService.GetUnresolvedGroupsAsync"/> so the
/// user can resolve a recurring phrase (e.g. "1 can diced tomatoes") once
/// across every recipe that uses it after a bulk ingest (MEP-026).
/// </summary>
public sealed class UnresolvedGroup
{
    /// <summary>The canonical ingredient id shared by every occurrence.</summary>
    public Guid CanonicalIngredientId { get; init; }

    /// <summary>Display name for <see cref="CanonicalIngredientId"/>.</summary>
    public string CanonicalIngredientName { get; init; } = string.Empty;

    /// <summary>
    /// The original notes text used as the matching key. Preserved as trimmed
    /// display form; matched case-insensitively against persisted
    /// <c>RecipeIngredient.Notes</c> values during bulk resolve.
    /// </summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>How many unresolved ingredients are in this group.</summary>
    public int OccurrenceCount { get; init; }
}
