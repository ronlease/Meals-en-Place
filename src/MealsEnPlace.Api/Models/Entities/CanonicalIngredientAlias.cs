namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A historical name variant that maps to a surviving <see cref="CanonicalIngredient"/>.
/// Rows are produced by the MEP-038 dedup pass when a noisier NER-derived ingredient
/// (e.g., "chopped onion", "onions") is folded into a more generic survivor (e.g., "onion"),
/// so the original string stays queryable and the fold is reversible / auditable.
/// Subsequent ingest runs may add additional rows as further dedup passes complete.
/// </summary>
public class CanonicalIngredientAlias
{
    /// <summary>The original canonical ingredient name that was folded into the survivor.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>The surviving <see cref="CanonicalIngredient"/> this alias now resolves to.</summary>
    public Guid CanonicalIngredientId { get; set; }

    /// <summary>When the alias row was created, UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    // Navigation properties

    /// <summary>The survivor canonical ingredient this alias resolves to.</summary>
    public CanonicalIngredient? CanonicalIngredient { get; set; }
}
