using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Standalone shopping list endpoints — manage items added directly from recipe detail views.
/// </summary>
[ApiController]
[Route("api/v1/shopping-list")]
[Produces("application/json")]
public class StandaloneShoppingListController(IShoppingListService shoppingListService) : ControllerBase
{
    /// <summary>Adds a recipe's ingredients to the standalone shopping list, comparing against current inventory.</summary>
    /// <param name="recipeId">The recipe whose ingredients to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the updated standalone shopping list.</returns>
    [HttpPost("add-from-recipe/{recipeId:guid}")]
    [ProducesResponseType(typeof(List<ShoppingListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShoppingListItemResponse>>> AddFromRecipe(
        Guid recipeId, CancellationToken cancellationToken = default)
    {
        var items = await shoppingListService.AddFromRecipeAsync(recipeId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns the standalone shopping list (items not tied to a meal plan).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the standalone shopping list items.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ShoppingListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShoppingListItemResponse>>> Get(
        CancellationToken cancellationToken = default)
    {
        var items = await shoppingListService.GetStandaloneShoppingListAsync(cancellationToken);
        return Ok(items);
    }
}
