namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A derived shopping list item representing the net quantity of a canonical ingredient
/// that is required by the active meal plan but not covered by current inventory.
/// </summary>
public class ShoppingListItem
{
    /// <summary>The canonical ingredient to purchase.</summary>
    public Guid CanonicalIngredientId { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The meal plan from which this item was derived.</summary>
    public Guid MealPlanId { get; set; }

    /// <summary>Optional display note (e.g., original measure string).</summary>
    public string? Notes { get; set; }

    /// <summary>Net quantity needed, in the unit specified by <see cref="UomId"/>.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The unit of measure for <see cref="Quantity"/>.</summary>
    public Guid UomId { get; set; }

    // Navigation properties

    /// <summary>The canonical ingredient to purchase.</summary>
    public CanonicalIngredient CanonicalIngredient { get; set; } = null!;

    /// <summary>The meal plan from which this item was derived.</summary>
    public MealPlan MealPlan { get; set; } = null!;

    /// <summary>The unit of measure for this item's quantity.</summary>
    public UnitOfMeasure Uom { get; set; } = null!;
}
