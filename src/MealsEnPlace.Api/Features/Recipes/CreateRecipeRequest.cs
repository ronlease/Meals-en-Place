namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Request body for manually creating a new recipe.
/// </summary>
public sealed class CreateRecipeRequest
{
    /// <summary>Cuisine type string (e.g., "Italian", "Mexican").</summary>
    public string CuisineType { get; init; } = string.Empty;

    /// <summary>Ingredient lines for the recipe. Must contain at least one entry.</summary>
    public List<CreateRecipeIngredientRequest> Ingredients { get; init; } = [];

    /// <summary>Step-by-step cooking instructions.</summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>Number of servings this recipe produces. Defaults to 4.</summary>
    public int ServingCount { get; init; } = 4;

    /// <summary>Display title of the recipe. Must not be empty.</summary>
    public string Title { get; init; } = string.Empty;
}
