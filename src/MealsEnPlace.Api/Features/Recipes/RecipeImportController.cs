using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Endpoints for searching TheMealDB, importing recipes, and listing the local library.
/// </summary>
[ApiController]
[Route("api/v1/recipes")]
[Produces("application/json")]
public sealed class RecipeImportController(IRecipeImportService recipeImportService) : ControllerBase
{
    /// <summary>Returns all local recipes with resolution status.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of imported recipes.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RecipeListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecipeListItemDto>>> GetAllLocalRecipes(CancellationToken cancellationToken)
    {
        var recipes = await recipeImportService.GetAllLocalRecipesAsync(cancellationToken);
        return Ok(recipes);
    }

    /// <summary>Imports a recipe from TheMealDB by meal ID.</summary>
    /// <param name="mealDbId">The TheMealDB meal identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the import result; 404 if not found on TheMealDB; 409 if already imported.</returns>
    [HttpPost("import/{mealDbId}")]
    [ProducesResponseType(typeof(RecipeImportResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecipeImportResultDto>> ImportById(string mealDbId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await recipeImportService.ImportByIdAsync(mealDbId, cancellationToken);
            return CreatedAtAction(nameof(GetAllLocalRecipes), result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already been imported"))
        {
            return Conflict(new ProblemDetails
            {
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
                Title = "Recipe Already Imported"
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No meal found"))
        {
            return NotFound(new ProblemDetails
            {
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
                Title = "Recipe Not Found"
            });
        }
    }

    /// <summary>Searches TheMealDB by recipe name.</summary>
    /// <param name="query">The search term (recipe name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with search results; 400 if query is empty.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<RecipeSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RecipeSearchResultDto>>> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new ProblemDetails { Detail = "The 'query' parameter is required.", Status = 400, Title = "Missing Query" });

        var results = await recipeImportService.SearchAsync(query, cancellationToken);
        return Ok(results);
    }

    /// <summary>Searches TheMealDB by category.</summary>
    /// <param name="category">The category name (e.g., "Seafood", "Beef").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with search results; 400 if category is empty.</returns>
    [HttpGet("search/category")]
    [ProducesResponseType(typeof(IReadOnlyList<RecipeSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RecipeSearchResultDto>>> SearchByCategory([FromQuery] string category, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(category))
            return BadRequest(new ProblemDetails { Detail = "The 'category' parameter is required.", Status = 400, Title = "Missing Category" });

        var results = await recipeImportService.SearchByCategoryAsync(category, cancellationToken);
        return Ok(results);
    }
}
