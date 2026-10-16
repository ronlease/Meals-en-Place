namespace MealsEnPlace.Api.Common;

/// <summary>
/// The result of parsing a raw measure string in the context of its ingredient name.
/// Produced by <see cref="UnitOfMeasureTokenParser.ParseWithIngredientContext"/>.
/// </summary>
public sealed class MeasureStringParseResult
{
    /// <summary>
    /// The token to record on the review queue when deterministic resolution fails.
    /// For a positive quantity this is the leading word of the remainder (after skipping
    /// junk words and size descriptors); for a zero quantity it is the whole string.
    /// Empty for junk and to-taste lines, which never queue.
    /// </summary>
    public string DeferralUnitToken { get; init; } = string.Empty;

    /// <summary>
    /// The quantity implied when the line has no number but leads with pinch / pinches /
    /// dash / dashes ("pinch of salt", "a dash of pepper"): always 1 in that case, null
    /// otherwise. Set together with <see cref="PreferredUnitOfMeasureAbbreviation"/>; never
    /// set for lines with an explicit quantity, junk lines or to-taste lines.
    /// </summary>
    public decimal? ImplicitQuantity { get; init; }

    /// <summary>
    /// Grams per one parsed unit when the unit is ingredient-specific and defined
    /// (currently only a US butter or margarine stick, 113 g); null otherwise.
    /// When set, it takes precedence over unit and alias lookup.
    /// </summary>
    public decimal? IngredientSpecificGramsPerUnit { get; init; }

    /// <summary>
    /// True when the quantity is positive and an unrecognized unit word may be treated as a
    /// count of "each": the remainder is a count of the ingredient noun, or it started with
    /// a size descriptor (e.g. "1 large yellow onion"). Computed.
    /// </summary>
    public bool IsCountFallbackEligible =>
        Quantity > 0m && (IsCountWithIngredientNoun || StartsWithSizeDescriptor);

    /// <summary>
    /// True when the measure string is a bare count or a count followed by the
    /// ingredient noun itself (e.g. "5", "4 chicken breasts" for "chicken breast"),
    /// so the unit of measure is "each".
    /// </summary>
    public bool IsCountWithIngredientNoun { get; init; }

    /// <summary>
    /// True when there is no quantity and the whole line is a junk token such as
    /// "None", "-", "to" or "or". Never a unit; never queued.
    /// </summary>
    public bool IsJunkLine { get; init; }

    /// <summary>
    /// True when there is no quantity and the line is a to-taste seasoning
    /// ("salt to taste", "salt and pepper", "pepper"). Never queued.
    /// </summary>
    public bool IsToTaste { get; init; }

    /// <summary>
    /// The unit abbreviation to resolve <see cref="ImplicitQuantity"/> against: "dash" when
    /// the ingredient context contains the whole word "pepper", otherwise "pinch" (so
    /// "dash of salt" is a pinch and "pinch of pepper" is a dash). Null when no implicit
    /// quantity applies.
    /// </summary>
    public string? PreferredUnitOfMeasureAbbreviation { get; init; }

    /// <summary>The parsed numeric quantity; 0 when no leading number was found.</summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// True when a size descriptor (large, small, medium, ...) was skipped before the
    /// first unit word.
    /// </summary>
    public bool StartsWithSizeDescriptor { get; init; }

    /// <summary>
    /// Candidate unit tokens, in priority order, to look up against the unit of
    /// measure table and alias table.
    /// </summary>
    public IReadOnlyList<string> UnitTokenCandidates { get; init; } = [];
}
