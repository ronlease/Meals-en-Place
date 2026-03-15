namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Represents a recipe ingredient after a container reference has been successfully resolved.
/// Quantity and UOM reflect the user-declared values, converted for display.
/// Notes preserves the original import string unchanged.
/// </summary>
public sealed class ResolvedIngredientResponse
{
    /// <summary>The canonical ingredient name (e.g., "Chopped Tomatoes").</summary>
    public string CanonicalIngredientName { get; init; } = string.Empty;

    /// <summary>Primary key of the <see cref="MealsEnPlace.Api.Models.Entities.RecipeIngredient"/>.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// True — the ingredient is now resolved and will participate in recipe matching math
    /// once all other ingredients in the recipe are also resolved.
    /// </summary>
    public bool IsContainerResolved { get; init; }

    /// <summary>
    /// The original import string preserved unchanged (e.g., "1 can chopped tomatoes").
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>Display-converted quantity (e.g., 14.5 in "oz").</summary>
    public decimal Quantity { get; init; }

    /// <summary>The id of the recipe this ingredient belongs to.</summary>
    public Guid RecipeId { get; init; }

    /// <summary>Display abbreviation for the resolved UOM (e.g., "oz", "fl oz").</summary>
    public string UomAbbreviation { get; init; } = string.Empty;

    /// <summary>The id of the resolved unit of measure.</summary>
    public Guid UomId { get; init; }
}
