namespace MealsEnPlace.Tools.Dedup;

/// <summary>
/// Groups <see cref="CanonicalIngredientFoldCandidate"/> entries by their
/// <see cref="CanonicalIngredientFoldCandidate.NormalizedKey"/> and picks a
/// survivor per multi-member group. Pure function of the input — no DB access.
/// <para>
/// Survivor selection rule (first to distinguish wins):
/// <list type="number">
///   <item><description>Shortest <c>Name</c> (most generic wins).</description></item>
///   <item><description>Highest <c>ReferenceCount</c> (most-used row absorbs the rest so matching history is preserved).</description></item>
///   <item><description>Alphabetical <c>Name</c> (stable final tie-breaker).</description></item>
/// </list>
/// </para>
/// </summary>
internal static class FoldGroupResolver
{
    /// <summary>
    /// Builds fold groups for every normalized key that has two or more
    /// candidates. Single-member groups are skipped (nothing to fold).
    /// Candidates with an empty normalized key are also skipped — those
    /// come from canonical rows whose name was entirely stopwords or
    /// punctuation, and the right call for those is manual review, not
    /// automatic folding.
    /// </summary>
    public static IReadOnlyList<FoldGroup> Resolve(IEnumerable<CanonicalIngredientFoldCandidate> candidates)
    {
        var groups = new List<FoldGroup>();

        var byKey = candidates
            .Where(c => !string.IsNullOrEmpty(c.NormalizedKey))
            .GroupBy(c => c.NormalizedKey, StringComparer.Ordinal);

        foreach (var group in byKey)
        {
            var members = group.ToList();
            if (members.Count < 2)
            {
                continue;
            }

            var ordered = members
                .OrderBy(c => c.Name.Length)
                .ThenByDescending(c => c.ReferenceCount)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .ToList();

            var survivor = ordered[0];
            var losers = ordered.Skip(1).ToList();
            groups.Add(new FoldGroup(survivor, losers));
        }

        // Stable outer ordering: largest fold groups first so dry-run reports
        // are easier to scan and so the biggest FK-reassignment batches land
        // early in the run (fail-fast on whatever pattern would break).
        return groups
            .OrderByDescending(g => g.Losers.Count)
            .ThenByDescending(g => g.TotalReassignedReferenceCount)
            .ThenBy(g => g.Survivor.Name, StringComparer.Ordinal)
            .ToList();
    }
}
