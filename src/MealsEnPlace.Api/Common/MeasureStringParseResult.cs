namespace MealsEnPlace.Api.Common;

/// <summary>
/// The result of parsing a raw measure string in the context of its ingredient name.
/// Produced by <see cref="UnitOfMeasureTokenParser.ParseWithIngredientContext"/>.
/// </summary>
public sealed class MeasureStringParseResult
{
    /// <summary>
    /// The token to record on the review queue when deterministic resolution fails.
    /// For a positive quantity this is the leading word of the remainder; for a
    /// zero quantity it is the whole string.
    /// </summary>
    public string DeferralUnitToken { get; init; } = string.Empty;

    /// <summary>
    /// True when the measure string is a bare count or a count followed by the
    /// ingredient noun itself (e.g. "5", "4 chicken breasts" for "chicken breast"),
    /// so the unit of measure is "each".
    /// </summary>
    public bool IsCountWithIngredientNoun { get; init; }

    /// <summary>The parsed numeric quantity; 0 when no leading number was found.</summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// Candidate unit tokens, in priority order, to look up against the unit of
    /// measure table and alias table.
    /// </summary>
    public IReadOnlyList<string> UnitTokenCandidates { get; init; } = [];
}
