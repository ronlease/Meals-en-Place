using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Summary row for the local recipe library list.
/// </summary>
public sealed class RecipeListItemDto
{
    /// <summary>Cuisine type.</summary>
    public string CuisineType { get; init; } = string.Empty;

    /// <summary>Dietary tags classified for this recipe.</summary>
    public IReadOnlyList<DietaryTag> DietaryTags { get; init; } = [];

    /// <summary>Local database ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Ingredient names for display.</summary>
    public IReadOnlyList<string> IngredientNames { get; init; } = [];

    /// <summary>Whether all container references are resolved.</summary>
    public bool IsFullyResolved { get; init; }

    /// <summary>Display title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Total ingredient count.</summary>
    public int TotalIngredients { get; init; }

    /// <summary>Number of unresolved container references.</summary>
    public int UnresolvedCount { get; init; }
}
