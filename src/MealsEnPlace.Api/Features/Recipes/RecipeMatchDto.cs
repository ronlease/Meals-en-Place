using MealsEnPlace.Api.Infrastructure.Claude;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Match result for a single recipe.
/// </summary>
public sealed record RecipeMatchDto
{
    /// <summary>Cuisine type.</summary>
    public string CuisineType { get; init; } = string.Empty;

    /// <summary>Final score after WasteBonus, capped at 1.0.</summary>
    public decimal FinalScore { get; init; }

    /// <summary>Ingredients present in inventory.</summary>
    public IReadOnlyList<MatchedIngredientDto> MatchedIngredients { get; init; } = [];

    /// <summary>Raw ingredient coverage ratio.</summary>
    public decimal MatchScore { get; init; }

    /// <summary>Quality tier.</summary>
    public MatchTier MatchTier { get; init; }

    /// <summary>Ingredients absent or insufficient.</summary>
    public IReadOnlyList<MissingIngredientDto> MissingIngredients { get; init; } = [];

    /// <summary>Recipe ID.</summary>
    public Guid RecipeId { get; init; }

    /// <summary>Claude substitution suggestions (NearMatch only).</summary>
    public IReadOnlyList<SubstitutionSuggestion> SubstitutionSuggestions { get; init; } = [];

    /// <summary>Recipe title.</summary>
    public string Title { get; init; } = string.Empty;
}
