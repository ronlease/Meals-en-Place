namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Full detail representation of a recipe in the local library.
/// </summary>
public sealed class RecipeDetailDto
{
    /// <summary>Cuisine type string (e.g., "Italian", "Mexican").</summary>
    public string CuisineType { get; init; } = string.Empty;

    /// <summary>Dietary tag strings derived from Claude classification.</summary>
    public List<string> DietaryTags { get; init; } = [];

    /// <summary>Primary key.</summary>
    public Guid Id { get; init; }

    /// <summary>Ingredient lines with resolved quantities and units.</summary>
    public List<RecipeIngredientDetailDto> Ingredients { get; init; } = [];

    /// <summary>Step-by-step cooking instructions.</summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>
    /// True when all ingredient lines have resolved container references.
    /// Only fully resolved recipes participate in recipe matching.
    /// </summary>
    public bool IsFullyResolved { get; init; }

    /// <summary>Number of servings this recipe produces.</summary>
    public int ServingCount { get; init; }

    /// <summary>URL of the original recipe source. Null for manually created recipes.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Display title of the recipe.</summary>
    public string Title { get; init; } = string.Empty;
}
