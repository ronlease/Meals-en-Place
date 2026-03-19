namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Represents a single ingredient line in a recipe detail response.
/// </summary>
public sealed class RecipeIngredientDetailDto
{
    /// <summary>The canonical ingredient identifier.</summary>
    public Guid CanonicalIngredientId { get; init; }

    /// <summary>Primary key of the recipe ingredient row.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name of the canonical ingredient.</summary>
    public string IngredientName { get; init; } = string.Empty;

    /// <summary>
    /// True when no container reference was involved, or when the user has declared
    /// the container's net weight or volume. False for unresolved container references.
    /// </summary>
    public bool IsContainerResolved { get; init; }

    /// <summary>
    /// The original recipe text when a container reference was resolved (e.g., "1 can chopped tomatoes").
    /// Null when no container reference was involved.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>Resolved quantity in the unit described by <see cref="UomAbbreviation"/>.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Abbreviation of the unit of measure (e.g., "oz", "ml", "ea"). Empty for unresolved container references.</summary>
    public string UomAbbreviation { get; init; } = string.Empty;

    /// <summary>The unit of measure identifier. Null for unresolved container references.</summary>
    public Guid? UomId { get; init; }
}
