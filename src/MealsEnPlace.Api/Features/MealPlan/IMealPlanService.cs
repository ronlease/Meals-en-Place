namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Generates and manages weekly meal plans.
/// </summary>
public interface IMealPlanService
{
    /// <summary>Generates a weekly meal plan based on current inventory, recipes, and preferences.</summary>
    Task<MealPlanResponse> GenerateMealPlanAsync(GenerateMealPlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent meal plan (by WeekStartDate), or null if none exist.</summary>
    Task<MealPlanResponse?> GetActiveMealPlanAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a meal plan by ID, or null if not found.</summary>
    Task<MealPlanResponse?> GetMealPlanAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns all meal plans.</summary>
    Task<List<MealPlanResponse>> ListMealPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>Swaps a slot to a different recipe. Returns the updated slot, or null if slot not found.</summary>
    Task<MealPlanSlotResponse?> SwapSlotAsync(Guid slotId, SwapSlotRequest request, CancellationToken cancellationToken = default);
}
