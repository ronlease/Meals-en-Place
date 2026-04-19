namespace MealsEnPlace.Api.Features.WasteReduction;

/// <summary>
/// Response DTO for a waste reduction alert.
/// </summary>
public record WasteAlertResponse
{
    /// <summary>Alert ID.</summary>
    public required Guid AlertId { get; init; }

    /// <summary>Name of the canonical ingredient approaching expiry.</summary>
    public required string CanonicalIngredientName { get; init; }

    /// <summary>When this alert was created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Days until the inventory item expires. Negative means already expired.</summary>
    public required int DaysUntilExpiry { get; init; }

    /// <summary>Expiry date of the inventory item.</summary>
    public required DateOnly ExpiryDate { get; init; }

    /// <summary>The inventory item that triggered this alert.</summary>
    public required Guid InventoryItemId { get; init; }

    /// <summary>Storage location of the inventory item.</summary>
    public required string Location { get; init; }

    /// <summary>Recipes that can use this ingredient.</summary>
    public required List<WasteAlertRecipeDto> MatchedRecipes { get; init; }

    /// <summary>Quantity of the inventory item in display units.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Display unit abbreviation.</summary>
    public required string UnitOfMeasureAbbreviation { get; init; }
}

/// <summary>
/// A recipe suggested for using an expiring inventory item.
/// </summary>
public record WasteAlertRecipeDto
{
    /// <summary>Cuisine type of the recipe.</summary>
    public required string CuisineType { get; init; }

    /// <summary>Recipe ID.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>Recipe title.</summary>
    public required string Title { get; init; }
}
