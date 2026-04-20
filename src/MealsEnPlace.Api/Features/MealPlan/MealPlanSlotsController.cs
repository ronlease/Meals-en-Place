using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Slot-scoped endpoints for marking meals as eaten and reversing the consume
/// (MEP-027 / MEP-031). The swap-slot action lives on
/// <see cref="MealPlanController"/> for historical reasons.
/// </summary>
[ApiController]
[Route("api/v1/meal-plan-slots")]
[Produces("application/json")]
public class MealPlanSlotsController(IMealConsumptionService consumptionService) : ControllerBase
{
    /// <summary>Marks the slot as eaten. Deducts inventory when auto-deplete is enabled.</summary>
    /// <param name="id">The meal plan slot ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the consume result; 404 if the slot is not found.</returns>
    [HttpPost("{id:guid}/consume")]
    [ProducesResponseType(typeof(ConsumeMealResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsumeMealResponse>> Consume(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await consumptionService.ConsumeAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(new ConsumeMealResponse
        {
            AutoDepleteApplied = result.AutoDepleteApplied,
            ConsumedAt = result.ConsumedAt,
            ShortIngredients = result.ShortIngredients
                .Select(s => new ShortIngredientResponse
                {
                    IngredientName = s.IngredientName,
                    ShortBy = s.ShortBy,
                    UnitOfMeasureAbbreviation = s.UnitOfMeasureAbbreviation
                })
                .ToList()
        });
    }

    /// <summary>Reverses a previous consume. Restores inventory when auto-deplete was on at consume time.</summary>
    /// <param name="id">The meal plan slot ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 when the slot was found and unconsumed; 404 if the slot does not exist.</returns>
    [HttpDelete("{id:guid}/consume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unconsume(Guid id, CancellationToken cancellationToken = default)
    {
        var found = await consumptionService.UnconsumeAsync(id, cancellationToken);
        return found ? NoContent() : NotFound();
    }
}
