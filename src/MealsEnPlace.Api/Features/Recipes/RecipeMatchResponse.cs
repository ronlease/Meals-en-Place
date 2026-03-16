namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Top-level response from the recipe matching endpoint.
/// </summary>
public sealed class RecipeMatchResponse
{
    /// <summary>All ingredients present (MatchScore == 1.0), sorted by FinalScore desc.</summary>
    public IReadOnlyList<RecipeMatchDto> FullMatches { get; init; } = [];

    /// <summary>Most ingredients present (>= 0.75), with substitution suggestions.</summary>
    public IReadOnlyList<RecipeMatchDto> NearMatches { get; init; } = [];

    /// <summary>At least half present (>= 0.5).</summary>
    public IReadOnlyList<RecipeMatchDto> PartialMatches { get; init; } = [];
}
