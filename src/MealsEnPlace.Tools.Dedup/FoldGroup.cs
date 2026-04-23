namespace MealsEnPlace.Tools.Dedup;

/// <summary>
/// A survivor canonical ingredient plus the loser rows whose foreign keys
/// the dedup runner will reassign to it. Produced by
/// <see cref="FoldGroupResolver"/> and consumed by the runner.
/// </summary>
internal sealed class FoldGroup
{
    public FoldGroup(CanonicalIngredientFoldCandidate survivor, IReadOnlyList<CanonicalIngredientFoldCandidate> losers)
    {
        Losers = losers;
        NormalizedKey = survivor.NormalizedKey;
        Survivor = survivor;
    }

    public IReadOnlyList<CanonicalIngredientFoldCandidate> Losers { get; }

    public string NormalizedKey { get; }

    public CanonicalIngredientFoldCandidate Survivor { get; }

    public int TotalReassignedReferenceCount => Losers.Sum(l => l.ReferenceCount);
}

/// <summary>
/// Snapshot of a <see cref="MealsEnPlace.Api.Models.Entities.CanonicalIngredient"/>
/// row plus its normalized-group key and its aggregate foreign-key usage count
/// across every child table. Used as the input to <see cref="FoldGroupResolver"/>
/// and carried through to the runner so survivor selection and reassignment can
/// happen without re-querying.
/// </summary>
internal sealed class CanonicalIngredientFoldCandidate
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string NormalizedKey { get; init; }

    /// <summary>
    /// Sum of foreign-key references to this canonical row across
    /// RecipeIngredient, InventoryItem, ShoppingListItem, SeasonalityWindow,
    /// and ConsumeAuditEntry. Used as the first tie-breaker in survivor
    /// selection — the most-used row absorbs the rest.
    /// </summary>
    public required int ReferenceCount { get; init; }
}
