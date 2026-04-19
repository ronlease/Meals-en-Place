using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Read-only projection of a <see cref="CanonicalIngredient"/> for reference data lookups.
/// </summary>
public class CanonicalIngredientDto
{
    /// <summary>Broad category used for grouping on the shopping list.</summary>
    public IngredientCategory Category { get; set; }

    /// <summary>Id of the preferred unit of measure when none is specified.</summary>
    public Guid DefaultUnitOfMeasureId { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Normalized display name.</summary>
    public string Name { get; set; } = string.Empty;
}
