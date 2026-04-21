namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A normalized ingredient that multiple inventory items and recipe ingredients map into,
/// regardless of brand or description (e.g., "Chicken Breast").
/// </summary>
public class CanonicalIngredient
{
    /// <summary>Broad category for grouping and shopping list organization.</summary>
    public IngredientCategory Category { get; set; }

    /// <summary>The preferred unit of measure when no explicit unit is specified.</summary>
    public Guid DefaultUnitOfMeasureId { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Normalized display name for this ingredient.</summary>
    public string Name { get; set; } = string.Empty;

    // Navigation properties

    /// <summary>Historical names folded into this survivor by a MEP-038 dedup pass.</summary>
    public ICollection<CanonicalIngredientAlias> Aliases { get; set; } = new List<CanonicalIngredientAlias>();

    /// <summary>The default unit of measure for this ingredient.</summary>
    public UnitOfMeasure DefaultUnitOfMeasure { get; set; } = null!;

    /// <summary>Inventory items mapped to this canonical ingredient.</summary>
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

    /// <summary>Recipe ingredient usages mapped to this canonical ingredient.</summary>
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();

    /// <summary>Seasonality windows defined for this ingredient.</summary>
    public ICollection<SeasonalityWindow> SeasonalityWindows { get; set; } = new List<SeasonalityWindow>();

    /// <summary>Shopping list items mapped to this canonical ingredient.</summary>
    public ICollection<ShoppingListItem> ShoppingListItems { get; set; } = new List<ShoppingListItem>();
}
