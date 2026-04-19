using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Request body for creating a new <see cref="CanonicalIngredient"/>.
/// </summary>
public class CreateCanonicalIngredientRequest
{
    /// <summary>Broad category for grouping and shopping list organization.</summary>
    public IngredientCategory Category { get; set; }

    /// <summary>Id of the preferred unit of measure when none is specified.</summary>
    public Guid DefaultUnitOfMeasureId { get; set; }

    /// <summary>Normalized display name for this ingredient.</summary>
    public string Name { get; set; } = string.Empty;
}
