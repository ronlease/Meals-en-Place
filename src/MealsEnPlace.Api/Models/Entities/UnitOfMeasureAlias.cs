namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A string variant that maps to a canonical <see cref="UnitOfMeasure"/>.
/// Examples: "c.", "Tbsp.", "lbs" mapping to Cup, Tablespoon, Pound respectively.
/// Consulted by <c>UomNormalizationService</c> after the abbreviation and name
/// lookups before falling back to the review queue.
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
