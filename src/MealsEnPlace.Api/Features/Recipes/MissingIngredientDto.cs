namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// A recipe ingredient not matched in inventory.
/// </summary>
public sealed class MissingIngredientDto
{
    /// <summary>Ingredient name.</summary>
    public string IngredientName { get; init; } = string.Empty;

    /// <summary>Required quantity (display units).</summary>
    public decimal RequiredQuantity { get; init; }

    /// <summary>Display unit for required quantity.</summary>
    public string RequiredUnitOfMeasure { get; init; } = string.Empty;
}
