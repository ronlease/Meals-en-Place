using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Push endpoints for shopping lists (MEP-028). One endpoint per provider —
/// keeps provider logic isolated and signals the requested provider clearly
/// in the URL so operators can reason about the side effects at a glance.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class ShoppingListPushController(IShoppingListPushTarget todoistTarget) : ControllerBase
{
    /// <summary>Pushes the meal-plan-bound shopping list to Todoist.</summary>
    /// <param name="mealPlanId">The meal plan whose shopping list to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with a summary of created / updated / closed counts. 400 when Todoist is not configured.</returns>
    [HttpPost("api/v1/meal-plans/{mealPlanId:guid}/shopping-list/push/todoist")]
    [ProducesResponseType(typeof(ShoppingListPushResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShoppingListPushResult>> PushMealPlanShoppingList(
        Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await todoistTarget.PushAsync(mealPlanId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem(new ValidationProblemDetails { Detail = ex.Message });
        }
    }

    /// <summary>Pushes the standalone shopping list to Todoist.</summary>
    [HttpPost("api/v1/shopping-list/push/todoist")]
    [ProducesResponseType(typeof(ShoppingListPushResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShoppingListPushResult>> PushStandaloneShoppingList(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await todoistTarget.PushAsync(null, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem(new ValidationProblemDetails { Detail = ex.Message });
        }
    }
}
