namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A dish stored in the local recipe library. A recipe is considered fully resolved
/// when all of its <see cref="RecipeIngredients"/> have
/// <see cref="RecipeIngredient.IsContainerResolved"/> = true. Only fully resolved recipes
/// participate in recipe matching.
/// </summary>
public class Recipe
{
    /// <summary>Cuisine type string (e.g., "Italian", "Mexican").</summary>
    public string CuisineType { get; set; } = string.Empty;

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Step-by-step cooking instructions.</summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Number of servings this recipe produces.</summary>
    public int ServingCount { get; set; }

    /// <summary>URL of the original recipe source, if any.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>TheMealDB meal ID, if this recipe was imported from that source.</summary>
    public string? TheMealDbId { get; set; }

    /// <summary>Display title of the recipe.</summary>
    public string Title { get; set; } = string.Empty;

    // Navigation properties

    /// <summary>Dietary tag join records for this recipe.</summary>
    public ICollection<RecipeDietaryTag> DietaryTags { get; set; } = new List<RecipeDietaryTag>();

    /// <summary>Meal plan slots to which this recipe has been assigned.</summary>
    public ICollection<MealPlanSlot> MealPlanSlots { get; set; } = new List<MealPlanSlot>();

    /// <summary>Ingredient lines that make up this recipe.</summary>
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();

    // Computed properties

    /// <summary>
    /// True when all <see cref="RecipeIngredients"/> have
    /// <see cref="RecipeIngredient.IsContainerResolved"/> = true.
    /// Computed from the collection; not stored as a database column.
    /// </summary>
    public bool IsFullyResolved =>
        RecipeIngredients.Count > 0 && RecipeIngredients.All(ri => ri.IsContainerResolved);
}
