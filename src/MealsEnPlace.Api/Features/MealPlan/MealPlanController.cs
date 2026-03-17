using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Meal plan generation and management endpoints.
/// </summary>
[ApiController]
[Route("api/v1/meal-plans")]
[Produces("application/json")]
public class MealPlanController(IMealPlanService mealPlanService) : ControllerBase
{
    /// <summary>Generates a new weekly meal plan.</summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(MealPlanResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MealPlanResponse>> Generate(
        [FromBody] GenerateMealPlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanService.GenerateMealPlanAsync(request, cancellationToken);
        return Ok(plan);
    }

    /// <summary>Returns the most recent meal plan.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(MealPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanResponse>> GetActive(CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanService.GetActiveMealPlanAsync(cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>Returns a meal plan by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MealPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanService.GetMealPlanAsync(id, cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>Returns all meal plans.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<MealPlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MealPlanResponse>>> List(CancellationToken cancellationToken = default)
    {
        var plans = await mealPlanService.ListMealPlansAsync(cancellationToken);
        return Ok(plans);
    }

    /// <summary>Swaps a meal plan slot to a different recipe.</summary>
    [HttpPut("slots/{slotId:guid}")]
    [ProducesResponseType(typeof(MealPlanSlotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanSlotResponse>> SwapSlot(
        Guid slotId, [FromBody] SwapSlotRequest request, CancellationToken cancellationToken = default)
    {
        var slot = await mealPlanService.SwapSlotAsync(slotId, request, cancellationToken);
        return slot is null ? NotFound() : Ok(slot);
    }
}
