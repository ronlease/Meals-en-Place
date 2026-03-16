namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// A recipe ingredient matched against inventory.
/// </summary>
public sealed class MatchedIngredientDto
{
    /// <summary>Available quantity in inventory (display units).</summary>
    public decimal AvailableQuantity { get; init; }

    /// <summary>Display unit for available quantity.</summary>
    public string AvailableUom { get; init; } = string.Empty;

    /// <summary>Ingredient name.</summary>
    public string IngredientName { get; init; } = string.Empty;

    /// <summary>True if the inventory item expires within 3 days.</summary>
    public bool IsExpiryImminent { get; init; }

    /// <summary>Required quantity (display units).</summary>
    public decimal RequiredQuantity { get; init; }

    /// <summary>Display unit for required quantity.</summary>
    public string RequiredUom { get; init; } = string.Empty;
}
