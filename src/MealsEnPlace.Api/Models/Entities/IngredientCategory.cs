namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Broad category for a canonical ingredient, used for grouping on the shopping list.
/// </summary>
public enum IngredientCategory
{
    /// <summary>Sauces, dressings, vinegars, and flavoring liquids.</summary>
    Condiment,

    /// <summary>Milk, cheese, butter, cream, and yogurt products.</summary>
    Dairy,

    /// <summary>Flour, rice, pasta, bread, and other starch staples.</summary>
    Grain,

    /// <summary>Ingredients that do not fit another category.</summary>
    Other,

    /// <summary>Fresh, frozen, or canned fruits and vegetables.</summary>
    Produce,

    /// <summary>Meat, poultry, seafood, eggs, and plant-based proteins.</summary>
    Protein,

    /// <summary>Dried herbs, ground spices, and seasoning blends.</summary>
    Spice
}
