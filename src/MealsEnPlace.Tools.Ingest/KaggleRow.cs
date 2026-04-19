namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// A parsed row from the Kaggle "Recipe Dataset (over 2M)" CSV. Raw string
/// columns are parsed into typed arrays by <see cref="KaggleRowReader"/> so
/// downstream code does not have to re-parse the JSON-array-style string
/// fields.
/// </summary>
internal sealed class KaggleRow
{
    /// <summary>
    /// Raw instruction step strings. Typically imperative sentences, sometimes
    /// with blog-voice narrative that <c>InstructionProseFilter</c> will drop.
    /// </summary>
    public required IReadOnlyList<string> Directions { get; init; }

    /// <summary>
    /// Raw ingredient strings as they appear on the source site, e.g.
    /// "1 c. firmly packed brown sugar", "1 can cream of mushroom soup".
    /// </summary>
    public required IReadOnlyList<string> Ingredients { get; init; }

    /// <summary>Source URL on the originating site (relative or absolute).</summary>
    public required string Link { get; init; }

    /// <summary>
    /// Pre-extracted canonical ingredient names per the dataset's NER pass.
    /// Not index-aligned with <see cref="Ingredients"/>; the set represents
    /// what ingredients the recipe contains, in a different order.
    /// </summary>
    public required IReadOnlyList<string> Ner { get; init; }

    /// <summary>
    /// Provenance label. "Gathered" (scraped from one of 28 sites) or
    /// "Recipes1M" (MIT-gated dataset; skipped by the ingest tool).
    /// </summary>
    public required string Source { get; init; }

    /// <summary>Root domain of the source site (e.g. "www.food.com").</summary>
    public required string Site { get; init; }

    /// <summary>The recipe's display title.</summary>
    public required string Title { get; init; }
}
