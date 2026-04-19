using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Response DTO for a shopping list item.
/// </summary>
public record ShoppingListItemResponse
{
    /// <summary>Ingredient name.</summary>
    public required string CanonicalIngredientName { get; init; }

    /// <summary>Ingredient category for grouping in UI.</summary>
    public required IngredientCategory Category { get; init; }

    /// <summary>Shopping list item ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Optional notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Quantity needed in display units.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Display unit abbreviation.</summary>
    public required string UnitOfMeasureAbbreviation { get; init; }
}
