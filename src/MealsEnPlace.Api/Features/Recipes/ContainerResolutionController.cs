using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Manages the resolution of container references on recipe ingredients.
/// A container reference ("1 can", "1 jar", etc.) is flagged during recipe import
/// because the system never assumes a container size. The user must declare the net
/// weight or volume before the ingredient participates in recipe matching math.
/// </summary>
[ApiController]
[Route("api/v1/recipes")]
[Produces("application/json")]
public class ContainerResolutionController(
    IContainerResolutionService containerResolutionService,
    UomDisplayConverter displayConverter) : ControllerBase
{
    /// <summary>
    /// Returns unresolved container references grouped by canonical ingredient
    /// and notes phrase, ordered by occurrence count descending. Primary review
    /// surface after a bulk ingest (MEP-026) when the same phrase may appear
    /// across hundreds of recipes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of groups (may be empty).</returns>
    [HttpGet("unresolved-groups")]
    [ProducesResponseType(typeof(IReadOnlyList<UnresolvedGroupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UnresolvedGroupResponse>>> GetUnresolvedGroups(
        CancellationToken cancellationToken)
    {
        var groups = await containerResolutionService.GetUnresolvedGroupsAsync(cancellationToken);
        var response = groups.Select(g => new UnresolvedGroupResponse
        {
            CanonicalIngredientId = g.CanonicalIngredientId,
            CanonicalIngredientName = g.CanonicalIngredientName,
            Notes = g.Notes,
            OccurrenceCount = g.OccurrenceCount
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Resolves every unresolved container reference in a group (identified by
    /// canonical ingredient id and notes phrase) in a single transaction.
    /// Applies the declared net weight or volume to every matching row.
    /// </summary>
    /// <param name="request">Group key, declared quantity, and UOM.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 200 with the count of rows updated; 400 when the request fails validation
    /// (non-positive quantity, unknown UOM, or empty notes).
    /// </returns>
    [HttpPost("unresolved-groups/resolve")]
    [ProducesResponseType(typeof(BulkResolveGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkResolveGroupResponse>> BulkResolveGroup(
        [FromBody] BulkResolveGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await containerResolutionService.BulkResolveAsync(
            request.CanonicalIngredientId,
            request.Notes,
            request.Quantity,
            request.UomId,
            cancellationToken);

        if (result.IsValidationError)
        {
            return BadRequest(new ProblemDetails
            {
                Detail = result.ErrorMessage,
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error"
            });
        }

        return Ok(new BulkResolveGroupResponse { AffectedCount = result.AffectedCount });
    }

    /// <summary>
    /// Returns all recipes that have at least one unresolved container reference,
    /// ordered alphabetically by title.
    /// </summary>
    /// <remarks>
    /// Use this endpoint to build a resolution queue UI. Each recipe in the response
    /// carries the list of unresolved ingredients alongside their original import strings
    /// so the user knows what to declare.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of unresolved recipes (may be empty).</returns>
    [HttpGet("unresolved")]
    [ProducesResponseType(typeof(IReadOnlyList<UnresolvedRecipeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UnresolvedRecipeResponse>>> GetUnresolvedRecipes(
        CancellationToken cancellationToken)
    {
        var recipes = await containerResolutionService.GetUnresolvedRecipesAsync(cancellationToken);
        var response = recipes.Select(MapToUnresolvedRecipeResponse).ToList();
        return Ok(response);
    }

    /// <summary>
    /// Returns all unresolved container reference ingredients for a specific recipe.
    /// </summary>
    /// <param name="recipeId">The id of the recipe to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 200 with the list of unresolved ingredients (may be empty when the recipe is
    /// fully resolved); 404 if the recipe does not exist.
    /// </returns>
    [HttpGet("{recipeId:guid}/unresolved-ingredients")]
    [ProducesResponseType(typeof(IReadOnlyList<UnresolvedIngredientResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<UnresolvedIngredientResponse>>> GetUnresolvedIngredients(
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var ingredients = await containerResolutionService
            .GetUnresolvedIngredientsAsync(recipeId, cancellationToken);

        if (ingredients is null)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"Recipe '{recipeId}' was not found.",
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found"
            });
        }

        var response = ingredients.Select(MapToUnresolvedIngredientResponse).ToList();
        return Ok(response);
    }

    /// <summary>
    /// Resolves a container reference on a recipe ingredient by declaring the net weight
    /// or volume of the container.
    /// </summary>
    /// <remarks>
    /// After this call the ingredient's <c>IsContainerResolved</c> flag is set to true,
    /// <c>Quantity</c> and <c>UomId</c> are updated to the declared values, and
    /// <c>Notes</c> is preserved unchanged (it continues to hold the original import string
    /// such as "1 can chopped tomatoes"). Once all ingredients in the recipe are resolved
    /// the recipe enters the matching pool automatically.
    /// </remarks>
    /// <param name="recipeId">The id of the recipe that owns the ingredient.</param>
    /// <param name="ingredientId">The id of the <c>RecipeIngredient</c> to resolve.</param>
    /// <param name="request">The declared quantity and unit of measure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 200 with the updated <see cref="ResolvedIngredientResponse"/>;
    /// 400 when the request fails validation (quantity not positive, UOM not found);
    /// 404 when the recipe or ingredient does not exist.
    /// </returns>
    [HttpPut("{recipeId:guid}/ingredients/{ingredientId:guid}/resolve")]
    [ProducesResponseType(typeof(ResolvedIngredientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResolvedIngredientResponse>> ResolveIngredient(
        Guid recipeId,
        Guid ingredientId,
        [FromBody] ResolveContainerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await containerResolutionService
            .ResolveAsync(recipeId, ingredientId, request, cancellationToken);

        if (result.IsRecipeNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"Recipe '{recipeId}' was not found.",
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found"
            });
        }

        if (result.IsIngredientNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"Ingredient '{ingredientId}' was not found on recipe '{recipeId}'.",
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found"
            });
        }

        if (result.IsValidationError)
        {
            return BadRequest(new ProblemDetails
            {
                Detail = result.ErrorMessage,
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error"
            });
        }

        var response = await MapToResolvedIngredientResponseAsync(
            result.ResolvedIngredient!,
            cancellationToken);

        return Ok(response);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static UnresolvedIngredientResponse MapToUnresolvedIngredientResponse(
        RecipeIngredient ingredient) =>
        new()
        {
            CanonicalIngredientName = ingredient.CanonicalIngredient?.Name ?? string.Empty,
            Id = ingredient.Id,
            Notes = ingredient.Notes ?? string.Empty
        };

    private static UnresolvedRecipeResponse MapToUnresolvedRecipeResponse(Recipe recipe) =>
        new()
        {
            Id = recipe.Id,
            Title = recipe.Title,
            UnresolvedIngredients = recipe.RecipeIngredients
                .Where(ri => !ri.IsContainerResolved)
                .OrderBy(ri => ri.CanonicalIngredient?.Name ?? string.Empty)
                .Select(MapToUnresolvedIngredientResponse)
                .ToList()
        };

    private async Task<ResolvedIngredientResponse> MapToResolvedIngredientResponseAsync(
        RecipeIngredient ingredient,
        CancellationToken cancellationToken)
    {
        var uomType = ingredient.Uom?.UomType ?? UomType.Arbitrary;
        var (displayQty, displayAbbr) = await displayConverter.ConvertAsync(
            ingredient.Quantity, uomType, cancellationToken);

        return new ResolvedIngredientResponse
        {
            CanonicalIngredientName = ingredient.CanonicalIngredient?.Name ?? string.Empty,
            Id = ingredient.Id,
            IsContainerResolved = ingredient.IsContainerResolved,
            Notes = ingredient.Notes,
            Quantity = displayQty,
            RecipeId = ingredient.RecipeId,
            UomAbbreviation = displayAbbr,
            UomId = ingredient.UomId ?? Guid.Empty
        };
    }
}
