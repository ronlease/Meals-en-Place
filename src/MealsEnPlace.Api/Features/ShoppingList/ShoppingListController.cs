using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Shopping list endpoints — derives what to buy from meal plan vs. inventory.
/// </summary>
[ApiController]
[Route("api/v1/meal-plans/{mealPlanId:guid}/shopping-list")]
[Produces("application/json")]
public class ShoppingListController(IShoppingListService shoppingListService) : ControllerBase
{
    /// <summary>Generates (or regenerates) the shopping list for a meal plan.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(List<ShoppingListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ShoppingListItemResponse>>> Generate(
        Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        var items = await shoppingListService.GenerateShoppingListAsync(mealPlanId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns the existing shopping list for a meal plan.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ShoppingListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShoppingListItemResponse>>> Get(
        Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        var items = await shoppingListService.GetShoppingListAsync(mealPlanId, cancellationToken);
        return Ok(items);
    }
}
