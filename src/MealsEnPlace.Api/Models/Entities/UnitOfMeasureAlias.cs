namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A string variant that maps to a canonical <see cref="UnitOfMeasure"/>.
/// Examples: "c.", "Tbsp.", "lbs" mapping to Cup, Tablespoon, Pound respectively.
/// Consulted by <c>UnitOfMeasureNormalizationService</c> after the abbreviation and name
/// lookups before falling back to the review queue.
/// <para>
/// No database-level uniqueness constraint on <see cref="Alias"/>. Recipe
/// notation uses case meaningfully -- uppercase "T" = Tablespoon, lowercase
/// "t" = Teaspoon (a 3x quantity difference) -- so neither case-sensitive
/// nor case-insensitive uniqueness correctly captures the domain. Uniqueness
/// is enforced at the service / controller layer (MEP-026 Phase 2), which
/// rejects accidental duplicates while permitting legitimate case-sensitive
/// variants as explicit overrides.
/// </para>
/// </summary>
public class UnitOfMeasureAlias
{
    /// <summary>The alias text (case-insensitive). Examples: "c.", "Tbsp.", "lbs".</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>When the alias row was created, UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The canonical <see cref="UnitOfMeasure"/> this alias maps to.</summary>
    public Guid UnitOfMeasureId { get; set; }

    // Navigation properties

    /// <summary>The canonical unit this alias resolves to.</summary>
    public UnitOfMeasure? UnitOfMeasure { get; set; }
}
