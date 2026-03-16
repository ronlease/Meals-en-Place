using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Query parameters for the recipe matching endpoint.
/// </summary>
public sealed class RecipeMatchRequest
{
    /// <summary>Optional cuisine type filter.</summary>
    public string? Cuisine { get; init; }

    /// <summary>Optional dietary tag filters. Recipes must carry ALL specified tags.</summary>
    public List<DietaryTag>? DietaryTags { get; init; }

    /// <summary>When true, only recipes using in-season ingredients are included.</summary>
    public bool SeasonalOnly { get; init; }
}
