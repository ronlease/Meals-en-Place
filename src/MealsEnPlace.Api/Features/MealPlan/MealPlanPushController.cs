using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Push endpoints for meal plans (MEP-029). Kept separate from
/// <see cref="MealPlanController"/> so the generation / swap / reorder
/// surface stays focused on local state, and provider-specific integrations
/// can evolve independently.
/// </summary>
[ApiController]
[Route("api/v1/meal-plans")]
[Produces("application/json")]
public sealed class MealPlanPushController(IMealPlanPushTarget todoistTarget) : ControllerBase
{
    /// <summary>Pushes every slot of the meal plan to Todoist as scheduled tasks.</summary>
    /// <param name="id">The meal plan to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with created / updated / closed counts; 400 when Todoist is not configured; 404 when the plan is not found.</returns>
    [HttpPost("{id:guid}/push/todoist")]
    [ProducesResponseType(typeof(MealPlanPushResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanPushResult>> PushToTodoist(
        Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await todoistTarget.PushAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem(new ValidationProblemDetails { Detail = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("was not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new ProblemDetails { Detail = ex.Message, Status = 404, Title = "Meal Plan Not Found" });
        }
    }
}
