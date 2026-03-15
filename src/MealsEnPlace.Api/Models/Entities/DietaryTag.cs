namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Claude-derived dietary classification tags for a recipe.
/// A recipe may carry multiple tags.
/// </summary>
public enum DietaryTag
{
    /// <summary>Primarily composed of meat, fish, or animal products.</summary>
    Carnivore,

    /// <summary>Contains no dairy ingredients.</summary>
    DairyFree,

    /// <summary>Contains no gluten-containing grains.</summary>
    GlutenFree,

    /// <summary>Low in carbohydrates.</summary>
    LowCarb,

    /// <summary>Contains no animal products of any kind.</summary>
    Vegan,

    /// <summary>Contains no meat, poultry, or fish.</summary>
    Vegetarian
}
