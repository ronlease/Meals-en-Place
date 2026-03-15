using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Read-only projection of a <see cref="UnitOfMeasure"/> for reference data lookups.
/// </summary>
public class UnitOfMeasureDto
{
    /// <summary>Short symbol used in display (e.g., "tsp", "ml", "oz").</summary>
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable name (e.g., "Teaspoon", "Milliliter", "Ounce").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Dimensional type of this unit.</summary>
    public UomType UomType { get; set; }
}
