using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// "What can I make?" recipe matching endpoint.
/// </summary>
[ApiController]
[Route("api/v1/recipes/match")]
[Produces("application/json")]
public class RecipeMatchingController(IRecipeMatchingService recipeMatchingService) : ControllerBase
{
    /// <summary>Returns recipes the user can make based on current inventory.</summary>
    /// <param name="cuisine">Optional cuisine filter (e.g., "Italian").</param>
    /// <param name="dietaryTags">Optional comma-separated dietary tags (e.g., "Vegetarian,GlutenFree").</param>
    /// <param name="seasonalOnly">When true, only returns recipes with seasonal ingredients.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with full, near, and partial recipe matches.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(RecipeMatchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecipeMatchResponse>> GetMatches(
        [FromQuery] string? cuisine,
        [FromQuery] string? dietaryTags,
        [FromQuery] bool seasonalOnly = false,
        CancellationToken cancellationToken = default)
    {
        var request = new RecipeMatchRequest
        {
            Cuisine = cuisine,
            DietaryTags = ParseDietaryTags(dietaryTags),
            SeasonalOnly = seasonalOnly
        };

        var response = await recipeMatchingService.MatchRecipesAsync(request, cancellationToken);
        return Ok(response);
    }

    private static List<DietaryTag>? ParseDietaryTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var tags = new List<DietaryTag>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<DietaryTag>(part, ignoreCase: true, out var tag))
                tags.Add(tag);
        }
        return tags.Count > 0 ? tags : null;
    }
}
