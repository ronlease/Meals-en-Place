namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A unit token encountered during ingest that did not resolve via abbreviation,
/// name, alias, or count-with-noun fallback. Queued for user review so a
/// <see cref="UnitOfMeasureAlias"/> can be created once and applied retroactively
/// to every affected ingredient, rather than burning Claude calls per occurrence.
/// <para>
/// One row per unique <see cref="UnitToken"/>. Repeat occurrences increment
/// <see cref="Count"/> and update <see cref="LastSeenAt"/> rather than inserting
/// a new row.
/// </para>
/// </summary>
public class UnresolvedUomToken
{
    /// <summary>Running count of occurrences across all ingest runs.</summary>
    public int Count { get; set; }

    /// <summary>When this token was first encountered, UTC.</summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>When this token was most recently encountered, UTC.</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// A representative ingredient context from the most recent occurrence
    /// (e.g., "flour", "olive oil"). Helps the reviewing user understand the
    /// intent of the unit when deciding how to map it.
    /// </summary>
    public string SampleIngredientContext { get; set; } = string.Empty;

    /// <summary>
    /// A representative original measure string from the most recent occurrence
    /// (e.g., "1 smidge", "a pinch of"). Used for display in the review UI.
    /// </summary>
    public string SampleMeasureString { get; set; } = string.Empty;

    /// <summary>
    /// The extracted unit token that could not be resolved (e.g., "smidge",
    /// "dram"). Case is preserved because recipe notation uses case
    /// meaningfully (see <see cref="UnitOfMeasureAlias"/>).
    /// </summary>
    public string UnitToken { get; set; } = string.Empty;
}
