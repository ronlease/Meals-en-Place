using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Result returned after importing a recipe from TheMealDB.
/// </summary>
public sealed class RecipeImportResultDto
{
    /// <summary>Dietary tags assigned by Claude.</summary>
    public IReadOnlyList<DietaryTag> DietaryTags { get; init; } = [];

    /// <summary>Local database ID of the imported recipe.</summary>
    public Guid RecipeId { get; init; }

    /// <summary>Display title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Total ingredient count.</summary>
    public int TotalIngredients { get; init; }

    /// <summary>Number of unresolved container references.</summary>
    public int UnresolvedCount { get; init; }
}
