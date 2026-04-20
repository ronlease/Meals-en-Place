using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Endpoints for manual recipe creation and local-library retrieval. The
/// TheMealDB search / import endpoints that previously lived here were
/// removed under MEP-033 when the Kaggle bulk ingest (MEP-026) became the
/// catalog source.
/// </summary>
[ApiController]
[Route("api/v1/recipes")]
[Produces("application/json")]
public sealed class RecipeImportController(IRecipeImportService recipeImportService) : ControllerBase
{
    /// <summary>Creates a new recipe manually.</summary>
    /// <param name="request">Recipe details including title, ingredients, instructions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the created recipe detail; 400 if validation fails.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(RecipeDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecipeDetailDto>> Create(
        [FromBody] CreateRecipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new ProblemDetails { Detail = "Title is required.", Status = 400, Title = "Validation Error" });

        if (request.Ingredients.Count == 0)
            return BadRequest(new ProblemDetails { Detail = "At least one ingredient is required.", Status = 400, Title = "Validation Error" });

        var recipe = await recipeImportService.CreateRecipeAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = recipe.Id }, recipe);
    }

    /// <summary>Returns all local recipes with resolution status.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of recipes.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RecipeListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecipeListItemDto>>> GetAllLocalRecipes(CancellationToken cancellationToken)
    {
        var recipes = await recipeImportService.GetAllLocalRecipesAsync(cancellationToken);
        return Ok(recipes);
    }

    /// <summary>Returns the full detail of a single recipe.</summary>
    /// <param name="id">The recipe ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the recipe detail; 404 if not found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecipeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var recipe = await recipeImportService.GetRecipeDetailAsync(id, cancellationToken);
        if (recipe is null)
            return NotFound(new ProblemDetails { Detail = $"Recipe '{id}' was not found.", Status = 404, Title = "Not Found" });
        return Ok(recipe);
    }
}
