using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Meal plan generation and management endpoints.
/// </summary>
[ApiController]
[Route("api/v1/meal-plans")]
[Produces("application/json")]
public class MealPlanController(
    IMealPlanReorderService mealPlanReorderService,
    IMealPlanService mealPlanService) : ControllerBase
{
    private const int DefaultUrgencyWindowDays = 7;

    /// <summary>Generates a new weekly meal plan.</summary>
    /// <param name="request">Generation parameters including optional filters and slot preferences.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the generated meal plan.</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(MealPlanResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MealPlanResponse>> Generate(
        [FromBody] GenerateMealPlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanService.GenerateMealPlanAsync(request, cancellationToken);
        return Ok(plan);
    }

    /// <summary>Returns the most recent meal plan.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the active meal plan; 404 if no plans exist.</returns>
    [HttpGet("active")]
    [ProducesResponseType(typeof(MealPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanResponse>> GetActive(CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanService.GetActiveMealPlanAsync(cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>Returns a meal plan by ID.</summary>
    /// <param name="id">The meal plan ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the meal plan; 404 if not found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MealPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanService.GetMealPlanAsync(id, cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>Returns all meal plans.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of all meal plans.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<MealPlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MealPlanResponse>>> List(CancellationToken cancellationToken = default)
    {
        var plans = await mealPlanService.ListMealPlansAsync(cancellationToken);
        return Ok(plans);
    }

    /// <summary>
    /// Applies a previously-previewed reorder. Permutes slot day assignments
    /// within each <see cref="MealSlot"/> so higher-urgency recipes move
    /// earlier in the plan. Returns the refreshed plan.
    /// </summary>
    [HttpPost("{id:guid}/reorder-by-expiry/apply")]
    [ProducesResponseType(typeof(MealPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanResponse>> ApplyReorderByExpiry(
        Guid id,
        [FromQuery] int? urgencyWindowDays = null,
        CancellationToken cancellationToken = default)
    {
        var window = ResolveUrgencyWindow(urgencyWindowDays);
        var plan = await mealPlanReorderService.ApplyAsync(id, window, cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>
    /// Previews a proposed reorder of the plan so recipes that consume
    /// expiring ingredients move to earlier days. Does not mutate; client
    /// confirms via <c>.../apply</c>.
    /// </summary>
    /// <param name="id">The meal plan ID.</param>
    /// <param name="urgencyWindowDays">
    /// Inclusive lookahead window for "expiring soon" in days. Defaults to 7.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id:guid}/reorder-by-expiry/preview")]
    [ProducesResponseType(typeof(ReorderPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReorderPreviewResponse>> PreviewReorderByExpiry(
        Guid id,
        [FromQuery] int? urgencyWindowDays = null,
        CancellationToken cancellationToken = default)
    {
        var window = ResolveUrgencyWindow(urgencyWindowDays);
        var preview = await mealPlanReorderService.PreviewAsync(id, window, cancellationToken);
        return preview is null ? NotFound() : Ok(preview);
    }

    private static int ResolveUrgencyWindow(int? value) =>
        value is null or <= 0 ? DefaultUrgencyWindowDays : value.Value;

    /// <summary>Swaps a meal plan slot to a different recipe.</summary>
    /// <param name="slotId">The ID of the slot to swap.</param>
    /// <param name="request">The new recipe assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the updated slot; 404 if the slot or recipe is not found.</returns>
    [HttpPut("slots/{slotId:guid}")]
    [ProducesResponseType(typeof(MealPlanSlotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealPlanSlotResponse>> SwapSlot(
        Guid slotId, [FromBody] SwapSlotRequest request, CancellationToken cancellationToken = default)
    {
        var slot = await mealPlanService.SwapSlotAsync(slotId, request, cancellationToken);
        return slot is null ? NotFound() : Ok(slot);
    }
}
