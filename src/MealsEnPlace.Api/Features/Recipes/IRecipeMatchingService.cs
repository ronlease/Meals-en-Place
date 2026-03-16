namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Scores current inventory against the local recipe library.
/// </summary>
public interface IRecipeMatchingService
{
    /// <summary>Runs the full recipe matching pipeline.</summary>
    Task<RecipeMatchResponse> MatchRecipesAsync(RecipeMatchRequest request, CancellationToken cancellationToken = default);
}
